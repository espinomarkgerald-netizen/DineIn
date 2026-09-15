using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Coordinates the existing GameFlowManager/GameDayManager; does not implement a second day loop.
public sealed class MultiplayerDayBridge : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte RequestEvent = 196, RejectedEvent = 197;
    private const string SnapshotKey = "restaurant.day.v2";
    [Serializable] private sealed class Request { public string run, action; public int day; }
    [Serializable] private sealed class Snapshot
    {
        public string run;
        public int revision, day;
        public bool running, closing, results;
        public float remaining, scale;
        public double sentAt, startedAt;
        public int[] report;
    }
    private MultiplayerSessionManager session;
    private GameDayManager day;
    private Snapshot snapshot;
    private bool committing;
    private int revision, appliedRevision, observedStartDay;
    private double startedAt = -1;
    private float nextPublish;
    private string readPayload;
    public int CurrentDay => GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
    public static bool IsActive => MultiplayerRestaurantBridge.IsActive;
    public static bool CanCommit => IsActive && MultiplayerSessionManager.Instance.IsAuthority
        && MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>().committing;
    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    private bool InterceptStart() => !CanCommit && TryRequestStart();
    public static bool TryRequestStart() => RequestAction("start");
    public static bool TryRequestNextDay() => RequestAction("next");
    private static bool RequestAction(string action)
    {
        if (!IsActive) return false;
        var session = MultiplayerSessionManager.Instance;
        if (!session.CanAct || session.LocalManager == null) return true;
        var bridge = session.GetComponent<MultiplayerDayBridge>();
        var request = new Request { run = session.RunId, day = bridge.CurrentDay, action = action };
        if (session.IsAuthority) bridge.Handle(session.LocalActorNumber, request);
        else PhotonNetwork.RaiseEvent(RequestEvent, JsonUtility.ToJson(request),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
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
            if (Time.unscaledTime >= nextPublish) PublishNow();
        }
        else { if (snapshot == null) ReadSnapshot(); ApplySnapshot(); }
    }
    public bool EveryoneReady(int readyDay)
    {
        if (!session.AllPlayersLoaded()) return false;
        foreach (var p in session.Run.participants)
        {
            if (p.departed || p.disconnectDeadline > 0) continue;
            if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(p.actor, out var player)
                || !Equals(player.CustomProperties[MultiplayerSessionManager.ReadyKey], readyDay)) return false;
        }
        return true;
    }
    private void Handle(int actor, Request request)
    {
        if (!session.IsAuthority || !MultiplayerProgressionContext.Ready || day == null || committing
            || request == null || request.run != session.RunId || request.day != CurrentDay || !session.ValidActor(actor)) return;
        bool accepted = false;
        committing = true;
        try
        {
            if (request.action == "start" && !day.ServiceActive && !day.HasDayResults && EveryoneReady(CurrentDay))
            {
                var computer = FindFirstObjectByType<ManagementComputerController>();
                accepted = computer != null && computer.StartShiftOnAuthority();
                if (accepted) startedAt = PhotonNetwork.Time;
            }
            else if (request.action == "next" && day.HasDayResults && EveryoneReady(CurrentDay + 1))
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
        "Everyone must be loaded and ready. Complete the restaurant checklist before opening.");
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
            report = day.CaptureMultiplayerReport() };
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
                || incoming.revision <= appliedRevision || incoming.report?.Length != 7) return;
            snapshot = incoming;
            readPayload = json;
        }
        catch (ArgumentException) { Debug.LogWarning("[Multiplayer] Invalid day snapshot."); }
    }
    private void ApplySnapshot()
    {
        if (snapshot == null || day == null || session.IsHostConnection || !MultiplayerProgressionContext.LocallyPrepared
            || snapshot.day != GameFlowManager.Instance.CurrentDay || !MultiplayerRestaurantBridge.Active.HasState) return;
        if (snapshot.revision > appliedRevision)
        {
            day.ApplyMultiplayerReport(snapshot.report);
            appliedRevision = snapshot.revision;
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
        }
    }
    public void RefreshAfterRejoin() { appliedRevision = 0; readPayload = null; ReadSnapshot(); ApplySnapshot(); }
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
