using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public partial class MultiplayerCustomerInteractionBridge
{
    [Serializable] private sealed class ReviewedOrderCommand
    {
        public MultiplayerActionContext context;
        public ReviewedOrderSelection selection;
    }
    private sealed class ReviewedOrderReceipt
    {
        public string requestJson;
        public MultiplayerActionResult result;
    }
    private readonly Dictionary<string, ReviewedOrderReceipt> reviewedOrderReceipts = new();
    private readonly Queue<string> reviewedOrderReceiptOrder = new();
    private ReviewedOrderCommand pendingReviewedOrder;
    private float reviewedOrderStarted, nextReviewedOrderRetry;
    private string reviewedOrderRun;
    private int reviewedOrderDay;

    public static bool TryConfirmOrder(CustomerGroup group, ReviewedOrderSelection selection)
    {
        if (!ReviewIsMultiplayer) return false;
        if (!CanOpenReview(group)) return true;
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerCustomerInteractionBridge>();
        bridge.RefreshReviewedOrderScope();
        if (bridge.pendingReviewedOrder != null) return true;
        var command = new ReviewedOrderCommand
        {
            context = MultiplayerActionContext.Capture(bridge.session, group, "order_confirm",
                bridge.orderTaskId, "reviewing"),
            selection = selection
        };
        bridge.pendingReviewedOrder = command;
        bridge.reviewedOrderStarted = Time.unscaledTime;
        bridge.SendReviewedOrder(command);
        return true;
    }

    private void RefreshReviewedOrderScope()
    {
        string run = session != null ? session.RunId : null;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        if (reviewedOrderRun == run && reviewedOrderDay == day) return;
        reviewedOrderRun = run;
        reviewedOrderDay = day;
        reviewedOrderReceipts.Clear();
        reviewedOrderReceiptOrder.Clear();
        pendingReviewedOrder = null;
    }

    private void SendReviewedOrder(ReviewedOrderCommand command)
    {
        nextReviewedOrderRetry = Time.unscaledTime + 1f;
        string json = JsonUtility.ToJson(command);
        if (session.IsAuthority) ReceiveReviewedOrderCommand(session.LocalActorNumber, json);
        else MultiplayerWire.Raise(ConfirmRequestEvent, json,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private void TickReviewedOrderSubmission()
    {
        RefreshReviewedOrderScope();
        var command = pendingReviewedOrder;
        if (command == null) return;
        if (!command.context.MatchesSession(session, session.LocalActorNumber))
        { pendingReviewedOrder = null; return; }
        if (Time.unscaledTime - reviewedOrderStarted >= 40f)
        {
            pendingReviewedOrder = null;
            if (OrderSubmissionStillVisible(command.context))
                WarningSlideUI.Instance?.Show("Order confirmation timed out. Check the customer and try again.");
            return;
        }
        if (Time.unscaledTime >= nextReviewedOrderRetry) SendReviewedOrder(command);
    }

    private bool OrderSubmissionStillVisible(MultiplayerActionContext context) => context != null
        && !orderCancelled && orderTarget != null && orderTarget.photonView.ViewID == context.customerView
        && orderTarget.Group != null && orderTarget.Group.currentOrderNumber == context.orderNumber;

    private void ReceiveReviewedOrderCommand(int sender, object payload)
    {
        if (session == null || !session.IsAuthority || payload is not string json || json.Length > 32768) return;
        ReviewedOrderCommand command;
        try { command = JsonUtility.FromJson<ReviewedOrderCommand>(json); }
        catch (ArgumentException) { return; }
        var context = command?.context;
        if (context == null || !context.MatchesSession(session, sender)
            || context.operation != "order_confirm" || context.expectedStage != "reviewing") return;
        RefreshReviewedOrderScope();
        string receiptKey = sender + ":" + context.actionId;
        if (reviewedOrderReceipts.TryGetValue(receiptKey, out var receipt))
        {
            if (receipt.requestJson == json) SendReviewedOrderResult(sender, receipt.result);
            return;
        }

        var view = PhotonView.Find(context.customerView);
        var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
        var group = customer != null ? customer.Group : null;
        string task = $"Customer:{context.customerView}:Order";
        var outcome = MultiplayerActionOutcome.StaleStage;
        string message = null;
        bool hasManager = session.TryGetManager(sender, out var manager);
        var hands = hasManager ? manager.GetComponent<WaiterHands>() : null;
        if (!context.MatchesOrder(group)) outcome = MultiplayerActionOutcome.WrongTarget;
        else if (group.IsNetworkObserver || group.IsTakeout || group.HasConfirmedOrder
            || group.state != CustomerGroup.GroupState.ReadyToOrder || !group.IsPlayerReviewingOrder
            || !customer.HasGeneratedOrder)
            outcome = MultiplayerActionOutcome.StaleStage;
        else if (claims == null || context.lease <= 0 || !claims.IsClaimedBy(task, sender)
            || claims.GetLease(task) != context.lease || context.expectedRevision > claims.CurrentRevision
            || customer.ReviewActor != sender)
            outcome = MultiplayerActionOutcome.AnotherOwner;
        else if (hands == null) outcome = MultiplayerActionOutcome.Unavailable;
        else if (hands.HasTray || hands.HasBill || hands.HasTicket || hands.HasMoney
            || (manager.GetComponent<BusserHands>() != null && manager.GetComponent<BusserHands>().HasTray))
            outcome = MultiplayerActionOutcome.OccupiedHands;
        else if (ReviewedOrderSubmission.TrySubmit(group, context.orderNumber, command.selection,
            MenuCatalog.Default, MultiplayerWorldRegistry.Kitchen, out var failure))
        {
            outcome = MultiplayerActionOutcome.Success;
            customer.ReviewActor = 0;
            customer.PublishAssignment();
            claims.CompleteOnAuthority(task, sender);
        }
        else
        {
            outcome = failure switch
            {
                ReviewedOrderSubmission.Failure.ChangedOrder => MultiplayerActionOutcome.StaleStage,
                ReviewedOrderSubmission.Failure.IncorrectSelection => MultiplayerActionOutcome.WrongTarget,
                ReviewedOrderSubmission.Failure.UnavailableStock => MultiplayerActionOutcome.UnavailableStock,
                _ => MultiplayerActionOutcome.Unavailable
            };
            message = ReviewedOrderSubmission.Message(failure);
        }

        var result = new MultiplayerActionResult
        {
            context = context, outcome = outcome, revision = claims != null ? claims.CurrentRevision : 0,
            message = message ?? MultiplayerActionFeedback.Message(outcome)
        };
        reviewedOrderReceipts.Add(receiptKey, new ReviewedOrderReceipt { requestJson = json, result = result });
        reviewedOrderReceiptOrder.Enqueue(receiptKey);
        while (reviewedOrderReceiptOrder.Count > 256) reviewedOrderReceipts.Remove(reviewedOrderReceiptOrder.Dequeue());
        SendReviewedOrderResult(sender, result);
    }

    private void SendReviewedOrderResult(int actor, MultiplayerActionResult result)
    {
        string json = JsonUtility.ToJson(result);
        if (actor == session.LocalActorNumber) ReceiveReviewedOrderResult(actor, json);
        else MultiplayerWire.Raise(ConfirmRejectedEvent, json,
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    private void ReceiveReviewedOrderResult(int sender, object payload)
    {
        if (session == null || PhotonNetwork.MasterClient == null || sender != PhotonNetwork.MasterClient.ActorNumber
            || payload is not string json || pendingReviewedOrder == null) return;
        MultiplayerActionResult result;
        try { result = JsonUtility.FromJson<MultiplayerActionResult>(json); }
        catch (ArgumentException) { return; }
        if (result?.context == null || !result.context.MatchesAction(pendingReviewedOrder.context)
            || result.context.actor != session.LocalActorNumber
            || !result.context.MatchesSession(session, session.LocalActorNumber)) return;
        pendingReviewedOrder = null; // Duplicate replies cannot produce another local effect.
        if (result.Accepted)
        {
            if (OrderSubmissionStillVisible(result.context))
                ProcessingBillIndicatorUI.Instance?.ShowForSeconds("Order Sent to Kitchen", 2f);
            // A newer customer snapshot may already have closed this notepad.
            if (orderTarget != null && orderTarget.photonView.ViewID == result.context.customerView) CancelOrder();
        }
        else if (OrderSubmissionStillVisible(result.context))
            WarningSlideUI.Instance?.Show(string.IsNullOrWhiteSpace(result.message)
                ? MultiplayerActionFeedback.Message(result.outcome) : result.message);
    }
}
