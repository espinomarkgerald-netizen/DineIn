using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Only the original host changes bookings, drink stock, meal charges and pour tips.
// Client callbacks are presentation; each host command has a run/day/epoch-fenced receipt.
[RequireComponent(typeof(MultiplayerSessionManager), typeof(MultiplayerTaskClaims))]
public sealed class MultiplayerFineDiningService : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte CommandEvent = 144, ReplyEvent = 145, SnapshotEvent = 146, SnapshotRequestEvent = 147;
    [SerializeField] private FineDiningReservations reservations;
    [SerializeField] private WinePouringUI pourUI;
    [SerializeField, Min(2f)] private float requestTimeoutSeconds = 8f;

    [Serializable] private sealed class BookingVersion { public string id; public int revision; }
    [Serializable] private sealed class PourGrant
    {
        public int view, actor, epoch, generation;
        public long lease;
        public string token, product;
        public double started;
    }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int day, revision;
        public FineDiningReservations.NetworkState reservations;
        public BookingVersion[] bookings;
        public PourGrant[] pours;
    }
    [Serializable] private sealed class Command
    {
        public MultiplayerActionContext context;
        public string booking, table, token, product;
        public int bookingRevision, generation;
    }
    [Serializable] private sealed class Reply
    {
        public MultiplayerActionContext context;
        public bool accepted;
        public string message, token;
        public int tip;
        public Snapshot snapshot;
    }
    private sealed class Pending
    {
        public Command command;
        public Action<Reply> completed;
        public float retryAt, until;
    }

    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private ManagementComputerStation[] stations;
    private readonly Dictionary<string, Pending> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Reply> receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> bookingSignatures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> bookingVersions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, PourGrant> pours = new();
    private PourGrant[] observedPours = Array.Empty<PourGrant>();
    private string scopeRun, previousSnapshot;
    private int scopeDay, revision, observedRevision = -1;
    private float nextPublish, nextRequest;
    private bool suspended, committingReservation, subscribed;
    private CustomerGroup localPour;
    private string localTask, localToken;
    private long localLease;
    private int localIntent;
    private bool waitingForClaim, committingPour;

    public static MultiplayerFineDiningService Active => FineDiningMultiplayerSession.IsActive
        ? MultiplayerSessionManager.Instance.GetComponent<MultiplayerFineDiningService>() : null;
    public static bool CommittingReservation => Active != null && Active.committingReservation;
    private bool InSession => session != null && FineDiningMultiplayerSession.IsActive && !session.Ended;
    public bool HasRestoredState => InSession && !suspended && reservations != null
        && GameFlowManager.Instance != null && reservations.ReservationDay == GameFlowManager.Instance.CurrentDay
        && (session.IsAuthority || observedRevision >= 0 && reservations.HasObservedReservationParties);
    public bool IsPouringFor(int view, int actor) => session != null && (session.IsAuthority
        ? pours.TryGetValue(view, out var grant) && grant.actor == actor
        : Array.Exists(observedPours, grant => grant.view == view && grant.actor == actor));

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        claims = GetComponent<MultiplayerTaskClaims>();
        stations = FindObjectsByType<ManagementComputerStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(station => station.gameObject.scene == gameObject.scene).ToArray();
    }
    public override void OnEnable()
    {
        base.OnEnable();
        Subscribe();
    }
    private void Subscribe()
    {
        if (claims == null || subscribed) return;
        claims.ClaimResult += OnClaimResult;
        claims.OwnerChanged += OnOwnerChanged;
        subscribed = true;
    }
    public override void OnDisable()
    {
        SuspendSession();
        if (subscribed && claims != null)
        {
            claims.ClaimResult -= OnClaimResult;
            claims.OwnerChanged -= OnOwnerChanged;
        }
        subscribed = false;
        base.OnDisable();
    }

    private void Update()
    {
        if (!InSession || suspended || GameFlowManager.Instance == null || !MultiplayerProgressionContext.Ready) return;
        Subscribe();
        EnsureScope();
        if (session.IsAuthority)
        {
            reservations.EnsureDay();
            if (Time.unscaledTime >= nextPublish)
            {
                PrunePours();
                PublishNow();
                nextPublish = Time.unscaledTime + .25f;
            }
        }
        else if (!HasRestoredState && Time.unscaledTime >= nextRequest) RequestLatest();

        foreach (var request in pending.Values.ToArray())
        {
            if (!pending.ContainsKey(request.command.context.actionId)) continue;
            if (Time.unscaledTime > request.until)
            {
                pending.Remove(request.command.context.actionId);
                request.completed?.Invoke(new Reply { context = request.command.context,
                    message = "Confirmation timed out. Check the updated table or booking before retrying." });
                RequestLatest();
            }
            else if (Time.unscaledTime >= request.retryAt) Transmit(request);
        }
        if (localTask != null && !waitingForClaim && !committingPour
            && (localPour == null || !localPour.HasWineRefillRequest || !session.CanAct
                || claims.GetOwner(localTask) != session.LocalActorNumber || claims.GetLease(localTask) != localLease))
            CloseLocalPour(true);
    }

    private void EnsureScope()
    {
        int day = GameFlowManager.Instance.CurrentDay;
        if (scopeRun == session.RunId && scopeDay == day) return;
        CloseLocalPour(false);
        CancelPending("The restaurant has moved to a new day.");
        foreach (var grant in pours.Values) MultiplayerServiceActions.Resolve(grant.view)?.SetMultiplayerPourActive(false);
        pours.Clear(); receipts.Clear(); bookingSignatures.Clear(); bookingVersions.Clear();
        observedPours = Array.Empty<PourGrant>();
        scopeRun = session.RunId; scopeDay = day; revision = 0; observedRevision = -1;
        previousSnapshot = null; nextPublish = nextRequest = 0f;
        reservations.ResetObservedReservations();
    }
    public void RequestRestoration()
    {
        suspended = false;
        CloseLocalPour(false);
        CancelPending("The connection changed. Select the task again after synchronization.");
        observedRevision = -1;
        reservations?.ResetObservedReservations();
        if (GameFlowManager.Instance != null && InSession) { EnsureScope(); RequestLatest(); }
    }
    public void RequestLatest()
    {
        if (!InSession || !session.IsConnected || GameFlowManager.Instance == null) return;
        nextRequest = Time.unscaledTime + 1f;
        if (session.IsAuthority) PublishNow();
        else MultiplayerWire.Raise(SnapshotRequestEvent, string.Empty,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    private void RefreshBookingVersions()
    {
        foreach (var booking in reservations.Bookings)
        {
            string signature = JsonUtility.ToJson(booking);
            if (bookingSignatures.TryGetValue(booking.id, out var previous) && previous == signature) continue;
            bookingSignatures[booking.id] = signature;
            bookingVersions.TryGetValue(booking.id, out int version);
            bookingVersions[booking.id] = version + 1;
        }
    }
    private Snapshot Capture()
    {
        RefreshBookingVersions();
        var snapshot = new Snapshot { run = session.RunId, day = GameFlowManager.Instance.CurrentDay,
            reservations = reservations.CaptureNetworkReservations(),
            bookings = bookingVersions.OrderBy(pair => pair.Key).Select(pair => new BookingVersion { id = pair.Key, revision = pair.Value }).ToArray(),
            pours = pours.Values.OrderBy(grant => grant.view).ToArray() };
        string signature = JsonUtility.ToJson(snapshot);
        if (signature != previousSnapshot) { revision++; previousSnapshot = signature; }
        snapshot.revision = revision;
        return snapshot;
    }
    public void PublishNow()
    {
        if (!InSession || !session.IsAuthority || reservations == null || !MultiplayerProgressionContext.Ready
            || GameFlowManager.Instance == null) return;
        EnsureScope();
        reservations.EnsureDay();
        int previous = revision;
        var snapshot = Capture();
        // Regular snapshots are change-driven; restoration requests send an explicit baseline.
        if (snapshot.revision != previous)
            MultiplayerWire.Raise(SnapshotEvent, JsonUtility.ToJson(snapshot),
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }
    private bool Apply(Snapshot snapshot)
    {
        if (!InSession || session.IsAuthority || snapshot == null || snapshot.run != session.RunId
            || GameFlowManager.Instance == null || snapshot.day != GameFlowManager.Instance.CurrentDay
            || snapshot.revision < observedRevision) return false;
        if (snapshot.revision == observedRevision) return true;
        if (!reservations.ApplyObservedReservations(snapshot.reservations)) return false;
        observedRevision = snapshot.revision;
        bookingVersions.Clear();
        foreach (var booking in snapshot.bookings ?? Array.Empty<BookingVersion>())
            if (booking != null && !string.IsNullOrEmpty(booking.id)) bookingVersions[booking.id] = booking.revision;
        observedPours = snapshot.pours ?? Array.Empty<PourGrant>();
        return true;
    }

    public static void RequestReservation(string operation, string booking, FineDiningTable table, Action<bool, string> completed)
    {
        var active = Active;
        if (active == null || !active.session.CanAct || !active.HasRestoredState || MultiplayerDayBridge.PreparationLocked)
        { completed?.Invoke(false, "Wait for the shared restaurant to finish synchronizing."); return; }
        if (active.session.IsAuthority) active.RefreshBookingVersions();
        if (!active.bookingVersions.TryGetValue(booking ?? string.Empty, out int version))
        { completed?.Invoke(false, "This request changed. Select it again."); return; }
        var command = new Command { context = MultiplayerActionContext.Capture(active.session, null,
            "booking_" + operation, null, "fine.booking"), booking = booking, table = table != null ? table.tableId : null,
            bookingRevision = version };
        active.Send(command, reply => completed?.Invoke(reply.accepted, reply.message));
    }

    private void Send(Command command, Action<Reply> completed)
    {
        if (!InSession || !session.IsConnected || suspended)
        { completed?.Invoke(new Reply { context = command.context, message = "The connection is unavailable." }); return; }
        var request = new Pending { command = command, completed = completed,
            until = Time.unscaledTime + Mathf.Max(2f, requestTimeoutSeconds) };
        pending[command.context.actionId] = request;
        Transmit(request);
    }
    private void Transmit(Pending request)
    {
        request.retryAt = Time.unscaledTime + 1f;
        if (session.IsAuthority) Handle(request.command, session.LocalActorNumber);
        else MultiplayerWire.Raise(CommandEvent, JsonUtility.ToJson(request.command),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void OnEvent(EventData ev)
    {
        if (!InSession || suspended || !MultiplayerProgressionContext.Ready || !MultiplayerWire.TryRead(ev, out var payload)) return;
        if (ev.Code < CommandEvent || ev.Code > SnapshotRequestEvent) return;
        EnsureScope();
        try
        {
            if (ev.Code == CommandEvent && session.IsAuthority && payload is string command && command.Length <= 16384)
                Handle(JsonUtility.FromJson<Command>(command), ev.Sender);
            else if (ev.Code == ReplyEvent && ev.Sender == session.Run.hostActor && payload is string reply && reply.Length <= 262144)
                Receive(JsonUtility.FromJson<Reply>(reply));
            else if (ev.Code == SnapshotEvent && ev.Sender == session.Run.hostActor && payload is string snapshot && snapshot.Length <= 262144)
                Apply(JsonUtility.FromJson<Snapshot>(snapshot));
            else if (ev.Code == SnapshotRequestEvent && session.IsAuthority && session.ValidActor(ev.Sender)
                && MultiplayerProgressionContext.Ready)
            {
                reservations.EnsureDay();
                MultiplayerWire.Raise(SnapshotEvent, JsonUtility.ToJson(Capture()),
                    new RaiseEventOptions { TargetActors = new[] { ev.Sender } }, SendOptions.SendReliable);
            }
        }
        catch (ArgumentException) { /* Invalid JSON is not an accepted command. */ }
    }
    private void Handle(Command command, int actor)
    {
        if (!InSession || !session.IsAuthority || command?.context == null || !command.context.MatchesSession(session, actor)
            || !MultiplayerProgressionContext.Ready) return;
        EnsureScope();
        string receiptId = actor + ":" + command.context.loadEpoch + ":" + command.context.actionId;
        if (receipts.TryGetValue(receiptId, out var existing)) { ReplyTo(actor, existing); return; }
        var reply = new Reply { context = command.context, message = "This task has changed. Select its current action." };
        bool completedPour = false;
        if (command.context.operation.StartsWith("booking_", StringComparison.Ordinal)) HandleBooking(command, actor, reply);
        else completedPour = HandlePour(command, actor, reply);
        // Persist the terminal outcome before any completion/release notification can run client callbacks.
        receipts[receiptId] = reply;
        if (completedPour)
        {
            RemovePour(command.context.customerView);
            claims.CompleteOnAuthority(PourTask(command.context.customerView), actor);
        }
        reply.snapshot = Capture();
        ReplyTo(actor, reply);
        MultiplayerWire.Raise(SnapshotEvent, JsonUtility.ToJson(reply.snapshot),
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }
    private void HandleBooking(Command command, int actor, Reply reply)
    {
        if (command.context.expectedStage != "fine.booking" || MultiplayerDayBridge.PreparationLocked
            || reservations == null || (!reservations.Preparing && GameDayManager.Instance?.ServiceActive != true))
        { reply.message = "Reservations cannot be changed at this time."; return; }
        RefreshBookingVersions();
        if (string.IsNullOrEmpty(command.booking) || !bookingVersions.TryGetValue(command.booking, out int current)
            || current != command.bookingRevision)
        { reply.message = "A teammate or the restaurant has updated this booking. Review its current details."; return; }
        if (!session.TryGetManager(actor, out var player) || (!reservations.Preparing && !AtComputer(player)))
        { reply.message = "Use the reservation monitor to change this booking."; return; }
        committingReservation = true;
        try
        {
            if (command.context.operation == "booking_assign")
            {
                var table = reservations.tables?.FirstOrDefault(value => value != null && value.tableId == command.table);
                reply.accepted = reservations.Assign(command.booking, table, out reply.message);
            }
            else if (command.context.operation == "booking_decline")
                reply.accepted = reservations.Decline(command.booking, out reply.message);
        }
        finally { committingReservation = false; }
    }
    private bool AtComputer(GameObject player) => stations != null && stations.Any(station => station != null
        && station.isActiveAndEnabled && station.Purpose == RestaurantComputerPurpose.Reservations
        && Near(player, station.StandPoint, station.GetInteractRadius()));
    private static bool Near(GameObject player, Transform point, float radius)
    {
        if (player == null || point == null) return false;
        Vector3 distance = NetworkPlayerMovementSync.AuthorityPosition(player) - point.position;
        distance.y = 0f;
        return distance.sqrMagnitude <= (radius + .35f) * (radius + .35f);
    }
    private static string PourTask(int view) => "Customer:" + view + ":Pour";
    private static int View(CustomerGroup group) => group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 : 0;

    public static void RequestPour(CustomerGroup group)
    {
        var active = Active;
        if (active == null || !active.session.CanAct || group == null || !group.HasWineRefillRequest) return;
        if (active.localTask != null || active.pourUI == null || active.pourUI.IsOpen)
        { WarningSlideUI.Instance?.Show("Finish your current drink service first."); return; }
        int view = View(group);
        if (view <= 0 || group.assignedBooth?.approachPoint == null) return;
        active.localPour = group;
        active.localTask = PourTask(view);
        active.waitingForClaim = true;
        active.localIntent++;
        if (!active.claims.RequestClaim(active.localTask)) active.CloseLocalPour(false);
    }
    private void OnClaimResult(string task, bool accepted)
    {
        if (task != localTask || !waitingForClaim) return;
        waitingForClaim = false;
        if (!accepted || localPour == null || session.LocalManager == null)
        { CloseLocalPour(false); return; }
        localLease = claims.GetLease(task);
        int intent = localIntent;
        var mover = session.LocalManager.GetComponent<PlayerMovement>();
        var stand = localPour.assignedBooth?.approachPoint;
        if (mover == null || stand == null || !mover.UI_MoveToAction(stand, 2.75f,
            () => { if (localIntent == intent) BeginLocalPour(); },
            () => { if (localIntent == intent) CancelLocalPour(); })) CloseLocalPour(true);
    }
    private Command PourCommand(string operation)
    {
        if (localPour == null) return null;
        return new Command { context = MultiplayerActionContext.Capture(session, localPour, operation, localTask, "fine.pour"),
            generation = localPour.FineDiningServiceGeneration, product = localPour.RequestedFineDiningDrink?.ProductId,
            token = localToken };
    }
    private void BeginLocalPour()
    {
        var command = PourCommand("pour_begin");
        int intent = localIntent;
        if (command == null) { CloseLocalPour(true); return; }
        Send(command, reply =>
        {
            if (localIntent != intent) return;
            if (!reply.accepted || localPour == null || pourUI == null)
            { Warn(reply.message); CloseLocalPour(true); return; }
            localToken = reply.token;
            if (!pourUI.Open(localPour.IsWaterPourRequest, CommitLocalPour, CancelLocalPour)) CancelLocalPour();
        });
    }
    private void CommitLocalPour()
    {
        var command = PourCommand("pour_commit");
        if (command == null) { CloseLocalPour(true); return; }
        committingPour = true;
        Send(command, reply =>
        {
            if (reply.accepted && reply.tip > 0) WarningSlideUI.Instance?.Show("Lovely service! Tip: \u20b1" + reply.tip);
            else if (!reply.accepted) Warn(reply.message);
            CloseLocalPour(true);
        });
    }
    private void CancelLocalPour()
    {
        if (committingPour) return; // Reconcile the commit receipt; never undo a possibly completed service.
        var command = PourCommand("pour_cancel");
        if (command != null && !string.IsNullOrEmpty(command.token)) Send(command, _ => { });
        CloseLocalPour(true);
    }
    private void CloseLocalPour(bool release)
    {
        string task = localTask;
        localIntent++;
        localPour = null; localTask = localToken = null; localLease = 0;
        waitingForClaim = committingPour = false;
        if (pourUI != null && pourUI.IsOpen) pourUI.CloseWithoutResult();
        if (release && task != null && claims != null && session?.IsConnected == true) claims.Release(task);
    }
    private bool HandlePour(Command command, int actor, Reply reply)
    {
        var context = command.context;
        int view = context.customerView;
        var group = MultiplayerServiceActions.Resolve(view);
        string task = PourTask(view);
        if (context.expectedStage != "fine.pour" || GameDayManager.Instance?.ServiceActive != true
            || group == null || !context.MatchesOrder(group) || group.FineDiningServiceGeneration != command.generation
            || !group.HasWineRefillRequest || group.state != CustomerGroup.GroupState.Eating
            || group.RequestedFineDiningDrink?.ProductId != command.product) return false;
        if (!claims.IsClaimedBy(task, actor) || context.lease <= 0 || claims.GetLease(task) != context.lease)
        { reply.message = "This drink service is no longer assigned to you."; return false; }
        if (!session.TryGetManager(actor, out var player)) return false;
        if (command.context.operation == "pour_cancel")
        {
            if (pours.TryGetValue(view, out var cancelled) && Matches(cancelled, command, actor)) RemovePour(view);
            reply.accepted = true; reply.message = string.Empty;
            claims.ReleaseOnAuthority(task, actor);
            return false;
        }
        if (!Near(player, group.assignedBooth?.approachPoint, 2.75f))
        { reply.message = "Move closer to this table before pouring."; return false; }
        WaiterHands.ReconcileMultiplayerHands(player);
        var hands = player.GetComponent<WaiterHands>();
        if (hands == null || hands.HasTray || hands.HasBill || hands.HasTicket || hands.HasMoney)
        { reply.message = "Finish the item you are carrying before pouring."; return false; }
        if (command.context.operation == "pour_begin")
        {
            if (pourUI == null || (group.IsWaterPourRequest && !pourUI.SupportsWaterService)
                || LobbyStockBridge.Instance == null || !LobbyStockBridge.Instance.HasProductStock(group.RequestedFineDiningDrink))
            { reply.message = "That drink is unavailable. No charge was added."; return false; }
            if (pours.TryGetValue(view, out var prior))
            {
                if (prior.actor != actor || prior.lease != context.lease || prior.epoch != context.loadEpoch
                    || prior.generation != command.generation) return false;
                reply.token = prior.token;
            }
            else
            {
                var grant = new PourGrant { view = view, actor = actor, epoch = context.loadEpoch,
                    generation = command.generation, lease = context.lease, product = command.product,
                    token = Guid.NewGuid().ToString("N"), started = PhotonNetwork.Time };
                pours[view] = grant; reply.token = grant.token;
                group.SetMultiplayerPourActive(true);
            }
            reply.accepted = true; reply.message = string.Empty;
        }
        else if (command.context.operation == "pour_commit" && pours.TryGetValue(view, out var grant)
            && Matches(grant, command, actor))
        {
            if (pourUI == null || PhotonNetwork.Time - grant.started < Mathf.Max(.25f, pourUI.pourSeconds))
            { reply.message = "Finish filling the glass before completing service."; return false; }
            bool served = false;
            int tip = 0;
            MultiplayerRestaurantBridge.Active.RunSystemAction(() => served = group.TryCompleteFineDiningPour(command.generation, out tip));
            reply.accepted = served; reply.tip = tip;
            reply.message = served ? string.Empty : "That drink is no longer available. No charge was added.";
            return true; // Both terminal success and stock failure release this finished attempt.
        }
        return false;
    }
    private static bool Matches(PourGrant grant, Command command, int actor) => grant.actor == actor
        && grant.epoch == command.context.loadEpoch && grant.lease == command.context.lease
        && grant.generation == command.generation && grant.product == command.product && grant.token == command.token;
    private void RemovePour(int view)
    {
        if (!pours.Remove(view)) return;
        MultiplayerServiceActions.Resolve(view)?.SetMultiplayerPourActive(false);
    }
    private void OnOwnerChanged(string task, int owner)
    {
        if (session != null && session.IsAuthority)
            foreach (var grant in pours.Values.ToArray())
                if (task == PourTask(grant.view) && (owner != grant.actor || claims.GetLease(task) != grant.lease)) RemovePour(grant.view);
        if (task == localTask && !waitingForClaim && !committingPour && owner != session.LocalActorNumber) CloseLocalPour(false);
    }
    private void PrunePours()
    {
        foreach (var grant in pours.Values.ToArray())
        {
            var group = MultiplayerServiceActions.Resolve(grant.view);
            bool valid = session.ValidActor(grant.actor) && group != null && group.HasWineRefillRequest
                && group.FineDiningServiceGeneration == grant.generation && group.state == CustomerGroup.GroupState.Eating
                && claims.IsClaimedBy(PourTask(grant.view), grant.actor) && claims.GetLease(PourTask(grant.view)) == grant.lease
                && session.Run.participants.Find(p => p.actor == grant.actor)?.loadEpoch == grant.epoch
                && PhotonNetwork.Time - grant.started <= reservations.additionalDrinkMaximumServiceSeconds
                && session.TryGetManager(grant.actor, out var player) && Near(player, group.assignedBooth?.approachPoint, 2.75f);
            if (valid) continue;
            RemovePour(grant.view);
            if (claims.GetLease(PourTask(grant.view)) == grant.lease) claims.ReleaseOnAuthority(PourTask(grant.view), grant.actor);
        }
    }
    private void ReplyTo(int actor, Reply reply)
    {
        if (actor == session.LocalActorNumber) Receive(reply);
        else MultiplayerWire.Raise(ReplyEvent, JsonUtility.ToJson(reply),
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }
    private void Receive(Reply reply)
    {
        if (reply?.context == null || !pending.TryGetValue(reply.context.actionId, out var request)
            || !request.command.context.MatchesAction(reply.context)) return;
        if (!session.IsAuthority && reply.snapshot != null) Apply(reply.snapshot);
        pending.Remove(reply.context.actionId);
        request.completed?.Invoke(reply);
    }
    private static void Warn(string message)
    { if (!string.IsNullOrWhiteSpace(message)) WarningSlideUI.Instance?.Show(message); }
    public void ParticipantUnavailable(int actor)
    {
        if (session == null || !session.IsAuthority) return;
        foreach (var grant in pours.Values.Where(value => value.actor == actor).ToArray()) RemovePour(grant.view);
        PublishNow();
    }
    private void CancelPending(string message)
    {
        var requests = pending.Values.ToArray(); pending.Clear();
        foreach (var request in requests) request.completed?.Invoke(new Reply { context = request.command.context, message = message });
    }
    public void SuspendSession()
    {
        suspended = true;
        CloseLocalPour(false);
        CancelPending("The session connection changed. Your confirmed restaurant state will be restored.");
    }
}
