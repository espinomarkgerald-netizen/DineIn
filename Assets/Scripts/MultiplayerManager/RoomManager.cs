using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("Room Settings")]
    [SerializeField] private string roomCode = "room1";
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Room Camera (Lobby / Pre-Spawn)")]
    [SerializeField] private GameObject roomCamera;

    public override void OnEnable()
    {
        base.OnEnable();

        // 1. Reset local state so we can spawn fresh in this scene instance
        PhotonNetwork.LocalPlayer.TagObject = null;

        // 2. Ensure room camera is active while we wait to join/spawn
        if (roomCamera != null)
        {
            roomCamera.SetActive(true);
        }

        Debug.Log("Scene Loaded: Initializing Connection...");
        HandleConnectionFlow();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        
        // Disconnect immediately so the 'Actor' is removed from the room
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Scene Unloading: Disconnecting...");
            PhotonNetwork.Disconnect();
        }
    }

    private void HandleConnectionFlow()
    {
        if (PhotonNetwork.NetworkClientState == ClientState.Disconnecting)
        {
            return; // OnDisconnected will catch this and reconnect
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            OnConnectedToMaster();
            return;
        }

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        // Safety check to ensure we are actually ready
        if (PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("Connected to Master. Joining Lobby...");
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby. Joining/Creating Room...");
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 10 };
        PhotonNetwork.JoinOrCreateRoom(roomCode, roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        // 3. Spawning Logic
        if (PhotonNetwork.LocalPlayer.TagObject == null)
        {
            Debug.Log("Joined Room. Spawning Local Player...");

            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            // Instantiate via Photon so others see us
            GameObject localPlayer = PhotonNetwork.Instantiate(
                playerPrefab.name, 
                pos, 
                rot
            );

            if (localPlayer != null)
            {
                // Mark as spawned
                PhotonNetwork.LocalPlayer.TagObject = localPlayer;

                // 4. Disable room camera ONLY now that player exists
                if (roomCamera != null)
                {
                    roomCamera.SetActive(false);
                }
            }
            else
            {
                Debug.LogError("Failed to Instantiate Player Prefab! Check if it's in a 'Resources' folder.");
            }
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected: {cause}");

        // Re-enable camera if we lose connection
        if (roomCamera != null)
        {
            roomCamera.SetActive(true);
        }

        // If we are still in the scene, try to reconnect immediately
        if (this.gameObject.activeInHierarchy)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // Add this to your RoomManager or a script on your Canvas
    public void RequestFollowMode()
    {
        // Find the local camera controller and trigger the mode
        CameraController[] cameras = FindObjectsByType<CameraController>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            // Only trigger for the one we actually own
            if (cam.photonView.IsMine)
            {
                cam.EnterFollowMode();
                Debug.Log("Bridge: Follow Mode Triggered");
            }
        }
    }
}