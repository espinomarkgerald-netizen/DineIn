using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

public partial class MultiplayerCustomerInteractionBridge
{
    [Serializable] private sealed class FoodRequest
    { public MultiplayerActionContext context; public long sequence; public int slot = -1; }
    private sealed class FoodAction
    { public FoodRequest request; public MultiplayerActionResult result; public float deadline, retryAt; }
    private readonly Dictionary<int, FoodAction> foodActions = new();
    private readonly Dictionary<(int actor, string id), MultiplayerActionResult> foodReceipts = new();
    private readonly Dictionary<int, long> foodSequences = new();
    private FoodAction pendingFoodAction;
    private long nextFoodSequence;
    private string foodRun;
    private int foodDay;
    private bool foodPickupAcknowledged;
    private float foodProjectionDeadline;

    private static string FoodClaim(MultiplayerActionContext context) => $"Order:{context.orderNumber}:Pickup";
    private static bool IsFoodOperation(MultiplayerActionContext context) => context != null
        && ((context.operation == "food_pickup" && context.expectedStage == "Prepared")
            || (context.operation == "food_deliver" && context.expectedStage == "Carried"));

    private void ResetFoodScope()
    {
        string run = session != null ? session.RunId : string.Empty;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        if (foodRun == run && foodDay == day) return;
        foodRun = run; foodDay = day;
        foodActions.Clear(); foodReceipts.Clear(); foodSequences.Clear();
        pendingFoodAction = null; foodPickupAcknowledged = false; carryPending = false;
    }

    private void SendFoodAction(CustomerGroup group, string operation, int slot)
    {
        ResetFoodScope();
        if (session == null || !session.CanAct || group == null || pendingFoodAction != null) return;
        string claim = $"Order:{group.currentOrderNumber}:Pickup";
        var context = MultiplayerActionContext.Capture(session, group, operation, claim,
            operation == "food_pickup" ? "Prepared" : "Carried");
        if (!IsFoodOperation(context) || context.lease <= 0 || !claims.IsClaimedBy(claim, session.LocalActorNumber)) return;
        pendingFoodAction = new FoodAction { request = new FoodRequest { context = context, slot = slot, sequence = ++nextFoodSequence },
            deadline = HygieneManager.ServiceTime + 10f, retryAt = HygieneManager.ServiceTime + 1f };
        if (operation == "food_pickup") carryPending = true;
        DispatchFoodAction();
    }

