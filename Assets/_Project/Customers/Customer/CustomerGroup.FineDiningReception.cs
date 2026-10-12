using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.AI;

public partial class CustomerGroup
{
    private Transform receptionEscortLeader;
    private float receptionStandUntil;
    private GameObject reservationIndicatorInstance;
    private GameObject reservationIndicatorDetails;
    private TMP_Text reservationIndicatorText;
    private string reservationGuestName;
    private int reservationArrivalMinute;

    private bool HasProtectedReservationReception =>
        !hasBeenAssigned && !string.IsNullOrEmpty(FineDiningReservationId) &&
        FineDiningReservations.For(this) != null;

    public bool ReadyForReceptionInteraction => !HasProtectedReservationReception ||
        FineDiningReceptionSettled;

    public bool ReceptionStandTransitionPending => Time.time < receptionStandUntil;

    public bool ReservedSeatingActionAvailable => !HasProtectedReservationReception || !hasBeenGreeted ||
        (IsNetworkObserver ? FineDiningSeatingAvailable :
        BoothAssignArrowManager.Instance != null && BoothAssignArrowManager.Instance.HasValidBooth(this));

    // Presentation reads the existing timers; it never changes customer patience.
    public float FineDiningBubblePatience01 => state == GroupState.ReadyToOrder && !HasFineDiningDrinkEvent &&
        ordinaryOrderPatience > 0f ? Mathf.Clamp01(ordinaryOrderRemaining / ordinaryOrderPatience)
        : QueuePatienceVisible ? QueuePatience01 : 1f;

    /// <summary>Only the autonomous Fine Dining host uses this; player seating still calls AssignToBooth.</summary>
    public bool BeginReceptionEscort(Booth booth, Transform leader)
    {
        var reservations = FineDiningReservations.For(this);
        if (reservations == null || leader == null || !CanBeSeated || !CanUseBooth(booth)) return false;
        bool standingUp = members.Exists(member => member != null && member.IsSeated);
        receptionStandUntil = standingUp ? Time.time + (reservations.waitingArea != null
            ? reservations.waitingArea.standTransitionSeconds : .25f) : Time.time;
        receptionEscortLeader = leader;
        AssignToBooth(booth);
        if (!hasBeenAssigned) receptionEscortLeader = null;
        return hasBeenAssigned;
    }

    public void FinishReceptionEscort() => receptionEscortLeader = null;

    private bool TryResolveFineDiningSeatApproach(CustomerAgent member, int index, Vector3 desired,
        float spacing, float clearance, Vector3[] planned, NavMeshAgent[] nearbyAgents, out Vector3 target)
    {
        target = desired;
        // Reserved diners stand from reception chairs before querying their walking agent.
        // TryWalkTo performs this same transition for the ordinary route.
        if (member.IsSeated) member.Unseat();
        var agent = member.Agent;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        // The existing Fast Food formation uses the same nearby complete-path search.
        // Keep this search local to the table; never snap a diner through a blocked route.
        for (int candidate = 0; candidate < 17; candidate++)
        {
            float angle = (candidate - 1) % 8 * Mathf.PI / 4f;
            float radius = candidate == 0 ? 0f : (1 + (candidate - 1) / 8) * spacing;
            Vector3 probe = desired + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(probe, out var hit, Mathf.Max(agent.radius, clearance), filter)) continue;
            bool blocked = false;
            for (int earlier = 0; earlier < index; earlier++)
            {
                var otherMember = members[earlier];
                if (otherMember == null || otherMember.Agent == null || assignedBooth.GetSeat(earlier) == null) continue;
                float separation = agent.radius + otherMember.Agent.radius + clearance;
                Vector3 delta = hit.position - planned[earlier]; delta.y = 0f;
                if (delta.sqrMagnitude < separation * separation) { blocked = true; break; }
            }
            if (blocked) continue;
            foreach (var other in nearbyAgents)
            {
                if (other == null || !other.enabled || !other.isOnNavMesh ||
                    other.gameObject.scene != gameObject.scene || other.GetComponentInParent<CustomerGroup>() == this) continue;
                float separation = agent.radius + other.radius + clearance;
                Vector3 delta = hit.position - other.nextPosition; delta.y = 0f;
                if (delta.sqrMagnitude < separation * separation) { blocked = true; break; }
                if (!other.hasPath) continue;
                delta = hit.position - other.destination; delta.y = 0f;
                if (delta.sqrMagnitude < separation * separation) { blocked = true; break; }
            }
            if (blocked || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            target = hit.position;
            return true;
        }
        return false;
    }

