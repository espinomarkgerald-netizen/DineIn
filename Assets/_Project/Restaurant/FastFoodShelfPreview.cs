using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Read-only stock presentation. Slots contain art only; inventory remains in the shared ledger.</summary>
[DisallowMultipleComponent]
public sealed class FastFoodShelfPreview : MonoBehaviour
{
    [SerializeField] private RestockStorageType storageType;
    [Tooltip("Authored art-only boxes at shelf positions. No draggable containers, colliders or stock identities.")]
    [SerializeField] private GameObject[] slots;
    private readonly Dictionary<string, int> assignments = new();
    private readonly HashSet<string> visible = new();
    private readonly List<RestockStoredContainerSaveData> eligible = new();
    private RestockOrderManager ledger;
    private InventoryManager inventory;
    private TMP_Text[][] labels;
    private bool dirty = true;

    private void OnEnable() { dirty = true; }
    private void OnDisable()
    {
        if (ledger != null) ledger.StoredContainersChanged -= MarkDirty;
        if (inventory != null) inventory.OnStockChanged -= StockChanged;
        ledger = null; inventory = null;
    }
    private void MarkDirty() => dirty = true;
    private void StockChanged(ItemType item, int count) => dirty = true;
    private void Update()
    {
        if (gameObject.scene.name != "Lobby2") return;
        if (ledger != RestockOrderManager.Instance || inventory != InventoryManager.Instance)
        {
            if (ledger != null) ledger.StoredContainersChanged -= MarkDirty;
            if (inventory != null) inventory.OnStockChanged -= StockChanged;
            ledger = RestockOrderManager.Instance; inventory = InventoryManager.Instance;
            if (ledger != null) ledger.StoredContainersChanged += MarkDirty;
            if (inventory != null) inventory.OnStockChanged += StockChanged;
            dirty = true;
        }
        if (!dirty || ledger == null || inventory == null || slots == null) return;
        dirty = false;
        if (labels == null)
        {
            labels = new TMP_Text[slots.Length][];
            for (int i = 0; i < slots.Length; i++)
                labels[i] = slots[i] != null ? slots[i].GetComponentsInChildren<TMP_Text>(true) : new TMP_Text[0];
        }
        visible.Clear(); eligible.Clear();
        foreach (var entry in ledger.StoredContainers)
            if (entry != null && entry.storageType == storageType && !string.IsNullOrEmpty(entry.containerID) &&
                inventory.TryGetBatch(entry.stockBatchID, out var batch) && batch.unitsRemaining > 0 && visible.Add(entry.containerID))
                eligible.Add(entry);
        foreach (string id in new List<string>(assignments.Keys))
            if (!visible.Contains(id)) assignments.Remove(id);
        foreach (var entry in eligible)
        {
            if (assignments.ContainsKey(entry.containerID)) continue;
            int slot = 0;
            while (slot < slots.Length && assignments.ContainsValue(slot)) slot++;
            if (slot == slots.Length) break;
            assignments.Add(entry.containerID, slot);
        }
        for (int i = 0; i < slots.Length; i++) if (slots[i] != null) slots[i].SetActive(false);
        var catalog = MenuCatalog.ForScene("Lobby2");
        foreach (var entry in eligible)
        {
            if (!assignments.TryGetValue(entry.containerID, out int slot) || slots[slot] == null) continue;
            string title = entry.itemType.ToString();
            if (catalog != null) foreach (var item in catalog.Ingredients)
                if (item != null && item.itemType == entry.itemType) { title = item.displayName; break; }
            foreach (var label in labels[slot]) if (label != null) { label.text = title; label.raycastTarget = false; }
            slots[slot].SetActive(true);
        }
    }
}
