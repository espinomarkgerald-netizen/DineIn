using System.Collections;
using UnityEngine;
using TMPro;

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
        FineDiningReservations.For(this)?.waitingArea?.IsSettled(this) == true;

    public bool ReceptionStandTransitionPending => Time.time < receptionStandUntil;

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
            !members.Exists(member => member != null && member.IsSeated))
        {
            ClearFineDiningReceptionIndicator();
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