    private void DispatchFoodAction()
    {
        if (pendingFoodAction == null) return;
        var request = pendingFoodAction.request;
        string json = JsonUtility.ToJson(request);
        if (session.IsAuthority) ReceiveFoodRequest(session.LocalActorNumber, json);
        else MultiplayerWire.Raise(request.context.operation == "food_pickup" ? CarryRequestEvent : ServeRequestEvent,
            json, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private bool RetainCommittedFoodPickup()
    {
        if (session == null || !session.CanAct || pickupCancelled || pickupTarget == null || pickupTaskId == null) return false;
        var group = pickupTarget.TargetGroup;
        if (group == null || group.currentOrderNumber != pickupTarget.orderNumber) return false;
        if (!claims.IsClaimedBy(pickupTaskId, session.LocalActorNumber)) return false;
        if (pendingFoodAction != null && pendingFoodAction.request.context.MatchesOrder(group)) return true;
        if (foodPickupAcknowledged) return true;
        var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        var hands = session.LocalManager != null ? session.LocalManager.GetComponent<WaiterHands>() : null;
        return customer != null && customer.CarrierActorNumber == session.LocalActorNumber
            && !customer.CarrierNeedsRecovery && group.state == CustomerGroup.GroupState.OrderTaken
            || (hands != null && hands.holdingTray == pickupTarget && pickupTarget.NetworkCarryLocked);
    }

    private void ForgetFoodIntent()
    {
        // Local intent only. Authority recovery owns physical items and leases.
        pendingFoodAction = null; foodPickupAcknowledged = false; carryPending = false;
    }

    private void TickFoodActions()
    {
        ResetFoodScope();
        if (session == null || !session.CanAct) { ForgetFoodIntent(); return; }
        if (foodPickupAcknowledged)
        {
            var hands = session.LocalManager != null ? session.LocalManager.GetComponent<WaiterHands>() : null;
            bool attached = pickupTarget != null && hands != null && hands.holdingTray == pickupTarget
                && pickupTarget.transform.IsChildOf(hands.TrayHoldPoint);
            if (attached) { foodPickupAcknowledged = false; carryPending = false; }
            else if (HygieneManager.ServiceTime >= foodProjectionDeadline)
            {
                // A presentation dependency timing out must not abandon the
                // host's already-accepted physical pickup or its claim.
                carryPending = false; foodProjectionDeadline = float.PositiveInfinity;
                WarningSlideUI.Instance?.Show("The tray view is still catching up. Your food is retained; select its current action to retry.");
            }
        }
        if (pendingFoodAction == null) return;
        var context = pendingFoodAction.request.context;
        if (!context.MatchesSession(session, session.LocalActorNumber)) { ForgetFoodIntent(); return; }
        if (HygieneManager.ServiceTime >= pendingFoodAction.deadline)
        {
            bool pickupMayHaveCommitted = pendingFoodAction.request.context.operation == "food_pickup";
            ForgetFoodIntent();
            // A lost reply is not proof that the host failed to attach the
            // tray. Keep its lease until a retry or the snapshot resolves it.
            foodPickupAcknowledged = pickupMayHaveCommitted;
            foodProjectionDeadline = float.PositiveInfinity;
            WarningSlideUI.Instance?.Show("The food action timed out. Select its current action to retry.");
            return;
        }
        if (HygieneManager.ServiceTime >= pendingFoodAction.retryAt)
        { pendingFoodAction.retryAt = HygieneManager.ServiceTime + 1f; DispatchFoodAction(); }
    }

    private void ReceiveFoodRequest(int sender, object payload)
    {
        if (HygieneManager.Defer(() => { if (this != null) ReceiveFoodRequest(sender, payload); })) return;
        if (session == null || !session.IsAuthority || payload is not string json || json.Length > 8192) return;
        ResetFoodScope();
        FoodRequest request;
        try { request = JsonUtility.FromJson<FoodRequest>(json); } catch (ArgumentException) { return; }
        var context = request?.context;
        if (!IsFoodOperation(context) || !context.MatchesSession(session, sender) || request.sequence <= 0) return;
        if (foodReceipts.TryGetValue((sender, context.actionId), out var receipt))
        { if (receipt.context.MatchesAction(context)) SendFoodResult(receipt); return; }
        if (foodActions.TryGetValue(sender, out var active) && active.request.context.MatchesAction(context)) return;
        if (foodSequences.TryGetValue(sender, out long sequence) && sequence >= request.sequence)
        { CompleteFoodAction(new FoodAction { request = request }, MultiplayerActionOutcome.Cancelled); return; }
        foodSequences[sender] = request.sequence;
        if (active != null && active.result == null) CompleteFoodAction(active, MultiplayerActionOutcome.Cancelled);
        var action = new FoodAction { request = request };
        foodActions[sender] = action;
        StartCoroutine(ValidateFoodArrival(action));
    }

    private IEnumerator ValidateFoodArrival(FoodAction action)
    {
        var context = action.request.context;
        float deadline = HygieneManager.ServiceTime + 0.4f;
        while (session != null && session.IsAuthority && context.MatchesSession(session, context.actor)
            && foodActions.TryGetValue(context.actor, out var active) && active == action && action.result == null)
        {
            if (HygieneManager.DecisionPaused) { yield return null; continue; }
            var outcome = ValidateFoodAction(action.request, out var customer, out var manager, out var tray,
                out var stand, out float radius, out var drop);
            if (outcome == MultiplayerActionOutcome.Success)
            {
                bool pickup = context.operation == "food_pickup";
                var hands = manager.GetComponent<WaiterHands>();
                bool accepted = pickup
                    ? customer.CarrierActorNumber == context.actor && hands.holdingTray == tray
                        || customer.CommitTrayPickup(context.actor, hands)
                    : customer.CommitServe(hands, tray, drop);
                CompleteFoodAction(action, accepted ? MultiplayerActionOutcome.Success : MultiplayerActionOutcome.StaleStage);
                // Receipt is stored first. Ownership notifications and reordered
                // retries cannot serve twice or turn success into a rejection.
                if (accepted && !pickup) claims.CompleteOnAuthority(FoodClaim(context), context.actor);
                yield break;
            }
            if (outcome != MultiplayerActionOutcome.Unreachable || HygieneManager.ServiceTime >= deadline)
            { CompleteFoodAction(action, outcome); yield break; }
            yield return null;
        }
    }

    private MultiplayerActionOutcome ValidateFoodAction(FoodRequest request, out MultiplayerCustomerSpawn customer,
        out GameObject manager, out FoodTray tray, out Transform stand, out float radius, out Transform drop)
    {
        var context = request.context;
        customer = null; manager = null; tray = null; stand = null; radius = 0f; drop = null;
        var group = MultiplayerServiceActions.Resolve(context.customerView);
        if (!context.MatchesOrder(group)) return MultiplayerActionOutcome.WrongTarget;
        if (context.expectedRevision < 0 || context.expectedRevision > claims.CurrentRevision) return MultiplayerActionOutcome.StaleStage;
        if (!claims.IsClaimedBy(FoodClaim(context), context.actor) || context.lease <= 0
            || claims.GetLease(FoodClaim(context)) != context.lease) return MultiplayerActionOutcome.AnotherOwner;
        if (!session.TryGetManager(context.actor, out manager) || manager == null || !manager.activeInHierarchy)
            return MultiplayerActionOutcome.Unavailable;
        customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        tray = kitchen != null ? kitchen.GetPreparedResult(context.orderNumber) : null;
        if (customer == null || tray == null || tray.TargetGroup != group || tray.orderNumber != context.orderNumber
            || !tray.gameObject.activeInHierarchy || !group.gameObject.activeInHierarchy || group.IsNetworkObserver)
            return MultiplayerActionOutcome.WrongTarget;
        if (group.state != CustomerGroup.GroupState.OrderTaken || !group.HasConfirmedOrder || group.IsPlayerReviewingOrder)
            return MultiplayerActionOutcome.StaleStage;
        var hands = manager.GetComponent<WaiterHands>();
        if (hands == null) return MultiplayerActionOutcome.Unavailable;
        bool pickup = context.operation == "food_pickup";
        if (pickup && customer.CarrierActorNumber == context.actor && hands.holdingTray == tray && tray.NetworkCarryLocked)
            return MultiplayerActionOutcome.Success;
        if (pickup)
        {
            if (hands.HasTray || hands.HasBill || hands.HasMoney || hands.HasTicket || manager.GetComponent<BusserHands>()?.HasTray == true
                || manager.GetComponentInChildren<TakeoutBagInteractable>() != null) return MultiplayerActionOutcome.OccupiedHands;
            if (customer.CarrierActorNumber != 0 || tray.NetworkCarryLocked) return MultiplayerActionOutcome.AnotherOwner;
            if (!kitchen.TryGetPreparedTray(context.orderNumber, out var prepared) || prepared != tray
                || !kitchen.TryGetPreparedSlot(context.orderNumber, out int slot) || slot != request.slot)
                return MultiplayerActionOutcome.StaleStage;
            var interaction = tray.GetComponent<FoodTrayInteractable>();
            if (interaction == null || !interaction.isActiveAndEnabled) return MultiplayerActionOutcome.Unavailable;
            stand = interaction.StandPoint; radius = interaction.GetInteractRadius();
        }
        else
        {
            if (hands.holdingTray != tray || customer.CarrierActorNumber != context.actor || !tray.NetworkCarryLocked)
                return MultiplayerActionOutcome.WrongTarget;
            if (customer.CarrierNeedsRecovery || group.assignedBooth == null || group.assignedBooth.CurrentGroup != group
                || group.submittedOrder == null || group.currentOrder == null) return MultiplayerActionOutcome.StaleStage;
            drop = ServePoint(group, out stand, out radius);
            if (drop == null) return MultiplayerActionOutcome.Unavailable;
        }
        if (stand == null) return MultiplayerActionOutcome.Unavailable;
        var delta = NetworkPlayerMovementSync.AuthorityPosition(manager) - stand.position; delta.y = 0;
        return delta.sqrMagnitude <= (radius + 0.35f) * (radius + 0.35f)
            ? MultiplayerActionOutcome.Success : MultiplayerActionOutcome.Unreachable;
    }

    private void CompleteFoodAction(FoodAction action, MultiplayerActionOutcome outcome)
    {
        if (action.result != null) return;
        var context = action.request.context;
        action.result = new MultiplayerActionResult { context = context, outcome = outcome, revision = claims.CurrentRevision };
        foodReceipts[(context.actor, context.actionId)] = action.result;
        // Sequences still reject evicted old actions; the receipt cache stays bounded.
        if (foodReceipts.Count > 4096)
        {
            foreach (var key in foodReceipts.Keys)
                if (key != (context.actor, context.actionId)) { foodReceipts.Remove(key); break; }
        }
        SendFoodResult(action.result);
    }

    private void SendFoodResult(MultiplayerActionResult result)
    {
        if (result.context.actor == session.LocalActorNumber) ApplyFoodResult(result);
        else MultiplayerWire.Raise(result.context.operation == "food_pickup" ? CarryRejectedEvent : ServeRejectedEvent,
            JsonUtility.ToJson(result), new RaiseEventOptions { TargetActors = new[] { result.context.actor } }, SendOptions.SendReliable);
    }

    private void ReceiveFoodReply(int sender, object payload)
    {
        if (session == null || sender != session.Run?.hostActor || payload is not string json || json.Length > 8192) return;
        try { ApplyFoodResult(JsonUtility.FromJson<MultiplayerActionResult>(json)); } catch (ArgumentException) { }
    }

    private void ApplyFoodResult(MultiplayerActionResult result)
    {
        if (pendingFoodAction == null || result?.context == null || result.context.actor != session.LocalActorNumber
            || !result.context.MatchesAction(pendingFoodAction.request.context)) return;
        pendingFoodAction = null;
        if (!result.Accepted)
        {
            carryPending = false; foodPickupAcknowledged = false;
            if (result.context.operation == "food_pickup" && !RetainCommittedFoodPickup()) CancelPickup();
            if (result.outcome != MultiplayerActionOutcome.Cancelled) WarningSlideUI.Instance?.Show(MultiplayerActionFeedback.Message(result.outcome));
            return;
        }
        if (result.context.operation == "food_pickup")
        {
            // The customer/item snapshot is the sole source of attachment.
            foodPickupAcknowledged = true; carryPending = true; foodProjectionDeadline = HygieneManager.ServiceTime + 8f;
        }
        else
        {
            ForgetFoodIntent(); pickupTaskId = null; pickupTarget = null;
            pickupCancelled = true; pickupPending = false;
        }
    }
}