    private void ReportFineDiningSeatingFailure(string reason)
    {
        if (!TrainingGroupProtected) { BecomeUnhappyAndLeave(); return; }
        foreach (var member in members)
            if (member != null && !member.IsSeated) member.StopAtCurrentPosition();
        TutorialSeatingFailure = reason;
        Debug.LogWarning("[FineDiningTutorial] " + reason, this);
    }

    private IEnumerator FollowReceptionEscort()
    {
        var area = FineDiningReservations.For(this)?.waitingArea;
        float spacing = area != null ? area.escortMemberSpacing : 1.25f;
        float interval = area != null ? area.escortRepathSeconds : .2f;
        while (ReceptionStandTransitionPending) yield return null;
        float nextRoute = 0f;
        while (receptionEscortLeader != null && receptionEscortLeader.gameObject.activeInHierarchy && state == GroupState.WalkingToBooth)
        {
            if (Time.time >= nextRoute)
            {
                nextRoute = Time.time + interval;
                Transform preceding = receptionEscortLeader;
                foreach (var member in members)
                {
                    if (member == null) continue;
                    Vector3 towardLeader = preceding.position - member.transform.position;
                    towardLeader.y = 0f;
                    float memberSpacing = Mathf.Max(spacing, member.Agent != null ? member.Agent.radius * 2f + .15f : spacing);
                    if (towardLeader.sqrMagnitude > memberSpacing * memberSpacing)
                    {
                        // Following the preceding person uses the same complete-path validation as normal seating.
                        // No Transform teleport or camera-dependent offset is used.
                        Vector3 target = preceding.position - towardLeader.normalized * memberSpacing;
                        member.TryWalkTo(target, out _);
                    }
                    else
                        member.StopAtCurrentPosition();
                    preceding = member.transform;
                }
            }
            yield return null;
        }
        receptionEscortLeader = null;
    }

    private void RefreshFineDiningReceptionIndicator()
    {
        if (!HasProtectedReservationReception || !CanBeSeated || !ReadyForReceptionInteraction ||
            !(IsNetworkObserver ? FineDiningReceptionSettled : members.Exists(member => member != null && member.IsSeated)))
        {
            ClearFineDiningReceptionIndicator();
            return;
        }
        if (!ReservedSeatingActionAvailable)
        {
            if (reservationIndicatorInstance != null) reservationIndicatorInstance.SetActive(false);
            if (IsReceptionClaimedByPlayer)
            {
                ReleasePlayerReceptionTask();
                RestaurantTaskClaim.ReleasePlayer(this);
            }
            return;
        }
        // The table arrow replaces the reception marker, including direct marker clicks.
        if (BoothAssignArrowManager.Instance != null && BoothAssignArrowManager.Instance.IsSelectingFor(this))
        {
            if (reservationIndicatorInstance != null) reservationIndicatorInstance.SetActive(false);
            return;
        }
        if (reservationIndicatorInstance == null)
        {
            var prefab = FineDiningReservations.For(this)?.waitingArea?.reservationIndicatorPrefab;
            if (prefab == null) return;
            reservationIndicatorInstance = Instantiate(prefab);
            reservationIndicatorInstance.name = name + "_Reservation";
            reservationIndicatorInstance.GetComponent<CustomerGreetBubbleUI>()?.Init(this);
            var follow = reservationIndicatorInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
            if (follow != null) ConfigureCustomerBubble(follow);
            var details = reservationIndicatorInstance.transform.Find("Details");
            reservationIndicatorDetails = details != null ? details.gameObject : null;
            reservationIndicatorText = details != null ? details.GetComponentInChildren<TMP_Text>(true) : null;
        }
        reservationIndicatorInstance.SetActive(CustomerGreetBubbleSpawner.Instance == null ||
            !CustomerGreetBubbleSpawner.Instance.IsShowingFor(this));
        bool showDetails = isSelected || receptionTaskOwner != ReceptionTaskOwner.None;
        if (reservationIndicatorDetails != null) reservationIndicatorDetails.SetActive(showDetails);
        if (showDetails && reservationIndicatorText != null)
        {
            var booking = FineDiningReservations.For(this)?.Booking(FineDiningReservationId);
            string guest = booking != null ? booking.guestName : reservationGuestName;
            int arrival = booking != null ? booking.arrivalMinute : reservationArrivalMinute;
            reservationIndicatorText.text = guest + "\n" + FineDiningReservations.Clock(arrival) +
                "  •  " + Size + (Size == 1 ? " guest" : " guests");
        }
    }

    private void ClearFineDiningReceptionIndicator()
    {
        if (reservationIndicatorInstance != null) Destroy(reservationIndicatorInstance);
        reservationIndicatorInstance = null;
        reservationIndicatorDetails = null;
        reservationIndicatorText = null;
    }
}
