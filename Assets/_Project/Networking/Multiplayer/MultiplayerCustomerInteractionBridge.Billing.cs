using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

public partial class MultiplayerCustomerInteractionBridge
{
    [Serializable] private sealed class BillRequest
    { public MultiplayerActionContext context; public long sequence; public int stage; }
    [Serializable] private sealed class BillReply
    { public MultiplayerActionResult result; public int stage; public Vector3 position; public Quaternion rotation; }
    private sealed class BillAction
    {
        public BillRequest request;
        public float deadline, retryAt;
        public BillReply reply;
        public int view => request.context.customerView;
        public int order => request.context.orderNumber;
        public long lease => request.context.lease;
    }
    private long nextBillAction;
    private BillAction pendingBillAction;
    private readonly Dictionary<(int actor, int view), BillAction> billActions = new();
    private readonly Dictionary<(int actor, string id), BillReply> billReceipts = new();
    private readonly Dictionary<(int actor, int view), long> billSequences = new();
    private int billActionDay;
    private MultiplayerCustomerSpawn.BillStage awaitingBillProjection;
    private float billProjectionDeadline;

    private bool BillActionIsCurrent(int actor, BillAction action, int day) => session != null && session.IsAuthority
        && GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentDay == day
        && action.request.context.MatchesSession(session, actor)
        && billActions.TryGetValue((actor, action.view), out var current) && current == action
        && claims.IsClaimedBy($"Customer:{action.view}:Bill", actor)
        && claims.GetLease($"Customer:{action.view}:Bill") == action.lease;

