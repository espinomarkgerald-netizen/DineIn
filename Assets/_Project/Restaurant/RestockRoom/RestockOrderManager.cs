using System;
using System.Collections.Generic;
using UnityEngine;

public enum RestockOrderState
{
    Ordered,
    InDelivery,
    Delivered,
    Collected,
    PartiallyStored,
    Stored,
    Cancelled
}

[Serializable]
public sealed class RestockCartLine
{
    public ItemData item;
    public int quantity;

    public int LineCost => item == null
        ? 0
        : Mathf.Max(0, quantity) * Mathf.Max(0, item.boxCost);
}

[Serializable]
public sealed class RestockOrderLineSaveData
{
    public string itemID;
    public ItemType itemType;
    public int orderedContainers;
    public int storedContainers;
}

[Serializable]
public sealed class RestockOrderSaveData
{
    public string orderID;
    public string restaurantID;
    public long createdUtcTicks;
    public long deliveryReadyUtcTicks;
    public int totalCost;
    public RestockOrderState state;
    public bool deliveryNoticeShown;
    public List<RestockOrderLineSaveData> lines = new List<RestockOrderLineSaveData>();
}

/// <summary>
/// Authoritative ledger for containers after checkout. Ordered containers are
/// deliberately not recipe-usable inventory; later delivery/hotbar phases move
/// these same records through the remaining states.
/// </summary>
[DefaultExecutionOrder(-490)]
public sealed class RestockOrderManager : MonoBehaviour
{
    public static RestockOrderManager Instance { get; private set; }

    [SerializeField] private List<RestockOrderSaveData> orders =
        new List<RestockOrderSaveData>();

    [Header("Delivery")]
    [SerializeField, Min(1f)] private float deliveryDelaySeconds = 5f;

    public event Action OrdersChanged;
    public event Action<RestockOrderSaveData> OrderDelivered;
    public IReadOnlyList<RestockOrderSaveData> Orders => orders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static RestockOrderManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RestockOrderManager existing = FindFirstObjectByType<RestockOrderManager>();
        if (existing != null)
            return existing;

