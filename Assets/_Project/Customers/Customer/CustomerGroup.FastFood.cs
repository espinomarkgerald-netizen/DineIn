using System.Collections;
using UnityEngine;

public partial class CustomerGroup
{
    // Set only by the authored Lobby2 coordinator. Other restaurants never opt in.
    public FastFoodRestaurant FastFood { get; private set; }
    public bool FastFoodDineIn { get; private set; }
    public bool FastFoodPaid { get; private set; }
    public bool FastFoodSelfPickup { get; internal set; }
    public bool FastFoodWasServed => firstDeliveryCompleted;
    public bool FastFoodAwaitingSeat => FastFood != null && FastFoodPaid && FastFoodDineIn
        && !leavingRoutineStarted && (state == GroupState.Waiting || state == GroupState.WalkingToBooth);
    public bool IsFastFoodCounterCustomer => FastFood != null && !FastFoodPaid;
    public bool IsFastFoodLeaving => leavingRoutineStarted;
    public bool HasReceivedCurrentFastFoodOrder => deliveredFastFoodOrder == currentOrderNumber;
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
        if (leavingRoutineStarted) return;
        if (HasReceivedCurrentFastFoodOrder && !waitingForRemake)
        { SetState(GroupState.NeedsBill); PayAndLeave(); }
        else FailFastFoodService("The restaurant closed before this order was served.");
    }
    private bool choosingFastFoodSeat;
    public bool CanChooseFastFoodSeat => FastFood != null && FastFoodPaid && FastFoodDineIn
        && !hasBeenAssigned && !leavingRoutineStarted && state == GroupState.Waiting;

    private Vector3 GetFastFoodFormationTarget(CustomerAgent member, int index, Vector3 center, Vector3 forward,
        Vector3 right, float sideSpacing, float rowSpacing)
    {
        if (member == FastFoodRepresentative) return center;
        int companion = 0;
        for (int i = 0; i < index; i++) if (members[i] != null && members[i] != FastFoodRepresentative) companion++;
        var station = FastFood.StationFor(this);
        if (station != null && station.Queue.CurrentFront == this && station.Queue.OrderPoint != null &&
            (center - station.Queue.OrderPoint.position).sqrMagnitude < .01f &&
            station.CompanionPoints != null && companion < station.CompanionPoints.Length && station.CompanionPoints[companion] != null)
            return station.CompanionPoints[companion].position;
        return center + right * (companion % 2 == 0 ? -1f : 1f) * Mathf.Max(.8f, sideSpacing)
            - forward * (1 + companion / 2) * Mathf.Max(1.2f, rowSpacing);
    }
    private bool TryResolveDistinctFastFoodDestination(Vector3 desired, out Vector3 target)
    {
        target = desired;
        if (!UnityEngine.AI.NavMesh.SamplePosition(desired, out var hit, .65f, UnityEngine.AI.NavMesh.AllAreas)) return false;
        target = hit.position;
        foreach (var destination in takeoutMemberDestinations.Values)
            if ((destination - target).sqrMagnitude < .64f) return false;
        return true;
    }

    internal void ChooseFastFoodSeat(Booth booth)
    {
        if (!CanChooseFastFoodSeat) return;
        choosingFastFoodSeat = true;
        try { AssignToBooth(booth); }
        finally { choosingFastFoodSeat = false; }
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
    }

    internal void FailFastFoodService(string reason)
    {
        if (leavingRoutineStarted) return;
        Debug.LogWarning("[FastFood] " + reason, this);
        BecomeUnhappyAndLeave();
    }

    private bool collectingFastFoodTray;
    internal bool IsCollectingFastFoodTray => collectingFastFoodTray;
    internal IEnumerator CollectFastFoodTray(FoodTray tray, Transform pickupPoint, float timeout)
    {
        // A duplicate request must not release the collector that already owns this slot.
        if (collectingFastFoodTray) yield break;
        if (tray == null || assignedBooth == null || pickupPoint == null ||
            members.Count == 0 || leavingRoutineStarted || FastFoodAwaitingSeat)
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
            // Let an already accepted manager command finish before attempting pickup.
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

            moving = collector.TryWalkTo(booth.GetNavigableApproachPosition(), out destination);
            deadline = Time.time + timeout;
            while (moving && collector != null && !collector.HasArrived(destination) && Time.time < deadline)
            {
                if (leavingRoutineStarted || tray == null || booth == null) yield break;
                yield return null;
            }
            if (!moving || collector == null || !collector.HasArrived(destination))
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
