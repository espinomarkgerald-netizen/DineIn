using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Scene-owned disposable run; next days reuse this scene and the existing gameplay systems.
[DefaultExecutionOrder(-11000)]
public class MultiplayerSessionManager : MonoBehaviourPunCallbacks
{
    public const string SceneName = "Lobby1 Multiplayer", RunKey = "restaurant.run.v2";
    public const string ProtocolKey = "restaurant.protocol", Protocol = "casual-session-16";
    public const string ResultRulesVersion = "casual-session-2";
    public const string ReadyKey = "restaurant.ready", LoadedKey = "restaurant.loaded";
    public const int RejoinSeconds = 90;
    public static MultiplayerSessionManager Instance { get; private set; }
    private readonly Dictionary<int, MultiplayerManagerRegistration> managers = new();
    private MultiplayerRunRecord run;
    [Header("Connection recovery")]
    [SerializeField, Range(10f, 60f)] private float automaticReconnectSeconds = 30f;
    [SerializeField, Range(10f, 60f)] private float restorationTimeoutSeconds = 25f;
    private bool leaving, reconnecting, restoringConnection, managementRestored;
    private float restoreUntil, nextRebind, nextRemember;
    private bool leaveInactive;
    public bool IsRecovering => reconnecting || restoringConnection;
    private int ReturnWindowSeconds => PhotonNetwork.CurrentRoom != null
        ? Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerTtl / 1000, 1, 300) : RejoinSeconds;
    private float reconnectUntil, nextReconnect;
    private int localActor;
    private int previousSerializationRate;
    public string Status { get; private set; } = "Loading restaurant…";
    // Remains a multiplayer context while disconnected/ended: never fall back to local simulation.
    public bool IsMultiplayerSession => Instance == this && isActiveAndEnabled;
    public bool IsConnected => IsMultiplayerSession && PhotonNetwork.InRoom;
    public bool IsHostConnection => IsConnected && run != null && PhotonNetwork.LocalPlayer.ActorNumber == run.hostActor
        && PhotonNetwork.MasterClient != null && PhotonNetwork.MasterClient.ActorNumber == run.hostActor;
    public bool IsAuthority => IsHostConnection && !Ended;
    public bool Ended => run != null && !string.IsNullOrEmpty(run.endReason);
    public bool CanAct => IsConnected && !Ended && run != null && !leaving && !reconnecting
        && !restoringConnection && ValidActor(localActor) && MultiplayerProgressionContext.Ready;
    public string RunId => run?.runId ?? string.Empty;
    public MultiplayerRunRecord Run => run;
    public Player LocalPhotonPlayer => IsConnected ? PhotonNetwork.LocalPlayer : null;
    public int LocalActorNumber => localActor;
    public Player[] ConnectedPlayers => IsConnected ? PhotonNetwork.PlayerList : Array.Empty<Player>();
    public int ConnectedHumanCount => Array.FindAll(ConnectedPlayers, p => !p.IsInactive).Length;
    public int PartySize => run?.partySize ?? 0;
    public string RestaurantType => "CasualDining";
    public GameObject LocalManager => CanAct && TryGetManager(localActor, out var manager)
        && manager.activeInHierarchy && manager.GetComponent<PhotonView>().OwnerActorNr == localActor
        && manager.GetComponent<PhotonView>().IsMine ? manager : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        PhotonNetwork.AutomaticallySyncScene = true;
        previousSerializationRate = PhotonNetwork.SerializationRate;
        PhotonNetwork.SerializationRate = 10;
        localActor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
        ReadRun();
        if (run == null) Status = "This room has no valid run. Return to the menu.";
        if (GetComponent<MultiplayerProgressionContext>() == null) gameObject.AddComponent<MultiplayerProgressionContext>();
        if (GetComponent<MultiplayerRestaurantBridge>() == null) gameObject.AddComponent<MultiplayerRestaurantBridge>();
        if (GetComponent<MultiplayerHUDBridge>() == null) gameObject.AddComponent<MultiplayerHUDBridge>();
        if (GetComponent<MultiplayerDayBridge>() == null) gameObject.AddComponent<MultiplayerDayBridge>();
        if (GetComponent<MultiplayerRestockBridge>() == null) gameObject.AddComponent<MultiplayerRestockBridge>();
        if (GetComponent<MultiplayerSessionUI>() == null) gameObject.AddComponent<MultiplayerSessionUI>();
        if (GetComponent<MultiplayerServiceActions>() == null) gameObject.AddComponent<MultiplayerServiceActions>();
        if (GetComponent<MultiplayerObjectBridge>() == null) gameObject.AddComponent<MultiplayerObjectBridge>();
        if (Debug.isDebugBuild && GetComponent<MultiplayerDiagnostics>() == null) gameObject.AddComponent<MultiplayerDiagnostics>();
        if (GetComponent<MultiplayerVoiceController>() == null) gameObject.AddComponent<MultiplayerVoiceController>();
        if (run != null && PhotonNetwork.MasterClient?.ActorNumber != run.hostActor) EndLocal("HostDisconnected");
        if (run != null && !Ended)
        {
            MultiplayerLastRun.Remember(run, localActor);
            nextRemember = Time.realtimeSinceStartup + 30f;
            if (PhotonNetwork.LocalPlayer?.HasRejoined == true) BeginRestoration();
        }
    }

    private void Update()
    {
        if (run == null || leaving) return;
        var auth = PlayFabAuthManager.Instance;
        var participant = run.participants.Find(p => p.actor == localActor);
        if (!Ended && (auth == null || !auth.IsLoggedIn || participant == null || participant.accountId != auth.PlayFabId))
        { LeaveToMenu(); return; }
        if (reconnecting)
        {
            if (Time.realtimeSinceStartup >= reconnectUntil) { ReturnAfterRecoveryFailure("Could not reconnect. Try Reconnect to Last Run while the host is still playing.", false); return; }
            Status = "Connection lost. Reconnecting… " + Mathf.CeilToInt(reconnectUntil - Time.realtimeSinceStartup) + "s";
            if (Time.realtimeSinceStartup >= nextReconnect && PhotonNetwork.NetworkClientState == ClientState.Disconnected)
            { nextReconnect = Time.realtimeSinceStartup + 3f; PhotonNetwork.ReconnectAndRejoin(); }
            return;
        }
        if (restoringConnection)
        {
            if (Time.realtimeSinceStartup >= restoreUntil)
            { ReturnAfterRecoveryFailure("Could not restore the run safely. You can try reconnecting again.", false); return; }
            if (Time.realtimeSinceStartup >= nextRebind)
            {
                nextRebind = Time.realtimeSinceStartup + 0.25f;
                bool hasAvatar = MultiplayerManagerRegistration.TryRebindActor(this, localActor, out _);
                if (hasAvatar && managementRestored && MultiplayerProgressionContext.Ready
                    && GetComponent<MultiplayerDayBridge>()?.HasRestoredState == true)
                { restoringConnection = false; PhotonCustomizationSync.PushToPhoton(); }
            }
        }
        if (!Ended && IsConnected && Time.realtimeSinceStartup >= nextRemember)
        { MultiplayerLastRun.Remember(run, localActor); nextRemember = Time.realtimeSinceStartup + 30f; }
        string loadedToken = participant != null ? RunId + ":" + participant.loadEpoch : string.Empty;
        if (!Ended && !restoringConnection && MultiplayerProgressionContext.Ready && IsConnected
            && TryGetManager(localActor, out _) && !Equals(PhotonNetwork.LocalPlayer.CustomProperties[LoadedKey], loadedToken))
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LoadedKey] = loadedToken });
        if (IsAuthority)
        {
            bool changed = false;
            foreach (var p in run.participants)
                if (!p.departed && p.disconnectDeadline > 0 && PhotonNetwork.Time > p.disconnectDeadline)
                { p.departed = true; p.provisionalDay = 0; p.provisionalHighestDay = 0; p.disconnectDeadline = 0; changed = true; }
            if (changed) PublishRun();
        }
        if (!Ended) Status = restoringConnection ? "Synchronizing restaurant…" : "Connected";
    }

    public bool AllPlayersLoaded()
    {
        if (!IsConnected || run == null) return false;
        foreach (var p in run.participants)
        {
            if (p.departed || p.disconnectDeadline > 0) continue;
            if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(p.actor, out var player) || player.IsInactive
                || !Equals(player.CustomProperties[LoadedKey], RunId + ":" + p.loadEpoch)) return false;
        }
        return true;
    }
    public bool ValidActor(int actor)
    {
        if (!IsConnected || Ended || run == null) return false;
        var p = run.participants.Find(value => value.actor == actor && !value.departed);
        return p != null && PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player)
            && !player.IsInactive && player.UserId == p.accountId;
    }
    public bool TryGetManager(int actor, out GameObject manager)
    {
        manager = null;
        if (!IsConnected || !managers.TryGetValue(actor, out var entry) || entry == null) return false;
        manager = entry.gameObject;
        return true;
    }
    internal void Register(MultiplayerManagerRegistration manager)
    {
        var view = manager.GetComponent<PhotonView>();
        if (!IsConnected || manager.gameObject.scene != gameObject.scene || view.Owner == null) return;
        managers[view.OwnerActorNr] = manager;
        if (view.IsMine && view.OwnerActorNr == localActor) PhotonNetwork.LocalPlayer.TagObject = manager.gameObject;
    }
    internal void Unregister(int actor, MultiplayerManagerRegistration manager)
    { if (managers.TryGetValue(actor, out var current) && current == manager) managers.Remove(actor); }

    public void CompleteDay(int day, string terminalReason)
    {
        if (!IsAuthority || day <= run.completedDay) return;
        run.completedDay = day;
        run.highestDay = Math.Max(run.highestDay, day);
        foreach (var p in run.participants)
        {
            if (p.departed) continue;
            if (p.disconnectDeadline > 0) p.provisionalDay = day;
            else p.completedDay = day;
        }
        if (!string.IsNullOrEmpty(terminalReason)) run.endReason = terminalReason;
        PublishRun();
    }
    public void ReachDay(int day)
    {
        if (!IsAuthority || day <= run.highestDay) return;
        run.highestDay = day;
        foreach (var p in run.participants)
        {
            if (p.departed) continue;
            if (p.disconnectDeadline > 0) p.provisionalHighestDay = day;
            else p.highestDay = day;
        }
        PublishRun();
    }
    private void PublishRun()
    {
        if (!IsHostConnection || run == null) return;
        run.revision++;
        if (Ended && string.IsNullOrEmpty(run.endedUtc)) run.endedUtc = DateTime.UtcNow.ToString("o");
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RunKey] = JsonUtility.ToJson(run) });
        MultiplayerRunRecords.Record(run, localActor, IsHostConnection);
        if (Ended) { ForgetLastRun(); StopSimulation(); }
    }
    private void ReadRun()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[RunKey] is not string json || json.Length > 32768) return;
        try
        {
            var incoming = JsonUtility.FromJson<MultiplayerRunRecord>(json);
            if (!MultiplayerRunRecords.Valid(incoming) || incoming.gameVersion != Application.version || (run != null &&
                (incoming.runId != run.runId || incoming.revision < run.revision || incoming.hostActor != run.hostActor
                    || incoming.hostAccountId != run.hostAccountId || incoming.startedUtc != run.startedUtc
                    || incoming.partySize != run.partySize || incoming.gameVersion != run.gameVersion
                    || Ended && string.IsNullOrEmpty(incoming.endReason)
                    || incoming.participants.Exists(p => !run.participants.Exists(old => old.actor == p.actor && old.accountId == p.accountId))))) return;
            run = incoming;
            MultiplayerRunRecords.Record(run, localActor, IsHostConnection);
            if (Ended) { ForgetLastRun(); StopSimulation(); }
        }
        catch (ArgumentException) { Status = "Invalid run record."; }
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    { if (changed.ContainsKey(RunKey) && !IsHostConnection) ReadRun(); }

    public override void OnPlayerLeftRoom(Player player)
    {
        managers.Remove(player.ActorNumber);
        if (run == null || Ended) return;
        if (player.ActorNumber == run.hostActor) { EndLocal("HostDisconnected"); return; }
        if (!IsAuthority) return;
        var p = run.participants.Find(value => value.actor == player.ActorNumber);
        if (p == null || p.departed) return;
        if (player.IsInactive) { p.loadEpoch++; p.disconnectDeadline = PhotonNetwork.Time + ReturnWindowSeconds; }
        else { p.departed = true; p.provisionalDay = 0; p.provisionalHighestDay = 0; p.disconnectDeadline = 0; }
        PublishRun();
    }
    public override void OnPlayerEnteredRoom(Player player)
    {
        MultiplayerManagerRegistration.TryRebindActor(this, player.ActorNumber, out _);
        if (!IsAuthority) return;
        var p = run.participants.Find(value => value.actor == player.ActorNumber && value.accountId == player.UserId);
        if (p == null || p.departed || p.disconnectDeadline > 0 && PhotonNetwork.Time > p.disconnectDeadline)
        { PhotonNetwork.CloseConnection(player); return; }
        p.completedDay = Math.Max(p.completedDay, p.provisionalDay);
        p.highestDay = Math.Max(p.highestDay, p.provisionalHighestDay);
        p.provisionalDay = 0;
        p.provisionalHighestDay = 0;
        p.disconnectDeadline = 0;
        PublishRun();
        GetComponent<MultiplayerRestaurantBridge>()?.Publish(true);
        GetComponent<MultiplayerDayBridge>()?.PublishNow();
    }
    public override void OnMasterClientSwitched(Player player)
    { if (run != null && player.ActorNumber != run.hostActor) EndLocal("HostDisconnected"); }
    public override void OnDisconnected(DisconnectCause cause)
    {
        managers.Clear();
        if (leaving || Ended || run == null) return;
        StopSimulation();
        if (localActor == run.hostActor) { EndLocal("HostDisconnected"); return; }
        GameDayManager.Instance?.PrepareNextMultiplayerDay(); // Discard replicas whose PUN groups are being destroyed.
        restoringConnection = false;
        if (!reconnecting) reconnectUntil = Time.realtimeSinceStartup + Mathf.Min(automaticReconnectSeconds, ReturnWindowSeconds);
        reconnecting = true;
        nextReconnect = Time.realtimeSinceStartup + 1f;
    }
    public override void OnJoinedRoom()
    {
        if (!reconnecting || run == null) return;
        var pointer = MultiplayerLastRun.Load(run.participants.Find(p => p.actor == localActor)?.accountId, out _);
        if (!MultiplayerLastRun.TryValidateJoinedRun(pointer, out var returned, out var reason)
            || returned.runId != run.runId || returned.hostActor != run.hostActor
            || returned.hostAccountId != run.hostAccountId || returned.revision < run.revision)
        { ReturnAfterRecoveryFailure(reason ?? "The run has changed and cannot be resumed.", true); return; }
        reconnecting = false;
        ReadRun();
        if (Ended) return;
        BeginRestoration();
    }
    private void BeginRestoration()
    {
        restoringConnection = true;
        managementRestored = false;
        restoreUntil = Time.realtimeSinceStartup + restorationTimeoutSeconds;
        nextRebind = 0f;
        GetComponent<MultiplayerRestaurantBridge>()?.RequestSnapshot();
        GetComponent<MultiplayerDayBridge>()?.RefreshAfterRejoin();
        MultiplayerManagerRegistration.TryRebindActor(this, localActor, out _);
    }
    public void SnapshotRestored() { managementRestored = true; }
    public override void OnJoinRoomFailed(short code, string message)
    {
        bool unavailable = code == ErrorCode.GameDoesNotExist || code == ErrorCode.JoinFailedWithRejoinerNotFound
            || code == ErrorCode.GameClosed;
        ReturnAfterRecoveryFailure(unavailable ? "That run or your reserved place is no longer available."
            : "Reconnection failed. Try Reconnect to Last Run from the menu.", unavailable);
    }
    private void ReturnAfterRecoveryFailure(string message, bool definitive)
    {
        if (leaving) return;
        if (definitive) ForgetLastRun();
        MultiplayerLastRun.MenuNotice = message;
        leaving = true;
        leaveInactive = !definitive;
        reconnecting = restoringConnection = false;
        GetComponent<MultiplayerVoiceController>()?.StopVoice();
        StopSimulation();
        StartCoroutine(LeaveRoutine());
    }
    private void ForgetLastRun()
    {
        if (run == null) return;
        MultiplayerLastRun.Forget(run.participants.Find(p => p.actor == localActor)?.accountId, run.runId);
    }
    public override void OnLeftRoom() { managers.Clear(); if (!leaving && !Ended) EndLocal("LeftRoom"); }
    private void EndLocal(string reason)
    {
        reconnecting = restoringConnection = false;
        ForgetLastRun();
        GetComponent<MultiplayerVoiceController>()?.StopVoice();
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        { PhotonNetwork.CurrentRoom.IsOpen = false; PhotonNetwork.CurrentRoom.IsVisible = false; }
        if (run != null && !Ended) { run.endReason = reason; run.endedUtc = DateTime.UtcNow.ToString("o"); }
        if (run != null) { run.revision++; MultiplayerRunRecords.Record(run, localActor, localActor == run.hostActor, true); }
        StopSimulation();
    }
    private void StopSimulation()
    {
        if (GameDayManager.Instance != null) GameDayManager.Instance.ObserveDayOnly = true;
        if (Ended) Status = "Run ended: " + run.endReason + ". Completed " + run.completedDay + " days.";
        FindFirstObjectByType<ManagementComputerController>()?.CloseComputer();
        CashierRegisterUI.Instance?.Hide();
        CardPaymentUI.Instance?.CloseNetworkPayment();
        ManagerComplaintSystem.Instance?.SuspendNetworkPresentation();
        if (!Ended) return; // A guest can restore its live presentation after a timely rejoin.
        foreach (var group in FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None)) group.StopAllCoroutines();
        foreach (var bot in FindObjectsByType<AutonomousStaffBot>(FindObjectsSortMode.None)) bot.enabled = false;
        foreach (var kitchen in FindObjectsByType<KitchenManager>(FindObjectsSortMode.None)) kitchen.StopAllCoroutines();
        foreach (var worker in FindObjectsByType<KitchenWorkerBot>(FindObjectsSortMode.None)) worker.StopAllCoroutines();
        foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
            if (agent.gameObject.scene == gameObject.scene && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }
    public void LeaveToMenu()
    {
        if (leaving) return;
        leaveInactive = run != null && !Ended && localActor != run.hostActor;
        if (leaveInactive && IsConnected) MultiplayerLastRun.Remember(run, localActor);
        if (IsHostConnection && !Ended) { run.endReason = "HostLeft"; PublishRun(); }
        else if (run != null) MultiplayerRunRecords.Record(run, localActor, false, Ended);
        leaving = true;
        reconnecting = restoringConnection = false;
        GetComponent<MultiplayerVoiceController>()?.StopVoice();
        StopSimulation();
        StartCoroutine(LeaveRoutine());
    }
    private IEnumerator LeaveRoutine()
    {
        PhotonNetwork.AutomaticallySyncScene = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(leaveInactive);
        else if (PhotonNetwork.NetworkClientState != ClientState.Disconnected) PhotonNetwork.Disconnect();
        float deadline = Time.realtimeSinceStartup + 5f;
        // PUN clears InRoom as soon as leave starts, before room cleanup finishes.
        while (PhotonNetwork.CurrentRoom != null && Time.realtimeSinceStartup < deadline) yield return null;
        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.Disconnect();
            deadline = Time.realtimeSinceStartup + 5f;
            while (PhotonNetwork.NetworkClientState != ClientState.Disconnected
                && Time.realtimeSinceStartup < deadline) yield return null;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene("NewGameMenu");
    }
    private void OnApplicationQuit()
    {
        if (run == null) return;
        if (localActor == run.hostActor && !Ended) { run.endReason = "HostLeft"; run.endedUtc = DateTime.UtcNow.ToString("o"); run.revision++; }
        if (localActor == run.hostActor || Ended) ForgetLastRun();
        else if (IsConnected) MultiplayerLastRun.Remember(run, localActor);
        MultiplayerRunRecords.Record(run, localActor, localActor == run.hostActor, Ended);
    }
    private void OnApplicationPause(bool paused)
    { if (paused && run != null && !Ended && IsConnected) MultiplayerLastRun.Remember(run, localActor); }
    private void OnDestroy()
    {
        managers.Clear();
        if (Instance == this) { PhotonNetwork.SerializationRate = previousSerializationRate; Instance = null; }
    }
}
