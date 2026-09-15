using System;
using Photon.Pun;
using UnityEngine;

public partial class MultiplayerCustomerSpawn
{
    [Serializable] private sealed class ServiceState
    {
        public string run, booth, orderJson, thought, billHolder;
        public int day, revision, phase, orderNumber, reviewActor, mood, result, billStage, billOwner, preparedSlot = -1;
        public int[] choices;
        public int carrier, kitchenState;
        public bool carrierRecovery, hasKitchen, kitchenPaused, kitchenAwaiting;
        public float kitchenStarted, preparation, cook, kitchenReady, authorityTime;
        public double sentAt;
        public bool reviewing, confirmed, complaint, paid, billDelivered, ready, greeted, patienceVisible;
        public bool eatingStarted, eatingComplete;
        public double eatingAt;
        public float eatingDuration, patience;
        public Vector3 queuePosition, paperPosition;
        public Quaternion paperRotation;
    }
    private int serviceRevision, appliedServiceRevision;
    private string lastServicePayload;
    private ServiceState pendingServiceState;
    private PhotonMessageInfo pendingServiceInfo;
    private float nextServiceBaseline;

    private void PublishServiceState()
    {
        using var measurement = MultiplayerDiagnostics.CustomerState.Auto();
        if (!simulating || session == null || !session.IsAuthority || Group == null) return;
        var paper = BillManager.Instance?.FindBillForGroup(Group);
        var holder = paper != null ? paper.GetComponentInParent<WaiterHands>(true) : null;
        BillOwnerActor = session.GetComponent<MultiplayerTaskClaims>().GetOwner($"Customer:{photonView.ViewID}:Bill");
        CurrentBillStage = Group.MultiplayerPaymentComplete ? BillStage.Paid
            : Group.HasReceivedBill ? MultiplayerWorldRegistry.MoneyFor(Group) != null ? BillStage.PaymentAvailable : BillStage.Complete
            : holder != null ? BillStage.Carried : paper != null ? BillStage.Printed
            : BillManager.Instance != null && BillManager.Instance.IsPrintingFor(Group) ? BillStage.Printing : BillStage.None;
        billSnapshotOrder = Group.currentOrderNumber;
        billPosition = paper != null ? paper.transform.position : Vector3.zero;
        billRotation = paper != null ? paper.transform.rotation : Quaternion.identity;
        var state = new ServiceState { run = session.RunId, day = GameFlowManager.Instance.CurrentDay,
            booth = Group.assignedBooth != null ? MultiplayerCustomerInteractionBridge.BoothIdentity(Group.assignedBooth) : "",
            phase = (int)Group.state, orderJson = generatedOrderJson, orderNumber = Group.currentOrderNumber,
            choices = generatedLegacyChoices, confirmed = Group.HasConfirmedOrder,
            reviewing = Group.IsPlayerReviewingOrder, reviewActor = ReviewActor, thought = Group.OutcomeThought,
            mood = Group.OutcomeThoughtMood, result = Group.ObservedFinalResult, complaint = Group.ComplaintPending,
            paid = Group.MultiplayerPaymentComplete, billDelivered = Group.HasReceivedBill,
            billStage = (int)CurrentBillStage, billOwner = BillOwnerActor,
            billHolder = MultiplayerWorldRegistry.HolderId(holder), paperPosition = holder == null ? billPosition : Vector3.zero,
            paperRotation = holder == null ? billRotation : Quaternion.identity,
            ready = ReadyForInteraction, greeted = Group.hasBeenGreeted,
            queuePosition = Group.QueueDestination, patience = Mathf.Round(Group.QueuePatience01 * 100f) / 100f,
            patienceVisible = Group.QueuePatienceVisible, eatingStarted = Group.MultiplayerEatingStarted,
            eatingAt = Group.MultiplayerEatingStartedAt, eatingDuration = Group.MultiplayerEatingDuration,
            eatingComplete = Group.MultiplayerEatingComplete };
        state.carrier = CarrierActorNumber; state.carrierRecovery = CarrierNeedsRecovery;
        var kitchen = MultiplayerWorldRegistry.Kitchen;
        if (Group.HasConfirmedOrder && kitchen != null && kitchen.TryGetForecast(Group.currentOrderNumber, out var forecast) && forecast.Group == Group)
        {
            state.hasKitchen = true; state.kitchenState = (int)forecast.State;
            state.kitchenStarted = forecast.StartedAt; state.preparation = forecast.PreparationDelaySeconds;
            state.cook = forecast.CookDurationSeconds; state.kitchenReady = forecast.PredictedReadyAt;
            state.kitchenPaused = forecast.IsPaused; state.kitchenAwaiting = forecast.AwaitingSpawn;
        }
        if (kitchen != null && kitchen.TryGetPreparedSlot(Group.currentOrderNumber, out int slot)) state.preparedSlot = slot;
        string signature = JsonUtility.ToJson(state);
        if (signature == lastServicePayload && Time.unscaledTime < nextServiceBaseline) return;
        if (signature != lastServicePayload) { lastServicePayload = signature; serviceRevision++; }
        state.revision = serviceRevision;
        state.authorityTime = Time.time; state.sentAt = PhotonNetwork.Time;
        nextServiceBaseline = Time.unscaledTime + 3f;
        photonView.RPC(nameof(ReceiveServiceState), RpcTarget.Others, JsonUtility.ToJson(state));
    }