    public void InvalidateBillAction(int view)
    {
        foreach (var key in new List<(int actor, int view)>(billActions.Keys))
        {
            if (key.view != view) continue;
            var action = billActions[key];
            if (action.reply == null)
                CompleteBillAction(view, 4, key.actor, Vector3.zero, Quaternion.identity, null, MultiplayerActionOutcome.Cancelled);
            billActions.Remove(key);
        }
        printingBills.Remove(view);
    }
    private void SendBillAction(int view, int stage, int order)
    {
        TickBillActions();
        string operation = stage == 0 ? "bill_request" : stage == 2 ? "bill_pickup" : "bill_deliver";
        string expected = stage == 0 ? "NeedsBill" : stage == 2 ? "Printed" : "Carried";
        var context = MultiplayerActionContext.Capture(session, billTarget, operation, $"Customer:{view}:Bill", expected);
        pendingBillAction = new BillAction { request = new BillRequest { context = context, sequence = ++nextBillAction, stage = stage },
            deadline = HygieneManager.ServiceTime + 40f, retryAt = HygieneManager.ServiceTime + 1f };
        DispatchBillAction();
    }
    private void DispatchBillAction()
    {
        if (pendingBillAction == null) return;
        string payload = JsonUtility.ToJson(pendingBillAction.request);
        if (session.IsAuthority) ReceiveBillAction(session.LocalActorNumber, payload);
        else MultiplayerWire.Raise(BillFlowEvent, payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    private void ResetBillActionDay()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        if (billActionDay == day) return;
        billActionDay = day; billActions.Clear(); billReceipts.Clear(); billSequences.Clear();
        pendingBillAction = null; printingBills.Clear(); awaitingBillProjection = MultiplayerCustomerSpawn.BillStage.None;
    }
    private void TickBillActions()
    {
        ResetBillActionDay();
        if (awaitingBillProjection != MultiplayerCustomerSpawn.BillStage.None && HygieneManager.ServiceTime >= billProjectionDeadline)
        {
            WarningSlideUI.Instance?.Show("The bill view did not arrive. Its paper is retained; select the customer to retry.");
            CancelBill();
        }
        if (pendingBillAction == null) return;
        if (HygieneManager.ServiceTime >= pendingBillAction.deadline)
        {
            WarningSlideUI.Instance?.Show("Billing timed out. Select the bill action to retry.");
            pendingBillAction = null; billTransitionPending = false;
            PauseBillApproach(); return;
        }
        if (HygieneManager.ServiceTime >= pendingBillAction.retryAt)
        { pendingBillAction.retryAt = HygieneManager.ServiceTime + 1f; DispatchBillAction(); }
    }
    private void ReceiveBillAction(int actor, object payload)
    {
        if (HygieneManager.Defer(() => { if (this != null) ReceiveBillAction(actor, payload); })) return;
        if (session == null || payload is not string json || json.Length > 8192) return;
        ResetBillActionDay();
        try
        {
            if (!session.IsAuthority)
            {
                if (actor == session.Run?.hostActor) ReceiveBillReply(JsonUtility.FromJson<BillReply>(json));
                return;
            }
            var request = JsonUtility.FromJson<BillRequest>(json);
            var context = request?.context;
            if (context == null || !context.MatchesSession(session, actor) || request.sequence <= 0) return;
            string operation = request.stage == 0 ? "bill_request" : request.stage == 2 ? "bill_pickup" : request.stage == 6 ? "bill_deliver" : null;
            string stage = request.stage == 0 ? "NeedsBill" : request.stage == 2 ? "Printed" : "Carried";
            if (operation == null || context.operation != operation || context.expectedStage != stage) return;
            var receiptKey = (actor, context.actionId);
            if (billReceipts.TryGetValue(receiptKey, out var receipt))
            { if (receipt.result.context.MatchesAction(context)) SendBillReply(actor, receipt); return; }
            var key = (actor, context.customerView);
            if (billActions.TryGetValue(key, out var active) && active.request.context.MatchesAction(context)) return;
            if (billSequences.TryGetValue(key, out long sequence) && sequence >= request.sequence)
            { RejectBillRequest(request, MultiplayerActionOutcome.Cancelled); return; }
            billSequences[key] = request.sequence;
            var group = MultiplayerServiceActions.Resolve(context.customerView);
            if (!context.MatchesOrder(group)) { RejectBillRequest(request, MultiplayerActionOutcome.WrongTarget); return; }
            string claim = $"Customer:{context.customerView}:Bill";
            if (!claims.IsClaimedBy(claim, actor) || claims.GetLease(claim) != context.lease || context.lease <= 0
                || context.expectedRevision > claims.CurrentRevision)
            { RejectBillRequest(request, MultiplayerActionOutcome.AnotherOwner); return; }
            if (active != null && active.reply == null)
            {
                CompleteBillAction(active.view, 4, actor, Vector3.zero, Quaternion.identity, null, MultiplayerActionOutcome.Cancelled);
                printingBills.Remove(active.view);
            }
            var action = new BillAction { request = request };
            billActions[key] = action;
            StartCoroutine(ValidateBillArrival(actor, action, billActionDay));
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid bill action payload."); }
    }
    private void RejectBillRequest(BillRequest request, MultiplayerActionOutcome outcome)
    {
        var reply = new BillReply { result = new MultiplayerActionResult { context = request.context,
            outcome = outcome, revision = claims.CurrentRevision }, stage = 4 };
        billReceipts[(request.context.actor, request.context.actionId)] = reply;
        SendBillReply(request.context.actor, reply);
    }
    private IEnumerator ValidateBillArrival(int actor, BillAction action, int day)
    {
        float deadline = HygieneManager.ServiceTime + 0.4f;
        while (BillActionIsCurrent(actor, action, day))
        {
            if (HygieneManager.DecisionPaused) { yield return null; continue; }
            var group = MultiplayerServiceActions.Resolve(action.view);
            if (!action.request.context.MatchesOrder(group))
            { ReplyBill(action.view, 4, actor, null, MultiplayerActionOutcome.WrongTarget); yield break; }
            var paper = BillManager.Instance?.FindBillForGroup(group);
            int stage = action.request.stage;
            if (stage == 6 && group.HasReceivedBill)
            { ReplyBill(action.view, 5, actor, null); yield break; }
            if (stage == 2 && session.TryGetManager(actor, out var heldOwner)
                && paper != null && paper.GetComponentInParent<WaiterHands>(true) == heldOwner.GetComponent<WaiterHands>())
            { ReplyBill(action.view, 3, actor, paper); yield break; }
            var booth = group.assignedBooth;
            Transform stand = stage == 2 ? paper?.StandPoint
                : booth != null ? booth.approachPoint != null ? booth.approachPoint : booth.transform : null;
            float radius = stage == 2 ? paper != null ? paper.GetInteractRadius() : 0f : 2.75f;
            if (stand == null) { ReplyBill(action.view, 4, actor, null, MultiplayerActionOutcome.Unavailable); yield break; }
            bool arrived = session.TryGetManager(actor, out var root) && BillWithinReach(root, stand, radius);
            if (arrived) { HandleBillFlow(action.view, stage, actor); yield break; }
            if (HygieneManager.ServiceTime >= deadline)
            { ReplyBill(action.view, 4, actor, null, MultiplayerActionOutcome.Unreachable); yield break; }
            yield return null;
        }
    }
    private static bool BillWithinReach(GameObject root, Transform stand, float radius)
    {
        if (root == null || stand == null) return false;
        var delta = NetworkPlayerMovementSync.AuthorityPosition(root) - stand.position;
        delta.y = 0;
        return delta.sqrMagnitude <= (radius + 0.35f) * (radius + 0.35f);
    }
    private void CompleteBillAction(int view, int stage, int actor, Vector3 position, Quaternion rotation, BillPaper paper,
        MultiplayerActionOutcome outcome = MultiplayerActionOutcome.Success)
    {
        if (!billActions.TryGetValue((actor, view), out var action) || action.reply != null) return;
        var reply = new BillReply { result = new MultiplayerActionResult { context = action.request.context,
            outcome = outcome, revision = claims.CurrentRevision }, stage = stage, position = position, rotation = rotation };
        action.reply = reply;
        billReceipts[(actor, action.request.context.actionId)] = reply;
        SendBillReply(actor, reply);
    }
    private void SendBillReply(int actor, BillReply reply)
    {
        if (actor == session.LocalActorNumber) ReceiveBillReply(reply);
        else MultiplayerWire.Raise(BillFlowEvent, JsonUtility.ToJson(reply),
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }
    private void ReceiveBillReply(BillReply reply)
    {
        if (pendingBillAction == null || reply?.result?.context == null
            || reply.result.context.actor != session.LocalActorNumber
            || !reply.result.context.MatchesAction(pendingBillAction.request.context)) return;
        pendingBillAction = null;
        if (!reply.result.Accepted)
        {
            billTransitionPending = false;
            if (reply.result.outcome != MultiplayerActionOutcome.Cancelled)
                WarningSlideUI.Instance?.Show(MultiplayerActionFeedback.Message(reply.result.outcome));
            PauseBillApproach(); return;
        }
        ObserveBill(reply.result.context.customerView, reply.stage, reply.position, reply.rotation);
    }
}
