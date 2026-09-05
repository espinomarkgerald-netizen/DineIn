using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        : Mathf.Max(0, quantity) * CasualDiningPolishManager.GetCurrentBoxCostOrBase(item);
}

[Serializable]
public sealed class RestockOrderLineSaveData
{
    public string itemID;
    public ItemType itemType;
    public int orderedContainers;
    public int storedContainers;
    public int unitCost;
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
    [SerializeField] private List<RestockStoredContainerSaveData> storedContainers =
        new List<RestockStoredContainerSaveData>();

    [Header("Delivery")]
    [SerializeField, Min(1f)] private float deliveryDelaySeconds = 5f;

    public event Action OrdersChanged;
    public event Action<RestockOrderSaveData> OrderDelivered;
    public IReadOnlyList<RestockOrderSaveData> Orders => orders;
    public IReadOnlyList<RestockStoredContainerSaveData> StoredContainers => storedContainers;

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
                storedContainers = 0,
                unitCost = CasualDiningPolishManager.GetCurrentBoxCostOrBase(cartLine.item)
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
        // Product identity comes from the current restaurant, never a persistent
        // inventory list captured in an earlier scene. Quantities stay in this ledger.
        IReadOnlyList<ItemData> catalog = CurrentCatalog?.Ingredients;
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
                bool wrongStorage = shelfStorage != item.requiredStorage;
                InventoryManager.Instance.UpdateBatchStorage(
                    stockBatchID,
                    shelfStorage,
                    wrongStorage,
                    Mathf.Max(1f, item.wrongStorageSpoilageMultiplier));
                if (InventoryManager.Instance.TryGetBatch(
                        stockBatchID,
                        out InventoryStockBatchSaveEntry storedBatch))
                {
                    expiresDay = storedBatch.expiresDay;
                }
                RefreshStoredState(order);
                OrdersChanged?.Invoke();
                GameSaveManager.Instance?.RequestSave();
                message = item.displayName + " stored (" +
                          Mathf.Max(1, item.unitsPerBox) + " units added)." +
                          (wrongStorage
                              ? " WARNING: wrong storage is accelerating spoilage."
                              : string.Empty);
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

    public int GetContainersInStates(ItemData item, params RestockOrderState[] states)
    {
        return CountItemContainers(item, states);
    }

