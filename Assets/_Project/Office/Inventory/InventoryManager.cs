using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Setup")]
    [SerializeField] private List<ItemData> items;

    private Dictionary<ItemType, int> inventory = new Dictionary<ItemType, int>();
    private readonly List<InventoryStockBatchSaveEntry> stockBatches =
        new List<InventoryStockBatchSaveEntry>();

    [Header("Inspector-Friendly Stock")]
    [SerializeField] private List<InventoryEntry> inspectorInventory = new List<InventoryEntry>();

    public event Action<ItemType, int> OnStockChanged;
    public int DiscardedUnitsToday { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            MenuCatalog activeCatalog = MenuCatalog.Default;
            if (activeCatalog != null && activeCatalog.Ingredients.Count > 0)
                items = new List<ItemData>(activeCatalog.Ingredients);
            InitializeInventory();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        foreach (var kvp in inventory)
            OnStockChanged?.Invoke(kvp.Key, kvp.Value);
    }

    private void InitializeInventory()
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("InventoryManager: No items assigned.");
            return;
        }

        foreach (var item in items)
        {
            if (item == null)
                continue;

            if (!inventory.ContainsKey(item.itemType))
            {
                // A new restaurant starts with one box of every configured
                // ingredient. Existing save data still replaces these values.
                int amount = Mathf.Max(1, item.unitsPerBox);
                inventory[item.itemType] = amount;
                CreateBatch(item, amount, CurrentDay, out _, out _);
            }
        }

        UpdateInspectorInventory();
    }

    private void UpdateInspectorInventory()
    {
        inspectorInventory.Clear();

        foreach (var kvp in inventory)
        {
            inspectorInventory.Add(new InventoryEntry
            {
                itemType = kvp.Key,
                stock = kvp.Value
            });
        }
    }

    public void AddStock(ItemType type, int amount)
    {
        if (amount <= 0)
            return;

        ItemData item = FindItem(type);
        if (item != null)
        {
            AddStockBatch(item, amount, CurrentDay, out _, out _);
            return;
        }

        if (!inventory.ContainsKey(type))
            inventory[type] = 0;

        inventory[type] += amount;
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
    }

    public void AddStockBatch(
        ItemData item,
        int amount,
        int receivedDay,
        out string batchID,
        out int expiresDay)
    {
        batchID = string.Empty;
        expiresDay = 0;
        if (item == null || amount <= 0)
            return;

        if (!inventory.ContainsKey(item.itemType))
            inventory[item.itemType] = 0;

        inventory[item.itemType] += amount;
        CreateBatch(item, amount, receivedDay, out batchID, out expiresDay);
        OnStockChanged?.Invoke(item.itemType, inventory[item.itemType]);
        UpdateInspectorInventory();
    }

    public bool UseStock(ItemType type, int amount)
    {
        if (amount <= 0)
            return false;

        if (!inventory.ContainsKey(type) || inventory[type] < amount)
            return false;

        inventory[type] -= amount;
        ConsumeBatches(type, amount);
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
        return true;
    }

    public int GetStock(ItemType type)
    {
        return inventory.TryGetValue(type, out int stock) ? stock : 0;
    }

    public bool IsTracked(ItemType type)
    {
        return inventory.ContainsKey(type);
    }

    public List<ItemData> Items => items;

    public bool HasExpiredStock(int currentDay)
    {
        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch != null && batch.unitsRemaining > 0 && batch.expiresDay <= currentDay)
                return true;
        }

        return false;
    }

    public int GetExpiredStock(ItemType type, int currentDay)
    {
        int total = 0;
        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch != null && batch.itemType == type &&
                batch.expiresDay <= currentDay)
                total += Mathf.Max(0, batch.unitsRemaining);
        }

        return total;
    }

    public int GetNextExpiryDay(ItemType type)
    {
        int result = int.MaxValue;
        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch != null && batch.itemType == type && batch.unitsRemaining > 0)
                result = Mathf.Min(result, batch.expiresDay);
        }

        return result == int.MaxValue ? 0 : result;
    }

    public int GetFreshStock(ItemType type, int currentDay)
    {
        return Mathf.Max(0, GetStock(type) - GetExpiredStock(type, currentDay));
    }

    public int GetNextFreshExpiryDay(ItemType type, int currentDay)
    {
        int result = int.MaxValue;
        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch != null && batch.itemType == type &&
                batch.unitsRemaining > 0 && batch.expiresDay > currentDay)
            {
                result = Mathf.Min(result, batch.expiresDay);
            }
        }

        return result == int.MaxValue ? 0 : result;
    }

    public bool TryGetBatch(
        string batchID,
        out InventoryStockBatchSaveEntry result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(batchID))
            return false;

        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch != null && string.Equals(
                    batch.batchID,
                    batchID,
                    StringComparison.Ordinal))
            {
                result = batch;
                return true;
            }
        }

        return false;
    }

    public int DiscardContainerStock(
        ItemType type,
        int maximumUnits,
        string batchID = null)
    {
        if (maximumUnits <= 0 || GetStock(type) <= 0)
            return 0;

        int discarded;
        if (!string.IsNullOrWhiteSpace(batchID))
        {
            if (!TryGetBatch(batchID, out InventoryStockBatchSaveEntry batch) ||
                batch.itemType != type)
                return 0;

            discarded = Mathf.Min(
                Mathf.Min(maximumUnits, batch.unitsRemaining),
                GetStock(type));
            batch.unitsRemaining -= discarded;
        }
        else
        {
            discarded = Mathf.Min(maximumUnits, GetStock(type));
            ConsumeBatches(type, discarded);
        }

        if (discarded <= 0)
            return 0;

        inventory[type] = Mathf.Max(0, inventory[type] - discarded);
        DiscardedUnitsToday += discarded;
        RemoveEmptyBatches();
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
        GameSaveManager.Instance?.RequestSave();
        return discarded;
    }

    public void ResetDiscardedUnitsForNewDay()
    {
        DiscardedUnitsToday = 0;
    }

    public bool UpdateBatchStorage(
        string batchID,
        RestockStorageType storageType,
        bool wrongStorage,
        float wrongStorageMultiplier)
    {
        if (!TryGetBatch(batchID, out InventoryStockBatchSaveEntry batch))
            return false;

        int currentDay = CurrentDay;
        if (wrongStorage && !batch.wrongStorage)
        {
            int remainingDays = Mathf.Max(1, batch.expiresDay - currentDay);
            int acceleratedDays = Mathf.Max(1,
                Mathf.CeilToInt(remainingDays / Mathf.Max(1f, wrongStorageMultiplier)));
            batch.expiresDay = Mathf.Min(batch.expiresDay, currentDay + acceleratedDays);
        }

        bool changed = batch.currentStorage != storageType ||
                       batch.wrongStorage != wrongStorage;
        batch.currentStorage = storageType;
        batch.wrongStorage = wrongStorage;
        if (changed)
            GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public void ConfigureItems(List<ItemData> configuredItems)
    {
        items = configuredItems != null ? new List<ItemData>(configuredItems) : new List<ItemData>();
        inventory.Clear();
        stockBatches.Clear();
        InitializeInventory();
    }

    public void ResetStock()
    {
        SetAllStock(0);
    }

    public void SetAllStock(int amount)
    {
        amount = Mathf.Max(0, amount);
        stockBatches.Clear();
        foreach (var key in inventory.Keys.ToArray())
        {
            inventory[key] = amount;
            ItemData item = FindItem(key);
            if (item != null && amount > 0)
                CreateBatch(item, amount, CurrentDay, out _, out _);
        }

        UpdateInspectorInventory();

        foreach (var kvp in inventory)
            OnStockChanged?.Invoke(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// One-time compatibility migration for saves created before restaurant
    /// products consumed inventory. Only empty items receive one starter box.
    /// </summary>
    public void EnsureStarterStockForFiniteInventory()
    {
        if (items == null)
            return;

        List<ItemType> changedItems = new List<ItemType>();
        foreach (ItemData item in items)
        {
            if (item == null)
                continue;

            if (!inventory.TryGetValue(item.itemType, out int current) || current <= 0)
            {
                int amount = Mathf.Max(1, item.unitsPerBox);
                inventory[item.itemType] = amount;
                CreateBatch(item, amount, CurrentDay, out _, out _);
                changedItems.Add(item.itemType);
            }
        }

        UpdateInspectorInventory();
        for (int i = 0; i < changedItems.Count; i++)
            OnStockChanged?.Invoke(changedItems[i], inventory[changedItems[i]]);
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.inventoryStocks.Clear();
        data.inventoryStockBatches.Clear();
        data.inventorySystemVersion = 2;
        data.discardedUnitsToday = Mathf.Max(0, DiscardedUnitsToday);

        foreach (var kvp in inventory)
        {
            data.inventoryStocks.Add(new InventorySaveEntry
            {
                itemType = kvp.Key,
                stock = kvp.Value
            });
        }


        for (int i = 0; i < stockBatches.Count; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch == null || batch.unitsRemaining <= 0)
                continue;

            data.inventoryStockBatches.Add(CloneBatch(batch));
        }
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        inventory.Clear();
        stockBatches.Clear();
        DiscardedUnitsToday = Mathf.Max(0, data.discardedUnitsToday);

        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (!inventory.ContainsKey(item.itemType))
                    inventory[item.itemType] = 0;
            }
        }

        if (data.inventoryStocks != null)
        {
            foreach (var entry in data.inventoryStocks)
            {
                inventory[entry.itemType] = Mathf.Max(0, entry.stock);
            }
        }


        if (data.inventoryStockBatches != null)
        {
            for (int i = 0; i < data.inventoryStockBatches.Count; i++)
            {
                InventoryStockBatchSaveEntry source = data.inventoryStockBatches[i];
                if (source == null || source.unitsRemaining <= 0)
                    continue;

                InventoryStockBatchSaveEntry clone = CloneBatch(source);
                if (data.saveSchemaVersion < 3)
                {
                    ItemData legacyItem = FindItem(clone.itemType);
                    if (legacyItem != null)
                        clone.currentStorage = legacyItem.requiredStorage;
                    clone.wrongStorage = false;
                }
                stockBatches.Add(clone);
            }
        }

        ReconcileBatchesToInventory(Mathf.Max(1, data.currentDay));

        UpdateInspectorInventory();

        foreach (var kvp in inventory)
            OnStockChanged?.Invoke(kvp.Key, kvp.Value);
    }

    private int CurrentDay => GameFlowManager.Instance != null
        ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
        : 1;

    private ItemData FindItem(ItemType type)
    {
        if (items == null)
            return null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemType == type)
                return items[i];
        }

        return null;
    }

    private void CreateBatch(
        ItemData item,
        int amount,
        int receivedDay,
        out string batchID,
        out int expiresDay)
    {
        receivedDay = Mathf.Max(1, receivedDay);
        batchID = Guid.NewGuid().ToString("N");
        expiresDay = receivedDay + Mathf.Max(1, Mathf.CeilToInt(item.shelfLifeDays));
        stockBatches.Add(new InventoryStockBatchSaveEntry
        {
            batchID = batchID,
            itemType = item.itemType,
            unitsRemaining = Mathf.Max(0, amount),
            receivedDay = receivedDay,
            expiresDay = expiresDay,
            wrongStorage = false,
            currentStorage = item.requiredStorage
        });
    }

    private void ConsumeBatches(ItemType type, int amount)
    {
        if (amount <= 0)
            return;

        stockBatches.Sort((a, b) =>
        {
            int aExpiry = a != null ? a.expiresDay : int.MaxValue;
            int bExpiry = b != null ? b.expiresDay : int.MaxValue;
            return aExpiry.CompareTo(bExpiry);
        });

        int remaining = amount;
        for (int i = 0; i < stockBatches.Count && remaining > 0; i++)
        {
            InventoryStockBatchSaveEntry batch = stockBatches[i];
            if (batch == null || batch.itemType != type || batch.unitsRemaining <= 0)
                continue;

            int used = Mathf.Min(remaining, batch.unitsRemaining);
            batch.unitsRemaining -= used;
            remaining -= used;
        }

        RemoveEmptyBatches();
    }

    private void ReconcileBatchesToInventory(int currentDay)
    {
        foreach (KeyValuePair<ItemType, int> entry in inventory)
        {
            int batchUnits = 0;
            for (int i = 0; i < stockBatches.Count; i++)
            {
                InventoryStockBatchSaveEntry batch = stockBatches[i];
                if (batch != null && batch.itemType == entry.Key)
                    batchUnits += Mathf.Max(0, batch.unitsRemaining);
            }

            int missing = Mathf.Max(0, entry.Value - batchUnits);
            ItemData item = FindItem(entry.Key);
            if (missing > 0 && item != null)
                CreateBatch(item, missing, currentDay, out _, out _);
            else if (batchUnits > entry.Value)
                ConsumeBatches(entry.Key, batchUnits - entry.Value);
        }

        RemoveEmptyBatches();
    }

    private void RemoveEmptyBatches()
    {
        stockBatches.RemoveAll(batch => batch == null || batch.unitsRemaining <= 0);
    }

    private static InventoryStockBatchSaveEntry CloneBatch(
        InventoryStockBatchSaveEntry source)
    {
        return new InventoryStockBatchSaveEntry
        {
            batchID = string.IsNullOrWhiteSpace(source.batchID)
                ? Guid.NewGuid().ToString("N")
                : source.batchID,
            itemType = source.itemType,
            unitsRemaining = Mathf.Max(0, source.unitsRemaining),
            receivedDay = Mathf.Max(1, source.receivedDay),
            expiresDay = Mathf.Max(1, source.expiresDay),
            wrongStorage = source.wrongStorage,
            currentStorage = source.currentStorage
        };
    }
}