        GameObject root = new GameObject("Restock Order Manager");
        return root.AddComponent<RestockOrderManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        TickDeliveries();
    }

    public string CreateOrder(
        string restaurantID,
        IReadOnlyList<RestockCartLine> cart,
        int totalCost)
    {
        if (cart == null || cart.Count == 0 || totalCost <= 0)
            return string.Empty;

        RestockOrderSaveData order = new RestockOrderSaveData
        {
            orderID = Guid.NewGuid().ToString("N"),
            restaurantID = string.IsNullOrWhiteSpace(restaurantID)
                ? "restaurant"
                : restaurantID.Trim(),
            createdUtcTicks = DateTime.UtcNow.Ticks,
            deliveryReadyUtcTicks = DateTime.UtcNow
                .AddSeconds(Mathf.Max(1f, deliveryDelaySeconds)).Ticks,
            totalCost = Mathf.Max(0, totalCost),
            state = RestockOrderState.InDelivery,
            deliveryNoticeShown = false
        };

        for (int i = 0; i < cart.Count; i++)
        {
            RestockCartLine cartLine = cart[i];
            if (cartLine?.item == null || cartLine.quantity <= 0)
                continue;

            order.lines.Add(new RestockOrderLineSaveData
            {
                itemID = cartLine.item.StableItemId,
                itemType = cartLine.item.itemType,
                orderedContainers = Mathf.Max(0, cartLine.quantity),
                storedContainers = 0
            });
        }

        if (order.lines.Count == 0)
            return string.Empty;

        orders.Add(order);
        OrdersChanged?.Invoke();
        return order.orderID;
    }

    public bool HasDeliveredOrders
    {
        get
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != null && orders[i].state == RestockOrderState.Delivered)
                    return true;
            }

            return false;
        }
    }

    public int HotbarContainerCount => CountOutstandingContainers(
        RestockOrderState.Collected,
        RestockOrderState.PartiallyStored);

    public int DeliveredContainerCount => CountOutstandingContainers(
        RestockOrderState.Delivered);

    public int GetHotbarContainers(ItemData item)
    {
        return CountItemContainers(
            item,
            RestockOrderState.Collected,
            RestockOrderState.PartiallyStored);
    }

    public int GetHotbarContainerCount(RestockStorageType storageType)
    {
        int total = 0;
        List<ItemData> items = GetHotbarItems();
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item != null && item.requiredStorage == storageType)
                total += GetHotbarContainers(item);
        }

        return total;
    }

    public List<ItemData> GetHotbarItems()
    {
        List<ItemData> result = new List<ItemData>();
        IReadOnlyList<ItemData> catalog = InventoryManager.Instance != null
            ? InventoryManager.Instance.Items
            : null;
        if (catalog == null)
            return result;

        for (int i = 0; i < catalog.Count; i++)
        {
            ItemData item = catalog[i];
            if (item != null && GetHotbarContainers(item) > 0)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Moves every arrived order to the temporary delivery hotbar. No usable
    /// inventory is granted here; stock is added only after shelf placement.
    /// </summary>
    public bool CollectDeliveredOrders()
    {
        bool changed = false;
        for (int i = 0; i < orders.Count; i++)
        {
            RestockOrderSaveData order = orders[i];
            if (order == null || order.state != RestockOrderState.Delivered)
                continue;

            order.state = RestockOrderState.Collected;
            changed = true;
        }

        if (changed)
        {
            OrdersChanged?.Invoke();
            GameSaveManager.Instance?.RequestSave();
        }

        return changed;
    }

    /// <summary>
    /// Completes one physical container placement. This is the sole bridge
    /// from the delivery ledger into recipe-usable InventoryManager stock.
    /// </summary>
    public bool TryStoreOneContainer(
        ItemData item,
        RestockStorageType shelfStorage,
        out string message)
    {
        return TryStoreOneContainer(
            item,
            shelfStorage,
            out message,
            out _,
            out _);
    }

    public bool TryStoreOneContainer(
        ItemData item,
        RestockStorageType shelfStorage,
        out string message,
        out string stockBatchID,
        out int expiresDay)
    {
        message = string.Empty;
        stockBatchID = string.Empty;
        expiresDay = 0;
        if (item == null)
        {
            message = "That delivery item is missing.";
            return false;
        }

        if (item.requiredStorage != shelfStorage)
        {
            message = item.displayName + " belongs in " +
                      item.requiredStorage.ToString().ToLowerInvariant() +
                      " storage.";
            return false;
        }

        if (InventoryManager.Instance == null)
        {
            message = "Inventory is not ready yet.";
            return false;
        }

        for (int o = 0; o < orders.Count; o++)
        {
            RestockOrderSaveData order = orders[o];
            if (order == null ||
                (order.state != RestockOrderState.Collected &&
                 order.state != RestockOrderState.PartiallyStored))
                continue;

            for (int l = 0; l < order.lines.Count; l++)
            {
                RestockOrderLineSaveData line = order.lines[l];
                if (!Matches(line, item) || line.storedContainers >= line.orderedContainers)
                    continue;

                line.storedContainers++;
                int currentDay = GameFlowManager.Instance != null
                    ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
                    : 1;
                InventoryManager.Instance.AddStockBatch(
                    item,
                    Mathf.Max(1, item.unitsPerBox),
                    currentDay,
                    out stockBatchID,
                    out expiresDay);
                RefreshStoredState(order);
                OrdersChanged?.Invoke();
                GameSaveManager.Instance?.RequestSave();
                message = item.displayName + " stored (" +
                          Mathf.Max(1, item.unitsPerBox) + " units added).";
                return true;
            }
        }

        message = "No delivered " + item.displayName + " boxes remain.";
        return false;
    }

    public bool ConsumeDeliveryNotice(RestockOrderSaveData order)
    {
        if (order == null || order.state != RestockOrderState.Delivered || order.deliveryNoticeShown)
            return false;

        order.deliveryNoticeShown = true;
        OrdersChanged?.Invoke();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public int GetPendingContainers(ItemData item)
    {
        if (item == null)
            return 0;

        int total = 0;
        for (int o = 0; o < orders.Count; o++)
        {
            RestockOrderSaveData order = orders[o];
            if (!ReservesCapacity(order))
                continue;

            for (int l = 0; l < order.lines.Count; l++)
            {
                RestockOrderLineSaveData line = order.lines[l];
                if (!Matches(line, item))
                    continue;

                total += Mathf.Max(0, line.orderedContainers - line.storedContainers);
            }
        }

        return total;
    }

    public int GetReservedContainers(
        RestockStorageType storageType,
        IReadOnlyList<ItemData> itemCatalog)
    {
        if (itemCatalog == null)
            return 0;

        int total = 0;
        for (int i = 0; i < itemCatalog.Count; i++)
        {
            ItemData item = itemCatalog[i];
            if (item != null && item.requiredStorage == storageType)
                total += GetPendingContainers(item);
        }

        return total;
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.restockOrders.Clear();
        for (int i = 0; i < orders.Count; i++)
            data.restockOrders.Add(CloneOrder(orders[i]));
    }

    public void ApplySaveData(GameSaveData data)
    {
        orders.Clear();
        if (data?.restockOrders != null)
        {
            for (int i = 0; i < data.restockOrders.Count; i++)
            {
                RestockOrderSaveData source = data.restockOrders[i];
                if (source != null && !string.IsNullOrWhiteSpace(source.orderID))
                {
                    RestockOrderSaveData clone = CloneOrder(source);
                    if ((clone.state == RestockOrderState.Ordered ||
                         clone.state == RestockOrderState.InDelivery) &&
                        clone.deliveryReadyUtcTicks <= 0L)
                    {
                        clone.state = RestockOrderState.InDelivery;
                        clone.deliveryReadyUtcTicks = DateTime.UtcNow
                            .AddSeconds(Mathf.Max(1f, deliveryDelaySeconds)).Ticks;
                    }

                    orders.Add(clone);
                }
            }
        }

        OrdersChanged?.Invoke();
    }

    private void TickDeliveries()
    {
        long now = DateTime.UtcNow.Ticks;
        bool changed = false;

        for (int i = 0; i < orders.Count; i++)
        {
            RestockOrderSaveData order = orders[i];
            if (order == null ||
                (order.state != RestockOrderState.Ordered &&
                 order.state != RestockOrderState.InDelivery))
                continue;

            if (order.deliveryReadyUtcTicks <= 0L)
                order.deliveryReadyUtcTicks = DateTime.UtcNow
                    .AddSeconds(Mathf.Max(1f, deliveryDelaySeconds)).Ticks;

            order.state = RestockOrderState.InDelivery;
            if (now < order.deliveryReadyUtcTicks)
                continue;

            order.state = RestockOrderState.Delivered;
            changed = true;
            OrderDelivered?.Invoke(order);
        }

        if (changed)
        {
            OrdersChanged?.Invoke();
            GameSaveManager.Instance?.RequestSave();
        }
    }

    private int CountOutstandingContainers(params RestockOrderState[] states)
    {
        int total = 0;
        for (int o = 0; o < orders.Count; o++)
        {
            RestockOrderSaveData order = orders[o];
            if (order == null || !HasState(order.state, states))
                continue;

            for (int l = 0; l < order.lines.Count; l++)
            {
                RestockOrderLineSaveData line = order.lines[l];
                if (line != null)
                    total += Mathf.Max(0, line.orderedContainers - line.storedContainers);
            }
        }

        return total;
    }

    private int CountItemContainers(ItemData item, params RestockOrderState[] states)
    {
        if (item == null)
            return 0;

        int total = 0;
        for (int o = 0; o < orders.Count; o++)
        {
            RestockOrderSaveData order = orders[o];
            if (order == null || !HasState(order.state, states))
                continue;

            for (int l = 0; l < order.lines.Count; l++)
            {
                RestockOrderLineSaveData line = order.lines[l];
                if (Matches(line, item))
                    total += Mathf.Max(0, line.orderedContainers - line.storedContainers);
            }
        }

        return total;
    }

    private static bool HasState(RestockOrderState state, RestockOrderState[] states)
    {
        if (states == null)
            return false;

        for (int i = 0; i < states.Length; i++)
        {
            if (state == states[i])
                return true;
        }

        return false;
    }

    private static void RefreshStoredState(RestockOrderSaveData order)
    {
        if (order?.lines == null)
            return;

        bool anyStored = false;
        bool allStored = true;
        for (int i = 0; i < order.lines.Count; i++)
        {
            RestockOrderLineSaveData line = order.lines[i];
            if (line == null)
                continue;

            anyStored |= line.storedContainers > 0;
            allStored &= line.storedContainers >= line.orderedContainers;
        }

        order.state = allStored
            ? RestockOrderState.Stored
            : anyStored
                ? RestockOrderState.PartiallyStored
                : RestockOrderState.Collected;
    }

    private static bool ReservesCapacity(RestockOrderSaveData order)
    {
        return order != null &&
               order.state != RestockOrderState.Stored &&
               order.state != RestockOrderState.Cancelled;
    }

    private static bool Matches(RestockOrderLineSaveData line, ItemData item)
    {
        if (line == null || item == null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.itemID))
        {
            return string.Equals(
                line.itemID,
                item.StableItemId,
                StringComparison.OrdinalIgnoreCase);
        }

        // Older saves did not have a stable ID, so ItemType remains a legacy
        // fallback only for those records.
        return line.itemType == item.itemType;
    }

    private static RestockOrderSaveData CloneOrder(RestockOrderSaveData source)
    {
        RestockOrderSaveData clone = new RestockOrderSaveData
        {
            orderID = source?.orderID ?? string.Empty,
            restaurantID = source?.restaurantID ?? string.Empty,
            createdUtcTicks = source?.createdUtcTicks ?? 0L,
            deliveryReadyUtcTicks = source?.deliveryReadyUtcTicks ?? 0L,
            totalCost = source != null ? Mathf.Max(0, source.totalCost) : 0,
            state = source?.state ?? RestockOrderState.Ordered,
            deliveryNoticeShown = source != null && source.deliveryNoticeShown
        };

        if (source?.lines != null)
        {
            for (int i = 0; i < source.lines.Count; i++)
            {
                RestockOrderLineSaveData line = source.lines[i];
                if (line == null)
                    continue;

                clone.lines.Add(new RestockOrderLineSaveData
                {
                    itemID = line.itemID,
                    itemType = line.itemType,
                    orderedContainers = Mathf.Max(0, line.orderedContainers),
                    storedContainers = Mathf.Clamp(
                        line.storedContainers,
                        0,
                        Mathf.Max(0, line.orderedContainers))
                });
            }
        }

        return clone;
    }
}
