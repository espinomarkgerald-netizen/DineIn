using UnityEngine;

public class GroupSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public CustomerGroup groupPrefab;
    public CustomerAgent customerPrefab;

    [Header("Points")]
    public Transform spawnPoint;

    [Header("Lobby Line (4 slots)")]
    public LobbyLineManager lobbyLine;

    [Header("Spawn Settings")]
    public float spawnInterval = 8f;
    public int minGroupSize = 1;
    public int maxGroupSize = 4;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnGroup();
        }
    }

    public void SpawnGroup()
    {
        if (groupPrefab == null || customerPrefab == null || spawnPoint == null || lobbyLine == null)
        {
            Debug.LogWarning("Spawner missing references.");
            return;
        }

        int size = Random.Range(minGroupSize, maxGroupSize + 1);

        var group = Instantiate(groupPrefab, spawnPoint.position, Quaternion.identity);
        group.name = $"Group_{size}";

        for (int i = 0; i < size; i++)
        {
            Vector3 offset = new Vector3((i % 2) * 0.6f, 0f, (i / 2) * 0.6f);
            var cust = Instantiate(customerPrefab, spawnPoint.position + offset, Quaternion.identity, group.transform);
            cust.name = $"Customer_{i + 1}";
            group.members.Add(cust);
        }

        // ✅ Put them into the 4-slot line
        lobbyLine.TryJoinLine(group);

        // Optional: set their state (if you still use it)
        group.state = CustomerGroup.GroupState.WalkingToLobby;
    }
}
