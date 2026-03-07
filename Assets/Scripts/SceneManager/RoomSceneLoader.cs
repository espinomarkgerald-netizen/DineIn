using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomSceneLoader : MonoBehaviourPunCallbacks
{
    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "CoreGameplay";

    [Header("Gate شروط")]
    [Tooltip("How many players must be inside the room before loading gameplay.")]
    [SerializeField] private int minPlayersToStart = 2;

    [Tooltip("If true, only the MasterClient can trigger the scene load.")]
    [SerializeField] private bool masterOnlyLoads = true;

    private bool loading;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        Debug.Log("[RoomSceneLoader] Awake -> AutoSyncScene=TRUE");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[RoomSceneLoader] OnJoinedRoom Room={PhotonNetwork.CurrentRoom?.Name} " +
                  $"Players={PhotonNetwork.CurrentRoom?.PlayerCount}/{PhotonNetwork.CurrentRoom?.MaxPlayers} " +
                  $"Master={PhotonNetwork.IsMasterClient}");

        TryStartIfReady();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[RoomSceneLoader] OnPlayerEnteredRoom: {newPlayer.NickName} " +
                  $"Players={PhotonNetwork.CurrentRoom?.PlayerCount}");

        TryStartIfReady();
    }

    private void TryStartIfReady()
    {
        if (loading) return;
        if (!PhotonNetwork.InRoom) return;

        // Only master triggers
        if (masterOnlyLoads && !PhotonNetwork.IsMasterClient) return;

        int count = PhotonNetwork.CurrentRoom.PlayerCount;

        if (count >= minPlayersToStart)
        {
            loading = true;
            Debug.Log($"[RoomSceneLoader] ✅ Ready (players={count}). Loading {gameplaySceneName}...");
            PhotonNetwork.LoadLevel(gameplaySceneName);
        }
        else
        {
            Debug.Log($"[RoomSceneLoader] Waiting for players... {count}/{minPlayersToStart}");
        }
    }
}
