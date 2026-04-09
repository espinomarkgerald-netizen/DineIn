using UnityEngine;

public class GroupSpawner : MonoBehaviour
{
    [Header("Shared Group Prefab")]
    [SerializeField] private CustomerGroup groupPrefab;

    [Header("Customer Prefabs by Type")]
    [SerializeField] private CustomerAgent customerPrefabGreen;
    [SerializeField] private CustomerAgent customerPrefabPink;
    [SerializeField] private CustomerAgent customerPrefabBlue;

    [Header("Customer Type Availability")]
    [SerializeField] private bool greenEnabled = true;
    [SerializeField] private bool pinkEnabled = false;
    [SerializeField] private bool blueEnabled = false;

    [Header("Points")]
    [SerializeField] private Transform spawnPoint;

    [Header("Lobby Line")]
    [SerializeField] private LobbyLineManager lobbyLine;

    [Header("Takeout")]
    [SerializeField] private TakeoutQueueManager takeoutQueueManager;
    [SerializeField] private bool takeoutEnabled = false;
    [SerializeField] [Range(0f, 1f)] private float takeoutSpawnChance = 0.2f;

    [Header("Spawn Settings")]
    [SerializeField] private bool autoSpawn = false;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private int minGroupSize = 1;
    [SerializeField] private int maxGroupSize = 4;

    [Header("Customer Type Spawn Weights")]
    [SerializeField] private float weightGreen = 0.7f;
    [SerializeField] private float weightPink = 0.2f;
    [SerializeField] private float weightBlue = 0.1f;

    private float timer;

    public bool TakeoutEnabled => takeoutEnabled;
    public bool GreenEnabled => greenEnabled;
    public bool PinkEnabled => pinkEnabled;
    public bool BlueEnabled => blueEnabled;

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

    public void SetTakeoutEnabled(bool enabled)
    {
        takeoutEnabled = enabled;
    }

    public void SetCustomerTypeAvailability(bool green, bool pink, bool blue)
    {
        greenEnabled = green;
        pinkEnabled = pink;
        blueEnabled = blue;
    }

    public void SetCustomerTypeEnabled(CustomerGroup.CustomerType type, bool enabled)
    {
        switch (type)
        {
            case CustomerGroup.CustomerType.Green:
                greenEnabled = enabled;
                break;

            case CustomerGroup.CustomerType.Pink:
                pinkEnabled = enabled;
                break;

            case CustomerGroup.CustomerType.Blue:
                blueEnabled = enabled;
                break;
        }
    }

    public bool IsCustomerTypeEnabled(CustomerGroup.CustomerType type)
    {
        switch (type)
        {
            case CustomerGroup.CustomerType.Pink:
                return pinkEnabled;

            case CustomerGroup.CustomerType.Blue:
                return blueEnabled;

            default:
                return greenEnabled;
        }
    }

    private CustomerGroup.CustomerType PickCustomerType()
    {
        float green = greenEnabled ? Mathf.Max(0f, weightGreen) : 0f;
        float pink = pinkEnabled ? Mathf.Max(0f, weightPink) : 0f;
        float blue = blueEnabled ? Mathf.Max(0f, weightBlue) : 0f;
        float total = green + pink + blue;

        if (total <= 0f)
        {
            if (greenEnabled) return CustomerGroup.CustomerType.Green;
            if (pinkEnabled) return CustomerGroup.CustomerType.Pink;
            if (blueEnabled) return CustomerGroup.CustomerType.Blue;
            return CustomerGroup.CustomerType.Green;
        }

        float roll = Random.Range(0f, total);

        if (roll < green)
            return CustomerGroup.CustomerType.Green;

        if (roll < green + pink)
            return CustomerGroup.CustomerType.Pink;

        return CustomerGroup.CustomerType.Blue;
    }

    private CustomerAgent GetCustomerPrefabForType(CustomerGroup.CustomerType type)
    {
        switch (type)
        {
            case CustomerGroup.CustomerType.Pink:
                return customerPrefabPink != null ? customerPrefabPink : customerPrefabGreen;

            case CustomerGroup.CustomerType.Blue:
                return customerPrefabBlue != null ? customerPrefabBlue : customerPrefabGreen;

            default:
                return customerPrefabGreen;
        }
    }

    public CustomerGroup SpawnGroup()
    {
        if (groupPrefab == null)
        {
            Debug.LogWarning("[GroupSpawner] groupPrefab is not assigned.");
            return null;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("[GroupSpawner] spawnPoint is not assigned.");
            return null;
        }

        if (lobbyLine == null)
        {
            Debug.LogWarning("[GroupSpawner] lobbyLine is not assigned.");
            return null;
        }

        CustomerGroup.CustomerType type = PickCustomerType();
        CustomerAgent memberPrefab = GetCustomerPrefabForType(type);

        if (memberPrefab == null)
        {
            Debug.LogWarning($"[GroupSpawner] No customer prefab assigned for type {type}.");
            return null;
        }

        bool spawnAsTakeout = takeoutEnabled
            && takeoutQueueManager != null
            && Random.value < takeoutSpawnChance;

        if (takeoutEnabled && takeoutQueueManager == null)
            Debug.LogWarning("[GroupSpawner] Takeout is enabled but TakeoutQueueManager is missing. Falling back to dine-in.");

        int size = Random.Range(minGroupSize, maxGroupSize + 1);

        CustomerGroup group = Instantiate(groupPrefab, spawnPoint.position, Quaternion.identity);
        group.name = $"Group_{type}_{size}";
        group.members.Clear();

        group.SetCustomerType(type);

        for (int i = 0; i < size; i++)
        {
            Vector3 offset = new Vector3((i % 2) * 0.6f, 0f, (i / 2) * 0.6f);

            CustomerAgent member = Instantiate(
                memberPrefab,
                spawnPoint.position + offset,
                Quaternion.identity,
                group.transform
            );

            member.name = $"{type}_Customer_{i + 1}";
            group.members.Add(member);
        }

        if (spawnAsTakeout)
        {
            group.SetServiceType(CustomerGroup.ServiceType.Takeout);
            group.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.None);
            group.state = CustomerGroup.GroupState.Waiting;

            if (group.GetComponent<TakeoutCustomerInteractable>() == null)
                group.gameObject.AddComponent<TakeoutCustomerInteractable>();

            if (group.GetComponent<Collider>() == null)
            {
                CapsuleCollider col = group.gameObject.AddComponent<CapsuleCollider>();
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