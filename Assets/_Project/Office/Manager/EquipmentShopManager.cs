using System.Collections.Generic;
using UnityEngine;

public class EquipmentShopManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject unlockedPrefab;
    [SerializeField] private GameObject lockedPrefab;
    [SerializeField] private List<Equipment> equipmentList;

    private readonly List<EquipmentItemUI> spawnedItems = new List<EquipmentItemUI>();
    public IReadOnlyList<EquipmentItemUI> GetSpawnedItems() => spawnedItems;

    private void Start()
    {
        InitializeShop();
    }

    private void OnEnable()
    {
        UnlockManager.OnEquipmentUnlocked += OnEquipmentUnlocked;
    }

    private void OnDisable()
    {
        UnlockManager.OnEquipmentUnlocked -= OnEquipmentUnlocked;
    }

    private void OnEquipmentUnlocked(string equipmentID)
    {
        Debug.Log($"OnEquipmentUnlocked event received for: {equipmentID}");
        RebuildShop();
    }

    /// <summary>
    /// Call this after EquipmentManager unlocks equipment for today
    /// </summary>
    public void InitializeShop()
    {
        RebuildShop();
    }

    public void RebuildShop()
    {
        Debug.Log($"EquipmentShopManager equipmentList count: {equipmentList?.Count}");
        if (!contentParent || equipmentList == null) return;

        // Debug: print total unlocked items before building
        int unlockedCount = 0;
        foreach (var equip in equipmentList)
        {
            if (equip == null) continue;
            bool isUnlocked = UnlockManager.Instance?.IsEquipmentUnlocked(equip.itemID) ?? false;
            Debug.Log($"Equipment {equip.displayName} unlocked? {isUnlocked}");
            if (isUnlocked) unlockedCount++;
        }
        Debug.Log($"Total unlocked equipment: {unlockedCount} / {equipmentList.Count}");

        // Clear previous UI
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        spawnedItems.Clear();

        if (equipmentList == null || equipmentList.Count == 0)
        {
            Debug.LogWarning("EquipmentShopManager: No equipment to display");
            return;
        }

        // Keep progression order stable even when ownership changes.
        var sorted = new List<Equipment>(equipmentList);
        sorted.RemoveAll(item => item == null);
        sorted.Sort(Equipment.CompareProgression);

        foreach (var equip in sorted)
        {
            bool unlocked = UnlockManager.Instance?.IsEquipmentUnlocked(equip.itemID) ?? false;

            GameObject prefab = unlocked ? unlockedPrefab : lockedPrefab;
            if (!prefab)
            {
                Debug.LogWarning($"Prefab missing for {(unlocked ? "unlocked" : "locked")} equipment {equip.displayName}");
                continue;
            }

            GameObject obj = Instantiate(prefab, contentParent);
            if (obj.TryGetComponent(out EquipmentItemUI ui))
            {
                ui.Setup(equip, unlocked);
                spawnedItems.Add(ui);
            }
            else
            {
                Debug.LogWarning($"EquipmentItemUI component missing on prefab for {equip.displayName}");
            }
        }

        Debug.Log($"EquipmentShopManager: spawned {spawnedItems.Count} items in UI");
    }
}
