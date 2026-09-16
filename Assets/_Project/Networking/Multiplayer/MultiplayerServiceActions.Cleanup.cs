using System.Collections.Generic;
using UnityEngine;

public sealed partial class MultiplayerServiceActions
{
    private sealed class DirtyOwner { public int actor; public Vector3 origin; public Quaternion rotation; public Booth booth; }
    private readonly Dictionary<int, DirtyOwner> dirtyOwners = new();
    private Reply deferredDirtyPickup;
    private float dirtyAttachUntil;
    private PlayerMovement dirtyDisposalMover;
    private PlayerMovement finishedDirtyDisposalMover;
    private PlayerMovement dirtyPickupMover;
    private uint dirtyPickupCommandVersion, dirtyDisposalCommandVersion, finishedDirtyCommandVersion;
    private bool suppressDirtyContinuation;
    private readonly HashSet<FoodTray> completedDirtyReplicas = new();

    public static bool IsCompletedDirtyReplica(FoodTray tray) => Active != null && tray != null
        && Active.completedDirtyReplicas.Contains(tray);

    public static bool IsAwaitingDirtyDisposal(PlayerMovement mover) => IsActive && Active != null
        && Active.ShouldHoldDirtyPosition(mover);

    internal bool ShouldHoldDirtyPosition(PlayerMovement mover) => mover != null
        && (dirtyDisposalMover == mover && pending?.operation == "cleanup_wash" && mover.CommandVersion == dirtyDisposalCommandVersion
            || dirtyPickupMover == mover && !suppressDirtyContinuation && mover.CommandVersion == dirtyPickupCommandVersion
            && (pending?.operation == "cleanup_pickup" || deferredDirtyPickup != null));
    public static bool CanCollectDirtyTray(FoodTray tray)
    {
        var active = Active;
        if (active != null) WaiterHands.ReconcileMultiplayerHands(active.session.LocalManager);
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
        Active.ApproachClaimed($"Order:{order}:Cleanup", mover, pickup.StandPoint, pickup.GetInteractRadius(), () =>
        {
            Active.suppressDirtyContinuation = false;
            Active.dirtyPickupMover = mover;
            Active.dirtyPickupCommandVersion = mover.CommandVersion;
            SendRequest("cleanup_pickup", 0, order);
        });
    }
    public static bool WashDirtyTray()
    {
        var active = Active;
        var hands = active != null ? active.session.LocalManager?.GetComponent<BusserHands>() : null;
        if (hands == null || !hands.HasTray || active.pending != null) return false;
        active.dirtyDisposalMover = hands.GetComponent<PlayerMovement>();
        active.dirtyDisposalCommandVersion = active.dirtyDisposalMover != null ? active.dirtyDisposalMover.CommandVersion : 0;
        if (SendRequest("cleanup_wash", 0, hands.holdingTray.orderNumber)) return true;
        active.dirtyDisposalMover = null;
        return false;
    }

    private void ReceiveCleanup(Reply reply)
    {
        if (reply.operation == "cleanup_pickup" && reply.accepted)
        {
            deferredDirtyPickup = reply;
            dirtyAttachUntil = Time.unscaledTime + 8f;
        }
        if (reply.operation != "cleanup_wash") return;
        var mover = dirtyDisposalMover;
        dirtyDisposalMover = null;
        if (!reply.accepted) return; // Still holding it: clicking the sink can retry.
        var hands = session.LocalManager?.GetComponent<BusserHands>();
        if (hands != null && hands.holdingTray != null && hands.holdingTray.orderNumber == reply.value)
        {
            var tray = hands.holdingTray;
            hands.holdingTray = null; // Projection only; authority already completed cleanup.
            completedDirtyReplicas.Add(tray);
            tray.gameObject.SetActive(false);
        }
        finishedDirtyDisposalMover = mover;
        finishedDirtyCommandVersion = dirtyDisposalCommandVersion;
    }

    private void TickDirtyContinuation()
    {
        if (finishedDirtyDisposalMover != null)
        {
            var finished = finishedDirtyDisposalMover;
            finishedDirtyDisposalMover = null;
            if (finished.CommandVersion == finishedDirtyCommandVersion && finished.CurrentTarget == null
                && finished.CurrentState == PlayerMovement.State.IdleAtHome)
                finished.FinishCurrentJob();
        }
        if ((pending?.operation == "cleanup_pickup" || deferredDirtyPickup != null)
            && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))) suppressDirtyContinuation = true;
        if (deferredDirtyPickup == null) return;
        var context = deferredDirtyPickup.result.context;
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        var mover = session.LocalManager?.GetComponent<PlayerMovement>();
        if (!session.CanAct || !context.MatchesSession(session, session.LocalActorNumber) || mover == null
            || !claims.IsClaimedBy($"Order:{context.orderNumber}:Cleanup", session.LocalActorNumber)
            || claims.GetLease($"Order:{context.orderNumber}:Cleanup") != context.lease)
        { deferredDirtyPickup = null; dirtyPickupMover = null; return; }
        if (suppressDirtyContinuation || mover != dirtyPickupMover || mover.CommandVersion != dirtyPickupCommandVersion)
        { deferredDirtyPickup = null; dirtyPickupMover = null; return; }
        var hands = mover.GetComponent<BusserHands>();
        if (hands != null && hands.holdingTray != null && hands.holdingTray.orderNumber == context.orderNumber
            && hands.holdingTray.transform.IsChildOf(hands.TrayHoldPoint))
        {
            if (mover.CurrentTarget == null && mover.CurrentState == PlayerMovement.State.IdleAtHome)
            {
                // Consume only once the original movement has finished. A new
                // command (even one already completed) supersedes this intent.
                deferredDirtyPickup = null; dirtyPickupMover = null;
                var sink = FindFirstObjectByType<SinkInteractable>();
                if (sink == null || !mover.UI_MoveToAction(sink.StandPoint, sink.GetInteractRadius(),
                    () => WashDirtyTray(), () => { }))
                    WarningSlideUI.Instance?.Show("Take the dirty tray to the sink to finish cleaning.");
                return;
            }
        }
        if (Time.unscaledTime >= dirtyAttachUntil)
        {
            deferredDirtyPickup = null; dirtyPickupMover = null;
            WarningSlideUI.Instance?.Show("The tray is still synchronizing. Select the sink when it appears in your hands.");
        }
    }
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
            pickup.SetCleanupPickable(false);
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
            tray.GetComponentInParent<BusserHands>(true)?.ReleaseTrayForRetry(owner.origin);
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
        tray.GetComponentInParent<BusserHands>(true)?.ReleaseTrayForRetry(owner.origin);
        tray.transform.SetPositionAndRotation(owner.origin, owner.rotation);
        if (owner.booth != null) tray.transform.SetParent(owner.booth.transform, true);
        tray.NetworkCarryLocked = false;
        WaiterHands.SetAllColliders(tray.gameObject, true);
        tray.GetComponent<FoodTrayInteractable>()?.SetCleanupPickable(true);
    }
}
