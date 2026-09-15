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
    public const string ProtocolKey = "restaurant.protocol", Protocol = "casual-session-4";
    public const string ResultRulesVersion = "casual-session-2";
    public const string ReadyKey = "restaurant.ready", LoadedKey = "restaurant.loaded";
    public const int RejoinSeconds = 90;
    public static MultiplayerSessionManager Instance { get; private set; }
    private readonly Dictionary<int, MultiplayerManagerRegistration> managers = new();
    private MultiplayerRunRecord run;
    private bool leaving, reconnecting, restoringConnection;
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
        if (run != null && PhotonNetwork.MasterClient?.ActorNumber != run.hostActor) EndLocal("HostDisconnected");
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
            if (Time.realtimeSinceStartup >= reconnectUntil) { EndLocal("Disconnected"); return; }
            Status = "Reconnecting… " + Mathf.CeilToInt(reconnectUntil - Time.realtimeSinceStartup) + "s";
            if (Time.realtimeSinceStartup >= nextReconnect && !PhotonNetwork.IsConnected)
            { nextReconnect = Time.realtimeSinceStartup + 3f; PhotonNetwork.ReconnectAndRejoin(); }
            return;
        }
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
        if (Ended) StopSimulation();
    }
    private void ReadRun()
    {
        if (PhotonNetwork.CurrentRoom?.CustomProperties[RunKey] is not string json || json.Length > 32768) return;
        try
        {
            var incoming = JsonUtility.FromJson<MultiplayerRunRecord>(json);
            if (!MultiplayerRunRecords.Valid(incoming) || (run != null &&
                (incoming.runId != run.runId || incoming.revision < run.revision || incoming.hostActor != run.hostActor
                    || incoming.hostAccountId != run.hostAccountId || incoming.startedUtc != run.startedUtc
                    || incoming.partySize != run.partySize || incoming.gameVersion != run.gameVersion
                    || Ended && string.IsNullOrEmpty(incoming.endReason)
                    || incoming.participants.Exists(p => !run.participants.Exists(old => old.actor == p.actor && old.accountId == p.accountId))))) return;
            run = incoming;
            MultiplayerRunRecords.Record(run, localActor, IsHostConnection);
            if (Ended) StopSimulation();
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
        if (player.IsInactive) { p.loadEpoch++; p.disconnectDeadline = PhotonNetwork.Time + RejoinSeconds; }
        else { p.departed = true; p.provisionalDay = 0; p.provisionalHighestDay = 0; p.disconnectDeadline = 0; }
        PublishRun();
    }
    public override void OnPlayerEnteredRoom(Player player)
    {
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
        if (!reconnecting) reconnectUntil = Time.realtimeSinceStartup + RejoinSeconds;
        reconnecting = true;
        nextReconnect = Time.realtimeSinceStartup + 1f;
    }
    public override void OnJoinedRoom()
    {
        if (!reconnecting || run == null) return;
        if (PhotonNetwork.CurrentRoom.CustomProperties[RunKey] is not string json || !json.Contains(run.runId))
        { EndLocal("RunUnavailable"); return; }
        reconnecting = false;
        restoringConnection = true;
        ReadRun();
        var participant = run.participants.Find(p => p.actor == localActor);
        if (participant == null || participant.departed) { EndLocal("RejoinExpired"); return; }
        if (Ended) return;
        GetComponent<MultiplayerRestaurantBridge>()?.RequestSnapshot();
        GetComponent<MultiplayerDayBridge>()?.RefreshAfterRejoin();
        foreach (var registration in FindObjectsByType<MultiplayerManagerRegistration>(FindObjectsSortMode.None)) Register(registration);
    }
    public void SnapshotRestored() { restoringConnection = false; }
    public override void OnJoinRoomFailed(short code, string message) => EndLocal("RunUnavailable");
    public override void OnLeftRoom() { managers.Clear(); if (!leaving && !Ended) EndLocal("LeftRoom"); }
    private void EndLocal(string reason)
    {
        reconnecting = false;
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
        if (IsHostConnection && !Ended) { run.endReason = "HostLeft"; PublishRun(); }
        else if (run != null) MultiplayerRunRecords.Record(run, localActor, false, true);
        leaving = true;
        reconnecting = false;
        StopSimulation();
        StartCoroutine(LeaveRoutine());
    }
    private IEnumerator LeaveRoutine()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (PhotonNetwork.InRoom && Time.realtimeSinceStartup < deadline) yield return null;
        if (PhotonNetwork.InRoom) PhotonNetwork.Disconnect();
        Time.timeScale = 1f;
        SceneManager.LoadScene("NewGameMenu");
    }
    private void OnApplicationQuit()
    {
        if (run == null) return;
        if (localActor == run.hostActor && !Ended) { run.endReason = "HostLeft"; run.endedUtc = DateTime.UtcNow.ToString("o"); run.revision++; }
        MultiplayerRunRecords.Record(run, localActor, localActor == run.hostActor, true);
    }
    private void OnDestroy()
    {
        managers.Clear();
        if (Instance == this) { PhotonNetwork.SerializationRate = previousSerializationRate; Instance = null; }
    }
}
