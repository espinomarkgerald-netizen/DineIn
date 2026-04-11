using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles room creation flow including:
/// - Leaving a current room before creating a new one
/// - Waiting for Photon to return to MasterServer / Lobby after leaving
/// - Storing a pending create request and auto-firing it once the client is ready
/// - Guarding the Create button while transitioning
/// </summary>
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
    [SerializeField] private string gameplaySceneName = "Multiplayer";

    [Header("Debug (Inspector Visible)")]
    [SerializeField] private string currentRoomCode;

    // Snapshot of room options captured when the player clicked Create.
    // Preserved across the leave → reconnect → create flow.
    private struct PendingRoomRequest
    {
        public bool IsSet;
        public byte MaxPlayers;
        public int Level;
        public string RoomCode;
    }

    private PendingRoomRequest _pending;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (createButton) createButton.onClick.AddListener(OnCreateClicked);
    }

    private void OnEnable()
    {
        ClearPending();
        RefreshButtonState();
        SetStatus("Ready.");
    }

    // -------------------------------------------------------------------------
    // Button handler
    // -------------------------------------------------------------------------

    private void OnCreateClicked()
    {
        // Capture the current room parameters immediately so they survive any
        // async wait (leave → reconnect → lobby → create).
        _pending = BuildRequest();
        _pending.IsSet = true;

        SetStatus($"[CreateRoomUI] Create requested | State={PhotonNetwork.NetworkClientState}");

        // 1. Still in a room → leave first; OnLeftRoom will continue the flow.
        if (PhotonNetwork.InRoom)
        {
            SetStatus("Leaving current room...");
            SetButtonInteractable(false);
            PhotonNetwork.LeaveRoom();
            return;
        }

        // 2. Not yet connected → ask bootstrap to connect; OnConnectedToMaster /
        //    OnJoinedLobby will continue the flow.
        if (!PhotonNetwork.IsConnected)
        {
            SetStatus("Connecting to Photon...");
            SetButtonInteractable(false);
            var pb = PhotonBootstrap.Instance;
            if (pb != null) pb.SafeConnect();
            return;
        }

        // 3. Connected but still transitioning (e.g. ConnectingToMasterServer after
        //    leaving a room) → just store the pending request and wait.
        if (!IsReadyToCreate())
        {
            SetStatus($"Waiting for Photon... ({PhotonNetwork.NetworkClientState})");
            SetButtonInteractable(false);
            return;
        }

        // 4. Already on Master / in Lobby → create immediately.
        ExecutePendingCreate();
    }

    // -------------------------------------------------------------------------
    // Photon callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired when Photon finishes leaving the previous room and returns to
    /// MasterServer. PhotonBootstrap will call JoinLobby from here, so we wait
    /// for OnJoinedLobby before creating.
    /// </summary>
    public override void OnLeftRoom()
    {
        Debug.Log("[CreateRoomUI] OnLeftRoom | pending=" + _pending.IsSet);

        // PhotonBootstrap.OnLeftRoom already calls JoinLobby → we wait for
        // OnJoinedLobby. Nothing to do here except log.
        if (_pending.IsSet)
            SetStatus("Left room. Waiting for lobby...");
    }

    /// <summary>
    /// Fired when the client lands back on MasterServer (before lobby join).
    /// Only create here when NOT auto-joining a lobby (PhotonBootstrap.autoJoinLobby=false).
    /// If bootstrap is joining the lobby, OnJoinedLobby is the safer trigger.
    /// </summary>
    public override void OnConnectedToMaster()
    {
        Debug.Log("[CreateRoomUI] OnConnectedToMaster | pending=" + _pending.IsSet
                  + " | InLobby=" + PhotonNetwork.InLobby);

        if (!_pending.IsSet) return;

        // If bootstrap is about to join the lobby, wait for OnJoinedLobby.
        var pb = PhotonBootstrap.Instance;
        bool bootstrapWillJoinLobby = pb != null;

        if (!bootstrapWillJoinLobby)
        {
            // No bootstrap managing lobby join → safe to create from here.
            ExecutePendingCreate();
        }
        // else: wait for OnJoinedLobby which fires right after.
    }

    /// <summary>
    /// Fired once the client is in the lobby — the safest state to call CreateRoom.
    /// </summary>
    public override void OnJoinedLobby()
    {
        Debug.Log("[CreateRoomUI] OnJoinedLobby | pending=" + _pending.IsSet);

        if (!_pending.IsSet) return;

        ExecutePendingCreate();
    }

    public override void OnCreatedRoom()
    {
        SetStatus($"Room created: {currentRoomCode}");
    }

    public override void OnJoinedRoom()
    {
        SetStatus($"Joined: {PhotonNetwork.CurrentRoom.Name} — loading {gameplaySceneName}");
        PhotonNetwork.LoadLevel(gameplaySceneName);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"Create failed ({returnCode}): {message}");
        ClearPending();
        RefreshButtonState();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus("Disconnected: " + cause);
        ClearPending();
        RefreshButtonState();
    }

    // -------------------------------------------------------------------------
    // Core create logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes the stored pending create request. Guards against invalid states.
    /// </summary>
    private void ExecutePendingCreate()
    {
        if (!_pending.IsSet)
        {
            Debug.LogWarning("[CreateRoomUI] ExecutePendingCreate called with no pending request.");
            return;
        }

        if (!IsReadyToCreate())
        {
            // Still not ready — keep waiting; one of the callbacks will retry.
            Debug.LogWarning($"[CreateRoomUI] ExecutePendingCreate: not ready yet | State={PhotonNetwork.NetworkClientState}");
            SetStatus($"Waiting for Photon... ({PhotonNetwork.NetworkClientState})");
            return;
        }

        currentRoomCode = _pending.RoomCode;

        var opts = new RoomOptions
        {
            MaxPlayers = _pending.MaxPlayers,
            IsOpen     = true,
            IsVisible  = false,
            CustomRoomProperties         = new Hashtable { { "lvl", _pending.Level } },
            CustomRoomPropertiesForLobby = new[] { "lvl" }
        };

        SetStatus($"Creating room: {currentRoomCode} (size={_pending.MaxPlayers} lvl={_pending.Level})");
        Debug.Log($"[CreateRoomUI] PhotonNetwork.CreateRoom({currentRoomCode}) | State={PhotonNetwork.NetworkClientState}");

        // Clear BEFORE the call so double-callbacks don't re-trigger.
        ClearPending();
        SetButtonInteractable(false);

        PhotonNetwork.CreateRoom(currentRoomCode, opts, TypedLobby.Default);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true only when Photon is in a state where CreateRoom is allowed.
    /// ConnectedToMasterServer and JoinedLobby are the two safe states.
    /// </summary>
    private static bool IsReadyToCreate()
    {
        var state = PhotonNetwork.NetworkClientState;
        return state == ClientState.ConnectedToMasterServer
            || state == ClientState.JoinedLobby;
    }

    private PendingRoomRequest BuildRequest()
    {
        byte maxPlayers = (byte)(roomSizeDropdown != null
            ? Mathf.Clamp(roomSizeDropdown.value + 1, 1, 6)
            : 6);

        int level = roomLevelDropdown != null
            ? Mathf.Clamp(roomLevelDropdown.value + 1, 1, 3)
            : 1;

        return new PendingRoomRequest
        {
            IsSet      = false, // caller sets this to true
            MaxPlayers = maxPlayers,
            Level      = level,
            RoomCode   = GenerateCode()
        };
    }

    private void ClearPending()
    {
        _pending = default;
    }

    private void RefreshButtonState()
    {
        // Re-enable the button only when not in the middle of a flow.
        SetButtonInteractable(!_pending.IsSet);
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (createButton) createButton.interactable = interactable;
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
