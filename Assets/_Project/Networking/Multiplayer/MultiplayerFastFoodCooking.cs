using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Fast Food-specific messages on the existing session/day envelope and task claims.
// Guests never simulate or debit the ledger; all visible operations receive a host receipt.
[RequireComponent(typeof(MultiplayerSessionManager), typeof(MultiplayerTaskClaims))]
public sealed class MultiplayerFastFoodCooking : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte RequestEvent = 140, ReplyEvent = 141, SnapshotRequestEvent = 142, SnapshotEvent = 143;
    [SerializeField] private FastFoodCookingController controller;
    [SerializeField, Min(.1f)] private float snapshotSeconds = .2f;
    [SerializeField, Min(2)] private float requestTimeoutSeconds = 8;
    [Serializable] private sealed class Command
    {
        public MultiplayerActionContext context;
        public int mode, portion, item, slot, order, phase;
        public long stationSequence;
        public string version;
        public string[] tasks;
        public long[] leases;
    }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int day, revision;
        public FastFoodCookingState.NetworkSnapshot state;
    }
    [Serializable] private sealed class Reply
    { public MultiplayerActionResult result; public Snapshot snapshot; }
    private sealed class Pending
    {
        public Command command;
        public Action<bool> completed;
        public string waitingTask;
        public int acquired;
        public bool sent;
        public float until, retry;
    }
    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private Pending pending;
    private readonly Dictionary<string, Reply> receipts = new();
    private readonly Dictionary<int, (int epoch, long sequence)> stationSequences = new();
    private int day, revision, received = -1;
    private string restoredRun, previousState;
    private int restoredDay;
    private FastFoodCookingState restoredState;
    private float nextPublish, nextRestoration;
    private long stationSequence;
    private bool suspended;
    private bool Active => !MultiplayerProgressionContext.RestorationInProgress && session != null && session.IsMultiplayerSession && session.RestaurantType == "FastFood";
    public bool HasRestoredState => Active && controller != null && controller.State != null && !suspended &&
        (session.IsAuthority || restoredRun == session.RunId && restoredDay == GameFlowManager.Instance.CurrentDay && restoredState == controller.State && received >= 0);
    public bool AwaitingAction => pending != null;

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        claims = GetComponent<MultiplayerTaskClaims>();
    }
    public override void OnEnable()
    {
        base.OnEnable();
        if (claims == null) claims = GetComponent<MultiplayerTaskClaims>();
        if (claims != null) claims.ClaimResult += OnClaimResult;
    }
    public override void OnDisable()
    {
        if (claims != null) claims.ClaimResult -= OnClaimResult;
        CancelPending();
        base.OnDisable();
    }
    private void Start()
    {
        if (controller == null) Debug.LogError("Fast Food multiplayer requires its authored cooking controller reference.", this);
        RequestRestoration();
    }
    private void Update()
    {
        if (!Active || !session.IsConnected || session.Ended || controller?.State == null || GameFlowManager.Instance == null) return;
        int currentDay = GameFlowManager.Instance.CurrentDay;
        if (day != currentDay)
        {
            CancelPending();
            if (day > 0) controller.ExitKitchen();
            receipts.Clear(); stationSequences.Clear(); received = -1; restoredState = null;
            revision = 0; day = currentDay; nextPublish = 0; previousState = null;
        }
        if (suspended) return;
        if (pending != null)
        {
            if (!session.CanAct || Time.realtimeSinceStartup >= pending.until)
            { CancelPending(); WarningSlideUI.Instance?.Show("Kitchen action cancelled. Check the current station and try again."); RequestRestoration(); }
            else if (pending.sent && Time.realtimeSinceStartup >= pending.retry) TransmitPending();
        }
        if (session.IsAuthority)
        {
            foreach (int actor in controller.State.NetworkParticipants)
                if (!session.ValidActor(actor)) ParticipantUnavailable(actor);
            if (Time.realtimeSinceStartup >= nextPublish) PublishNow();
        }
        else if (!HasRestoredState && Time.realtimeSinceStartup >= nextRestoration) RequestRestoration();
    }
    public void SelectStation(FastFoodStationMode mode)
    {
        if (!Active || suspended || !session.IsConnected || controller?.State == null) return;
        CancelPending();
        var context = MultiplayerActionContext.Capture(session, null, "Station", null, "Station");
        var command = new Command { context = context, mode = (int)mode, stationSequence = ++stationSequence };
        if (session.IsAuthority) Handle(command, session.LocalActorNumber);
        else MultiplayerWire.Raise(RequestEvent, JsonUtility.ToJson(command), new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void RequestAction(string operation, FastFoodCookingState.Portion portion, ItemData item, int slot, Action<bool> completed)
    {
        if (!Active || suspended || !session.CanAct || pending != null || controller?.State == null || HygieneManager.KitchenPaused)
        { completed?.Invoke(false); return; }
        var state = controller.State;
        int order = operation == "Serve" ? slot : portion?.order ?? -1;
        var command = new Command
        {
            context = MultiplayerActionContext.Capture(session, null, operation, null, portion?.stage.ToString() ?? operation),
            mode = (int)state.Mode, portion = portion?.networkId ?? 0, item = item != null ? (int)item.itemType : -1,
            slot = slot, order = order, phase = (int)GameFlowManager.Instance.CurrentRestaurantSessionState,
            version = operation == "Serve" ? state.TicketVersion(order) : operation == "Basket" ? state.BasketVersion(slot) : FastFoodCookingState.PortionVersion(portion)
        };
        command.tasks = RequiredTasks(command);
        if (command.tasks.Length == 0 || string.IsNullOrEmpty(command.version)) { completed?.Invoke(false); return; }
        command.leases = new long[command.tasks.Length];
        pending = new Pending { command = command, completed = completed, until = Time.realtimeSinceStartup + requestTimeoutSeconds };
        AcquireNext();
    }
    private static string[] RequiredTasks(Command command)
    {
        var tasks = new List<string>();
        if (command.context.operation == "Serve") tasks.Add("FastFood:Assembly:" + command.order);
        else if (command.context.operation == "Basket") tasks.Add("FastFood:Basket:" + command.slot);
        else if (command.portion > 0) tasks.Add("FastFood:Portion:" + command.portion);
        if (command.context.operation == "Load") tasks.Add("FastFood:Surface:" + command.mode + ":" + command.slot + ":" + command.context.expectedStage);
        return tasks.ToArray();
    }
    private void AcquireNext()
    {
        var request = pending;
        if (request == null) return;
        if (request.acquired >= request.command.tasks.Length)
        {
            request.waitingTask = null;
            request.command.context.lease = request.command.leases[0];
            request.command.context.expectedRevision = session.IsAuthority ? revision : received;
            request.sent = true; TransmitPending(); return;
        }
        request.waitingTask = request.command.tasks[request.acquired];
        if (!claims.RequestClaim(request.waitingTask) && pending == request) CancelPending();
    }
    private void OnClaimResult(string task, bool accepted)
    {
        var request = pending;
        if (request == null || request.waitingTask != task || request.sent) return;
        if (!accepted) { CancelPending(); WarningSlideUI.Instance?.Show("A teammate is using that food or slot. Try another available slot."); return; }
        request.command.leases[request.acquired++] = claims.GetLease(task);
        request.waitingTask = null;
        AcquireNext();
    }
    private void TransmitPending()
    {
        var request = pending;
        if (request == null) return;
        request.retry = Time.realtimeSinceStartup + 1;
        if (session.IsAuthority) Handle(request.command, session.LocalActorNumber);
        else MultiplayerWire.Raise(RequestEvent, JsonUtility.ToJson(request.command), new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void OnEvent(EventData ev)
    {
        if (ev.Code < RequestEvent || ev.Code > SnapshotEvent || !Active || controller?.State == null || !MultiplayerWire.TryRead(ev, out object payload)) return;
        try
        {
            if (ev.Code == RequestEvent && session.IsAuthority && payload is string command) Handle(JsonUtility.FromJson<Command>(command), ev.Sender);
            else if (ev.Code == SnapshotRequestEvent && session.IsAuthority) SendSnapshot(ev.Sender);
            else if (ev.Code == SnapshotEvent && ev.Sender == session.Run.hostActor && payload is string state) Apply(JsonUtility.FromJson<Snapshot>(state));
            else if (ev.Code == ReplyEvent && ev.Sender == session.Run.hostActor && payload is string reply) Receive(JsonUtility.FromJson<Reply>(reply));
        }
        catch (ArgumentException) { Debug.LogWarning("Ignored malformed Fast Food kitchen message.", this); }
    }
    private void Handle(Command command, int sender)
    {
        if (!Active || !session.IsAuthority || suspended || controller?.State == null || command?.context == null || !command.context.MatchesSession(session, sender)) return;
        var key = sender + ":" + command.context.loadEpoch + ":" + command.context.actionId;
        if (receipts.TryGetValue(key, out var previous)) { SendReply(sender, previous); return; }
        var state = controller.State;
        var mode = (FastFoodStationMode)command.mode;
        if (!Enum.IsDefined(typeof(FastFoodStationMode), mode)) return;
        if (command.context.operation == "Station")
        {
            if (command.stationSequence <= 0 || stationSequences.TryGetValue(sender, out var last) && last.epoch == command.context.loadEpoch && last.sequence >= command.stationSequence) return;
            if (mode != FastFoodStationMode.None && (!session.TryGetManager(sender, out _) || HygieneManager.KitchenPaused)) return;
            stationSequences[sender] = (command.context.loadEpoch, command.stationSequence);
            state.SetParticipantStation(sender, mode); PublishNow(); return;
        }
        var outcome = MultiplayerActionOutcome.Unavailable;
        bool phaseValid = GameFlowManager.Instance != null && command.phase == (int)GameFlowManager.Instance.CurrentRestaurantSessionState
            && !GameFlowManager.Instance.RestaurantDayHasTerminalOutcome;
        bool validClaims = command.tasks != null && command.leases != null && command.tasks.Length == command.leases.Length
            && command.tasks.SequenceEqual(RequiredTasks(command)) && command.tasks.Length > 0
            && command.tasks.Select((task, i) => claims.IsClaimedBy(task, sender) && claims.GetLease(task) == command.leases[i]).All(ok => ok);
        var portion = state.FindPortion(command.portion);
        string version = command.context.operation == "Serve" ? state.TicketVersion(command.order)
            : command.context.operation == "Basket" ? state.BasketVersion(command.slot) : FastFoodCookingState.PortionVersion(portion);
        if (command.context.expectedStage != (portion?.stage.ToString() ?? command.context.operation)) outcome = MultiplayerActionOutcome.StaleStage;
        else if (!validClaims) outcome = MultiplayerActionOutcome.AnotherOwner;
        else if (string.IsNullOrEmpty(version) || version != command.version) outcome = MultiplayerActionOutcome.StaleStage;
        else if (phaseValid && session.TryGetManager(sender, out var manager) && manager != null && manager.activeInHierarchy
            && state.ParticipantStation(sender) == mode && mode != FastFoodStationMode.None && !HygieneManager.KitchenPaused
            && (!HygieneManager.HoldNewCooking || command.context.operation != "Load" || portion?.stage != FastFoodCookingStage.Waiting))
        {
            bool accepted = state.ExecuteNetworkAction(command.context.operation, mode, command.portion, command.item, command.slot, command.order);
            outcome = accepted ? MultiplayerActionOutcome.Success : MultiplayerActionOutcome.Unavailable;
        }
        var reply = new Reply { result = new MultiplayerActionResult { context = command.context, outcome = outcome,
            revision = ++revision, message = outcome == MultiplayerActionOutcome.Success ? string.Empty : "The kitchen task changed or is occupied. Check the current food and try again." }, snapshot = Capture() };
        receipts[key] = reply;
        if (command.tasks != null) foreach (var task in command.tasks) claims.ReleaseOnAuthority(task, sender);
        SendReply(sender, reply);
        PublishNow();
    }
    private void SendReply(int actor, Reply reply)
    {
        if (actor == session.LocalActorNumber) Receive(reply);
        else MultiplayerWire.Raise(ReplyEvent, JsonUtility.ToJson(reply), new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }
    private void Receive(Reply reply)
    {
        if (reply?.result?.context == null) return;
        var request = pending;
        if (request == null || !request.command.context.MatchesAction(reply.result.context)) return;
        if (!session.IsAuthority && (reply.snapshot == null || reply.snapshot.revision >= received && !Apply(reply.snapshot))) return;
        pending = null;
        ReleaseClaims(request);
        if (!reply.result.Accepted) WarningSlideUI.Instance?.Show(reply.result.message);
        request.completed?.Invoke(reply.result.Accepted);
    }
    private Snapshot Capture() => new Snapshot { run = session.RunId, day = GameFlowManager.Instance.CurrentDay,
        revision = revision, state = controller.State.CaptureNetworkState() };
    public void PublishNow()
    {
        if (!Active || suspended || !session.IsAuthority || controller?.State == null || GameFlowManager.Instance == null) return;
        nextPublish = Time.realtimeSinceStartup + snapshotSeconds;
        var snapshot = Capture();
        string statePayload = JsonUtility.ToJson(snapshot.state);
        if (previousState == statePayload) return;
        previousState = statePayload;
        snapshot.revision = ++revision;
        MultiplayerWire.Raise(SnapshotEvent, JsonUtility.ToJson(snapshot), new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }
    private void SendSnapshot(int actor)
    {
        if (suspended || !session.ValidActor(actor)) return;
        MultiplayerWire.Raise(SnapshotEvent, JsonUtility.ToJson(Capture()), new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }
    private bool Apply(Snapshot snapshot)
    {
        if (snapshot == null || session.IsAuthority || snapshot.run != session.RunId || snapshot.day != GameFlowManager.Instance.CurrentDay || snapshot.revision < received) return false;
        if (!controller.State.ApplyNetworkState(snapshot.state)) return false;
        received = snapshot.revision; restoredRun = snapshot.run; restoredDay = snapshot.day; restoredState = controller.State;
        return true;
    }
    public void RequestRestoration()
    {
        if (session == null) session = GetComponent<MultiplayerSessionManager>();
        if (!Active || !session.IsConnected || controller?.State == null) return;
        suspended = false;
        nextRestoration = Time.realtimeSinceStartup + 1;
        if (session.IsAuthority) PublishNow();
        else MultiplayerWire.Raise(SnapshotRequestEvent, true, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void ParticipantUnavailable(int actor)
    {
        if (!Active || !session.IsAuthority || controller?.State == null) return;
        controller.State.SetParticipantStation(actor, FastFoodStationMode.None);
        stationSequences.Remove(actor);
        PublishNow();
    }
    public void SuspendSession()
    {
        suspended = true;
        CancelPending(); controller?.ExitKitchen(); restoredState = null; received = -1;
        if (session != null && session.IsAuthority && controller?.State != null)
            foreach (int actor in controller.State.NetworkParticipants) controller.State.SetParticipantStation(actor, FastFoodStationMode.None);
    }
    private void CancelPending()
    {
        var request = pending; pending = null;
        if (request == null) return;
        ReleaseClaims(request);
        request.completed?.Invoke(false);
    }
    private void ReleaseClaims(Pending request)
    {
        if (claims == null || request.command.tasks == null) return;
        foreach (string task in request.command.tasks)
            if (claims.IsPending(task) || claims.IsClaimedBy(task, session.LocalActorNumber)) claims.Release(task);
    }
}
