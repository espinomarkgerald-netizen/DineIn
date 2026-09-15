using System.Collections.Generic;
using UnityEngine;

public sealed partial class MultiplayerServiceActions
{
    private sealed class DirtyOwner { public int actor; public Vector3 origin; public Quaternion rotation; public Booth booth; }
    private readonly Dictionary<int, DirtyOwner> dirtyOwners = new();
    public static bool CanCollectDirtyTray(FoodTray tray)
    {
        var active = Active;
        var hands = WaiterHands.ActivePlayerHands;
        var busser = BusserHands.ActivePlayerHands;
        return active != null && active.session.CanAct && tray != null && !tray.NetworkCarryLocked
            && tray.GetComponent<FoodTrayInteractable>()?.IsCleanupPickable == true && busser != null && !busser.HasTray
            && hands != null && !hands.HasTray && !hands.HasBill && !hands.HasMoney && !hands.HasTicket && active.LocalBag == null;
    }
    public static void CollectDirtyTray(FoodTray tray)
    {
        if (!CanCollectDirtyTray(tray)) return;
        var pickup = tray.GetComponent<FoodTrayInteractable>();
        var mover = Active.session.LocalManager.GetComponent<PlayerMovement>();
        int order = tray.orderNumber;
        Active.ApproachClaimed($"Order:{order}:Cleanup", mover, pickup.StandPoint, pickup.GetInteractRadius(), () => SendRequest("cleanup_pickup", 0, order));
    }
    public static bool WashDirtyTray() => BusserHands.ActivePlayerHands != null && BusserHands.ActivePlayerHands.HasTray
        && SendRequest("cleanup_wash", 0, BusserHands.ActivePlayerHands.holdingTray.orderNumber);
    private static FoodTray FindTray(int order)
    {
        foreach (var tray in MultiplayerWorldRegistry.All<FoodTray>()) if (tray.orderNumber == order) return tray;
        return null;
    }
    private bool ExecuteCleanup(Command request, int actor, GameObject player)
    {
        var hands = player.GetComponent<BusserHands>();
        var waiter = player.GetComponent<WaiterHands>();
        var tray = FindTray(request.value);
        if (hands == null || waiter == null || tray == null) return false;
        if (request.operation == "cleanup_pickup")
        {
            if (!session.GetComponent<MultiplayerTaskClaims>().IsClaimedBy($"Order:{tray.orderNumber}:Cleanup", actor)) return false;
            var pickup = tray.GetComponent<FoodTrayInteractable>();
            var booth = tray.GetComponentInParent<Booth>();
            if (hands.HasTray || waiter.HasTray || waiter.HasMoney || waiter.HasBill || waiter.HasTicket || LocalBagFor(actor) != null
                || tray.NetworkCarryLocked || dirtyOwners.ContainsKey(tray.orderNumber) || pickup == null || !pickup.IsCleanupPickable
                || !Near(player, pickup.StandPoint, pickup.GetInteractRadius())
                || booth != null && booth.CurrentGroup != null && !pickup.IsComplaintRemoval) return false;
            if (RestaurantTaskClaim.IsClaimedByBot(tray)
                && !RestaurantTaskClaim.TryTakeOver(RestaurantTaskClaim.GetMultiplayerTaskId(tray))) return false;
            var owner = new DirtyOwner { actor = actor, origin = tray.transform.position, rotation = tray.transform.rotation, booth = booth };
            if (!hands.PickupTray(tray)) return false;
            tray.NetworkCarryLocked = true;
            dirtyOwners[tray.orderNumber] = owner;
            return true;
        }
        if (request.operation != "cleanup_wash" || !dirtyOwners.TryGetValue(tray.orderNumber, out var reservation)
            || reservation.actor != actor || hands.holdingTray != tray) return false;
        var sink = FindFirstObjectByType<SinkInteractable>();
        if (sink == null || !Near(player, sink.StandPoint, sink.GetInteractRadius())) return false;
        dirtyOwners.Remove(tray.orderNumber);
        session.GetComponent<MultiplayerTaskClaims>().CompleteOnAuthority($"Order:{tray.orderNumber}:Cleanup", actor);
        hands.DisposeTray(true);
        GameDayManager.Instance.RegisterTrayCleaned();
        return true;
    }
    private void RecoverDirtyTrays()
    {
        foreach (int order in new List<int>(dirtyOwners.Keys))
        {
            var owner = dirtyOwners[order];
            var tray = FindTray(order);
            if (tray != null && session.ValidActor(owner.actor)) continue;
            dirtyOwners.Remove(order);
            if (tray == null) continue;
            tray.GetComponentInParent<BusserHands>()?.ReleaseTrayForRetry(owner.origin);
            tray.transform.SetPositionAndRotation(owner.origin, owner.rotation);
            WaiterHands.SetAllColliders(tray.gameObject, true);
            if (owner.booth != null) tray.transform.SetParent(owner.booth.transform, true);
            tray.NetworkCarryLocked = false;
            tray.GetComponent<FoodTrayInteractable>()?.SetCleanupPickable(true);
        }
    }

    public void ReleaseDirtyClaim(int order)
    {
        if (!session.IsAuthority || !dirtyOwners.TryGetValue(order, out var owner)) return;
        dirtyOwners.Remove(order);
        var tray = FindTray(order);
        if (tray == null) return;
        tray.GetComponentInParent<BusserHands>()?.ReleaseTrayForRetry(owner.origin);
        tray.transform.SetPositionAndRotation(owner.origin, owner.rotation);
        if (owner.booth != null) tray.transform.SetParent(owner.booth.transform, true);
        tray.NetworkCarryLocked = false;
        WaiterHands.SetAllColliders(tray.gameObject, true);
        tray.GetComponent<FoodTrayInteractable>()?.SetCleanupPickable(true);
    }
}
