using System;
using System.Collections;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class MultiplayerMenuController : MonoBehaviourPunCallbacks
{
    [Header("Existing Multiplayer UI")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_Dropdown restaurantDropdown;
    [SerializeField] private TMP_Dropdown partySizeDropdown;
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text[] statusTexts;
    [SerializeField] private TMP_Text[] rosterTexts;
    [SerializeField] private TMP_Text createHeading, joinHeading;

    [Header("Returning players")]
    [SerializeField] private GameObject reconnectCard;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private TMP_Text reconnectDescription;

    [Header("Tabs")]
    [SerializeField] private Button joinTabButton;
    [SerializeField] private Button createTabButton;
    [SerializeField] private GameObject joinContents;
    [SerializeField] private GameObject createContents;

    private void ShowJoinTab() => ShowTab(false);
    private void ShowCreateTab()
    {
        if (PhotonNetwork.InRoom && !starting) { PhotonNetwork.LeaveRoom(false); return; }
        ShowTab(true);
    }

    private void ShowTab(bool create)
    {
        joinContents.SetActive(!create);
        createContents.SetActive(create);
        SetTabColor(joinTabButton, !create);
        SetTabColor(createTabButton, create); RefreshReconnectCard();
    }

    private static void SetTabColor(Button button, bool selected)
    {
        var colors = button.colors;
        colors.normalColor = selected ? colors.selectedColor : Color.white;
        button.colors = colors;
    }

    private bool busy;
    private bool starting;
    private bool pendingCreate;
    private string pendingCode;
    private string pendingAccountId;
    private string submittedCode;
    private string startingRunId;
    private string createTabLabel;
    private byte pendingSize;
    private float operationDeadline;
    private const string RestaurantKey = MultiplayerDestinations.RestaurantKey;
    [SerializeField] private string gameplayScene = MultiplayerDestinations.CasualScene;
    [SerializeField] private string fastFoodGameplayScene = MultiplayerDestinations.FastFoodScene;
    private string pendingRestaurant;
    private int lastLevelSelectIndex = -1;
    private string RoomRestaurant => PhotonNetwork.CurrentRoom?.CustomProperties[RestaurantKey] as string;
    private string Destination => MultiplayerDestinations.TryGet(RoomRestaurant, out var scene, out _) ? scene : null;

    private bool SupportedRoom() => MultiplayerDestinations.TryGet(RoomRestaurant, out var scene, out _)
        && Equals(PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerSessionManager.ProtocolKey], MultiplayerSessionManager.Protocol)
        && Equals(PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerDestinations.SceneKey], scene);

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = PhotonNetwork.CurrentRoom == null;
        PhotonBootstrap.ConnectionFailed += OnConnectionFailed;
        int selectedCapacity = Mathf.Clamp(partySizeDropdown.value, 0, 2);
        partySizeDropdown.ClearOptions();
        partySizeDropdown.AddOptions(new System.Collections.Generic.List<string> {
            "Up to 2 Players", "Up to 3 Players", "Up to 4 Players" });
        partySizeDropdown.SetValueWithoutNotify(selectedCapacity);
        partySizeDropdown.RefreshShownValue();
        createTabLabel = createTabButton.GetComponentInChildren<TMP_Text>()?.text;
        if (reconnectButton != null) reconnectButton.onClick.AddListener(ReconnectLastRun);
        createButton.onClick.AddListener(Create);
        joinButton.onClick.AddListener(Join);
        joinTabButton.onClick.AddListener(ShowJoinTab);
        createTabButton.onClick.AddListener(ShowCreateTab);
        foreach (var text in rosterTexts) text.richText = false;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        ShowJoinTab();
        RefreshRoom();
        if (!string.IsNullOrEmpty(MultiplayerLastRun.MenuNotice))
        { SetStatus(MultiplayerLastRun.MenuNotice); MultiplayerLastRun.MenuNotice = null; }
    }

    // Keep room callbacks alive when the existing panel animation hides this UI.
    public override void OnDisable() { }

    private void OnDestroy()
    {
        PhotonBootstrap.ConnectionFailed -= OnConnectionFailed;
        PhotonNetwork.RemoveCallbackTarget(this);
        if (reconnectButton != null) reconnectButton.onClick.RemoveListener(ReconnectLastRun);
        createButton.onClick.RemoveListener(Create);
        joinButton.onClick.RemoveListener(Join);
        joinTabButton.onClick.RemoveListener(ShowJoinTab);
        createTabButton.onClick.RemoveListener(ShowCreateTab);
    }

    // Level Select and the existing dropdown use the same authored order, not enum ordinals
    // (RestaurantType.FastFood is 0) or localized/decorative option labels.
    private static string RestaurantForSelection(int index) => index == 0 ? nameof(RestaurantType.CasualDining)
        : index == 1 ? nameof(RestaurantType.FastFood) : null;

    public bool TrySelectRestaurant(int index, out string error)
    {
        error = null;
        // Existing rooms and in-flight requests own their destination.
        if (busy || starting || PhotonNetwork.InRoom) return true;
        if (!MultiplayerDestinations.TryGet(RestaurantForSelection(index), out _, out _))
        { error = "Choose Casual Dining or Fast Food. Fine Dining is not available yet."; return false; }
        if (restaurantDropdown == null || restaurantDropdown.options.Count <= index)
        { error = "The multiplayer restaurant selector is not configured correctly."; return false; }
        // Reopening the panel must retain an explicit choice made in its dropdown.
        // A different Level Select restaurant starts a new selection context.
        if (lastLevelSelectIndex != index)
        {
            restaurantDropdown.SetValueWithoutNotify(index);
            restaurantDropdown.RefreshShownValue();
            lastLevelSelectIndex = index;
        }
        return true;
    }

    private void Create()
    {
        if (PhotonNetwork.InRoom) { RoomAction(); return; }
        if (busy || PhotonNetwork.InRoom) return;
        pendingRestaurant = RestaurantForSelection(restaurantDropdown.value);
        if (!MultiplayerDestinations.TryGet(pendingRestaurant, out var scene, out _))
        {
            SetStatus("Choose Casual Dining or Fast Food. Fine Dining is not available yet.");
            return;
        }
        if (scene != (pendingRestaurant == nameof(RestaurantType.FastFood) ? fastFoodGameplayScene : gameplayScene))
        { SetStatus("The selected restaurant's multiplayer scene is configured incorrectly."); return; }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        { SetStatus("The selected restaurant's multiplayer scene is missing from this build."); return; }
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new StringBuilder("DINE-");
        for (int i = 0; i < 4; i++) code.Append(alphabet[UnityEngine.Random.Range(0, alphabet.Length)]);
        pendingSize = (byte)Mathf.Clamp(partySizeDropdown.value + 2, 2, 4);
        Begin(true, code.ToString());
    }

    private void Join()
    {
        if (PhotonNetwork.InRoom) { RoomAction(); return; }
        if (busy || PhotonNetwork.InRoom) return;
        string code = codeInput.text.Trim().ToUpperInvariant();
        if (code.Length == 4) code = "DINE-" + code;
        if (code.Length != 9 || !code.StartsWith("DINE-") ||
            !Array.TrueForAll(code.Substring(5).ToCharArray(), c => c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'))
        {
            SetStatus("Enter a DINE-XXXX room code.");
            return;
        }
        codeInput.text = code;
        Begin(false, code);
    }

    private void Begin(bool create, string code, bool rejoin = false)
    {
        if (!CampaignCloudSync.CanEnterGameplay()) return;
        var account = PlayFabAuthManager.Instance;
        if (account == null || !account.IsLoggedIn || string.IsNullOrEmpty(account.PlayFabId))
        { SetStatus("Sign in before starting multiplayer."); return; }
        pendingAccountId = account.PlayFabId;
        if (!rejoin) { rejoinPointer = null; rejoinFailure = null; }
        pendingRejoin = rejoin; fallbackRejoin = awaitingAdmission = false;
        PhotonNetwork.AutomaticallySyncScene = false;
        busy = true;
        operationDeadline = Time.realtimeSinceStartup + 30f;
        pendingCreate = create;
        pendingCode = code;
        RefreshControls();
        if (Ready() && PhotonBootstrap.ConnectionMatchesAccount(account.PlayFabId)) { Submit(); return; }
        SetStatus("Connecting...");
        if (PhotonBootstrap.Instance == null)
            new GameObject("PhotonBootstrap").AddComponent<PhotonBootstrap>();
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonBootstrap.Instance.SafeConnect();
    }

    private static bool Ready() =>
        PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer ||
        PhotonNetwork.NetworkClientState == ClientState.JoinedLobby;

    public override void OnConnectedToMaster()
    {
        // Let Bootstrap finish its optional JoinLobby callback before submitting.
        if (isActiveAndEnabled) StartCoroutine(SubmitAfterCallbacks());
    }

    private IEnumerator SubmitAfterCallbacks()
    {
        yield return null;
        Submit();
    }

    public override void OnJoinedLobby() => Submit();

    private void Submit()
    {
        if (pendingCode == null || !Ready()) return;
        string code = pendingCode;
        submittedCode = code;
        pendingCode = null;
        var account = PlayFabAuthManager.Instance;
        if (account == null || !account.IsLoggedIn || account.PlayFabId != pendingAccountId || !PhotonBootstrap.ConnectionMatchesAccount(account.PlayFabId))
        { Fail("Sign in and reconnect before joining a run."); return; }
        PhotonNetwork.NickName = account != null && account.IsLoggedIn &&
            !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName.Trim() : "Player";
        if (pendingRejoin)
        {
            if (!MultiplayerLastRun.IsCompatible(rejoinPointer, account.PlayFabId, DateTime.UtcNow, out var reason))
            { Fail(reason); return; }
            TraceRoom("rejoin request", code);
            SetStatus("Checking your reserved place and reconnecting...");
            if (!(fallbackRejoin ? PhotonNetwork.JoinRoom(code) : PhotonNetwork.RejoinRoom(code))) Fail("Could not start reconnecting. Please retry.");
            return;
        }
        SetStatus(pendingCreate ? "Creating..." : "Joining...");
        PhotonCustomizationSync.PushToPhoton();
        string destination = null;
        if (pendingCreate && !MultiplayerDestinations.TryGet(pendingRestaurant, out destination, out _))
        { Fail("The selected multiplayer restaurant is unsupported."); return; }
        TraceRoom(pendingCreate ? "create request" : "join request", code);
        bool sent = pendingCreate
            ? PhotonNetwork.CreateRoom(code, new RoomOptions
            {
                MaxPlayers = pendingSize, IsOpen = true, IsVisible = false, PublishUserId = true,
                // The stable run roster + ExpectedUsers reserve seats. Dropped transport actors
                // are removed immediately, so hosts can release offline seats without waiting for TTL.
                PlayerTtl = 0, EmptyRoomTtl = 0,
                CustomRoomProperties = new Hashtable { { RestaurantKey, pendingRestaurant },
                    { MultiplayerDestinations.SceneKey, destination },
                    { MultiplayerSessionManager.ProtocolKey, MultiplayerSessionManager.Protocol } }
            }, TypedLobby.Default)
            : PhotonNetwork.JoinRoom(code);
        if (!sent) Fail("Unable to contact room. Try again.");
    }

    public override void OnCreatedRoom() => TraceRoom("created", PhotonNetwork.CurrentRoom?.Name);

    public override void OnJoinedRoom()
    {
        TraceRoom("joined", PhotonNetwork.CurrentRoom?.Name);
        if (!SupportedRoom())
        {
            rejoinFailure = "Room version or restaurant is unsupported. Use the same updated game version.";
            pendingRejoin = awaitingAdmission = false;
            PhotonNetwork.LeaveRoom(false);
            return;
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties[MultiplayerSessionManager.RunKey] is string existingRun)
        {
            if (!pendingRejoin)
            {
                // Manual room-code join follows exactly the same admission checks as Rejoin Session.
                try
                {
                    var record = JsonUtility.FromJson<MultiplayerRunRecord>(existingRun);
                    var member = record?.participants?.Find(p => p.accountId == PhotonNetwork.LocalPlayer.UserId);
                    rejoinPointer = MultiplayerRunRecords.Valid(record) && member != null
                        ? new LastMultiplayerRun { room = PhotonNetwork.CurrentRoom.Name, runId = record.runId,
                            accountId = member.accountId, actor = member.actor, restaurant = record.restaurant }
                        : null;
                }
                catch (ArgumentException) { rejoinPointer = null; }
                pendingRejoin = true;
            }
            CompleteLastRunJoin(); return;
        }
        if (pendingRejoin) { CompleteLastRunJoin(); return; }
        PhotonNetwork.AutomaticallySyncScene = true;
        busy = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {
            [MultiplayerSessionManager.ReadyKey] = false, [MultiplayerSessionManager.LoadedKey] = "" });
        RefreshRoom();
    }
    public override void OnPlayerEnteredRoom(Player player) => RefreshRoom();
    public override void OnPlayerLeftRoom(Player player) => RefreshRoom();
    public override void OnMasterClientSwitched(Player player) => RefreshRoom();
    public override void OnPlayerPropertiesUpdate(Player player, Hashtable changed) => RefreshRoom();
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        RefreshRoom();
        if (awaitingAdmission) { CompleteLastRunJoin(); return; }
        if (starting && PhotonNetwork.IsMasterClient && changed[MultiplayerSessionManager.RunKey] is string json
            && !string.IsNullOrEmpty(startingRunId) && json.Contains(startingRunId))
        {
            MultiplayerRunRecord accepted;
            try { accepted = JsonUtility.FromJson<MultiplayerRunRecord>(json); }
            catch (ArgumentException) { return; }
            if (!SupportedRoom() || !MultiplayerRunRecords.Valid(accepted) || accepted.runId != startingRunId
                || accepted.restaurant != RoomRestaurant || accepted.sceneName != Destination)
            {
                startingRunId = null;
                Fail("Run restaurant or destination does not match the room. Leave and create a supported room.");
                return;
            }
            startingRunId = null;
            PhotonNetwork.LoadLevel(accepted.sceneName);
        }
    }
    public override void OnLeftRoom()
    {
        starting = false; busy = false; pendingRejoin = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        RefreshRoom();
        if (!string.IsNullOrEmpty(rejoinFailure)) { SetStatus(rejoinFailure); rejoinFailure = null; }
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!string.IsNullOrEmpty(rejoinFailure))
        { string notice = rejoinFailure; rejoinFailure = null; Fail(notice); return; }
        if (cause == DisconnectCause.DisconnectByClientLogic && busy) return;
        if (busy) Fail(PhotonBootstrap.Instance?.LastConnectionError ?? "Photon disconnected (" + cause + "). Please retry.");
    }
    private void OnConnectionFailed(string message) { if (busy) Fail(message); }
    public override void OnCreateRoomFailed(short code, string message)
    {
        TraceRoom("create failed", submittedCode, code, message);
        Fail(code == ErrorCode.GameIdAlreadyExists ? "Code already used. Press Create again."
            : "Could not create room (" + code + "). Reconnect and retry.");
    }
    public override void OnJoinRoomFailed(short code, string message)
    {
        TraceRoom("join failed", submittedCode, code, message);
        if (!pendingRejoin) { Fail(JoinFailureMessage(code)); return; }
        if (code == ErrorCode.JoinFailedWithRejoinerNotFound)
        { fallbackRejoin = true; pendingCode = rejoinPointer.room; return; }
        bool expired = code == ErrorCode.GameDoesNotExist;
        if (expired) MultiplayerLastRun.Forget(rejoinPointer?.accountId, rejoinPointer?.runId);
        Fail(expired ? "That run or your reserved place no longer exists. Join or create a new room."
            : code == ErrorCode.JoinFailedFoundActiveJoiner || code == ErrorCode.JoinFailedPeerAlreadyJoined
                ? "Your previous connection is still active. Wait a moment, then reconnect."
                : "Unable to reconnect right now. Check your connection and try again.");
    }

    internal static string JoinFailureMessage(short code) =>
        code == ErrorCode.GameDoesNotExist ? "Room not found (32758). Check the exact code and that the host is still in the room." :
            code == ErrorCode.GameFull ? "Room is full." :
            code == ErrorCode.GameClosed ? "Room is closed to joining. Check with the host." :
            code == ErrorCode.JoinFailedFoundActiveJoiner || code == ErrorCode.JoinFailedPeerAlreadyJoined
                ? "This account is already in the room. Each player must sign in with a different account." :
            code == ErrorCode.JoinFailedFoundInactiveJoiner
                ? "Your place is reserved. Use Rejoin Session to return." :
            "Unable to join (" + code + "). Check your connection and try again.";

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void TraceRoom(string action, string code, short failure = 0, string detail = null)
    {
        var client = PhotonNetwork.NetworkingClient;
        var room = PhotonNetwork.CurrentRoom;
        // Application/account IDs are identifiers; never log AuthValues, tokens or credentials.
        Debug.Log($"[Room] {action} code='{code}' account={PhotonNetwork.LocalPlayer?.UserId} " +
            $"app={client.AppId} region={PhotonNetwork.CloudRegion} version={client.AppVersion} " +
            $"state={PhotonNetwork.NetworkClientState} room='{room?.Name}' open={room?.IsOpen} " +
            $"players={room?.PlayerCount}/{room?.MaxPlayers} failure={failure} message={detail}");
    }

    private void Fail(string message)
    {
        if (pendingRejoin && !PhotonNetwork.InRoom && !Ready()) PhotonNetwork.Disconnect();
        pendingRejoin = awaitingAdmission = false;
        PhotonNetwork.AutomaticallySyncScene = !PhotonNetwork.InRoom;
        busy = false;
        pendingCode = null;
        starting = false;
        RefreshRoom();
        SetStatus(message);
    }

    private void RefreshRoom()
    {
        RefreshControls();
        if (!PhotonNetwork.InRoom)
        {
            codeText.text = "<size=50%>Room code</size>\n<nobr>—</nobr>";
            if (joinCodeText != null) joinCodeText.text = codeText.text;
            foreach (var text in rosterTexts) text.text = "Waiting…";
            SetStatus("Waiting…");
            return;
        }
        var room = PhotonNetwork.CurrentRoom;
        codeText.text = "<size=50%>Room code</size>\n<nobr>" + MultiplayerSocialUI.Escape(room.Name) + "</nobr>";
        if (joinCodeText != null) joinCodeText.text = codeText.text;
        var roster = new StringBuilder();
        var players = PhotonNetwork.PlayerList;
        Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        foreach (var player in players)
        {
            string nickname = string.IsNullOrWhiteSpace(player.NickName) ? "Player" : player.NickName.Replace('\n',' ').Replace('\r',' ');
            if (nickname.Length > 22) nickname = nickname.Substring(0,22);
            roster.Append(player == players[0] ? "" : "\n").Append(nickname).Append(player.IsMasterClient ? " (HOST)" : "")
                .Append(player.IsLocal ? " (YOU)" : "")
                .Append(player.IsInactive ? "\nReconnecting" : Equals(player.CustomProperties[MultiplayerSessionManager.ReadyKey], true) ? "\nREADY" : "\nNot ready");
        }
        for (int i = players.Length; i < room.MaxPlayers; i++) roster.Append("\nOpen seat\nWaiting…");
        foreach (var text in rosterTexts) text.text = roster.ToString();
        if (!SupportedRoom())
        {
            SetStatus("Room version or restaurant differs. All players must use the same updated game version.");
            return;
        }
        bool enoughPlayers = HasPlayablePartySize(room.PlayerCount, room.MaxPlayers);
        SetStatus(starting ? "STARTING..." : enoughPlayers ? "Ready up. Host starts when all are ready." : "Waiting for players…");
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCardRefresh)
        { nextCardRefresh = Time.unscaledTime + 1f; RefreshReconnectCard(); }
        // Recover a ready connection even when the menu missed a callback while hidden.
        if (busy && pendingCode != null && Ready()
            && PlayFabAuthManager.Instance?.IsLoggedIn == true
            && PhotonBootstrap.ConnectionMatchesAccount(PlayFabAuthManager.Instance.PlayFabId)) Submit();
        if (busy && Time.realtimeSinceStartup > operationDeadline)
        {
            if (awaitingAdmission && PhotonNetwork.InRoom)
            { rejoinFailure = "Host admission timed out. Your session remains available to retry."; pendingRejoin = awaitingAdmission = false; PhotonNetwork.LeaveRoom(false); }
            else Fail(PhotonBootstrap.Instance?.LastConnectionError ?? "Room request timed out. Please retry.");
        }
        if (starting && startingRunId != null && Time.realtimeSinceStartup > operationDeadline)
        {
            startingRunId = null; starting = false;
            if (PhotonNetwork.IsMasterClient) PhotonNetwork.CurrentRoom.IsOpen = true;
            SetStatus("The run could not be registered. Ready up and retry."); RefreshControls();
        }
    }

    private void RoomAction()
    {
        if (!PhotonNetwork.InRoom || starting) return;
        if (!SupportedRoom())
        { SetStatus("Room restaurant, destination or version is inconsistent. Leave and create a supported room."); return; }
        var players = PhotonNetwork.PlayerList;
        var room = PhotonNetwork.CurrentRoom;
        bool allReady = EveryonePresentReady(players, room.MaxPlayers);
        if (!PhotonNetwork.IsMasterClient || !allReady)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.ReadyKey] =
                !Equals(PhotonNetwork.LocalPlayer.CustomProperties[MultiplayerSessionManager.ReadyKey], true) });
            return;
        }
        MultiplayerDestinations.TryGet(RoomRestaurant, out var scene, out var rules);
        var record = new MultiplayerRunRecord { runId = Guid.NewGuid().ToString("N"),
            hostActor = PhotonNetwork.LocalPlayer.ActorNumber, hostAccountId = PhotonNetwork.LocalPlayer.UserId,
            partySize = players.Length, gameVersion = Application.version, restaurant = RoomRestaurant,
            sceneName = scene, rulesVersion = rules,
            startedUtc = DateTime.UtcNow.ToString("o") };
        foreach (var player in players) record.participants.Add(new MultiplayerRunParticipant {
            accountId = player.UserId, actor = player.ActorNumber, initialActor = player.ActorNumber, displayName = player.NickName });
        if (!MultiplayerRunRecords.Valid(record)) { SetStatus("Each player must use a different signed-in account."); return; }
        if (!Application.CanStreamedLevelBeLoaded(Destination))
        {
            SetStatus("Multiplayer scene is missing from build.");
            return;
        }
        starting = true;
        startingRunId = record.runId;
        operationDeadline = Time.realtimeSinceStartup + 8f;
        // The host admits only the frozen roster; an open transport room permits long-disconnect returns.
        room.IsOpen = true;
        room.IsVisible = false;
        room.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.RunKey] = JsonUtility.ToJson(record) });
    }

    private void RefreshControls()
    {
        if (createHeading != null) createHeading.text = PhotonNetwork.InRoom ? "ROOM LOBBY" : "CREATE ROOM";
        if (joinHeading != null) joinHeading.text = PhotonNetwork.InRoom ? "ROOM LOBBY" : "JOIN ROOM";
        var roomTabLabel = joinTabButton.GetComponentInChildren<TMP_Text>();
        if (roomTabLabel != null) roomTabLabel.text = PhotonNetwork.InRoom ? "Room" : "Join";
        var leaveLabel = createTabButton.GetComponentInChildren<TMP_Text>();
        if (leaveLabel != null) leaveLabel.text = PhotonNetwork.InRoom ? "LEAVE ROOM" : createTabLabel;
        createTabButton.interactable = !starting && !busy;
        RefreshReconnectCard();
        bool editable = !busy && !PhotonNetwork.InRoom;
        createButton.interactable = joinButton.interactable = editable || PhotonNetwork.InRoom && !starting;
        restaurantDropdown.interactable = partySizeDropdown.interactable = codeInput.interactable = editable;
        if (PhotonNetwork.InRoom)
        {
            bool ready = Equals(PhotonNetwork.LocalPlayer.CustomProperties[MultiplayerSessionManager.ReadyKey], true);
            bool all = EveryonePresentReady(PhotonNetwork.PlayerList, PhotonNetwork.CurrentRoom.MaxPlayers);
            string label = PhotonNetwork.IsMasterClient && all ? "START RUN" : ready ? "NOT READY" : "READY";
            createButton.GetComponentInChildren<TMP_Text>().text = label;
            joinButton.GetComponentInChildren<TMP_Text>().text = label;
        }
        else
        {
            createButton.GetComponentInChildren<TMP_Text>().text = "Create Room";
            joinButton.GetComponentInChildren<TMP_Text>().text = "Join";
        }
    }

    internal static bool HasPlayablePartySize(int count, int capacity) =>
        capacity >= 2 && capacity <= 4 && count >= 2 && count <= capacity;

    private static bool EveryonePresentReady(Player[] players, int capacity) =>
        HasPlayablePartySize(players.Length, capacity) && Array.TrueForAll(players,
            p => !p.IsInactive && !string.IsNullOrEmpty(p.UserId)
                && Equals(p.CustomProperties[MultiplayerSessionManager.ReadyKey], true));

    private void SetStatus(string message)
    {
        foreach (var text in statusTexts) text.text = message;
    }
}
