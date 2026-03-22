using UnityEngine;
using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [SerializeField] private List<ItemData> items;

    private Dictionary<ItemType, int> inventory = new Dictionary<ItemType, int>();

    // Event to notify UI
    public event Action<ItemType, int> OnStockChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeInventory();
    }

    void InitializeInventory()
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("InventoryManager: No items assigned.");
            return;
        }

        foreach (var item in items)
        {
            if (!inventory.ContainsKey(item.itemType))
            {
                inventory[item.itemType] = 0;
            }
        }
    }

    public void AddStock(ItemType type, int amount)
    {
        if (!inventory.ContainsKey(type))
        {
            inventory[type] = 0;
        }

        inventory[type] += amount;

        // Notify UI
        OnStockChanged?.Invoke(type, inventory[type]);
    }

    public bool UseStock(ItemType type, int amount)
    {
        if (!inventory.ContainsKey(type) || inventory[type] < amount)
            return false;

        inventory[type] -= amount;

        // Notify UI
        OnStockChanged?.Invoke(type, inventory[type]);
        return true;
    }

    public int GetStock(ItemType type)
    {
        if (!inventory.ContainsKey(type))
            return 0;

        return inventory[type];
    }
}