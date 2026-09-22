using System.Collections;
using UnityEngine;

public partial class CustomerGroup
{
    // Set only by the authored Lobby2 coordinator. Other restaurants never opt in.
    public FastFoodRestaurant FastFood { get; private set; }
    public bool FastFoodDineIn { get; private set; }
    public bool FastFoodPaid { get; private set; }
    public bool FastFoodSelfPickup { get; internal set; }
    public bool FastFoodRequestsTableDelivery => FastFood != null && FastFoodPaid && FastFoodDineIn && !FastFoodSelfPickup;
    public bool AllowsStaffFoodDelivery => FastFood == null || (FastFoodRequestsTableDelivery &&
        !IsFastFoodLeaving && !FastFoodAwaitingSeat && assignedBooth != null && assignedBooth.CurrentGroup == this &&
        members.Count > 0 && members.TrueForAll(member => member != null && member.IsSeated));
    public bool FastFoodWasServed => firstDeliveryCompleted;
    public bool FastFoodAwaitingSeat => FastFood != null && FastFoodPaid && FastFoodDineIn
        && !leavingRoutineStarted && (state == GroupState.Waiting || state == GroupState.WalkingToBooth);
    public bool IsFastFoodCounterCustomer => FastFood != null && !FastFoodPaid;
    public bool IsFastFoodLeaving => leavingRoutineStarted || state == GroupState.Leaving
        || state == GroupState.UnhappyLeft || state == GroupState.AngryLeft;
    public bool HasReceivedCurrentFastFoodOrder => deliveredFastFoodOrder >= 0 && deliveredFastFoodOrder == currentOrderNumber;
    private int deliveredFastFoodOrder = -1;
    private CustomerAgent fastFoodRepresentative;
    public CustomerAgent FastFoodRepresentative
    {
        get { if (fastFoodRepresentative == null) fastFoodRepresentative = members.Find(m => m != null); return fastFoodRepresentative; }
    }
    internal bool NeedsFastFoodRemake => FastFoodPaid && waitingForRemake && state == GroupState.ReadyToOrder && !isPlayerReviewingOrder && !leavingRoutineStarted;
    internal void TryConfirmFastFoodRemake() => TakeOrderFromWaiter(chosenFood, chosenDrink, null);
    internal void EndFastFoodServiceAtClosing()
    {
        if (IsFastFoodLeaving) return;
        if (HasReceivedCurrentFastFoodOrder && !waitingForRemake)
        { SetState(GroupState.NeedsBill); PayAndLeave(); }
        else FailFastFoodService("The restaurant closed before this order was served.");
    }
    private bool choosingFastFoodSeat;
    [SerializeField, Min(1f)] private float fastFoodSeatingArrivalGraceSeconds = 6f;
    public bool CanChooseFastFoodSeat => FastFood != null && FastFoodPaid && FastFoodDineIn
        && !hasBeenAssigned && !leavingRoutineStarted && state == GroupState.Waiting;

