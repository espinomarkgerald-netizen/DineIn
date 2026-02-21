using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomUI : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Dropdown roomSizeDropdown;
    [SerializeField] private TMP_Dropdown roomLevelDropdown;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Code Settings")]
    [SerializeField] private string codePrefix = "DINE-";
    [SerializeField] private bool forceUppercase = true;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "CoreGameplay";

    [Header("Debug (Inspector Visible)")]
    [SerializeField] private string currentRoomCode;

    private bool pendingCreate;

    private void Awake()
    {
        if (createButton) createButton.onClick.AddListener(OnCreateClicked);
    }

    private void OnEnable()
    {
        pendingCreate = false;
        SetStatus("Ready.");
    }

    private void OnCreateClicked()
    {
        // Ask bootstrap to connect (ONLY bootstrap should call ConnectUsingSettings)
        var pb = FindFirstObjectByType<PhotonBootstrap>();
        if (pb != null) pb.SafeConnect();

        // If not ready yet, wait for ConnectedToMaster
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            pendingCreate = true;
            SetStatus("Connecting to Photon...");
            return;
        }

        CreateNow();
    }

    public override void OnConnectedToMaster()
    {
        // IMPORTANT: we must be ready before creating
        if (pendingCreate && PhotonNetwork.IsConnectedAndReady)
        {
            CreateNow();
        }
    }

    private void CreateNow()
    {
        pendingCreate = false;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Still not connected. Try again.");
            pendingCreate = true;
            return;
        }

        int maxPlayers = roomSizeDropdown != null ? Mathf.Clamp(roomSizeDropdown.value + 1, 1, 6) : 6;
        int level      = roomLevelDropdown != null ? Mathf.Clamp(roomLevelDropdown.value + 1, 1, 3) : 1;

        currentRoomCode = GenerateCode();

        var opts = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayers,
            IsOpen = true,
            IsVisible = false,
            CustomRoomProperties = new Hashtable { { "lvl", level } },
            CustomRoomPropertiesForLobby = new[] { "lvl" }
        };

        SetStatus($"Creating room: {currentRoomCode} ...");
        PhotonNetwork.CreateRoom(currentRoomCode, opts, TypedLobby.Default);
    }

    public override void OnCreatedRoom()
    {
        SetStatus($"Room created: {currentRoomCode}");
    }

    public override void OnJoinedRoom()
    {
        SetStatus($"Joined: {PhotonNetwork.CurrentRoom.Name} -> Loading {gameplaySceneName}");
        PhotonNetwork.LoadLevel(gameplaySceneName);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"Create failed: {message} ({returnCode})");
        pendingCreate = false;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus("Disconnected: " + cause);
        pendingCreate = false;
    }

    private string GenerateCode()
    {
        int n = Random.Range(1000, 9999);
        string code = $"{codePrefix}{n}";
        return forceUppercase ? code.ToUpperInvariant() : code;
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log("[CreateRoomUI] " + msg);
    }
}
