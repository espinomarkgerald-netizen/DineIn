using UnityEngine;
using System;
using System.Collections.Generic;


public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Setup")]
    [SerializeField] private List<ItemData> items; // assign in inspector

    private Dictionary<ItemType, int> inventory = new Dictionary<ItemType, int>();

    [Header("Inspector-Friendly Stock")]
    [SerializeField] private List<InventoryEntry> inspectorInventory = new List<InventoryEntry>();

    public event Action<ItemType, int> OnStockChanged;

    void Awake()
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

    void Start()
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
            if (!inventory.ContainsKey(item.itemType))
                inventory[item.itemType] = 0;
        }

        UpdateInspectorInventory();
    }

    private void UpdateInspectorInventory()
    {
        inspectorInventory.Clear();
        foreach (var kvp in inventory)
            inspectorInventory.Add(new InventoryEntry { itemType = kvp.Key, stock = kvp.Value });
    }

    public void AddStock(ItemType type, int amount)
    {
        if (!inventory.ContainsKey(type))
            inventory[type] = 0;

        inventory[type] += amount;
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
    }

    public bool UseStock(ItemType type, int amount)
    {
        if (!inventory.ContainsKey(type) || inventory[type] < amount)
            return false;

        inventory[type] -= amount;
        OnStockChanged?.Invoke(type, inventory[type]);
        UpdateInspectorInventory();
        return true;
    }

    public int GetStock(ItemType type)
    {
        return inventory.ContainsKey(type) ? inventory[type] : 0;
    }

    public List<ItemData> Items => items; // expose items for UI
}