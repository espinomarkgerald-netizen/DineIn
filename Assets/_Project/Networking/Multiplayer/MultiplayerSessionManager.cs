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
public partial class MultiplayerSessionManager : MonoBehaviourPunCallbacks
{
    public const string SceneName = "Lobby1 Multiplayer", RunKey = "restaurant.run.v2";
    public const string ProtocolKey = "restaurant.protocol", Protocol = "shared-session-23";
    public const string ResultRulesVersion = "casual-session-2";
    public const string ReadyKey = "restaurant.ready", LoadedKey = "restaurant.loaded";
    public const int RejoinSeconds = 90;
    public static MultiplayerSessionManager Instance { get; private set; }
    private readonly Dictionary<int, MultiplayerManagerRegistration> managers = new();
    private MultiplayerRunRecord run;
    [SerializeField] private MultiplayerSocialUI socialUIPrefab;
    [Tooltip("Scene component implementing IMultiplayerSessionGameplay. Empty uses the existing Casual Dining bridge.")]
    [SerializeField] private MonoBehaviour gameplayBinding;
    private IMultiplayerSessionGameplay gameplay;
    [Header("Connection recovery")]
    [SerializeField, Range(10f, 60f)] private float automaticReconnectSeconds = 30f;
    [SerializeField, Range(10f, 60f)] private float restorationTimeoutSeconds = 25f;
    private bool leaving, reconnecting, restoringConnection;
    private float restoreUntil, nextRebind, nextRemember;
    private bool leaveInactive;
    public bool IsRecovering => reconnecting || restoringConnection || recoveryFailed;
    private float reconnectUntil, nextReconnect;
    private int localActor;
    private int previousSerializationRate;
    private float previousDispatchThreshold;
    public string Status { get; private set; } = "Loading restaurant…";
    // Remains a multiplayer context while disconnected/ended: never fall back to local simulation.
    public bool IsMultiplayerSession => Instance == this && isActiveAndEnabled;
    public bool IsConnected => IsMultiplayerSession && PhotonNetwork.InRoom;
    public bool IsHostConnection => IsConnected && run != null && PhotonNetwork.LocalPlayer.ActorNumber == run.hostActor
        && PhotonNetwork.MasterClient != null && PhotonNetwork.MasterClient.ActorNumber == run.hostActor;
    public bool IsAuthority => IsHostConnection && !Ended;
    public bool Ended => run != null && !string.IsNullOrEmpty(run.endReason);
    public bool CanAct => IsConnected && !Ended && run != null && !leaving && !reconnecting
        && !restoringConnection && !recoveryFailed && !intentionalLeavePending && ValidActor(localActor)
        && gameplay?.ReadyForInput == true;
    public string RunId => run?.runId ?? string.Empty;
    public MultiplayerRunRecord Run => run;
    public Player LocalPhotonPlayer => IsConnected ? PhotonNetwork.LocalPlayer : null;
    public int LocalActorNumber => localActor;
    public Player[] ConnectedPlayers => IsConnected ? PhotonNetwork.PlayerList : Array.Empty<Player>();
    public int ConnectedHumanCount => Array.FindAll(ConnectedPlayers, p => !p.IsInactive).Length;
    public int PartySize => run?.partySize ?? 0;
    public string RestaurantType => run?.restaurant ?? string.Empty;
    public GameObject LocalManager => CanAct && TryGetManager(localActor, out var manager)
        && manager.activeInHierarchy && manager.GetComponent<PhotonView>().OwnerActorNr == localActor
        && manager.GetComponent<PhotonView>().IsMine ? manager : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        PhotonNetwork.EnableCloseConnection = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        // Hygiene decisions pause simulation; room updates must still resolve that pause.
        previousDispatchThreshold = PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate;
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = Mathf.Max(0f, previousDispatchThreshold);
        previousSerializationRate = PhotonNetwork.SerializationRate;
        PhotonNetwork.SerializationRate = 10;
        localActor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
        ReadRun();
        if (run == null) Status = "This room has no valid run. Return to the menu.";
        gameplay = gameplayBinding as IMultiplayerSessionGameplay;
        if (gameplay == null && RestaurantType == "CasualDining")
        {
            var bridge = GetComponent<MultiplayerRestaurantBridge>();
            if (bridge == null) bridge = gameObject.AddComponent<MultiplayerRestaurantBridge>();
            gameplayBinding = bridge; gameplay = bridge;
        }
        gameplay?.InitializeSession();
        if (GetComponent<MultiplayerSessionUI>() == null) gameObject.AddComponent<MultiplayerSessionUI>();
        if (GetComponent<MultiplayerVoiceController>() == null) gameObject.AddComponent<MultiplayerVoiceController>();
        if (socialUIPrefab == null) socialUIPrefab = Resources.Load<MultiplayerSocialUI>("UI/MultiplayerSocialUI");
        if (socialUIPrefab != null) Instantiate(socialUIPrefab, transform, false);
        if (run != null && PhotonNetwork.MasterClient?.ActorNumber != run.hostActor) EndLocal("HostDisconnected");
        if (run != null && !Ended)
        {
            MultiplayerLastRun.Remember(run, localActor);
            nextRemember = Time.realtimeSinceStartup + 30f;
            if (!IsHostConnection) BeginRestoration();
        }
    }

    private void Update()
    {
        if (run == null || leaving) return;
        var auth = PlayFabAuthManager.Instance;
        var participant = run.participants.Find(p => p.accountId == auth?.PlayFabId);
        if (participant?.kicked == true) { ReturnAfterRecoveryFailure(RemovedMessage, true); return; }
        if (participant?.departed == true) { ReturnAfterRecoveryFailure("You left the session.", true); return; }
        RecordSuccessfulExperience();
        TickLeaveRequest();
        if (!Ended && (auth == null || !auth.IsLoggedIn || participant == null || participant.accountId != auth.PlayFabId))
        { LeaveToMenu(); return; }
        if (reconnecting) { TickRecovery(); return; }
        if (recoveryFailed) return;
        if (restoringConnection)
        {
            if (Time.realtimeSinceStartup >= restoreUntil)
            { FailRecovery("Synchronization timed out. Retry or return to the menu."); return; }
            if (Time.realtimeSinceStartup >= nextRebind)
            {
                nextRebind = Time.realtimeSinceStartup + 0.25f;
                bool hasAvatar = ValidActor(localActor) && MultiplayerManagerRegistration.TryRebindActor(this, localActor, out _);
                if (hasAvatar && gameplay?.RestorationComplete == true)
                { restoringConnection = false; PhotonCustomizationSync.PushToPhoton(); }
            }
        }
        if (!Ended && IsConnected && Time.realtimeSinceStartup >= nextRemember)
        { MultiplayerLastRun.Remember(run, localActor); nextRemember = Time.realtimeSinceStartup + 30f; }
        string loadedToken = participant != null ? RunId + ":" + participant.loadEpoch : string.Empty;
        if (!Ended && !restoringConnection && gameplay?.ReadyForInput == true && IsConnected
            && TryGetManager(localActor, out _) && !Equals(PhotonNetwork.LocalPlayer.CustomProperties[LoadedKey], loadedToken))
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LoadedKey] = loadedToken });
        if (!Ended) Status = intentionalLeavePending ? "Leaving session..."
            : restoringConnection ? "Synchronizing restaurant…" : "Connected";
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
    // A reserved seat does not reserve interrupted work; other players may take it.
    public bool IsActorInReconnectGrace(int actor) => false;
    public bool ValidActor(int actor)
    {
        if (!IsConnected || Ended || run == null) return false;
        var p = run.participants.Find(value => value.actor == actor && !value.departed && !value.kicked);
        return p != null && p.disconnectDeadline == 0 && PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player)
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

    private float nextExperienceSave;
    private void RecordSuccessfulExperience()
    {
        if (run == null || Time.realtimeSinceStartup < nextExperienceSave) return;
        nextExperienceSave = Time.realtimeSinceStartup + 1f;
        var auth = PlayFabAuthManager.Instance;
        if (auth == null || !auth.IsLoggedIn || LocalSaveManager.NeedsRecovery ||
            !CampaignSaveStore.AccountMatches(auth.PlayFabId) || LocalSaveManager.Data.progressAccount != auth.PlayFabId) return;
        var own = run.participants.Find(p => p.accountId == auth.PlayFabId);
        if (own?.successfulDays == null) return;
        string restaurant = run.restaurant == "FastFood" ? LocalSaveManager.FastFoodId :
            run.restaurant == "FineDining" ? LocalSaveManager.FineDiningId : LocalSaveManager.CasualDiningId;
        var ids = LocalSaveManager.Data.ownedItemIds;
        var added = new List<string>();
        foreach (int day in own.successfulDays)
        {
            if (day < 1 || day > own.completedDay) continue;
            string key = "restaurant-day:" + restaurant + ":" + run.runId + ":" + day;
            if (!ids.Contains(key)) { ids.Add(key); added.Add(key); }
        }
        if (added.Count > 0 && !LocalSaveManager.TrySave()) foreach (var id in added) ids.Remove(id);
    }

    public void CompleteDay(int day, string terminalReason)
    {
        if (!IsAuthority || day <= run.completedDay) return;
        run.completedDay = day;
        run.highestDay = Math.Max(run.highestDay, day);
        foreach (var p in run.participants)
        {
            if (p.departed) continue;
            if (p.disconnectDeadline > 0) p.provisionalDay = day;
            else
            {
                p.completedDay = day;
                if (string.IsNullOrEmpty(terminalReason))
                {
                    p.successfulDays ??= new List<int>();
                    if (!p.successfulDays.Contains(day)) p.successfulDays.Add(day);
                }
            }
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
        RecordSuccessfulExperience();
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
            if (!MultiplayerRunRecords.Valid(incoming) || incoming.gameVersion != Application.version
                || incoming.sceneName != gameObject.scene.name
                || !Equals(PhotonNetwork.CurrentRoom.CustomProperties[ProtocolKey], Protocol)
                || !Equals(PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerDestinations.RestaurantKey], incoming.restaurant)
                || !Equals(PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerDestinations.SceneKey], incoming.sceneName)
                || (run != null &&
                (incoming.runId != run.runId || incoming.revision < run.revision || incoming.hostActor != run.hostActor
                    || incoming.restaurant != run.restaurant || incoming.sceneName != run.sceneName || incoming.rulesVersion != run.rulesVersion
                    || incoming.hostAccountId != run.hostAccountId || incoming.startedUtc != run.startedUtc
                    || incoming.partySize != run.partySize || incoming.gameVersion != run.gameVersion
                    || Ended && string.IsNullOrEmpty(incoming.endReason)
                    || incoming.participants.Exists(p => !run.participants.Exists(old => old.accountId == p.accountId && old.initialActor == p.initialActor))))) return;
            run = incoming;
            RecordSuccessfulExperience();
            MultiplayerRunRecords.Record(run, localActor, IsHostConnection);
            if (Ended) { ForgetLastRun(); StopSimulation(); }
        }
        catch (ArgumentException) { Status = "Invalid run record."; }
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.ContainsKey(RunKey)) return;
        if (!IsHostConnection) ReadRun();
        FinishPendingRemovals(changed[RunKey] as string);
    }

    public override void OnPlayerLeftRoom(Player player)
    {
        managers.Remove(player.ActorNumber);
        if (run == null || Ended) return;
        if (player.ActorNumber == run.hostActor) { EndLocal("HostDisconnected"); return; }
        if (!IsAuthority) return;
        gameplay?.ParticipantUnavailable(player.ActorNumber);
        var p = run.participants.Find(value => value.actor == player.ActorNumber);
        if (p == null || p.departed) return;
        p.loadEpoch++;
        p.disconnectDeadline = Math.Max(1d, PhotonNetwork.Time); // Presence marker, not expiry.
        PublishRun();
        RefreshReservations();
    }
    public override void OnPlayerEnteredRoom(Player player)
    {
        if (IsAuthority) AdmitParticipant(player);
    }
    public override void OnMasterClientSwitched(Player player)
    { if (run != null && player.ActorNumber != run.hostActor) EndLocal("HostDisconnected"); }
    public override void OnDisconnected(DisconnectCause cause)
    {
        managers.Clear();
        if (leaving || Ended || run == null) return;
        StopSimulation();
        if (localActor == run.hostActor) { EndLocal("HostDisconnected"); return; }
        gameplay?.ConnectionLost();
        restoringConnection = false;
        joinInFlight = false;
        if (!reconnecting && !recoveryFailed) StartRecovery();
    }
    public override void OnJoinedRoom()
    {
        if ((!reconnecting && !recoveryFailed) || run == null) return;
        if (recoveryFailed) reconnectUntil = Time.realtimeSinceStartup + restorationTimeoutSeconds;
        reconnecting = true; recoveryFailed = false;
        localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        admissionPending = true; joinInFlight = false;
        ReadRun();
        Status = "Rejoining - waiting for host admission...";
        TryFinishAdmission();
    }
    private void BeginRestoration()
    {
        restoringConnection = true;
        restoreUntil = Time.realtimeSinceStartup + restorationTimeoutSeconds;
        nextRebind = 0f;
        gameplay?.RequestRestoration();
        MultiplayerManagerRegistration.TryRebindActor(this, localActor, out _);
    }
    public override void OnJoinRoomFailed(short code, string message)
    {
        if (!reconnecting) return;
        joinInFlight = false;
        if (code == ErrorCode.JoinFailedWithRejoinerNotFound) fallbackJoin = true;
        else if (code == ErrorCode.GameDoesNotExist)
        { ReturnAfterRecoveryFailure("The original session has ended.", true); return; }
        nextReconnect = Time.realtimeSinceStartup + 3f;
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
        MultiplayerLastRun.Forget(PlayFabAuthManager.Instance?.PlayFabId, run.runId);
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
        if (Ended) Status = "Run ended: " + run.endReason + ". Completed " + run.completedDay + " days.";
        gameplay?.SuspendSession(Ended);
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
        MultiplayerSocialUI.HideForExit();
        var sessionUI = GetComponent<MultiplayerSessionUI>();
        if (sessionUI != null) sessionUI.enabled = false;
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
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene("NewGameMenu");
        else SceneLoader.TryLoadScene("NewGameMenu", LoadSceneMode.Single, out _);
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
        if (Instance == this)
        {
            PhotonNetwork.SerializationRate = previousSerializationRate;
            PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = previousDispatchThreshold;
            Instance = null;
        }
    }
}
