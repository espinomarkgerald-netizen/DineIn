using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject playerPrefab;     // MUST be inside a Resources folder
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnSpacing = 1.5f;

    [Header("Room Camera (Pre-Spawn)")]
    [SerializeField] private GameObject roomCamera;

    [Header("Timing")]
    [SerializeField] private float waitForInRoomSeconds = 8f;

    private bool spawnedThisScene;
    private bool spawning;

    private void Start()
    {
        if (roomCamera != null) roomCamera.SetActive(true);

        Debug.Log($"[RoomManager] Start | ConnectedReady={PhotonNetwork.IsConnectedAndReady} " +
                  $"InRoom={PhotonNetwork.InRoom} Room={(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "-")}");

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (spawnedThisScene || spawning) yield break;
        spawning = true;

        // Wait for InRoom to become true (PhotonNetwork.LoadLevel sync timing)
        float t = 0f;
        while (!PhotonNetwork.InRoom && t < waitForInRoomSeconds)
        {
            t += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("[RoomManager] NOT in a room. Cannot spawn.");
            spawning = false;
            yield break;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[RoomManager] playerPrefab is NOT assigned in Inspector!");
            spawning = false;
            yield break;
        }

        // Prefab must be in Resources for PhotonNetwork.Instantiate(string,...)
        string prefabName = playerPrefab.name;
        if (Resources.Load(prefabName) == null)
        {
            Debug.LogError($"[RoomManager] Prefab '{prefabName}' not found in Resources. Put it in Assets/Resources/.");
            spawning = false;
            yield break;
        }

        // If TagObject already set, we already spawned (best check)
        if (PhotonNetwork.LocalPlayer.TagObject != null)
        {
            Debug.Log("[RoomManager] LocalPlayer.TagObject already set. Skipping spawn.");
            if (roomCamera != null) roomCamera.SetActive(false);
            spawnedThisScene = true;
            spawning = false;
            yield break;
        }

        // Optional: push customization before spawn
        var pfm = FindFirstObjectByType<PlayfabManager>();
        if (pfm != null)
        {
            pfm.PushCustomizationToPhoton();
            Debug.Log("[RoomManager] ✅ Called PushCustomizationToPhoton()");
        }
        else
        {
            Debug.LogWarning("[RoomManager] ⚠️ PlayfabManager not found yet. Spawning with defaults.");
        }

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        int actorIndex = GetStableActorIndex(PhotonNetwork.LocalPlayer);
        Vector3 offset = new Vector3(actorIndex * spawnSpacing, 0f, 0f);
        Vector3 spawnPos = basePos + offset;

        Debug.Log($"[RoomManager] Spawning '{prefabName}' ActorNumber={PhotonNetwork.LocalPlayer.ActorNumber} Index={actorIndex} at {spawnPos}");

        GameObject localPlayer = PhotonNetwork.Instantiate(prefabName, spawnPos, rot);

        if (localPlayer != null)
        {
            PhotonNetwork.LocalPlayer.TagObject = localPlayer;

            if (roomCamera != null) roomCamera.SetActive(false);

            spawnedThisScene = true;
            Debug.Log("[RoomManager] ✅ Local player spawned.");
        }
        else
        {
            Debug.LogError("[RoomManager] ❌ PhotonNetwork.Instantiate returned null.");
        }

        spawning = false;
    }

    private int GetStableActorIndex(Player p)
    {
        if (PhotonNetwork.CurrentRoom == null) return 0;

        // Sort actor numbers so every client computes the SAME order
        List<int> actorNums = new List<int>();
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