    private Vector3 GetFastFoodFormationTarget(CustomerAgent member, int index, Vector3 center, Vector3 forward,
        Vector3 right, float sideSpacing, float rowSpacing)
    {
        if (member == FastFoodRepresentative) return center;
        int companion = 0;
        for (int i = 0; i < index; i++) if (members[i] != null && members[i] != FastFoodRepresentative) companion++;
        // Takeout pairs stay beside their representative instead of using the
        // separate companion anchors authored for larger dine-in parties.
        if (!FastFoodDineIn)
            return center - right * (companion + 1) * Mathf.Max(.8f, sideSpacing);
        var station = FastFood.StationFor(this);
        if (station != null && station.Queue.CurrentFront == this && station.Queue.OrderPoint != null &&
            (center - station.Queue.OrderPoint.position).sqrMagnitude < .01f &&
            station.CompanionPoints != null && companion < station.CompanionPoints.Length && station.CompanionPoints[companion] != null)
            return station.CompanionPoints[companion].position;
        return center + right * (companion % 2 == 0 ? -1f : 1f) * Mathf.Max(.8f, sideSpacing)
            - forward * (1 + companion / 2) * Mathf.Max(1.2f, rowSpacing);
    }
    private bool TryResolveDistinctFastFoodDestination(CustomerAgent member, Vector3 desired, out Vector3 target)
    {
        target = desired;
        var agent = member.Agent;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var path = new UnityEngine.AI.NavMeshPath();
        var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        // A formation can straddle a doorway or mesh edge. Search nearby distinct,
        // reachable positions instead of leaving a companion with no destination.
        for (int candidate = 0; candidate < 25; candidate++)
        {
            float angle = (candidate - 1) % 8 * Mathf.PI / 4f;
            float radius = candidate == 0 ? 0f : (1 + (candidate - 1) / 8) * .8f;
            Vector3 probe = desired + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out var hit, .65f, filter)) continue;
            bool occupied = false;
            foreach (var destination in takeoutMemberDestinations.Values)
                if ((destination - hit.position).sqrMagnitude < .64f) { occupied = true; break; }
            if (occupied || !agent.CalculatePath(hit.position, path) ||
                path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
            target = hit.position;
            return true;
        }
        return false;
    }

    internal void ChooseFastFoodSeat(Booth booth)
    {
        if (!CanChooseFastFoodSeat) return;
        choosingFastFoodSeat = true;
        try { AssignToBooth(booth); }
        finally { choosingFastFoodSeat = false; }
    }

    internal bool CanReachFastFoodTable(Booth booth)
    {
        if (booth == null || booth.approachPoint == null) return false;
        var path = new UnityEngine.AI.NavMeshPath();
        foreach (var member in members)
        {
            var agent = member != null ? member.Agent : null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            var filter = new UnityEngine.AI.NavMeshQueryFilter
                { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!UnityEngine.AI.NavMesh.SamplePosition(booth.GetCustomerApproachPosition(), out var hit, 1f, filter)
                || !agent.CalculatePath(hit.position, path)
                || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) return false;
        }
        return members.Count > 0;
    }

    internal void ConfigureFastFood(FastFoodRestaurant restaurant, bool dineIn)
    {
        FastFood = restaurant;
        FastFoodDineIn = dineIn;
        if (restaurant.CustomerExit != null) exitPoint = restaurant.CustomerExit;
        // Reuse the existing counter review/payment path until payment succeeds.
        SetServiceType(ServiceType.Takeout);
    }

    internal bool CommitFastFoodPayment()
    {
        if (!IsFastFoodCounterCustomer || state != GroupState.OrderTaken || !hasConfirmedOrder)
            return false;
        FastFoodPaid = true;
        RestaurantTaskClaim.Complete(this);
        ClearOrderBubble();
        SetTakeoutQueueState(TakeoutQueueState.None);
        if (FastFoodDineIn)
        {
            SetServiceType(ServiceType.DineIn);
            SetState(GroupState.Waiting);
            StopLinePatience();
            ShowCustomThought(Size == 1 ? "I'll find a seat." : "We'll find a table.", happyFaceSprite);
        }
        SpawnTableNumber();
        return true;
    }

    internal void FinishFastFoodSeating()
    {
        assignedBooth?.ClearMenuBook();
        SetState(GroupState.OrderTaken);
        SpawnTableNumber();
        FastFood?.OnSeated(this);
        if (FastFoodRequestsTableDelivery)
            ShowCustomThought("Please bring our food to the table.", happyFaceSprite);
    }

    internal void FailFastFoodService(string reason)
    {
        if (IsFastFoodLeaving) return;
        Debug.LogWarning("[FastFood] " + reason, this);
        BecomeUnhappyAndLeave();
        if (FastFoodPaid)
            ShowCustomThought(FastFoodRequestsTableDelivery ? "Our food never arrived." : "We couldn't collect our food.", unhappyFaceSprite);
    }

    private bool collectingFastFoodTray;
    internal bool IsCollectingFastFoodTray => collectingFastFoodTray;
    private static bool HasReachedFastFoodTable(CustomerAgent customer, Vector3 approach)
    {
        if (customer == null) return false;
        if (customer.HasArrived(approach)) return true;
        var agent = customer.Agent;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending ||
            agent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete) return false;
        Vector3 offset = customer.transform.position - approach;
        offset.y = 0f;
        // The player can legitimately be standing at the shared service point.
        // Use the table's interaction radius, but never snap across a NavMesh wall.
        return offset.sqrMagnitude <= 1.5f * 1.5f && !agent.Raycast(approach, out _);
    }
    internal bool CanCollectFastFoodTray => FastFoodPaid && FastFoodDineIn && FastFoodSelfPickup &&
        !IsFastFoodLeaving && !collectingFastFoodTray && !HasReceivedCurrentFastFoodOrder &&
        state == GroupState.OrderTaken && hasConfirmedOrder && !isPlayerReviewingOrder &&
        assignedBooth != null && assignedBooth.CurrentGroup == this && members.Count > 0 &&
        members.TrueForAll(member => member != null && member.IsSeated);
    internal IEnumerator CollectFastFoodTray(FoodTray tray, Transform pickupPoint, float timeout)
    {
        // A duplicate request must not release the collector that already owns this slot.
        if (collectingFastFoodTray) yield break;
        if (tray == null || pickupPoint == null || !CanCollectFastFoodTray)
        { FastFood?.ReleasePickup(this); yield break; }
        var collector = FastFoodRepresentative;
        var booth = assignedBooth;
        var interactable = tray.GetComponent<FoodTrayInteractable>();
        if (collector == null || collector.TrayCarryAnchor == null || interactable == null)
        { FastFood.ReleasePickup(this); FailFastFoodService("The customer tray carry setup is incomplete."); yield break; }
        Transform seat = booth.GetSeat(members.IndexOf(collector));
        if (seat == null || booth.NetworkTrayPoint == null)
        { FastFood.ReleasePickup(this); FailFastFoodService("The table is missing its seat or tray anchor."); yield break; }

        collectingFastFoodTray = true;
        Transform outputParent = tray.transform.parent;
        Vector3 outputPosition = tray.transform.position;
        Quaternion outputRotation = tray.transform.rotation;
        bool reserved = false;
        bool delivered = false;
        try
        {
            // Reservation is owned exclusively by this group's collector.
            float claimDeadline = Time.time + timeout;
            while (!leavingRoutineStarted && tray != null && !HasReceivedCurrentFastFoodOrder &&
                   !(reserved = interactable.TryBeginCustomerPickup(this)) && Time.time < claimDeadline)
                yield return null;
            if (!reserved || leavingRoutineStarted || tray == null || HasReceivedCurrentFastFoodOrder) yield break;

            collector.Unseat();
            Vector3 destination;
            bool moving = collector.TryWalkTo(pickupPoint.position, out destination);
            float deadline = Time.time + timeout;
            while (moving && collector != null && !collector.HasArrived(destination) && Time.time < deadline)
            {
                if (leavingRoutineStarted || tray == null) yield break;
                yield return null;
            }
            if (!moving || collector == null || !collector.HasArrived(destination))
            { FailFastFoodService("The food pickup counter could not be reached."); yield break; }
            if (!collector.BeginTrayCarry(tray))
            { FailFastFoodService("The customer could not carry this order."); yield break; }
            FastFood.ReleasePickup(this);

            moving = collector.TryWalkTo(booth.GetCustomerApproachPosition(), out destination);
            deadline = Time.time + timeout;
            while (moving && collector != null && !HasReachedFastFoodTable(collector, destination) && Time.time < deadline)
            {
                if (leavingRoutineStarted || tray == null || booth == null) yield break;
                yield return null;
            }
            if (!moving || !HasReachedFastFoodTable(collector, destination))
            { FailFastFoodService("The table return path is blocked."); yield break; }
            if (leavingRoutineStarted || tray == null || booth == null || booth.CurrentGroup != this ||
                state != GroupState.OrderTaken || !hasConfirmedOrder || isPlayerReviewingOrder) yield break;

            collector.EndTrayCarry(tray);
            WaiterHands.AttachKeepingWorldScale(tray.transform, booth.NetworkTrayPoint, Vector3.zero, Quaternion.identity);
            WaiterHands.SetAllColliders(tray.gameObject, true);
            interactable.NotifyDeliveredToTable();
            collector.SnapToSeat(seat.position, booth.GetSeatedRotation(seat.position));
            deliveredFastFoodOrder = currentOrderNumber;
            ReceiveFoodFromWaiter(tray.DeliveredContents, tray);
            delivered = true;
        }
        finally
        {
            collectingFastFoodTray = false;
            FastFood?.ReleasePickup(this);
            if (collector != null) collector.EndTrayCarry(tray);
            if (tray != null && reserved && !delivered)
            {
                if (leavingRoutineStarted) Destroy(tray.gameObject);
                else
                {
                    tray.transform.SetParent(outputParent, true);
                    tray.transform.SetPositionAndRotation(outputPosition, outputRotation);
                    WaiterHands.SetAllColliders(tray.gameObject, true);
                    interactable.RestoreAfterCustomerPickup(this);
                }
            }
        }
    }
}
