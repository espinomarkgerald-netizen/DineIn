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

    [Header("Inspector-Friendly Stock")]
    [SerializeField] private List<InventoryEntry> inspectorInventory = new List<InventoryEntry>();

    public event Action<ItemType, int> OnStockChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
                inventory[item.itemType] = 0;
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

        if (!inventory.ContainsKey(type))
            inventory[type] = 0;

        inventory[type] += amount;
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
    }

    public bool UseStock(ItemType type, int amount)
    {
        if (amount <= 0)
            return false;

        if (!inventory.ContainsKey(type) || inventory[type] < amount)
            return false;

        inventory[type] -= amount;
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

    public void ResetStock()
    {
        foreach (var key in inventory.Keys.ToArray())
            inventory[key] = 0;

        UpdateInspectorInventory();

        foreach (var kvp in inventory)
            OnStockChanged?.Invoke(kvp.Key, kvp.Value);
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.inventoryStocks.Clear();

        foreach (var kvp in inventory)
        {
            data.inventoryStocks.Add(new InventorySaveEntry
            {
                itemType = kvp.Key,
                stock = kvp.Value
            });
        }
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        inventory.Clear();

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

        UpdateInspectorInventory();

        foreach (var kvp in inventory)
            OnStockChanged?.Invoke(kvp.Key, kvp.Value);
    }
}