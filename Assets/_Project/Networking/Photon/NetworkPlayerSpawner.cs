using Photon.Pun;
using UnityEngine;

/// <summary>
/// Spawns the local player prefab via PhotonNetwork.Instantiate when the Multiplayer scene loads.
/// Attach this to any persistent GameObject inside the Multiplayer scene.
/// The Player prefab must live in a Resources folder (e.g. "Assets/Resources/Player.prefab").
/// </summary>
public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField] private string playerPrefabName = "Player";
    [SerializeField] private bool usePartySlots;

    [Header("Spawn Settings")]
    [Tooltip("Transform whose position/rotation will be used as the spawn point. Leave null to use Vector3.zero.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Random radius around the spawn point so multiple players don't overlap.")]
    [SerializeField] private float spawnScatterRadius = 1.5f;

    private bool spawned;

    private void Start()
    {
        // If we're already in a room when this scene loads, spawn immediately.
        // (Master called PhotonNetwork.LoadLevel which loads for everyone simultaneously.)
        if (PhotonNetwork.InRoom && !spawned)
            SpawnLocalPlayer();
    }

    public override void OnJoinedRoom()
    {
        // Safety net: in case the scene was already active before joining.
        if (!spawned)
            SpawnLocalPlayer();
    }

    /// <summary>Instantiates the local player over the network from the Resources folder.</summary>
    private void SpawnLocalPlayer()
    {
        if (spawned || !PhotonNetwork.InRoom) return;
        bool managerScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby1 Multiplayer";
        if (managerScene && (gameObject.scene.name != "Lobby1 Multiplayer" ||
            playerPrefabName != "ManagerMultiplayer")) return;
        if (PhotonNetwork.LocalPlayer.TagObject is GameObject existing && existing != null)
        {
            spawned = true;
            return;
        }
        spawned = true;

        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        if (spawnPoint != null)
        {
            // Scatter slightly so players don't stack on top of each other.
            int slot = 0;
            foreach (var member in PhotonNetwork.PlayerList)
                if (member.ActorNumber < PhotonNetwork.LocalPlayer.ActorNumber) slot++;
            Vector2 scatter = usePartySlots
                ? new Vector2(slot % 2, slot / 2) * spawnScatterRadius
                : Random.insideUnitCircle * spawnScatterRadius;
            position = spawnPoint.position + new Vector3(scatter.x, 0f, scatter.y);
            rotation = spawnPoint.rotation;
        }

        GameObject player = PhotonNetwork.Instantiate(playerPrefabName, position, rotation);

        if (player == null)
        {
            Debug.LogError($"[NetworkPlayerSpawner] PhotonNetwork.Instantiate failed — " +
                           $"make sure '{playerPrefabName}.prefab' is inside a Resources folder.");
        }
        else
        {
            PhotonNetwork.LocalPlayer.TagObject = player;
            Debug.Log($"[NetworkPlayerSpawner] Spawned {player.name} at {position} " +
                      $"(actor #{PhotonNetwork.LocalPlayer.ActorNumber})");
        }
    }
}
