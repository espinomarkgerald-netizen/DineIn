using System.Collections;
using UnityEngine;

public partial class CustomerGroup
{
    private CustomerTypeProfile visitProfile;
    private Object assistedServiceOwner;
    private int childCount;
    private Coroutine familyPlay;
    public float OrderingDurationMultiplier => visitProfile != null ? Mathf.Max(.1f, visitProfile.orderingDurationMultiplier) : 1f;
    public float WalkingDurationMultiplier => visitProfile != null ? 1f / Mathf.Max(.3f, visitProfile.walkingSpeedMultiplier) : 1f;
    public bool RequiresAssistedService => visitProfile != null && visitProfile.assistedPriorityService;
    public bool ReceivingAssistedService => RequiresAssistedService && assistedServiceOwner is AutonomousStaffBot worker &&
        worker.isActiveAndEnabled && RestaurantTaskClaim.IsClaimedByBot(this, worker);
    internal void BeginAssistedService(Object owner) { if (RequiresAssistedService) assistedServiceOwner = owner; }
    internal void EndAssistedService(Object owner) { if (assistedServiceOwner == owner) assistedServiceOwner = null; }

    internal void ConfigureCampaignVisit(CustomerTypeProfile profile)
    {
        StopFamilyPlay();
        assistedServiceOwner = null;
        childCount = 0;
        visitProfile = gameObject.scene.name == "Lobby2" && !CampaignSaveStore.ProtectedSession ? profile : null;
    }

    internal void ConfigureCampaignMembers()
    {
        // Representative remains the first adult. Children are members of this same order/seat group.
        childCount = visitProfile != null && visitProfile.family ? Random.Range(1, Mathf.Min(2, members.Count - 1) + 1) : 0;
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].ConfigureCampaignAppearance(
                visitProfile != null ? visitProfile.walkingSpeedMultiplier : 1f,
                i >= members.Count - childCount ? visitProfile.childVisualScale : 1f);
    }

    private void BeginFamilyPlay()
    {
        StopFamilyPlay();
        if (childCount > 0 && FastFood != null && FastFoodDineIn && assignedBooth != null)
            familyPlay = StartCoroutine(FamilyPlay());
    }

    private void StopFamilyPlay()
    {
        if (familyPlay != null) StopCoroutine(familyPlay);
        familyPlay = null;
    }

    private void OnDisable()
    {
        StopFamilyPlay();
        assistedServiceOwner = null;
    }

    private IEnumerator FamilyPlay()
    {
        while (state == GroupState.Eating && !leavingRoutineStarted)
        {
            yield return new WaitForSeconds(Mathf.Max(2f, visitProfile.childPlayInterval));
            if (state != GroupState.Eating || leavingRoutineStarted || assignedBooth == null) yield break;
            int index = Random.Range(members.Count - childCount, members.Count);
            var child = members[index];
            var table = assignedBooth.GetComponent<FastFoodTable>();
            var seat = assignedBooth.GetSeat(index);
            if (child == null || !child.IsSeated || seat == null || table == null) continue;
            Vector3 home = child.SeatedApproachPosition;
            // An authored dining-only zone is mandatory; no random route through kitchen/storage.
            if (!table.TryGetFamilyPlayPoint(home, visitProfile.childPlayRadius, child.Agent, out var playPoint)) continue;
            child.Unseat(); // Existing seat-to-navigation transition; SeatAnchor ownership stays with the family.
            try
            {
                if (child.TryWalkTo(playPoint, out var destination))
                {
                    float deadline = Time.time + visitProfile.childPlayTravelSeconds;
                    while (state == GroupState.Eating && !leavingRoutineStarted && !child.HasArrived(destination) && Time.time < deadline)
                        yield return null;
                }
                if (state != GroupState.Eating || leavingRoutineStarted) yield break;
                if (child.TryWalkTo(home, out var returnPoint))
                {
                    float deadline = Time.time + visitProfile.childPlayTravelSeconds;
                    while (state == GroupState.Eating && !leavingRoutineStarted && !child.HasArrived(returnPoint) && Time.time < deadline)
                        yield return null;
                    if (state == GroupState.Eating && !leavingRoutineStarted && child.HasArrived(returnPoint))
                    {
                        child.SnapToSeat(seat.position, assignedBooth.GetSeatedRotation(seat.position));
                        child.SetEating(true);
                    }
                }
                // A blocked optional return never extends the meal or blocks normal departure.
            }
            finally { if (child != null && !child.IsSeated) child.SetEating(false); }
        }
    }
}
