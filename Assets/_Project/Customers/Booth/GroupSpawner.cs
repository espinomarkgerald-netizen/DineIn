using UnityEngine;

public class GroupSpawner : MonoBehaviour
{
    public static GroupSpawner Instance { get; private set; }

    [Header("Shared Group Prefab")]
    [SerializeField] private CustomerGroup groupPrefab;

    [Header("Global Customer Bubble Layout")]
    [SerializeField, Range(-500f, 500f)]
    [InspectorName("Max Zoom In Offset (Pixels)")]
    [Tooltip("Signed vertical offset from the animated customer head at maximum zoom in. Negative is lower.")]
    private float maxZoomInBubbleOffsetPixels;
    [SerializeField, Range(-500f, 500f)]
    [InspectorName("Max Zoom Out Offset (Pixels)")]
    [Tooltip("Signed vertical offset from the animated customer head at maximum zoom out. Negative is lower.")]
    private float maxZoomOutBubbleOffsetPixels;

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
    [Tooltip("Maximum members per customer group on mobile. This reduces animated agents and NavMesh work without changing desktop balance.")]
    [SerializeField, Min(1)] private int mobileMaxGroupSize = 3;

    [Header("Customer Type Spawn Weights")]
    [SerializeField] private float weightGreen = 0.7f;
    [SerializeField] private float weightPink = 0.2f;
    [SerializeField] private float weightBlue = 0.1f;

    [Header("Shift Cap (set by ShiftScaler at runtime)")]
    [SerializeField] private int groupsForShift = 0;

    private int groupsSpawnedThisShift;
    private float timer;
    private MainCameraController cameraController;

    public bool TakeoutEnabled => takeoutEnabled;
    public bool AutoSpawnEnabled => autoSpawn;
    public bool GreenEnabled => greenEnabled;
    public bool PinkEnabled => pinkEnabled;
    public bool BlueEnabled => blueEnabled;
    public float CurrentBubbleOffsetPixels => Mathf.Lerp(
        maxZoomInBubbleOffsetPixels,
        maxZoomOutBubbleOffsetPixels,
        ResolveNormalizedZoom());

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private float ResolveNormalizedZoom()
    {
        if (cameraController == null)
        {
            Camera gameplayCamera = UIRoot.GameplayCameraOrNull();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera != null)
                cameraController = gameplayCamera.GetComponentInParent<MainCameraController>();
        }

        return cameraController != null ? cameraController.NormalizedZoom : 0f;
    }

    private void Update()
    {
        if (!autoSpawn)
            return;

        // Respect the shift cap when it has been set (> 0)
        if (groupsForShift > 0 && groupsSpawnedThisShift >= groupsForShift)
            return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnGroup();
        }
    }

    /// <summary>Sets the maximum number of groups to spawn this shift. 0 = unlimited.</summary>
    public void SetGroupsForShift(int count)
    {
        groupsForShift = Mathf.Max(0, count);
        groupsSpawnedThisShift = 0;
    }

    /// <summary>Returns the number of groups spawned so far this shift.</summary>
    public int GroupsSpawnedThisShift => groupsSpawnedThisShift;

    /// <summary>
    /// Allows a scene-level flow controller (such as the tutorial) to pause and
    /// restore automatic spawning without changing the normal spawn pipeline.
    /// Manual SpawnGroup calls remain available for controlled sequences.
    /// </summary>
    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;

        if (!enabled)
            timer = 0f;
    }

    /// <summary>Enables or disables the takeout spawn path at runtime.</summary>
    public void SetTakeoutEnabled(bool enabled) { takeoutEnabled = enabled; }

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

        float takeoutRoll = Random.value;
        bool spawnAsTakeout = takeoutEnabled
            && takeoutQueueManager != null
            && takeoutRoll < takeoutSpawnChance;

        if (takeoutEnabled && takeoutQueueManager == null)
            Debug.LogWarning("[GroupSpawner] Takeout is enabled but TakeoutQueueManager is missing. Falling back to dine-in.");

        int effectiveMaxGroupSize = Mathf.Max(1, maxGroupSize);
        if (Application.isMobilePlatform)
            effectiveMaxGroupSize = Mathf.Min(effectiveMaxGroupSize, Mathf.Max(1, mobileMaxGroupSize));

        int effectiveMinGroupSize = Mathf.Clamp(minGroupSize, 1, effectiveMaxGroupSize);
        int size = Random.Range(effectiveMinGroupSize, effectiveMaxGroupSize + 1);

        Debug.Log(
            $"[GroupSpawner] Routing {type} group of {size}: " +
            $"takeoutEnabled={takeoutEnabled}, queueAssigned={takeoutQueueManager != null}, " +
            $"roll={takeoutRoll:0.000}, chance={takeoutSpawnChance:0.000}, selectedTakeout={spawnAsTakeout}.",
            this);

        CustomerGroup group = Instantiate(groupPrefab, spawnPoint.position, Quaternion.identity);
        group.name = $"Group_{type}_{size}";
        group.members.Clear();
        group.SetBubbleLayoutSource(this);

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

        groupsSpawnedThisShift++;

        // Apply shift-scaled patience so each group gets the correct timer for this day
        if (ShiftScaler.Instance != null)
            group.SetPatienceSeconds(ShiftScaler.Instance.CurrentPatienceSeconds);

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
                col.isTrigger = true;
            }

            takeoutQueueManager.Enqueue(group);
            return group;
        }

        lobbyLine.TryJoinLine(group);
        group.state = CustomerGroup.GroupState.WalkingToLobby;

        return group;
    }
}