    public int GetContainerCountInStates(params RestockOrderState[] states)
    {
        return CountOutstandingContainers(states);
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

        data.restockStoredContainers.Clear();
        for (int i = 0; i < storedContainers.Count; i++)
        {
            RestockStoredContainerSaveData entry = storedContainers[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.containerID))
                data.restockStoredContainers.Add(CloneStoredContainer(entry));
        }
    }

    public void ApplySaveData(GameSaveData data)
    {
        orders.Clear();
        storedContainers.Clear();
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

        if (data?.restockStoredContainers != null)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.restockStoredContainers.Count; i++)
            {
                RestockStoredContainerSaveData source = data.restockStoredContainers[i];
                if (source == null || string.IsNullOrWhiteSpace(source.containerID) ||
                    !ids.Add(source.containerID))
                    continue;
                storedContainers.Add(CloneStoredContainer(source));
            }
        }

        OrdersChanged?.Invoke();
    }

    public void RegisterPhysicalContainer(
        RestockStorageContainer identity,
        ShelfGrid grid,
        int column,
        int row,
        float rotationY)
    {
        if (identity == null || identity.Item == null || grid == null ||
            string.IsNullOrWhiteSpace(identity.StockBatchID))
            return;

        RestockStoredContainerSaveData entry = FindStoredContainer(identity.ContainerID);
        if (entry == null)
        {
            entry = new RestockStoredContainerSaveData
            {
                containerID = identity.ContainerID
            };
            storedContainers.Add(entry);
        }

        entry.stockBatchID = identity.StockBatchID;
        entry.itemID = identity.Item.StableItemId;
        entry.itemType = identity.Item.itemType;
        entry.shelfID = grid.StableShelfId;
        entry.column = Mathf.Max(0, column);
        entry.row = Mathf.Max(0, row);
        entry.rotationY = rotationY;
        entry.storageType = grid.StorageType;
        entry.wrongStorage = grid.StorageType != identity.Item.requiredStorage;
        GameSaveManager.Instance?.RequestSave();
    }

    public void RemovePhysicalContainer(string containerID)
    {
        if (string.IsNullOrWhiteSpace(containerID))
            return;
        int removed = storedContainers.RemoveAll(entry =>
            entry != null && string.Equals(
                entry.containerID,
                containerID,
                StringComparison.Ordinal));
        if (removed > 0)
            GameSaveManager.Instance?.RequestSave();
    }

    public int RestorePhysicalContainers(
        Scene scene,
        IReadOnlyList<ShelfGrid> grids,
        out int relocatedCount)
    {
        relocatedCount = 0;
        if (!scene.IsValid() || !scene.isLoaded || grids == null || grids.Count == 0 ||
            InventoryManager.Instance == null)
            return storedContainers.Count;

        Dictionary<string, RestockStorageContainer> existing =
            new Dictionary<string, RestockStorageContainer>(StringComparer.Ordinal);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            RestockStorageContainer[] found =
                roots[r].GetComponentsInChildren<RestockStorageContainer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                RestockStorageContainer identity = found[i];
                if (identity != null && !string.IsNullOrWhiteSpace(identity.ContainerID))
                    existing[identity.ContainerID] = identity;
            }
        }

        int recoveryCount = 0;
        bool repairedLegacyRotations = false;
        for (int i = storedContainers.Count - 1; i >= 0; i--)
        {
            RestockStoredContainerSaveData entry = storedContainers[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.containerID))
            {
                storedContainers.RemoveAt(i);
                continue;
            }

            InventoryStockBatchSaveEntry batch = null;
            if (!string.IsNullOrWhiteSpace(entry.stockBatchID) &&
                !InventoryManager.Instance.TryGetBatch(entry.stockBatchID, out batch))
            {
                // The exact batch was consumed or discarded; its physical box
                // must not be recreated on a later room visit.
                storedContainers.RemoveAt(i);
                continue;
            }

            if (existing.ContainsKey(entry.containerID))
                continue;

            ItemData item = FindCatalogItem(entry.itemID, entry.itemType);
            if (item == null || item.worldContainerPrefab == null)
            {
                recoveryCount++;
                continue;
            }

            ShelfGrid grid = FindGrid(grids, entry.shelfID);
            int column = entry.column;
            int row = entry.row;
            if (grid == null || !grid.IsCellFree(column, row))
            {
                grid = FindRecoveryGrid(grids, entry.storageType, out column, out row);
                if (grid == null)
                {
                    recoveryCount++;
                    continue;
                }
                relocatedCount++;
                entry.shelfID = grid.StableShelfId;
                entry.column = column;
                entry.row = row;
                entry.storageType = grid.StorageType;
                entry.wrongStorage = grid.StorageType != item.requiredStorage;
            }

            GameObject box = Instantiate(
                item.worldContainerPrefab,
                grid.GetCellWorldPosition(column, row),
                item.worldContainerPrefab.transform.rotation);
            float authoredRotationY = item.worldContainerPrefab.transform.eulerAngles.y;
            if (!Mathf.Approximately(entry.rotationY, authoredRotationY))
            {
                // Older saves stored only Y and accidentally erased the prefab's
                // authored X/Z orientation, which made boxes reappear sideways.
                entry.rotationY = authoredRotationY;
                repairedLegacyRotations = true;
            }
            SceneManager.MoveGameObjectToScene(box, scene);
            RestockStorageContainer identity = box.GetComponent<RestockStorageContainer>();
            if (identity == null)
                identity = box.AddComponent<RestockStorageContainer>();
            int expiresDay = batch != null ? batch.expiresDay : 0;
            identity.Bind(
                item,
                entry.stockBatchID,
                expiresDay,
                entry.containerID,
                entry.storageType,
                entry.wrongStorage);
            DraggableStorageBox draggable = box.GetComponent<DraggableStorageBox>();
            if (draggable == null)
                draggable = box.AddComponent<DraggableStorageBox>();
            if (!draggable.TryPlaceInitially(grid, column, row))
            {
                Destroy(box);
                recoveryCount++;
                continue;
            }
            existing[entry.containerID] = identity;
        }

        if (relocatedCount > 0 || repairedLegacyRotations)
            GameSaveManager.Instance?.RequestSave();
        return recoveryCount;
    }

    public void ClearAll()
    {
        orders.Clear();
        storedContainers.Clear();
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
                    unitCost = Mathf.Max(0, line.unitCost),
                    storedContainers = Mathf.Clamp(
                        line.storedContainers,
                        0,
                        Mathf.Max(0, line.orderedContainers))
                });
            }
        }

        return clone;
    }

    private RestockStoredContainerSaveData FindStoredContainer(string containerID)
    {
        if (string.IsNullOrWhiteSpace(containerID))
            return null;
        for (int i = 0; i < storedContainers.Count; i++)
        {
            RestockStoredContainerSaveData entry = storedContainers[i];
            if (entry != null && string.Equals(
                    entry.containerID,
                    containerID,
                    StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private static RestockStoredContainerSaveData CloneStoredContainer(
        RestockStoredContainerSaveData source)
    {
        return source == null ? null : new RestockStoredContainerSaveData
        {
            containerID = source.containerID,
            stockBatchID = source.stockBatchID,
            itemID = source.itemID,
            itemType = source.itemType,
            shelfID = source.shelfID,
            column = Mathf.Max(0, source.column),
            row = Mathf.Max(0, source.row),
            rotationY = source.rotationY,
            storageType = source.storageType,
            wrongStorage = source.wrongStorage
        };
    }

    private static MenuCatalog CurrentCatalog => RestockFlowCoordinator.Instance != null
        ? RestockFlowCoordinator.Instance.RestaurantCatalog
        : MenuCatalog.Default;

    private static ItemData FindCatalogItem(string itemID, ItemType itemType)
    {
        IReadOnlyList<ItemData> items = CurrentCatalog?.Ingredients;
        if (items == null)
            return null;
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;
            if (!string.IsNullOrWhiteSpace(itemID) && string.Equals(
                    item.StableItemId,
                    itemID,
                    StringComparison.OrdinalIgnoreCase))
                return item;
            if (string.IsNullOrWhiteSpace(itemID) && item.itemType == itemType)
                return item;
        }
        return null;
    }

    private static ShelfGrid FindGrid(IReadOnlyList<ShelfGrid> grids, string shelfID)
    {
        if (string.IsNullOrWhiteSpace(shelfID))
            return null;
        for (int i = 0; i < grids.Count; i++)
        {
            ShelfGrid grid = grids[i];
            if (grid != null && string.Equals(
                    grid.StableShelfId,
                    shelfID,
                    StringComparison.Ordinal))
                return grid;
        }
        return null;
    }

    private static ShelfGrid FindRecoveryGrid(
        IReadOnlyList<ShelfGrid> grids,
        RestockStorageType preferredStorage,
        out int column,
        out int row)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < grids.Count; i++)
            {
                ShelfGrid grid = grids[i];
                if (grid == null || (pass == 0 && grid.StorageType != preferredStorage))
                    continue;
                if (grid.TryGetFirstFreeCell(out column, out row))
                    return grid;
            }
        }
        column = -1;
        row = -1;
        return null;
    }
}