    [PunRPC]
    private void ReceiveServiceState(string json, PhotonMessageInfo info)
    {
        if (simulating || session == null || GameFlowManager.Instance == null || info.Sender == null || info.Sender.ActorNumber != session.Run?.hostActor
            || string.IsNullOrEmpty(json) || json.Length > 131072) return;
        ServiceState state;
        try { state = JsonUtility.FromJson<ServiceState>(json); } catch (ArgumentException) { return; }
        if (state == null || state.run != session.RunId || state.day != GameFlowManager.Instance.CurrentDay
            || state.revision <= appliedServiceRevision
            || (pendingServiceState != null && state.revision < pendingServiceState.revision)) return;
        pendingServiceState = state; pendingServiceInfo = info;
        ApplyPendingServiceState();
    }

    private void ApplyPendingServiceState()
    {
        var state = pendingServiceState;
        if (state == null || Group == null || simulating || GameFlowManager.Instance == null) return;
        if (state.day != GameFlowManager.Instance.CurrentDay) { pendingServiceState = null; return; }
        var booth = string.IsNullOrEmpty(state.booth) ? null : MultiplayerCustomerInteractionBridge.ResolveBooth(state.booth);
        if (!string.IsNullOrEmpty(state.booth) && booth == null) return;
        var holder = MultiplayerWorldRegistry.ResolveHolder(state.billHolder);
        if (!string.IsNullOrEmpty(state.billHolder) && holder == null) return;
        var info = pendingServiceInfo;
        if (!string.IsNullOrEmpty(state.orderJson) && state.choices?.Length == 4)
            ReceiveGeneratedOrder(state.orderJson, state.orderNumber, state.choices, info);
        Group.PresentMultiplayerBillDelivered(state.billDelivered);
        if (booth != null)
            ReceiveAssignment(state.booth, state.phase, state.reviewing, state.reviewActor,
                state.thought, state.mood, state.result, state.complaint, state.paid, info);
        else ReceiveQueue(state.phase, state.queuePosition, state.patience, state.patienceVisible,
            state.ready, state.greeted, state.thought, state.mood, state.result, state.complaint, info);
        CurrentBillStage = (BillStage)state.billStage; BillOwnerActor = state.billOwner;
        CarrierActorNumber = state.carrier; CarrierNeedsRecovery = state.carrierRecovery;
        if (state.hasKitchen) ReceiveKitchenEntry(state.orderNumber, state.kitchenStarted, state.preparation,
            state.cook, state.kitchenReady, state.kitchenPaused, state.kitchenAwaiting, state.kitchenState,
            state.authorityTime, state.sentAt, info);
        billSnapshotOrder = state.orderNumber; billPosition = state.paperPosition; billRotation = state.paperRotation;
        if (CurrentBillStage == BillStage.Printed || CurrentBillStage == BillStage.Carried)
        {
            var paper = BillManager.Instance?.PresentMultiplayerBill(Group, state.paperPosition, state.paperRotation);
            if (paper == null) return;
            var oldHolder = paper.GetComponentInParent<WaiterHands>(true);
            if (holder == null)
            {
                oldHolder?.DetachBillPaper(paper);
                paper.transform.SetParent(null, true);
                paper.transform.SetPositionAndRotation(state.paperPosition, state.paperRotation);
            }
            paper.PresentHolder(holder);
        }
        else if (CurrentBillStage >= BillStage.Complete || CurrentBillStage == BillStage.None)
            BillManager.Instance?.RemoveObservedBill(Group);
        if (state.eatingStarted) ReceiveEating(state.eatingAt, state.eatingDuration, state.eatingComplete, info);
        var served = MultiplayerWorldRegistry.Kitchen?.GetPreparedResult(state.orderNumber);
        if (served != null && Group.state == CustomerGroup.GroupState.Eating) Group.PresentObservedServed(served);
        appliedServiceRevision = state.revision; pendingServiceState = null;
        Group.ReconcileMultiplayerBubbles(this);
    }
}
