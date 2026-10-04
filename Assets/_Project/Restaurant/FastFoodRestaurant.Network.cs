using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FastFoodRestaurant
{
    [Serializable] public sealed class NetworkState
    {
        public bool accepting, available;
        public List<StationState> stations = new();
    }
    [Serializable] public sealed class StationState
    {
        public int index, front, phase;
        public int[] queue;
        public float progress;
    }
    public bool CanReviewCounterOrder(CustomerGroup group) => Operational && serviceAvailable && group != null
        && group.FastFood == this && !group.FastFoodPaid && !group.HasConfirmedOrder
        && group.state == CustomerGroup.GroupState.ReadyToOrder
        && group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint
        && StationFor(group)?.IsKiosk == false && StationFor(group)?.Queue.CurrentFront == group
        && StationFor(group)?.Flow.ActiveGroup == group;

    internal int NetworkStation(CustomerGroup group) => Array.IndexOf(serviceStations, StationFor(group));
    internal int NetworkWaiting(CustomerGroup group) => waitingSlots.TryGetValue(group, out var slot) ? slot : -1;
    internal int NetworkPickup(CustomerGroup group) => Array.IndexOf(customerPickupSlots, ReservedPickupFor(group));
    internal float NetworkSeatWait(CustomerGroup group) => seatWait.TryGetValue(group, out var elapsed) ? elapsed : 0f;
    internal float NetworkReadyWait(CustomerGroup group) => readyWait.TryGetValue(group, out var elapsed) ? elapsed : 0f;
    internal void PresentCustomer(CustomerGroup group, CustomerGroup.FastFoodNetworkState state)
    {
        if (group == null || !group.IsNetworkObserver || state == null) return;
        customers.Add(group);
        if (state.station >= 0 && state.station < serviceStations.Length) stationOwners[group] = serviceStations[state.station];
        if (state.paid && !paid.Contains(group)) paid.Add(group);
        if (state.paid) settled.Add(group);
        if (state.waiting >= 0) waitingSlots[group] = state.waiting; else waitingSlots.Remove(group);
        if (state.pickup >= 0 && state.pickup < customerPickupSlots.Length) pickupReservations[group] = customerPickupSlots[state.pickup];
        else pickupReservations.Remove(group);
        seatWait[group] = state.seatWait; readyWait[group] = state.readyWait;
        if (state.bagReady) readyBags.Add(group); else readyBags.Remove(group);
    }
    public NetworkState CaptureNetworkState()
    {
        var state = new NetworkState { accepting = acceptingCustomers, available = serviceAvailable };
        for (int i = 0; i < serviceStations.Length; i++)
        {
            var station = serviceStations[i];
            if (station == null) continue;
            state.stations.Add(new StationState { index = i, front = NetworkView(station.Queue?.CurrentFront),
                phase = (int)(station.Flow?.CurrentPhase ?? TakeoutFlowManager.TakeoutPhase.None),
                queue = station.Queue?.CaptureNetworkQueue(), progress = station.ServiceProgress });
        }
        return state;
    }
    private static int NetworkView(CustomerGroup group) => group != null
        ? group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 : 0;
    public bool ApplyNetworkState(NetworkState state)
    {
        if (!MultiplayerRestaurantBridge.IsObserver || state?.stations == null) return false;
        acceptingCustomers = state.accepting; serviceAvailable = state.available;
        bool complete = true;
        foreach (var entry in state.stations)
        {
            if (entry.index < 0 || entry.index >= serviceStations.Length || serviceStations[entry.index] == null) return false;
            var station = serviceStations[entry.index];
            var front = MultiplayerServiceActions.Resolve(entry.front);
            if (entry.front > 0 && front == null) { complete = false; continue; }
            complete &= station.Queue != null && station.Queue.PresentNetworkQueue(entry.queue, front);
            station.Flow?.PresentNetworkState(front, entry.phase);
            station.PresentNetworkProgress(entry.progress);
        }
        customers.RemoveWhere(group => group == null);
        paid.RemoveAll(group => group == null || group.IsFastFoodLeaving);
        foreach (var group in new List<CustomerGroup>(waitingSlots.Keys))
            if (group == null || group.IsFastFoodLeaving) waitingSlots.Remove(group);
        return complete;
    }
    // The authority chooses once; observer payment objects are presentation only.
    internal bool UsesCard(CustomerGroup group)
    {
        if (!cardChoices.TryGetValue(group, out bool useCard))
            cardChoices[group] = useCard = cardPaymentPrefab != null && CardPaymentService.ShouldUseCardPayment();
        return useCard;
    }
    internal MoneyPickup CreateNetworkCard(CustomerGroup group, int amount = -1)
    {
        if (cardPaymentPrefab == null || group == null) return null;
        var point = StationFor(group)?.StaffApproach;
        var card = Instantiate(cardPaymentPrefab, point != null ? point : transform);
        card.Init(group, amount >= 0 ? amount : group.GetCurrentOrderTotal(), point, cardPayment: true);
        return card;
    }
}
