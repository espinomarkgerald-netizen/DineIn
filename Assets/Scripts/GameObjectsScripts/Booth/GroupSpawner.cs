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
    [SerializeField] private bool takeoutEnabled = false;
    [SerializeField] [Range(0f, 1f)] private float takeoutSpawnChance = 0.2f;

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

    /// <summary>Enables or disables the takeout spawn path at runtime.</summary>
    public void SetTakeoutEnabled(bool enabled) { takeoutEnabled = enabled; }

    /// <summary>Returns the current takeout-enabled state.</summary>
    public bool TakeoutEnabled => takeoutEnabled;

    public CustomerGroup SpawnGroup()
    {
        if (groupPrefab == null || customerPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[GroupSpawner] Spawner missing core references (groupPrefab / customerPrefab / spawnPoint).");
            return null;
        }

        // Dine-in requires a lobby line. If it is missing, we cannot spawn at all.
        if (lobbyLine == null)
        {
            Debug.LogWarning("[GroupSpawner] LobbyLineManager not assigned — cannot spawn dine-in groups.");
            return null;
        }

        // Decide service type for this spawn.
        bool spawnAsTakeout = takeoutEnabled
            && takeoutQueueManager != null
            && Random.value < takeoutSpawnChance;

        if (takeoutEnabled && takeoutQueueManager == null)
            Debug.LogWarning("[GroupSpawner] takeoutEnabled is true but TakeoutQueueManager is not assigned — falling back to dine-in.");

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

        if (spawnAsTakeout)
        {
            group.SetServiceType(CustomerGroup.ServiceType.Takeout);
            group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);
            group.state = CustomerGroup.GroupState.Waiting;

            // TakeoutCustomerInteractable implements IInteractable so the waiter's
            // custom raycast system (PlayerMovement.TryClickInteractable) can detect
            // a click on this group while holding the bag. OnMouseDown is NOT used.
            if (group.GetComponent<TakeoutCustomerInteractable>() == null)
                group.gameObject.AddComponent<TakeoutCustomerInteractable>();

            // A Collider on the group root is required for Physics.RaycastAll to hit it.
            // Layer 8 (Customer) is already in PlayerMovement.clickMask in this scene.
            if (group.GetComponent<Collider>() == null)
            {
                var col = group.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.45f;
                col.height = 1.8f;
                col.isTrigger = false;
            }

            takeoutQueueManager.Enqueue(group);
            return group;
        }

        lobbyLine.TryJoinLine(group);
        group.state = CustomerGroup.GroupState.WalkingToLobby;

        return group;
    }
}