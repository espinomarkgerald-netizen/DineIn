using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Scene-owned start requests and recoverable day presentation, not another day simulation.
public sealed class MultiplayerDayBridge : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte StartRequest = 196, StartRejected = 197;
    private const string DaySnapshotKey = "restaurant.day";
    private MultiplayerSessionManager session;
    private GameDayManager day;
    private bool committing;
    private bool initialized;
    private bool migrationPaused;
    private double startedAt = -1d;
    private float nextPublish;
    private int snapshotDay;
    public int CurrentDay => snapshotDay > 0 ? snapshotDay : 1;
    private int observedStartDay;
    private object[] snapshot;

    public static bool IsActive => MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession;
    public static bool CanCommit => IsActive && MultiplayerSessionManager.Instance.IsAuthority
        && MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>() is MultiplayerDayBridge bridge
        && bridge.committing;

    public static bool TryRequestStart()
    {
        if (!IsActive) return false;
        var session = MultiplayerSessionManager.Instance;
        var bridge = session.GetComponent<MultiplayerDayBridge>();
        if (bridge == null || !bridge.isActiveAndEnabled || session.LocalManager == null) return true;
        if (session.IsAuthority) bridge.HandleStart(session.LocalActorNumber);
        else PhotonNetwork.RaiseEvent(StartRequest, null,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        return true;
    }

    private bool InterceptStart()
    {
        if (!IsActive) return false;
        if (committing && session.IsAuthority) return false;
        return TryRequestStart();
    }

    private void Awake() => session = GetComponent<MultiplayerSessionManager>();

    private void Update()
    {
        if (session == null || !session.IsMultiplayerSession) return;
        if (day == null)
        {
            day = GameDayManager.Instance;
            if (day == null) return;
            day.StartShiftInterception = InterceptStart;
            day.ObserveDayOnly = !session.IsAuthority;
        }
        if (!initialized)
        {
            initialized = true;
            ReadSnapshot();
            if (session.IsAuthority && snapshot != null)
            {
                // Recovered sessions must never rerun StartShift and its side effects.
                migrationPaused = startedAt >= 0d;
                day.ObserveDayOnly = true;
                ApplySnapshot();
                day.ObserveDayOnly = migrationPaused;
            }
        }
        if (!session.IsAuthority || migrationPaused)
        {
            ApplySnapshot();
            return;
        }
        int currentDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        if (snapshotDay != 0 && snapshotDay != currentDay) startedAt = -1d;
        if (Time.unscaledTime >= nextPublish || snapshot == null
            || (bool)snapshot[1] != day.ShiftRunning || (bool)snapshot[2] != day.ClosingOut
            || (bool)snapshot[3] != day.HasDayResults || snapshotDay != currentDay)
            Publish();
    }

    private void HandleStart(int sender)
    {
        if (session == null || !session.IsAuthority || !MultiplayerProgressionContext.Ready
            || !initialized || day == null || committing || migrationPaused) return;
        if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var player) || player.IsInactive
            || !session.TryGetManager(sender, out var manager) || manager == null || !manager.activeInHierarchy) return;
        if (day.ServiceActive || day.HasDayResults || startedAt >= 0d) { Publish(); return; }
        var computer = FindFirstObjectByType<ManagementComputerController>();
        if (computer == null) return;
        committing = true;
        bool accepted;
        try { accepted = computer.StartShiftOnAuthority(); }
        finally { committing = false; }
        if (accepted)
        {
            startedAt = PhotonNetwork.Time;
            Publish();
            computer.CloseComputer();
        }
        else if (sender == session.LocalActorNumber)
            WarningSlideUI.Instance?.Show("Complete the restaurant's pre-open requirements before starting the shift.");
        else PhotonNetwork.RaiseEvent(StartRejected, null,
            new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (session == null || !session.IsMultiplayerSession) return;
        if (photonEvent.Code == StartRequest && session.IsAuthority) HandleStart(photonEvent.Sender);
        else if (photonEvent.Code == StartRejected && photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
            WarningSlideUI.Instance?.Show("Complete the restaurant's pre-open requirements before starting the shift.");
    }

    private void Publish()
    {
        if (!session.IsAuthority || day == null || migrationPaused) return;
        snapshotDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        snapshot = new object[] { snapshotDay, day.ShiftRunning, day.ClosingOut, day.HasDayResults,
            day.TimeRemaining, startedAt, PhotonNetwork.Time, Time.timeScale };
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { DaySnapshotKey, snapshot } });
        nextPublish = Time.unscaledTime + 0.5f;
    }

    private void ReadSnapshot()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties[DaySnapshotKey] is not object[] value || value.Length != 8
            || value[0] is not int number || value[1] is not bool || value[2] is not bool || value[3] is not bool
            || value[4] is not float || value[5] is not double start || value[6] is not double || value[7] is not float) return;
        snapshot = value;
        snapshotDay = number;
        startedAt = start;
    }

    private void ApplySnapshot()
    {
        if (snapshot == null || day == null || !day.ObserveDayOnly) return;
        bool running = (bool)snapshot[1];
        // Time is presentation only. Only the authority may expire the day.
        float remaining = (float)snapshot[4];
        if (running && !migrationPaused)
            remaining -= (float)System.Math.Max(0d, PhotonNetwork.Time - (double)snapshot[6]) * (float)snapshot[7];
        day.ApplyObservedDay(snapshotDay, running, (bool)snapshot[2], (bool)snapshot[3], remaining);
        if (startedAt >= 0d && observedStartDay != snapshotDay)
        {
            observedStartDay = snapshotDay;
            FindFirstObjectByType<ManagementComputerController>()?.CloseComputer();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (session == null || !session.IsMultiplayerSession || !changed.ContainsKey(DaySnapshotKey)) return;
        if (!session.IsAuthority || migrationPaused) { ReadSnapshot(); ApplySnapshot(); }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        ReadSnapshot();
        if (day == null) return;
        day.ObserveDayOnly = true;
        ApplySnapshot();
        migrationPaused = session.IsAuthority && startedAt >= 0d;
        day.ObserveDayOnly = !session.IsAuthority || migrationPaused;
    }

    private void OnDestroy()
    {
        if (day != null && day.StartShiftInterception == (System.Func<bool>)InterceptStart)
        {
            day.StartShiftInterception = null;
            day.ObserveDayOnly = false;
        }
    }
}
