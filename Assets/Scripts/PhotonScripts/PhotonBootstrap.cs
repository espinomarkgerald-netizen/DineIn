using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonBootstrap : MonoBehaviourPunCallbacks
{
    public static PhotonBootstrap Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool autoConnectOnStart = true;
    [SerializeField] private bool autoJoinLobby = true;

    private bool connectRequested;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;

        Debug.Log($"[PhotonBootstrap] Awake | Connected={PhotonNetwork.IsConnected} State={PhotonNetwork.NetworkClientState}");
    }

    private void Start()
    {
        if (autoConnectOnStart)
            SafeConnect();
    }

    public void SafeConnect()
    {
        // already connected and ready
        if (PhotonNetwork.IsConnectedAndReady)
        {
            connectRequested = false;
            return;
        }

        // if Photon is already connected but not ready yet, don't spam connect
        if (PhotonNetwork.IsConnected)
            return;

        // prevent spam-clicking connect
        if (connectRequested)
            return;

        // Only connect from safe base states
        var state = PhotonNetwork.NetworkClientState;
        if (state != ClientState.PeerCreated &&
            state != ClientState.Disconnected)
        {
            // we're in some in-between state already (connecting/auth/etc)
            return;
        }

        connectRequested = true;
        Debug.Log("[PhotonBootstrap] ConnectUsingSettings()");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        connectRequested = false;

        Debug.Log($"[PhotonBootstrap] ConnectedToMaster | InRoom={PhotonNetwork.InRoom} InLobby={PhotonNetwork.InLobby}");

        if (autoJoinLobby && !PhotonNetwork.InRoom && !PhotonNetwork.InLobby)
        {
            Debug.Log("[PhotonBootstrap] JoinLobby()");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[PhotonBootstrap] JoinedLobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        connectRequested = false;
        Debug.LogWarning("[PhotonBootstrap] Disconnected: " + cause);
        // no auto-reconnect here
    }

    // Only call from your "Exit Multiplayer" or "Logout"
    public void LeaveRoomAndReturnToLobby()
    {
        if (!PhotonNetwork.IsConnectedAndReady) return;

        if (PhotonNetwork.InRoom)
        {
            Debug.Log("[PhotonBootstrap] Leaving room...");
            PhotonNetwork.LeaveRoom();
        }
        else if (!PhotonNetwork.InLobby)
        {
            Debug.Log("[PhotonBootstrap] Joining lobby...");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[PhotonBootstrap] LeftRoom -> Joining lobby");
        if (autoJoinLobby && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby(TypedLobby.Default);
    }
}
