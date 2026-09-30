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

    [Header("Returning players")]
    [SerializeField, Range(30, 300)] private int playerReturnSeconds = MultiplayerSessionManager.RejoinSeconds;
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
    private string startingRunId;
    private string createTabLabel;
    private byte pendingSize;
    private float operationDeadline;
    private const string RestaurantKey = "RestaurantType";
    private const string Destination = "Lobby1 Multiplayer";

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

    private void Create()
    {
        if (PhotonNetwork.InRoom) { RoomAction(); return; }
        if (busy || PhotonNetwork.InRoom) return;
        if (restaurantDropdown.value != 0)
        {
            SetStatus("Only Casual Dining is available.");
            return;
        }
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
        var account = PlayFabAuthManager.Instance;
        if (account == null || !account.IsLoggedIn || string.IsNullOrEmpty(account.PlayFabId))
        { SetStatus("Sign in before starting multiplayer."); return; }
        pendingRejoin = rejoin;
        PhotonNetwork.AutomaticallySyncScene = !rejoin;
        busy = true;
        operationDeadline = Time.realtimeSinceStartup + 30f;
        pendingCreate = create;
        pendingCode = code;
        RefreshControls();
        if (Ready() && PhotonBootstrap.ConnectionMatchesAccount(account.PlayFabId)) { Submit(); return; }
        SetStatus("Connecting...");
        if (PhotonBootstrap.Instance == null)
            new GameObject("PhotonBootstrap").AddComponent<PhotonBootstrap>();
        PhotonNetwork.AutomaticallySyncScene = !rejoin;
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
        pendingCode = null;
        var account = PlayFabAuthManager.Instance;
        if (account == null || !account.IsLoggedIn || !PhotonBootstrap.ConnectionMatchesAccount(account.PlayFabId))
        { Fail("Sign in and reconnect before joining a run."); return; }
        PhotonNetwork.NickName = account != null && account.IsLoggedIn &&
            !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName.Trim() : "Player";
        if (pendingRejoin)
        {
            if (!MultiplayerLastRun.IsCompatible(rejoinPointer, account.PlayFabId, DateTime.UtcNow, out var reason))
            { MultiplayerLastRun.Forget(account.PlayFabId); Fail(reason); return; }
            SetStatus("Checking your reserved place and reconnecting...");
            if (!PhotonNetwork.RejoinRoom(code)) Fail("Could not start reconnecting. Please retry.");
            return;
        }
        SetStatus(pendingCreate ? "Creating..." : "Joining...");
        PhotonCustomizationSync.PushToPhoton();
        bool sent = pendingCreate
            ? PhotonNetwork.CreateRoom(code, new RoomOptions
            {
                MaxPlayers = pendingSize, IsOpen = true, IsVisible = false, PublishUserId = true,
                PlayerTtl = Mathf.Clamp(playerReturnSeconds, 30, 300) * 1000, EmptyRoomTtl = 0,
                CustomRoomProperties = new Hashtable { { RestaurantKey, "CasualDining" },
                    { MultiplayerSessionManager.ProtocolKey, MultiplayerSessionManager.Protocol } }
            }, TypedLobby.Default)
            : PhotonNetwork.JoinRoom(code);
        if (!sent) Fail("Unable to contact room. Try again.");
    }

    public override void OnJoinedRoom()
    {
        if (pendingRejoin) { CompleteLastRunJoin(); return; }
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
        if (starting && PhotonNetwork.IsMasterClient && changed[MultiplayerSessionManager.RunKey] is string json
            && !string.IsNullOrEmpty(startingRunId) && json.Contains(startingRunId))
        {
            startingRunId = null;
            PhotonNetwork.LoadLevel(Destination);
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
        if (cause == DisconnectCause.DisconnectByClientLogic && busy) return;
        if (busy) Fail(PhotonBootstrap.Instance?.LastConnectionError ?? "Photon disconnected (" + cause + "). Please retry.");
    }
    private void OnConnectionFailed(string message) { if (busy) Fail(message); }
    public override void OnCreateRoomFailed(short code, string message) =>
        Fail(code == ErrorCode.GameIdAlreadyExists ? "Code already used. Press Create again."
            : "Could not create room (" + code + "). Reconnect and retry.");
    public override void OnJoinRoomFailed(short code, string message)
    {
        if (!pendingRejoin) { Fail(JoinFailureMessage(code)); return; }
        bool expired = code == ErrorCode.GameDoesNotExist || code == ErrorCode.JoinFailedWithRejoinerNotFound
            || code == ErrorCode.GameClosed;
        if (expired) MultiplayerLastRun.Forget(rejoinPointer?.accountId, rejoinPointer?.runId);
        Fail(expired ? "That run or your reserved place no longer exists. Join or create a new room."
            : code == ErrorCode.JoinFailedFoundActiveJoiner || code == ErrorCode.JoinFailedPeerAlreadyJoined
                ? "Your previous connection is still active. Wait a moment, then reconnect."
                : "Unable to reconnect right now. Check your connection and try again.");
    }

    internal static string JoinFailureMessage(short code) =>
        code == ErrorCode.GameDoesNotExist ? "Room not found. Use the same updated game version and ask the host for a new room code." :
            code == ErrorCode.GameFull ? "Room is full." :
            code == ErrorCode.GameClosed ? "Room has already started. Ask the host to create a new room." :
            code == ErrorCode.JoinFailedFoundActiveJoiner || code == ErrorCode.JoinFailedPeerAlreadyJoined
                ? "This account is already in the room. Each player must sign in with a different account." :
            code == ErrorCode.JoinFailedFoundInactiveJoiner
                ? "Your previous connection still reserves a place. Reconnect from that game, or wait 90 seconds and retry." :
            "Unable to join (" + code + "). Reconnect, then ask the host to create a fresh room.";

    private void Fail(string message)
    {
        if (pendingRejoin && !PhotonNetwork.InRoom && !Ready()) PhotonNetwork.Disconnect();
        pendingRejoin = false;
        PhotonNetwork.AutomaticallySyncScene = true;
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
            codeText.text = "DINE IN CODE: ----";
            if (joinCodeText != null) joinCodeText.text = codeText.text;
            foreach (var text in rosterTexts) text.text = "";
            SetStatus("Create or join a room.");
            return;
        }
        var room = PhotonNetwork.CurrentRoom;
        codeText.text = "DINE IN CODE: " + room.Name;
        if (joinCodeText != null) joinCodeText.text = codeText.text;
        var roster = new StringBuilder($"PLAYERS {room.PlayerCount}/{room.MaxPlayers}");
        var players = PhotonNetwork.PlayerList;
        Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        foreach (var player in players)
        {
            string nickname = string.IsNullOrWhiteSpace(player.NickName) ? "Player" : player.NickName.Replace('\n',' ').Replace('\r',' ');
            if (nickname.Length > 22) nickname = nickname.Substring(0,22);
            roster.Append('\n').Append(nickname).Append(player.IsMasterClient ? " (HOST)" : "")
                .Append(player.IsLocal ? " (YOU)" : "")
                .Append(player.IsInactive ? " • Reconnecting" : Equals(player.CustomProperties[MultiplayerSessionManager.ReadyKey], true) ? " • Ready" : " • Preparing");
        }
        for (int i = players.Length; i < room.MaxPlayers; i++) roster.Append("\nOpen slot");
        foreach (var text in rosterTexts) text.text = roster.ToString();
        if (!Equals(room.CustomProperties[RestaurantKey], "CasualDining") ||
            !Equals(room.CustomProperties[MultiplayerSessionManager.ProtocolKey], MultiplayerSessionManager.Protocol))
        {
            SetStatus("Room version or restaurant differs. All players must use the same updated game version.");
            return;
        }
        bool enoughPlayers = HasPlayablePartySize(room.PlayerCount, room.MaxPlayers);
        SetStatus(starting ? "STARTING..." : enoughPlayers ? "Ready up. The host starts when everyone here is ready." : "WAITING FOR AT LEAST 2 PLAYERS...");
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
            Fail(PhotonBootstrap.Instance?.LastConnectionError ?? "Room request timed out. Please retry.");
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
        var players = PhotonNetwork.PlayerList;
        var room = PhotonNetwork.CurrentRoom;
        bool allReady = EveryonePresentReady(players, room.MaxPlayers);
        if (!PhotonNetwork.IsMasterClient || !allReady)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.ReadyKey] =
                !Equals(PhotonNetwork.LocalPlayer.CustomProperties[MultiplayerSessionManager.ReadyKey], true) });
            return;
        }
        if (!Equals(room.CustomProperties[MultiplayerSessionManager.ProtocolKey], MultiplayerSessionManager.Protocol)) return;
        var record = new MultiplayerRunRecord { runId = Guid.NewGuid().ToString("N"),
            hostActor = PhotonNetwork.LocalPlayer.ActorNumber, hostAccountId = PhotonNetwork.LocalPlayer.UserId,
            partySize = players.Length, gameVersion = Application.version, startedUtc = DateTime.UtcNow.ToString("o") };
        foreach (var player in players) record.participants.Add(new MultiplayerRunParticipant {
            accountId = player.UserId, actor = player.ActorNumber, displayName = player.NickName });
        if (!MultiplayerRunRecords.Valid(record)) { SetStatus("Each player must use a different signed-in account."); return; }
        if (!Application.CanStreamedLevelBeLoaded(Destination))
        {
            SetStatus("Multiplayer scene is missing from build.");
            return;
        }
        starting = true;
        startingRunId = record.runId;
        operationDeadline = Time.realtimeSinceStartup + 8f;
        room.IsOpen = false;
        room.IsVisible = false;
        room.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.RunKey] = JsonUtility.ToJson(record) });
    }

    private void RefreshControls()
    {
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
            createButton.GetComponentInChildren<TMP_Text>().text = "CREATE";
            joinButton.GetComponentInChildren<TMP_Text>().text = "JOIN";
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
