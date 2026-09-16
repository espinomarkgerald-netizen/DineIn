using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Coordinates the existing GameFlowManager/GameDayManager; does not implement a second day loop.
public sealed partial class MultiplayerDayBridge : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte RequestEvent = 196, RejectedEvent = 197;
    private const string SnapshotKey = "restaurant.day.v2";
    [Serializable] private sealed class Request
    { public string run, action, requestId; public int day, sequence; public bool ready; }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int revision, day;
        public bool running, closing, results, readinessActive;
        public float remaining, scale;
        public double sentAt, startedAt;
        public int[] report;
        public MultiplayerDayReadiness readiness;
        public int noticeRevision;
        public string notice;
        public bool stockoutActive;
        public int stockoutEpisode;
        public string stockoutMessage;
    }
    private MultiplayerSessionManager session;
    private GameDayManager day;
    private Snapshot snapshot;
    private bool committing;
    private int revision, appliedRevision, observedStartDay;
    private double startedAt = -1;
    private float nextPublish;
    private string readPayload;
    private MultiplayerDayReadiness readiness;
    private Request pendingVote;
    private int voteSequence, noticeRevision, observedNotice;
    private string notice;
    private float retryVoteAt;
    private float nextSnapshotRead, nextBaselineRequest;
    public MultiplayerDayReadiness Readiness => readiness;
    public static bool PreparationLocked => IsActive && !MultiplayerSessionManager.Instance.Ended
        && MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.readiness != null;
    public int CurrentDay => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
    public static bool IsActive => MultiplayerRestaurantBridge.IsActive;
    public static bool CanCommit => IsActive && MultiplayerSessionManager.Instance.IsAuthority
        && MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>().committing;
    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    private bool InterceptStart() => !CanCommit && TryRequestStart();
    public static bool TryRequestStart()
    {
        if (!IsActive) return false;
        var session = MultiplayerSessionManager.Instance;
        if (!session.IsHostConnection) return true;
        return RequestAction("start");
    }
    public static bool TryRequestNextDay() => RequestAction("next");
    private static bool RequestAction(string action)
    {
        if (!IsActive) return false;
        var session = MultiplayerSessionManager.Instance;
        if (!session.CanAct || session.LocalManager == null) return true;
        if (!session.IsHostConnection) return true;
        var bridge = session.GetComponent<MultiplayerDayBridge>();
        var request = new Request { run = session.RunId, day = bridge.CurrentDay, action = action,
            requestId = bridge.readiness?.id };
        if (session.IsAuthority) bridge.Handle(session.LocalActorNumber, request);
        return true;
    }
    private void Update()
    {
        if (day == null)
        {
            day = GameDayManager.Instance;
            if (day == null) return;
            day.StartShiftInterception = InterceptStart;
        }
        day.ObserveDayOnly = !session.IsAuthority;
        if (!session.IsConnected || !MultiplayerProgressionContext.LocallyPrepared) return;
        if (session.IsHostConnection)
        {
            TickStockout();
            TickReadiness();
            if (Time.unscaledTime >= nextPublish) PublishNow();
        }
        else
        {
            if (Time.unscaledTime >= nextSnapshotRead)
            {
                nextSnapshotRead = Time.unscaledTime + 1f;
                ReadSnapshot();
                bool waitingForStart = readiness != null && readiness.CountingDown
                    && PhotonNetwork.Time > readiness.deadline + 2d;
                bool missingBaseline = !MultiplayerRestaurantBridge.Active.HasState
                    || snapshot != null && snapshot.day != CurrentDay;
                if ((waitingForStart || missingBaseline) && Time.unscaledTime >= nextBaselineRequest)
                {
                    nextBaselineRequest = Time.unscaledTime + 5f;
                    MultiplayerRestaurantBridge.Active.RequestSnapshot();
                }
            }
            ApplySnapshot();
        }
        if (pendingVote != null)
        {
            if (readiness == null || readiness.id != pendingVote.requestId
                || readiness.Sequence(session.LocalActorNumber) >= pendingVote.sequence) pendingVote = null;
            else if (session.CanAct && Time.unscaledTime >= retryVoteAt) SendVote();
        }
    }
    public bool EveryoneReady(int readyDay)
    {
        return readiness != null && readiness.day == readyDay && readiness.AllReady && session.AllPlayersLoaded();
    }
    public bool LocalReady => readiness != null && readiness.IsReady(session.LocalActorNumber);
    public bool VotePending => pendingVote != null;
    public void ToggleLocalReady()
    {
        var current = GameDayManager.Instance;
        if (!session.CanAct || session.IsHostConnection || readiness == null || pendingVote != null
            || current == null || current.ServiceActive || current.HasDayResults) return;
        voteSequence = Math.Max(voteSequence, readiness.Sequence(session.LocalActorNumber));
        pendingVote = new Request { run = session.RunId, day = CurrentDay, action = "ready",
            requestId = readiness.id, sequence = ++voteSequence, ready = !LocalReady };
        SendVote();
    }
    public void CancelReadyRequest() => RequestAction("cancel");
    private void SendVote()
    {
        retryVoteAt = Time.unscaledTime + 1f;
        PhotonNetwork.RaiseEvent(RequestEvent, JsonUtility.ToJson(pendingVote),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void GetReadiness(int readyDay, out int ready, out int total)
    {
        ready = total = 0;
        if (readiness == null || readiness.day != readyDay) return;
        ready = readiness.Count; total = readiness.actors.Length;
    }
    private string CurrentRoster(out int[] actors)
    {
        var ids = new List<int>(); var members = new List<string>();
        if (session.Run == null) { actors = Array.Empty<int>(); return ""; }
        foreach (var p in session.Run.participants)
        {
            if (p.departed || p.disconnectDeadline > 0) continue;
            ids.Add(p.actor); members.Add(p.actor + ":" + p.loadEpoch);
        }
        ids.Sort(); members.Sort(StringComparer.Ordinal); actors = ids.ToArray();
        return string.Join("|", members);
    }
    private void CancelReadiness(string reason)
    {
        readiness = null; pendingVote = null;
        notice = reason; noticeRevision++;
        ShowNotice(); PublishNow();
    }
    private void ShowNotice()
    {
        if (observedNotice >= noticeRevision || string.IsNullOrEmpty(notice) || WarningSlideUI.Instance == null) return;
        observedNotice = noticeRevision; WarningSlideUI.Instance.Show(notice);
    }
    private void TickReadiness()
    {
        if (readiness == null || !session.IsAuthority) return;
        if (readiness.day != CurrentDay || !session.AllPlayersLoaded() || CurrentRoster(out _) != readiness.roster)
        { CancelReadiness("Players changed. The host can request Start Day again when everyone is ready to prepare."); return; }
        if (MultiplayerRestockBridge.Active?.StockRoomInUse == true)
        { CancelReadiness("Finish the stock-room visit before requesting Start Day."); return; }
        if (readiness.BeginCountdown(PhotonNetwork.Time)) PublishNow();
        if (!readiness.CountingDown || PhotonNetwork.Time < readiness.deadline) return;
        var computer = FindFirstObjectByType<ManagementComputerController>();
        bool accepted = false;
        committing = true;
        try { accepted = computer != null && computer.StartShiftOnAuthority(); }
        finally { committing = false; }
        if (!accepted) { CancelReadiness("Start cancelled. The host must check the pre-open checklist again."); return; }
        startedAt = PhotonNetwork.Time; readiness = null;
        notice = "Day " + CurrentDay + " has started!"; noticeRevision++;
        FindFirstObjectByType<DailyNewspaperPresenter>()?.Close();
        ShowNotice(); PublishNow();
    }
    private void Handle(int actor, Request request)
    {
        if (!session.IsAuthority || !MultiplayerProgressionContext.Ready || day == null || committing
            || request == null || request.run != session.RunId || request.day != CurrentDay || !session.ValidActor(actor)) return;
        if (request.action == "ready")
        {
            if (readiness != null && readiness.SetVote(request.requestId, actor, request.sequence, request.ready)) PublishNow();
            return;
        }
        if (actor != session.Run.hostActor) return;
        if (request.action == "cancel")
        { if (readiness != null && request.requestId == readiness.id) CancelReadiness("Start cancelled. Continue preparing the restaurant."); return; }
        if (readiness != null) return;
        bool accepted = false;
        committing = true;
        try
        {
            if (request.action == "start" && !day.ServiceActive && !day.HasDayResults && session.AllPlayersLoaded())
            {
                var computer = FindFirstObjectByType<ManagementComputerController>();
                if (MultiplayerRestockBridge.Active?.StockRoomInUse == true)
                { WarningSlideUI.Instance?.Show("Finish the stock-room visit before requesting Start Day."); return; }
                accepted = computer != null && computer.CanRequestMultiplayerStart();
                if (accepted)
                {
                    string roster = CurrentRoster(out var actors);
                    readiness = MultiplayerDayReadiness.Create(CurrentDay, session.Run.hostActor, actors, roster);
                }
            }
            else if (request.action == "next" && day.HasDayResults && session.AllPlayersLoaded())
            {
                int before = CurrentDay;
                MultiplayerRestaurantBridge.Active.RunSystemAction(() => GameFlowManager.Instance.CompleteRestaurantDay());
                accepted = CurrentDay > before;
                if (accepted) startedAt = -1;
            }
        }
        finally { committing = false; }
        PublishNow();
        if (!accepted)
        {
            if (actor == session.LocalActorNumber) ShowRejected();
            else PhotonNetwork.RaiseEvent(RejectedEvent, null,
                new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        }
    }
    private static void ShowRejected() => WarningSlideUI.Instance?.Show(
        "Everyone must be loaded. Complete the restaurant checklist before requesting Start Day.");
    public void PublishNow()
    {
        if (!session.IsHostConnection || !MultiplayerProgressionContext.LocallyPrepared) return;
        day ??= GameDayManager.Instance;
        if (day == null || GameFlowManager.Instance == null) return;
        if (snapshot == null || snapshot.day != CurrentDay || snapshot.running != day.ShiftRunning
            || snapshot.closing != day.ClosingOut || snapshot.results != day.HasDayResults)
            MultiplayerRestaurantBridge.Active.Publish(true);
        snapshot = new Snapshot { run = session.RunId, revision = ++revision, day = CurrentDay,
            running = day.ShiftRunning, closing = day.ClosingOut, results = day.HasDayResults,
            remaining = day.TimeRemaining, scale = Time.timeScale, sentAt = PhotonNetwork.Time, startedAt = startedAt,
            report = day.CaptureMultiplayerReport(), readiness = readiness,
            readinessActive = readiness != null && !day.ServiceActive && !day.HasDayResults,
            notice = notice, noticeRevision = noticeRevision,
            stockoutActive = stockoutActive, stockoutEpisode = stockoutEpisode, stockoutMessage = stockoutMessage };
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [SnapshotKey] = JsonUtility.ToJson(snapshot) });
        nextPublish = Time.unscaledTime + 0.5f;
    }
    private void ReadSnapshot()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[SnapshotKey] is not string json || json.Length > 1048576) return;
        if (json == readPayload) return;
        try
        {
            var incoming = JsonUtility.FromJson<Snapshot>(json);
            if (incoming == null || incoming.run != session.RunId || incoming.day < 1
                || incoming.revision <= appliedRevision || incoming.report?.Length != 7
                || !NormalizeReadiness(incoming)) return;
            snapshot = incoming;
            readPayload = json;
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid day snapshot."); }
    }
    private static bool NormalizeReadiness(Snapshot incoming)
    {
        // JsonUtility may materialize a serialized null nested object as an empty instance.
        // Phase is authoritative; ended readiness data cannot veto a valid running day.
        if (!incoming.readinessActive || incoming.running || incoming.closing || incoming.results)
        { incoming.readinessActive = false; incoming.readiness = null; return true; }
        return incoming.readiness != null && incoming.readiness.Valid && incoming.readiness.day == incoming.day;
    }
    private void ApplySnapshot()
    {
        if (snapshot == null || day == null || session.IsHostConnection || !MultiplayerProgressionContext.LocallyPrepared
            || snapshot.day != GameFlowManager.Instance.CurrentDay || !MultiplayerRestaurantBridge.Active.HasState) return;
        if (snapshot.revision > appliedRevision)
        {
            day.ApplyMultiplayerReport(snapshot.report);
            appliedRevision = snapshot.revision;
            readiness = snapshot.readiness;
            if (readiness == null) pendingVote = null;
            notice = snapshot.notice; noticeRevision = snapshot.noticeRevision;
            stockoutActive = snapshot.stockoutActive;
            stockoutEpisode = snapshot.stockoutEpisode;
            stockoutMessage = snapshot.stockoutMessage;
        }
        float remaining = snapshot.remaining;
        if (snapshot.running && !session.Ended)
            remaining -= (float)Math.Max(0d, PhotonNetwork.Time - snapshot.sentAt) * snapshot.scale;
        day.ApplyObservedDay(snapshot.day, snapshot.running && !session.Ended, snapshot.closing && !session.Ended,
            snapshot.results, remaining);
        if (snapshot.startedAt >= 0 && observedStartDay != snapshot.day)
        {
            observedStartDay = snapshot.day;
            FindFirstObjectByType<ManagementComputerController>()?.CloseComputer();
            FindFirstObjectByType<DailyNewspaperPresenter>()?.Close();
        }
        ShowNotice();
        ShowStockoutNotice();
    }
    public void RefreshAfterRejoin()
    {
        appliedRevision = 0; readPayload = null; pendingVote = null; observedStockoutEpisode = 0;
        ReadSnapshot();
        // Rebuild current controls without replaying an old start/cancel announcement.
        if (snapshot != null) observedNotice = snapshot.noticeRevision;
        ApplySnapshot();
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    { if (!session.IsHostConnection && changed.ContainsKey(SnapshotKey)) { ReadSnapshot(); ApplySnapshot(); } }
    public void OnEvent(EventData ev)
    {
        if (!session.IsConnected) return;
        if (ev.Code == RequestEvent && session.IsAuthority && ev.CustomData is string json && json.Length <= 1024)
        { try { Handle(ev.Sender, JsonUtility.FromJson<Request>(json)); } catch (ArgumentException) { } }
        else if (ev.Code == RejectedEvent && ev.Sender == session.Run?.hostActor) ShowRejected();
    }
    private void OnDestroy()
    {
        if (day != null && day.StartShiftInterception == (Func<bool>)InterceptStart)
        { day.StartShiftInterception = null; day.ObserveDayOnly = false; }
    }
}
