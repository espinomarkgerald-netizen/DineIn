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

    [Tooltip("Optional authored positions in frozen participant order. Fast Food uses four safe entry positions.")]
    [SerializeField] private Transform[] partySpawnPoints;

    [Tooltip("Random radius around the spawn point so multiple players don't overlap.")]
    [SerializeField] private float spawnScatterRadius = 1.5f;

    private bool spawned;
    private Coroutine admissionRoutine;

    private void Start()
    {
        // If we're already in a room when this scene loads, spawn immediately.
        // (Master called PhotonNetwork.LoadLevel which loads for everyone simultaneously.)
        if (PhotonNetwork.InRoom && !spawned)
            QueueSpawn();
    }

    public override void OnJoinedRoom()
    {
        // Safety net: in case the scene was already active before joining.
        QueueSpawn();
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        spawned = false;
        if (admissionRoutine != null) StopCoroutine(admissionRoutine);
        admissionRoutine = null;
    }
    private void QueueSpawn()
    {
        if (spawned || admissionRoutine != null) return;
        admissionRoutine = StartCoroutine(WaitForAdmission());
    }
    private System.Collections.IEnumerator WaitForAdmission()
    {
        // JoinRoom completes before the host has mapped a returning account to its new actor.
        yield return null;
        var session = MultiplayerSessionManager.Instance;
        while (PhotonNetwork.InRoom && session != null && !session.Ended
            && !session.ValidActor(PhotonNetwork.LocalPlayer.ActorNumber)) yield return null;
        admissionRoutine = null;
        if (PhotonNetwork.InRoom && (session == null || !session.Ended)) SpawnLocalPlayer();
    }

    /// <summary>Instantiates the local player over the network from the Resources folder.</summary>
    private void SpawnLocalPlayer()
    {
        if (!PhotonNetwork.InRoom) return;
        var session = MultiplayerSessionManager.Instance;
        if (session != null && gameObject.scene != session.gameObject.scene) return;
        if (PhotonNetwork.LocalPlayer.HasRejoined)
        {
            // PUN replays the inactive actor's cached instantiation. Creating a
            // replacement here would leave two avatars when that replay arrives.
            spawned = true;
            PhotonCustomizationSync.PushToPhoton();
            MultiplayerManagerRegistration.TryRebindActor(MultiplayerSessionManager.Instance,
                PhotonNetwork.LocalPlayer.ActorNumber, out _);
            return;
        }
        if (spawned) return;
        if (PhotonNetwork.LocalPlayer.TagObject is GameObject existing && existing != null
            && existing.scene == gameObject.scene && existing.TryGetComponent<PhotonView>(out var existingView)
            && existingView.IsMine && existingView.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            spawned = true;
            return;
        }
        spawned = true;

        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        int slot = session?.Run?.participants.FindIndex(p => p.accountId == PhotonNetwork.LocalPlayer.UserId) ?? -1;
        if (slot < 0)
        {
            slot = 0;
            foreach (var member in PhotonNetwork.PlayerList)
                if (member.ActorNumber < PhotonNetwork.LocalPlayer.ActorNumber) slot++;
        }
        if (partySpawnPoints != null && slot < partySpawnPoints.Length && partySpawnPoints[slot] != null)
        {
            position = partySpawnPoints[slot].position;
            rotation = partySpawnPoints[slot].rotation;
        }
        else if (spawnPoint != null)
        {
            Vector2 scatter = usePartySlots
                ? new Vector2(slot % 2, slot / 2) * spawnScatterRadius
                : Random.insideUnitCircle * spawnScatterRadius;
            position = spawnPoint.position + new Vector3(scatter.x, 0f, scatter.y);
            rotation = spawnPoint.rotation;
        }

        PhotonCustomizationSync.PushToPhoton();
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
