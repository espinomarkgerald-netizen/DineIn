using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject unlockedPrefab;
    [SerializeField] private GameObject lockedPrefab;
    [SerializeField] private List<ItemData> itemList;
    [SerializeField] private ShopCheckoutManager checkoutManager;

    private readonly List<ShopItemUI> spawnedItems = new List<ShopItemUI>();
    private int lastKnownDay = -1;

    public IReadOnlyList<ShopItemUI> GetSpawnedItems() => spawnedItems;

    private void Awake()
    {
        if (!contentParent || !unlockedPrefab || !lockedPrefab)
            Debug.LogWarning($"[ShopManager] Missing references in {name}");

        if (!checkoutManager)
            Debug.LogWarning($"[ShopManager] CheckoutManager not assigned in {name}");
    }

    private void OnEnable()
    {
        UnlockManager.OnIngredientUnlocked += HandleIngredientUnlocked;
        lastKnownDay = GetCurrentDaySafe();
        RebuildShop();
    }

    private void OnDisable()
    {
        UnlockManager.OnIngredientUnlocked -= HandleIngredientUnlocked;
    }

    private void Update()
    {
        int currentDay = GetCurrentDaySafe();
        if (currentDay != lastKnownDay)
        {
            lastKnownDay = currentDay;
            RebuildShop();
        }
    }

    private void HandleIngredientUnlocked(ItemData item)
    {
        RebuildShop();
    }

    public void RefreshShopNow()
    {
        lastKnownDay = GetCurrentDaySafe();
        RebuildShop();
    }

    public void RebuildShop()
    {
        if (!contentParent)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        spawnedItems.Clear();

        if (itemList == null || itemList.Count == 0)
        {
            checkoutManager?.UpdateTotalCost();
            return;
        }

        int currentDay = GetCurrentDaySafe();

        var sorted = new List<ItemData>(itemList);
        sorted.Sort((a, b) =>
        {
            bool aAvailable = IsItemAvailable(a, currentDay);
            bool bAvailable = IsItemAvailable(b, currentDay);

            int availableCompare = bAvailable.CompareTo(aAvailable);
            if (availableCompare != 0)
                return availableCompare;

            int dayCompare = a.dayToUnlock.CompareTo(b.dayToUnlock);
            if (dayCompare != 0)
                return dayCompare;

            string aName = a != null ? a.displayName : string.Empty;
            string bName = b != null ? b.displayName : string.Empty;
            return string.Compare(aName, bName, StringComparison.Ordinal);
        });

        foreach (var item in sorted)
        {
            if (item == null)
                continue;

            bool available = IsItemAvailable(item, currentDay);
            GameObject prefab = available ? unlockedPrefab : lockedPrefab;

            if (!prefab)
                continue;

            GameObject obj = Instantiate(prefab, contentParent);

            if (obj.TryGetComponent(out ShopItemUI ui))
            {
                ui.Setup(item, available);

                if (checkoutManager != null)
                    ui.OnQuantityChanged += checkoutManager.UpdateTotalCost;

                spawnedItems.Add(ui);
            }
        }

        checkoutManager?.UpdateTotalCost();
    }

    private bool IsItemAvailable(ItemData item, int currentDay)
    {
        if (item == null)
            return false;

        bool manuallyUnlocked = UnlockManager.Instance != null && UnlockManager.Instance.IsIngredientUnlocked(item);
        bool unlockedByDay = currentDay >= item.dayToUnlock;

        return manuallyUnlocked || unlockedByDay;
    }

    private int GetCurrentDaySafe()
    {
        if (GameFlowManager.Instance == null)
            return 1;

        object manager = GameFlowManager.Instance;
        Type type = manager.GetType();

        string[] propertyNames =
        {
            "CurrentDay",
            "currentDay",
            "Day",
            "CurrentDayIndex"
        };

        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int))
                return Mathf.Max(1, (int)property.GetValue(manager));
        }

        string[] fieldNames =
        {
            "currentDay",
            "CurrentDay",
            "day",
            "currentDayIndex"
        };

        foreach (string fieldName in fieldNames)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
                return Mathf.Max(1, (int)field.GetValue(manager));
        }

        return 1;
    }
}