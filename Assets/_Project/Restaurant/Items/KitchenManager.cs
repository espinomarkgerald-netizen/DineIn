using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KitchenManager : MonoBehaviour
{
    public enum ForecastState
    {
        Cooking,
        WaitingForSpawnSlot,
        Completed,
        Canceled
    }

    public readonly struct OrderForecast
    {
        public CustomerGroup Group { get; }
        public int OrderNumber { get; }
        public bool IsTakeout { get; }
        public float StartedAt { get; }
        public float PreparationDelaySeconds { get; }
        public float CookDurationSeconds { get; }
        public float PredictedReadyAt { get; }
        public ForecastState State { get; }
        public bool IsPaused { get; }
        public bool AwaitingSpawn { get; }

        public float RemainingSeconds => AwaitingSpawn ? 0f : IsPaused ? PreparationDelaySeconds + CookDurationSeconds
            : Mathf.Max(0f, PredictedReadyAt - Time.time);
        public bool HasReliableReadyTime => !IsPaused && State == ForecastState.Cooking;

        internal OrderForecast(
            CustomerGroup group,
            int orderNumber,
            bool isTakeout,
            float startedAt,
            float preparationDelaySeconds,
            float cookDurationSeconds,
            float predictedReadyAt,
            ForecastState state, bool isPaused = false, bool awaitingSpawn = false)
        {
            Group = group;
            OrderNumber = orderNumber;
            IsTakeout = isTakeout;
            StartedAt = startedAt;
            PreparationDelaySeconds = preparationDelaySeconds;
            CookDurationSeconds = cookDurationSeconds;
            PredictedReadyAt = predictedReadyAt;
            State = state;
            IsPaused = isPaused;
            AwaitingSpawn = awaitingSpawn;
        }
    }

    private sealed class ActiveOrderForecast
    {
        public CustomerGroup group;
        public int orderNumber;
        public bool isTakeout;
        public float startedAt;
        public float preparationDelaySeconds;
        public float cookDurationSeconds;
        public float predictedReadyAt;
        public ForecastState state;
        public bool isPaused;
        public bool awaitingSpawn;

        public OrderForecast Snapshot => new OrderForecast(
            group,
            orderNumber,
            isTakeout,
            startedAt,
            preparationDelaySeconds,
            cookDurationSeconds,
            predictedReadyAt,
            state, isPaused, awaitingSpawn);
    }

    [Header("Dine-In Spawn Points")]
    public Transform[] traySpawnPoints;
    public FoodTray foodTrayPrefab;

    [Header("Takeout Spawn Points")]
    [SerializeField] private Transform[] takeoutSpawnPoints;
    [SerializeField] private GameObject takeoutBagPrefab;

    [Header("Timing")]
    public float cookSeconds = 5f;
    [SerializeField, Min(0f)] private float preparationDelaySeconds = 2f;

    [Header("Queueing")]
    [SerializeField] private float waitForFreeSlotCheckInterval = 0.25f;
    [SerializeField, Min(1f)] private float maxSlotWaitSeconds = 8f;

    private readonly HashSet<int> cookingOrders = new HashSet<int>();
    private readonly HashSet<int> completedOrders = new HashSet<int>();
    private readonly Dictionary<int, ActiveOrderForecast> activeOrderForecasts =
        new Dictionary<int, ActiveOrderForecast>();

    private TrayPickupQueue pickupQueue;
    private readonly Dictionary<int, FoodTray> preparedResults = new();
    private readonly Dictionary<int, int> preparedSlots = new();
    private readonly HashSet<int> spawningResults = new();

    public bool TryGetPreparedSlot(int orderNumber, out int slot) => preparedSlots.TryGetValue(orderNumber, out slot);
    public FoodTray GetPreparedResult(int orderNumber) => preparedResults.TryGetValue(orderNumber, out var tray) ? tray : null;

    public bool TryGetPreparedTray(int orderNumber, out FoodTray tray)
    {
        tray = null;
        if (!preparedResults.TryGetValue(orderNumber, out var result) || result == null
            || !preparedSlots.TryGetValue(orderNumber, out int slot) || traySpawnPoints == null
            || slot < 0 || slot >= traySpawnPoints.Length || traySpawnPoints[slot] == null
            || result.transform.parent != traySpawnPoints[slot] || result.orderNumber != orderNumber
            || result.TargetGroup == null || !result.TargetGroup.HasConfirmedOrder
            || result.TargetGroup.currentOrderNumber != orderNumber
            || result.TargetGroup.state != CustomerGroup.GroupState.OrderTaken) return false;
        tray = result;
        return true;
    }

    public bool CarryPreparedTray(CustomerGroup group, WaiterHands hands)
    {
        if (group == null || group.IsNetworkObserver || hands == null || hands.HasTray || hands.HasBill
            || (hands.GetComponent<BusserHands>() != null && hands.GetComponent<BusserHands>().HasTray)
            || hands.HasTicket || hands.HasMoney || !TryGetPreparedTray(group.currentOrderNumber, out var tray)
            || tray.TargetGroup != group || !completedOrders.Contains(group.currentOrderNumber)) return false;
        tray.NetworkCarryLocked = true;
        if (!hands.PickupTray(tray)) { tray.NetworkCarryLocked = false; return false; }
        preparedSlots.Remove(group.currentOrderNumber);
        return true;
    }

    public void PresentCarriedTray(CustomerGroup group, WaiterHands hands)
    {
        if (group == null || (!group.IsNetworkObserver && !group.PauseAfterSeating)
            || !group.HasConfirmedOrder || foodTrayPrefab == null || MenuCatalog.Default == null) return;
        int order = group.currentOrderNumber;
        if (!preparedResults.TryGetValue(order, out var tray) || tray == null)
        {
            // Recovery uses the preserved order, never another kitchen job or output slot.
            tray = Instantiate(foodTrayPrefab, transform);
            tray.Init(group, preserveOrderSnapshot: true);
            preparedResults[order] = tray;
        }
        preparedSlots.Remove(order);
        tray.NetworkCarryLocked = true;
        foreach (var body in tray.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        WaiterHands.SetAllColliders(tray.gameObject, false);
        if (hands == null)
        {
            tray.transform.SetParent(transform, true);
            tray.gameObject.SetActive(false); // Preserved, awaiting carrier recovery; not a free-slot result.
            return;
        }
        if (hands.holdingTray == tray) { tray.gameObject.SetActive(true); return; }
        if (hands.HasTray || hands.HasBill || hands.HasTicket || hands.HasMoney) return;
        tray.gameObject.SetActive(true);
        hands.PickupTray(tray);
    }

    public bool ResumePreparedSpawn(CustomerGroup group)
    {
        if (group == null || group.IsNetworkObserver || group.IsTakeout || !group.PauseAfterSeating
            || !activeOrderForecasts.TryGetValue(group.currentOrderNumber, out var active)
            || active.group != group || !active.awaitingSpawn || !active.isPaused
            || completedOrders.Contains(active.orderNumber) || !spawningResults.Add(active.orderNumber)) return false;
        active.awaitingSpawn = false;
        StartCoroutine(CookAndSpawn(group, active.orderNumber, false, 0f, 0f, resumeAtSpawn: true));
        return true;
    }

    public void PresentPreparedResult(CustomerGroup group, int slotIndex)
    {
        if (group == null || !group.IsNetworkObserver || !group.HasConfirmedOrder || group.IsTakeout
            || traySpawnPoints == null || slotIndex < 0 || slotIndex >= traySpawnPoints.Length
            || traySpawnPoints[slotIndex] == null || foodTrayPrefab == null || MenuCatalog.Default == null) return;
        int order = group.currentOrderNumber;
        if (preparedResults.TryGetValue(order, out var existing) && existing != null) return;
        var slot = traySpawnPoints[slotIndex];
        // Never choose an alternative slot on observers.
        if (slot.GetComponentInChildren<FoodTray>() != null) return;
        var tray = Instantiate(foodTrayPrefab, slot.position, slot.rotation, slot);
        tray.Init(group, preserveOrderSnapshot: true);
        BlockPreparedPickup(tray);
        preparedResults[order] = tray;
        preparedSlots[order] = slotIndex;
    }

    private static void BlockPreparedPickup(FoodTray tray)
    {
        var interaction = tray.GetComponent<FoodTrayInteractable>();
        if (interaction != null) { interaction.SetCleanupPickable(false); interaction.enabled = true; }
        foreach (var collider in tray.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var selectionCollider = tray.GetComponent<Collider>();
        if (selectionCollider == null) selectionCollider = tray.GetComponentInChildren<Collider>(true);
        if (selectionCollider != null) selectionCollider.enabled = true;
        foreach (var body in tray.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
    }

    public event Action<CustomerGroup, int> OrderStarted;
    public event Action<CustomerGroup, int, bool> OrderFinished;
    public event Action<OrderForecast> OrderForecastChanged;

    public int ActiveForecastCount => activeOrderForecasts.Count;

    private void Awake()
    {
        pickupQueue = GetComponent<TrayPickupQueue>();
        if (pickupQueue == null)
            pickupQueue = gameObject.AddComponent<TrayPickupQueue>();
    }

    private void Start()
    {
        ApplyKitchenAssignmentCookTime();
    }

    private void OnDestroy()
    {
        activeOrderForecasts.Clear();
    }

    public bool TryGetForecast(int orderNumber, out OrderForecast forecast)
    {
        if (activeOrderForecasts.TryGetValue(orderNumber, out ActiveOrderForecast active))
        {
            forecast = active.Snapshot;
            return true;
        }

        forecast = default;
        return false;
    }

    public bool TryGetNextDineInForecast(out OrderForecast forecast)
    {
        bool found = false;
        forecast = default;

        foreach (ActiveOrderForecast active in activeOrderForecasts.Values)
        {
            if (!IsUsableDineInForecast(active))
                continue;

            if (!found || active.predictedReadyAt < forecast.PredictedReadyAt ||
                (Mathf.Approximately(active.predictedReadyAt, forecast.PredictedReadyAt) &&
                 active.orderNumber < forecast.OrderNumber))
            {
                forecast = active.Snapshot;
                found = true;
            }
        }

        return found;
    }

    public int CopyActiveDineInForecasts(List<OrderForecast> destination)
    {
        if (destination == null)
            return 0;

        destination.Clear();
        foreach (ActiveOrderForecast active in activeOrderForecasts.Values)
        {
            if (IsUsableDineInForecast(active))
                destination.Add(active.Snapshot);
        }

        destination.Sort((left, right) =>
        {
            int timeComparison = left.PredictedReadyAt.CompareTo(right.PredictedReadyAt);
            return timeComparison != 0
                ? timeComparison
                : left.OrderNumber.CompareTo(right.OrderNumber);
        });
        return destination.Count;
    }

    private void ApplyKitchenAssignmentCookTime()
    {
        if (KitchenAssignmentSaveBridge.Instance == null)
        {
            Debug.LogWarning("[KitchenManager] KitchenAssignmentSaveBridge not found. Using default cookSeconds.");
            return;
        }

        cookSeconds = KitchenAssignmentSaveBridge.Instance.GetMealSpawnTime();

        Debug.Log(
            $"[KitchenManager] Applied cookSeconds = {cookSeconds} | " +
            $"Chef: {KitchenAssignmentSaveBridge.Instance.AssignedChefName} ({KitchenAssignmentSaveBridge.Instance.AssignedChefStars}★) | " +
            $"Barista: {KitchenAssignmentSaveBridge.Instance.AssignedBaristaName} ({KitchenAssignmentSaveBridge.Instance.AssignedBaristaStars}★)"
        );
    }

    public bool CanAcceptOrderNumber(int orderNumber) => orderNumber >= 0
        && !completedOrders.Contains(orderNumber) && !cookingOrders.Contains(orderNumber);

    public void PresentObservedAcceptedOrder(CustomerGroup group, int orderNumber,
        float startedAt, float preparationDelay, float cookDuration, float predictedReadyAt,
        bool isPaused, bool awaitingSpawn, ForecastState state)
    {
        if (group == null || !group.IsNetworkObserver || !group.HasConfirmedOrder
            || group.currentOrderNumber != orderNumber || orderNumber < 0) return;
        if (activeOrderForecasts.TryGetValue(orderNumber, out var existing))
        {
            // Order numbers are unique in the real collection; never replace another group.
            if (existing.group != group) return;
            if (existing.startedAt == startedAt && existing.preparationDelaySeconds == preparationDelay
                && existing.cookDurationSeconds == cookDuration && existing.predictedReadyAt == predictedReadyAt
                && existing.isPaused == isPaused && existing.awaitingSpawn == awaitingSpawn && existing.state == state) return;
        }
        var entry = new ActiveOrderForecast
        {
            group = group, orderNumber = orderNumber, isTakeout = group.IsTakeout,
            startedAt = startedAt, preparationDelaySeconds = preparationDelay,
            cookDurationSeconds = cookDuration, predictedReadyAt = predictedReadyAt,
            state = state, isPaused = isPaused, awaitingSpawn = awaitingSpawn
        };
        activeOrderForecasts[orderNumber] = entry;
        NotifyForecastChanged(entry);
        // Presentation only: no cookingOrders registration, OrderStarted, or coroutine.
    }

    public bool ResumeAcceptedOrder(CustomerGroup group)
    {
        if (group == null || group.IsNetworkObserver || !group.PauseAfterSeating
            || !activeOrderForecasts.TryGetValue(group.currentOrderNumber, out var active)
            || active.group != group || !active.isPaused || active.awaitingSpawn
            || spawningResults.Contains(active.orderNumber)
            || !IsOrderStillValid(group, active.orderNumber)) return false;
        active.isPaused = false;
        active.startedAt = Time.time;
        active.predictedReadyAt = active.startedAt + active.preparationDelaySeconds + active.cookDurationSeconds;
        NotifyForecastChanged(active);
        StartCoroutine(CookAndSpawn(group, active.orderNumber, active.isTakeout,
            active.preparationDelaySeconds, active.cookDurationSeconds, pauseBeforeSpawn: true));
        return true;
    }

    public bool ProcessOrder(CustomerGroup group, bool pauseAfterAcceptance = false)
    {
        if (group == null)
        {
            Debug.LogError("[KitchenManager] ProcessOrder called with null group.");
            return false;
        }

        int orderNo = group.currentOrderNumber;
        if (orderNo < 0)
        {
            Debug.LogError($"[KitchenManager] ProcessOrder — invalid orderNumber ({orderNo}) on {group.name}. Order not started.");
            return false;
        }

        if (group.state != CustomerGroup.GroupState.OrderTaken)
        {
            Debug.LogError($"[KitchenManager] ProcessOrder — {group.name} is in state '{group.state}', expected 'OrderTaken'. Order not started.");
            return false;
        }

        if (!group.HasConfirmedOrder || group.IsPlayerReviewingOrder)
        {
            Debug.LogWarning(
                $"[KitchenManager] ProcessOrder ignored for {group.name} because its order is not fully confirmed.",
                group);
            return false;
        }

        if (completedOrders.Contains(orderNo))
        {
            Debug.LogWarning($"[KitchenManager] Order #{orderNo} already finished spawning. Duplicate call ignored.");
            return false;
        }

        if (!cookingOrders.Add(orderNo))
        {
            Debug.LogWarning($"[KitchenManager] Order #{orderNo} is already being cooked. Duplicate call ignored.");
            return false;
        }

        bool isTakeout = group.IsTakeout;
        float startedAt = Time.time;
        float preparationSnapshot = Mathf.Max(0f, preparationDelaySeconds);
        float cookSnapshot = Mathf.Max(0f, cookSeconds);
        ActiveOrderForecast activeForecast = new ActiveOrderForecast
        {
            group = group,
            orderNumber = orderNo,
            isTakeout = isTakeout,
            startedAt = startedAt,
            preparationDelaySeconds = preparationSnapshot,
            cookDurationSeconds = cookSnapshot,
            predictedReadyAt = startedAt + preparationSnapshot + cookSnapshot,
            state = ForecastState.Cooking,
            isPaused = pauseAfterAcceptance
        };
        activeOrderForecasts[orderNo] = activeForecast;

        Debug.Log($"[KitchenManager] Starting cook for order #{orderNo} — group={group.name} isTakeout={isTakeout}.");
        NotifyForecastChanged(activeForecast);
        if (!pauseAfterAcceptance) StartCoroutine(CookAndSpawn(
            group,
            orderNo,
            isTakeout,
            preparationSnapshot,
            cookSnapshot));
        if (!pauseAfterAcceptance) OrderStarted?.Invoke(group, orderNo);
        return true;
    }

    private IEnumerator CookAndSpawn(
        CustomerGroup group,
        int orderNo,
        bool isTakeout,
        float preparationSnapshot,
        float cookSnapshot, bool pauseBeforeSpawn = false, bool resumeAtSpawn = false)
    {
        bool spawnedSuccessfully = false;
        bool heldBeforeSpawn = false;

        try
        {
            if (!resumeAtSpawn && preparationSnapshot > 0f)
                yield return new WaitForSeconds(preparationSnapshot);

            if (!resumeAtSpawn && ProcessingBillIndicatorUI.Instance != null)
                ProcessingBillIndicatorUI.Instance.Show("Order #" + orderNo + " is being prepared");

            if (!resumeAtSpawn && cookSnapshot > 0f)
                yield return new WaitForSeconds(cookSnapshot);

            if (!IsOrderStillValid(group, orderNo))
            {
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            if (pauseBeforeSpawn)
            {
                if (!activeOrderForecasts.TryGetValue(orderNo, out var held)) yield break;
                heldBeforeSpawn = true;
                held.isPaused = true;
                held.awaitingSpawn = true;
                NotifyForecastChanged(held);
                ProcessingBillIndicatorUI.Instance?.Hide();
                yield break;
            }

            Transform[] targetSlots = isTakeout ? takeoutSpawnPoints : traySpawnPoints;

            if (targetSlots == null || targetSlots.Length == 0)
            {
                Debug.LogError($"[KitchenManager] No spawn points assigned for {(isTakeout ? "takeout" : "dine-in")} — assign them in the Inspector on KitchenManager.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            if (!isTakeout && foodTrayPrefab == null)
            {
                Debug.LogError("[KitchenManager] FoodTray prefab not assigned in Inspector.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            if (isTakeout && takeoutBagPrefab == null)
            {
                Debug.LogError("[KitchenManager] Takeout bag prefab not assigned in Inspector — assign PaperBag prefab to KitchenManager.takeoutBagPrefab.");
                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();
                yield break;
            }

            Transform freeSlot = GetFirstFreeSlot(targetSlots);
            float slotWaitStarted = Time.time;

            if (freeSlot == null)
                SetForecastState(orderNo, ForecastState.WaitingForSpawnSlot);

            while (freeSlot == null && Time.time - slotWaitStarted < maxSlotWaitSeconds)
            {
                if (!IsOrderStillValid(group, orderNo))
                {
                    if (ProcessingBillIndicatorUI.Instance != null)
                        ProcessingBillIndicatorUI.Instance.Hide();
                    yield break;
                }

                freeSlot = GetFirstFreeSlot(targetSlots);

                if (freeSlot == null)
                    yield return new WaitForSeconds(waitForFreeSlotCheckInterval);
            }

            if (freeSlot == null)
            {
                string serviceType = isTakeout ? "takeout" : "dine-in";
                Debug.LogError($"[KitchenManager] Timed out waiting for a free {serviceType} slot for order #{orderNo}.", this);
                yield break;
            }

            if (isTakeout)
            {
                GameObject bag = Instantiate(takeoutBagPrefab, freeSlot.position, freeSlot.rotation, freeSlot);

                TakeoutBagInteractable requiredInteractable = bag.GetComponent<TakeoutBagInteractable>();
                if (requiredInteractable == null)
                {
                    Debug.LogError("[KitchenManager] Spawned takeout bag is missing TakeoutBagInteractable. Order cannot be delivered.", bag);
                    Destroy(bag);
                    yield break;
                }

                TakeoutBagMarker marker = bag.GetComponent<TakeoutBagMarker>();
                if (marker != null)
                    marker.Init(group);

                requiredInteractable.Init(group);

                TakeoutFlowManager flow = TakeoutFlowManager.Instance;
                if (flow == null || !flow.NotifyBagReady(group))
                {
                    Debug.LogError($"[KitchenManager] Takeout flow rejected the ready bag for order #{orderNo}.", this);
                    Destroy(bag);
                    yield break;
                }

                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.ShowForSeconds("Takeout order #" + orderNo + " is ready for pickup!", 3f);

                Debug.Log($"[KitchenManager] Takeout bag spawned at '{freeSlot.name}' for order #{orderNo}.");
            }
            else
            {
                FoodTray tray = Instantiate(foodTrayPrefab, freeSlot.position, freeSlot.rotation, freeSlot);
                tray.Init(group, preserveOrderSnapshot: resumeAtSpawn);

                FoodTrayInteractable it = tray.GetComponent<FoodTrayInteractable>();
                if (resumeAtSpawn)
                {
                    BlockPreparedPickup(tray);
                    preparedResults[orderNo] = tray;
                    preparedSlots[orderNo] = Array.IndexOf(traySpawnPoints, freeSlot);
                }
                else if (it != null)
                    it.SetDeliveryPickable(pickupQueue);
                else
                    Debug.LogWarning("[KitchenManager] FoodTrayInteractable missing on FoodTray prefab.");

                if (ProcessingBillIndicatorUI.Instance != null)
                    ProcessingBillIndicatorUI.Instance.Hide();

                Debug.Log($"[KitchenManager] Dine-in tray spawned at '{freeSlot.name}' for order #{orderNo}.");
            }

            spawnedSuccessfully = true;
            completedOrders.Add(orderNo);
        }
        finally
        {
            if (!heldBeforeSpawn)
            {
                cookingOrders.Remove(orderNo);
                if (resumeAtSpawn && activeOrderForecasts.TryGetValue(orderNo, out var finished))
                {
                    finished.isPaused = true;
                    finished.awaitingSpawn = false;
                    finished.state = spawnedSuccessfully ? ForecastState.Completed : ForecastState.Canceled;
                    NotifyForecastChanged(finished);
                }
                else CompleteForecast(orderNo, spawnedSuccessfully ? ForecastState.Completed : ForecastState.Canceled);
                OrderFinished?.Invoke(group, orderNo, spawnedSuccessfully);

                if (!spawnedSuccessfully && ProcessingBillIndicatorUI.Instance != null && cookingOrders.Count == 0)
                    ProcessingBillIndicatorUI.Instance.Hide();
            }
        }
    }

    private static bool IsUsableDineInForecast(ActiveOrderForecast active)
    {
        return active != null && !active.isTakeout && active.group != null &&
               active.state != ForecastState.Completed &&
               active.state != ForecastState.Canceled;
    }

    private void SetForecastState(int orderNumber, ForecastState state)
    {
        if (!activeOrderForecasts.TryGetValue(orderNumber, out ActiveOrderForecast active) ||
            active.state == state)
        {
            return;
        }

        active.state = state;
        NotifyForecastChanged(active);
    }

    private void CompleteForecast(int orderNumber, ForecastState finalState)
    {
        if (!activeOrderForecasts.TryGetValue(orderNumber, out ActiveOrderForecast active))
            return;

        active.state = finalState;
        NotifyForecastChanged(active);
        activeOrderForecasts.Remove(orderNumber);
    }

    private void NotifyForecastChanged(ActiveOrderForecast active)
    {
        if (active != null)
            OrderForecastChanged?.Invoke(active.Snapshot);
    }

    private bool IsOrderStillValid(CustomerGroup group, int orderNo)
    {
        if (group == null)
            return false;

        if (group.currentOrderNumber != orderNo)
            return false;

        if (group.state != CustomerGroup.GroupState.OrderTaken ||
            !group.HasConfirmedOrder || group.IsPlayerReviewingOrder)
            return false;

        if (completedOrders.Contains(orderNo))
            return false;

        return true;
    }

    private Transform GetFirstFreeSlot(Transform[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];
            if (slot == null)
                continue;

            if (!SlotHasSpawnedOrder(slot))
                return slot;
        }

        return null;
    }

    private bool SlotHasSpawnedOrder(Transform slot)
    {
        for (int i = 0; i < slot.childCount; i++)
        {
            Transform child = slot.GetChild(i);
            if (child == null)
                continue;

            if (child.GetComponent<FoodTray>() != null)
                return true;

            TakeoutBagInteractable bag = child.GetComponent<TakeoutBagInteractable>();
            if (bag != null)
            {
                if (bag.TargetGroup == null)
                {
                    Destroy(child.gameObject);
                    continue;
                }

                return true;
            }

            if (child.GetComponent<TakeoutBagMarker>() != null)
                return true;
        }

        return false;
    }
}
