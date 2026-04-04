using System.Collections.Generic;
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
        UnlockManager.OnIngredientUnlocked += _ => RebuildShop();
    }

    private void OnDisable()
    {
        UnlockManager.OnIngredientUnlocked -= _ => RebuildShop();
    }

    private void Start()
    {
        RebuildShop();
    }

    public void RebuildShop()
    {
        if (!contentParent) return;

        // Clear previous UI
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        spawnedItems.Clear();

        if (itemList == null || itemList.Count == 0) return;

        // Sort unlocked first
        var sorted = new List<ItemData>(itemList);
        sorted.Sort((a, b) =>
        {
            bool aUnlocked = UnlockManager.Instance?.IsIngredientUnlocked(a) ?? false;
            bool bUnlocked = UnlockManager.Instance?.IsIngredientUnlocked(b) ?? false;

            // 1. Unlocked first
            int unlockCompare = bUnlocked.CompareTo(aUnlocked);
            if (unlockCompare != 0)
                return unlockCompare;

            // 2. Then sort by day
            return a.dayToUnlock.CompareTo(b.dayToUnlock);
        });

        foreach (var item in sorted)
        {
            bool unlocked = UnlockManager.Instance?.IsIngredientUnlocked(item) ?? false;
            GameObject prefab = unlocked ? unlockedPrefab : lockedPrefab;
            if (!prefab) continue;

            GameObject obj = Instantiate(prefab, contentParent);
            if (obj.TryGetComponent(out ShopItemUI ui))
            {
                ui.Setup(item, unlocked);
                if (checkoutManager != null)
                    ui.OnQuantityChanged += checkoutManager.UpdateTotalCost;
                spawnedItems.Add(ui);
            }
        }

        checkoutManager?.UpdateTotalCost();
    }
}