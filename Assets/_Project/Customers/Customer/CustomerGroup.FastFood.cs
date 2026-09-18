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
    private bool choosingFastFoodSeat;
    public bool CanChooseFastFoodSeat => FastFood != null && FastFoodPaid && FastFoodDineIn
        && !hasBeenAssigned && !leavingRoutineStarted && state == GroupState.Waiting;

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
            ShowCustomThought("We'll wait for a table.", happyFaceSprite);
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

    internal IEnumerator CollectFastFoodTray(FoodTray tray, Transform pickupPoint, float timeout,
        Vector3 carryOffset)
    {
        if (tray == null || assignedBooth == null || members.Count == 0)
        { FastFoodSelfPickup = false; yield break; }
        CustomerAgent collector = members.Find(member => member != null);
        if (collector == null) { FastFoodSelfPickup = false; yield break; }
        Transform seat = assignedBooth.GetSeat(members.IndexOf(collector));
        var interactable = tray.GetComponent<FoodTrayInteractable>();
        // Reserve the output before yielding so neither staff nor a player can take it.
        interactable?.NotifyDeliveredToTable();
        try
        {
        collector.Unseat();
        Vector3 destination = pickupPoint.position;
        bool arrived = collector.TryWalkTo(destination, out destination);
        float deadline = Time.time + timeout;
        while (arrived && !collector.HasArrived(destination) && Time.time < deadline)
        {
            if (leavingRoutineStarted || tray == null) yield break;
            yield return null;
        }
        arrived = arrived && collector.HasArrived(destination);
        if (!arrived)
        {
            // An unreachable pickup becomes a normal staff-delivery order.
            FastFoodSelfPickup = false;
        }
        else
        {
            WaiterHands.SetAllColliders(tray.gameObject, false);
            WaiterHands.AttachKeepingWorldScale(tray.transform, collector.transform,
                carryOffset, Quaternion.identity);
        }

        Vector3 approach = assignedBooth.GetNavigableApproachPosition();
        collector.TryWalkTo(approach, out approach);
        deadline = Time.time + timeout;
        while (!collector.HasArrived(approach) && Time.time < deadline)
        {
            if (leavingRoutineStarted || tray == null) yield break;
            yield return null;
        }
        if (seat != null) collector.SnapToSeat(seat.position, assignedBooth.GetSeatedRotation(seat.position));
        if (tray == null || leavingRoutineStarted) yield break;
        if (!arrived)
        {
            interactable?.SetDeliveryPickable(FindFirstObjectByType<TrayPickupQueue>());
            yield break;
        }
        Transform drop = assignedBooth.NetworkTrayPoint;
        if (drop == null) { FailFastFoodService("The table needs a TableFoodSpawn anchor."); yield break; }
        tray.transform.SetParent(drop, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;
        WaiterHands.SetAllColliders(tray.gameObject, true);
        ReceiveFoodFromWaiter(tray.DeliveredContents, tray);
        }
        finally
        {
            // A departing customer must not leave a reserved tray stuck on the counter
            // or parented to an agent that is about to be destroyed.
            if (tray != null && !firstDeliveryCompleted && leavingRoutineStarted)
                Destroy(tray.gameObject);
        }
    }
}
