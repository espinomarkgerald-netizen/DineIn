using System;
using System.Collections;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MultiplayerMenuController : MonoBehaviourPunCallbacks
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

    [Header("Tabs")]
    [SerializeField] private Button joinTabButton;
    [SerializeField] private Button createTabButton;
    [SerializeField] private GameObject joinContents;
    [SerializeField] private GameObject createContents;

    private void ShowJoinTab() => ShowTab(false);
    private void ShowCreateTab() => ShowTab(true);

    private void ShowTab(bool create)
    {
        joinContents.SetActive(!create);
        createContents.SetActive(create);
        SetTabColor(joinTabButton, !create);
        SetTabColor(createTabButton, create);
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
    private byte pendingSize;
    private const string RestaurantKey = "RestaurantType";
    private const string Destination = "Lobby1 Multiplayer";

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
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
    }

    // Keep room callbacks alive when the existing panel animation hides this UI.
    public override void OnDisable() { }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        createButton.onClick.RemoveListener(Create);
        joinButton.onClick.RemoveListener(Join);
        joinTabButton.onClick.RemoveListener(ShowJoinTab);
        createTabButton.onClick.RemoveListener(ShowCreateTab);
    }

    private void Create()
    {
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

    private void Begin(bool create, string code)
    {
        busy = true;
        pendingCreate = create;
        pendingCode = code;
        RefreshControls();
        if (Ready()) { Submit(); return; }
        SetStatus("Connecting...");
        if (PhotonBootstrap.Instance == null)
            new GameObject("PhotonBootstrap").AddComponent<PhotonBootstrap>();
        PhotonBootstrap.Instance.SafeConnect();
    }

    private static bool Ready() =>
        PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer ||
        PhotonNetwork.NetworkClientState == ClientState.JoinedLobby;

    public override void OnConnectedToMaster()
    {
        // Let Bootstrap finish its optional JoinLobby callback before submitting.
        StartCoroutine(SubmitAfterCallbacks());
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
        PhotonNetwork.NickName = account != null && account.IsLoggedIn &&
            !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName.Trim() : "Player";
        SetStatus(pendingCreate ? "Creating..." : "Joining...");
        bool sent = pendingCreate
            ? PhotonNetwork.CreateRoom(code, new RoomOptions
            {
                MaxPlayers = pendingSize, IsOpen = true, IsVisible = false,
                CustomRoomProperties = new Hashtable { { RestaurantKey, "CasualDining" } }
            }, TypedLobby.Default)
            : PhotonNetwork.JoinRoom(code);
        if (!sent) Fail("Unable to contact room. Try again.");
    }

    public override void OnJoinedRoom() { busy = false; RefreshRoom(); }
    public override void OnPlayerEnteredRoom(Player player) => RefreshRoom();
    public override void OnPlayerLeftRoom(Player player) => RefreshRoom();
    public override void OnMasterClientSwitched(Player player) => RefreshRoom();
    public override void OnPlayerPropertiesUpdate(Player player, Hashtable changed) => RefreshRoom();
    public override void OnRoomPropertiesUpdate(Hashtable changed) => RefreshRoom();
    public override void OnLeftRoom() { starting = false; busy = false; RefreshRoom(); }
    public override void OnDisconnected(DisconnectCause cause) => Fail("Disconnected. Try again.");
    public override void OnCreateRoomFailed(short code, string message) =>
        Fail(code == ErrorCode.GameIdAlreadyExists ? "Code already used. Press Create again." : "Unable to create room.");
    public override void OnJoinRoomFailed(short code, string message) =>
        Fail(code == ErrorCode.GameDoesNotExist ? "Room not found." :
            code == ErrorCode.GameFull ? "Room is full." :
            code == ErrorCode.GameClosed ? "Room has already started." : "Unable to join room.");

    private void Fail(string message)
    {
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
            roster.Append('\n').Append(string.IsNullOrWhiteSpace(player.NickName) ? "Player" : player.NickName);
        for (int i = players.Length; i < room.MaxPlayers; i++) roster.Append("\nWaiting...");
        foreach (var text in rosterTexts) text.text = roster.ToString();
        if (!Equals(room.CustomProperties[RestaurantKey], "CasualDining"))
        {
            SetStatus("Unsupported restaurant.");
            return;
        }
        bool full = room.MaxPlayers >= 2 && room.MaxPlayers <= 4 && room.PlayerCount == room.MaxPlayers;
        SetStatus(full || starting ? "STARTING..." : "WAITING FOR PLAYERS...");
        if (!full || starting || !PhotonNetwork.IsMasterClient) return;
        if (!Application.CanStreamedLevelBeLoaded(Destination))
        {
            SetStatus("Multiplayer scene is missing from build.");
            return;
        }
        starting = true;
        room.IsOpen = false;
        room.IsVisible = false;
        PhotonNetwork.LoadLevel(Destination);
    }

    private void RefreshControls()
    {
        bool editable = !busy && !PhotonNetwork.InRoom;
        createButton.interactable = joinButton.interactable = editable;
        restaurantDropdown.interactable = partySizeDropdown.interactable = codeInput.interactable = editable;
    }

    private void SetStatus(string message)
    {
        foreach (var text in statusTexts) text.text = message;
    }
}
