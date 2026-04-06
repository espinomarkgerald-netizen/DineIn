using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnSpacing = 1.5f;

    [Header("Room Camera")]
    [Tooltip("The scene camera used during and after gameplay. " +
             "This is NEVER disabled — it is the only rendering camera in this scene.")]
    [SerializeField] private GameObject roomCamera;

    [Header("Timing")]
    [SerializeField] private float waitForInRoomSeconds = 8f;

    private bool spawnedThisScene;
    private bool spawning;

    private void Start()
    {
        // Ensure the room camera is on when the scene loads.
        if (roomCamera != null)
        {
            roomCamera.SetActive(true);
            Debug.Log($"[RoomManager] Room camera '{roomCamera.name}' enabled on Start. " +
                      $"Active={roomCamera.activeInHierarchy} | " +
                      $"Camera enabled={roomCamera.GetComponentInChildren<Camera>()?.enabled}");
        }
        else
        {
            Debug.LogWarning("[RoomManager] roomCamera is not assigned — scene will have no rendering camera!");
        }

        Debug.Log($"[RoomManager] Start | ConnectedReady={PhotonNetwork.IsConnectedAndReady} " +
                  $"InRoom={PhotonNetwork.InRoom} " +
                  $"Room={(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "-")}");

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (spawnedThisScene || spawning) yield break;
        spawning = true;

        // Wait for InRoom to become true (PhotonNetwork.LoadLevel sync timing).
        float t = 0f;
        while (!PhotonNetwork.InRoom && t < waitForInRoomSeconds)
        {
            t += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("[RoomManager] NOT in a room after timeout. Cannot spawn.");
            spawning = false;
            yield break;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[RoomManager] playerPrefab is NOT assigned in Inspector!");
            spawning = false;
            yield break;
        }

        // Prefab must be inside a Resources folder for PhotonNetwork.Instantiate.
        string prefabName = playerPrefab.name;
        if (Resources.Load(prefabName) == null)
        {
            Debug.LogError($"[RoomManager] Prefab '{prefabName}' not found in Resources. " +
                           "Place it under Assets/Resources/.");
            spawning = false;
            yield break;
        }

        // LocalPlayer.TagObject is set after a successful spawn — skip if already done.
        if (PhotonNetwork.LocalPlayer.TagObject != null)
        {
            Debug.Log("[RoomManager] LocalPlayer.TagObject already set. Skipping duplicate spawn.");
            spawnedThisScene = true;
            spawning = false;
            yield break;
        }

        // Push appearance customization before spawning so remote clients see it immediately.
        var pfm = FindFirstObjectByType<PlayfabManager>();
        if (pfm != null)
        {
            pfm.PushCustomizationToPhoton();
            Debug.Log("[RoomManager] Called PushCustomizationToPhoton().");
        }
        else
        {
            Debug.LogWarning("[RoomManager] PlayfabManager not found. Spawning with defaults.");
        }

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot  = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        int actorIndex = GetStableActorIndex(PhotonNetwork.LocalPlayer);
        Vector3 spawnPos = basePos + new Vector3(actorIndex * spawnSpacing, 0f, 0f);

        Debug.Log($"[RoomManager] Spawning '{prefabName}' | " +
                  $"ActorNumber={PhotonNetwork.LocalPlayer.ActorNumber} " +
                  $"Index={actorIndex} at {spawnPos}");

        GameObject localPlayer = PhotonNetwork.Instantiate(prefabName, spawnPos, rot);

        if (localPlayer == null)
        {
            Debug.LogError("[RoomManager] PhotonNetwork.Instantiate returned null.");
            spawning = false;
            yield break;
        }

        PhotonNetwork.LocalPlayer.TagObject = localPlayer;
        spawnedThisScene = true;
        spawning = false;

        // Hand the room camera directly to PlayerSetup so it binds it before
        // any coroutine timing can cause a miss. The camera stays active — it is
        // the scene's only rendering camera and must never be disabled.
        Camera sceneCam = roomCamera != null
            ? roomCamera.GetComponentInChildren<Camera>(true)
            : null;

        if (sceneCam != null)
        {
            PlayerSetup setup = localPlayer.GetComponent<PlayerSetup>();
            if (setup != null)
            {
                setup.InjectCamera(sceneCam);
                Debug.Log($"[RoomManager] Injected scene camera '{sceneCam.name}' into PlayerSetup. " +
                          $"Camera active={sceneCam.isActiveAndEnabled} | " +
                          $"TargetDisplay={sceneCam.targetDisplay} | " +
                          $"AudioListener={sceneCam.GetComponent<AudioListener>() != null}");
            }
            else
            {
                Debug.LogWarning("[RoomManager] PlayerSetup not found on spawned player — " +
                                 "camera must be bound by PlayerSetup's own coroutine.");
            }
        }
        else
        {
            Debug.LogWarning("[RoomManager] roomCamera has no Camera component — " +
                             "PlayerSetup will fall back to scene scan.");
        }

        Debug.Log($"[RoomManager] Local player spawned successfully. " +
                  $"RoomCamera active={roomCamera?.activeInHierarchy}");
    }

    private int GetStableActorIndex(Player p)
    {
        if (PhotonNetwork.CurrentRoom == null) return 0;

        var actorNums = new List<int>();
        foreach (var kv in PhotonNetwork.CurrentRoom.Players)
            actorNums.Add(kv.Value.ActorNumber);

        actorNums.Sort();

        int myActor = p.ActorNumber;
        for (int i = 0; i < actorNums.Count; i++)
        {
            if (actorNums[i] == myActor)
                return i;
        }

        return 0;
    }
}
