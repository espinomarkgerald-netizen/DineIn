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

    [Header("Takeout")]
    [SerializeField] private TakeoutQueueManager takeoutQueueManager;
    [SerializeField] private bool forceTakeoutForTesting = true;
    [SerializeField] private int forcedTakeoutSize = 1;

    [Header("Spawn Settings")]
    public bool autoSpawn = false;
    public float spawnInterval = 8f;
    public int minGroupSize = 1;
    public int maxGroupSize = 4;

    private float timer;

    private void Update()
    {
        if (!autoSpawn)
            return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnGroup();
        }
    }

    public CustomerGroup SpawnGroup()
    {
        if (groupPrefab == null || customerPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("Spawner missing references.");
            return null;
        }

        if (!forceTakeoutForTesting && lobbyLine == null)
        {
            Debug.LogWarning("Spawner missing LobbyLineManager.");
            return null;
        }

        if (forceTakeoutForTesting && takeoutQueueManager == null)
        {
            Debug.LogWarning("Spawner missing TakeoutQueueManager.");
            return null;
        }

        int size = forceTakeoutForTesting
            ? forcedTakeoutSize
            : Random.Range(minGroupSize, maxGroupSize + 1);

        var group = Instantiate(groupPrefab, spawnPoint.position, Quaternion.identity);
        group.name = $"Group_{size}";

        for (int i = 0; i < size; i++)
        {
            Vector3 offset = new Vector3((i % 2) * 0.6f, 0f, (i / 2) * 0.6f);
            var cust = Instantiate(customerPrefab, spawnPoint.position + offset, Quaternion.identity, group.transform);
            cust.name = $"Customer_{i + 1}";
            group.members.Add(cust);
        }

        if (forceTakeoutForTesting)
        {
            group.SetServiceType(CustomerGroup.ServiceType.Takeout);
            group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);
            group.state = CustomerGroup.GroupState.Waiting;

            takeoutQueueManager.Enqueue(group);
            return group;
        }

        lobbyLine.TryJoinLine(group);
        group.state = CustomerGroup.GroupState.WalkingToLobby;

        return group;
    }
}