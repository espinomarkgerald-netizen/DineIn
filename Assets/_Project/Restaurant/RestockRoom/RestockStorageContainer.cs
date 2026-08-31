using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime identity for a physical delivered box/crate.</summary>
public sealed class RestockStorageContainer : MonoBehaviour
{
    [SerializeField] private ItemData item;
    [SerializeField, HideInInspector] private string containerID;
    [SerializeField, HideInInspector] private string stockBatchID;
    [SerializeField, HideInInspector] private int expiresDay;
    [SerializeField, HideInInspector] private RestockStorageType currentStorage;
    [SerializeField, HideInInspector] private bool wrongStorage;
    [Header("Editable Box Label")]
    [SerializeField] private TMP_Text[] itemNameTexts;
    [SerializeField] private Image[] itemIcons;
    [Tooltip("Optional dedicated quantity labels. If left empty, the quantity is appended to the item-name labels.")]
    [SerializeField] private TMP_Text[] quantityTexts;
    [SerializeField] private string quantityPrefix = "x";
    [SerializeField] private bool appendQuantityToItemName = true;
    [SerializeField] private bool removeContainerWhenEmpty = true;
    [SerializeField, HideInInspector] private bool labelReferencesConfigured;
    [Header("Expiry Presentation")]
    [SerializeField] private string expiredLabel = "EXPIRED";
    [SerializeField] private Color expiredLabelColor = new Color(0.94f, 0.16f, 0.16f, 1f);
    private InventoryManager subscribedInventory;
    private bool emptyRemovalRequested;
    private bool stockBatchObserved;

    public ItemData Item => item;
    public string ContainerID
    {
        get
        {
            EnsureContainerID();
            return containerID;
        }
    }
    public string StockBatchID => stockBatchID;
    public int ExpiresDay => expiresDay;
    public RestockStorageType CurrentStorage => currentStorage;
    public bool WrongStorage => wrongStorage;
    public bool HasConfiguredLabels => labelReferencesConfigured;
    public int CurrentRemainingQuantity => ResolveRemainingQuantity(out _);

    private void OnEnable()
    {
        SubscribeToInventory();
    }

    private void Start()
    {
        SubscribeToInventory();
        if (item != null)
            RefreshExpiryState();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
    }

    public void ConfigureLabels(TMP_Text[] configuredNameTexts, Image[] configuredIcons)
    {
        itemNameTexts = configuredNameTexts;
        itemIcons = configuredIcons;
        labelReferencesConfigured = true;
    }

    public void ConfigureQuantityLabels(TMP_Text[] configuredQuantityTexts)
    {
        quantityTexts = configuredQuantityTexts;
    }

    public void Bind(ItemData configuredItem)
    {
        Bind(configuredItem, string.Empty, 0);
    }

    public void Bind(
        ItemData configuredItem,
        string configuredBatchID,
        int configuredExpiresDay)
    {
        Bind(
            configuredItem,
            configuredBatchID,
            configuredExpiresDay,
            containerID,
            configuredItem != null ? configuredItem.requiredStorage : RestockStorageType.Dry,
            false);
    }

    public void Bind(
        ItemData configuredItem,
        string configuredBatchID,
        int configuredExpiresDay,
        string configuredContainerID,
        RestockStorageType configuredStorage,
        bool configuredWrongStorage)
    {
        item = configuredItem;
        containerID = string.IsNullOrWhiteSpace(configuredContainerID)
            ? System.Guid.NewGuid().ToString("N")
            : configuredContainerID.Trim();
        string nextBatchID = configuredBatchID ?? string.Empty;
        if (!string.Equals(stockBatchID, nextBatchID, System.StringComparison.Ordinal))
            stockBatchObserved = false;
        stockBatchID = nextBatchID;
        expiresDay = Mathf.Max(0, configuredExpiresDay);
        currentStorage = configuredStorage;
        wrongStorage = configuredWrongStorage;
        emptyRemovalRequested = false;
        SubscribeToInventory();
        if (item == null)
            return;

        gameObject.name = item.displayName + " Storage Box";

        ResolveLabelReferences();
        for (int i = 0; i < itemIcons.Length; i++)
        {
            Image icon = itemIcons[i];
            if (icon == null)
                continue;

            icon.sprite = item.sprite;
            icon.enabled = item.sprite != null;
            icon.preserveAspect = true;
        }


        RefreshExpiryState();
    }

    public void UpdateStorageEnvironment(RestockStorageType storageType)
    {
        if (item == null)
            return;
        currentStorage = storageType;
        wrongStorage = storageType != item.requiredStorage;
        if (!string.IsNullOrWhiteSpace(stockBatchID) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.UpdateBatchStorage(
                stockBatchID,
                storageType,
                wrongStorage,
                Mathf.Max(1f, item.wrongStorageSpoilageMultiplier));
            if (InventoryManager.Instance.TryGetBatch(
                    stockBatchID,
                    out InventoryStockBatchSaveEntry batch))
            {
                expiresDay = batch.expiresDay;
            }
        }
        RefreshExpiryState();
    }

    public void RestoreStorageEnvironment(
        RestockStorageType storageType,
        bool configuredWrongStorage)
    {
        currentStorage = storageType;
        wrongStorage = configuredWrongStorage;
        RefreshExpiryState();
    }

    public bool TryResolveLegacyItem()
    {
        if (item != null)
            return true;

        ResolveLabelReferences();
        IReadOnlyList<ItemData> catalog = InventoryManager.Instance != null
            ? InventoryManager.Instance.Items
            : null;
        if (catalog == null)
            return false;

        for (int t = 0; t < itemNameTexts.Length; t++)
        {
            TMP_Text label = itemNameTexts[t];
            string labelName = label != null ? Normalize(label.text) : string.Empty;
            if (labelName.Length < 3)
                continue;

            for (int i = 0; i < catalog.Count; i++)
            {
                ItemData candidate = catalog[i];
                string candidateName = candidate != null
                    ? Normalize(candidate.displayName)
                    : string.Empty;
                if (candidateName.Length >= 3 &&
                    (candidateName.Contains(labelName) || labelName.Contains(candidateName)))
                {
                    item = candidate;
                    RefreshExpiryState();
                    return true;
                }
            }
        }

        return false;
    }

    public void RefreshExpiryState()
    {
        if (item == null)
            return;

        if (!string.IsNullOrWhiteSpace(stockBatchID) &&
            InventoryManager.Instance != null &&
            InventoryManager.Instance.TryGetBatch(
                stockBatchID,
                out InventoryStockBatchSaveEntry batch))
        {
            expiresDay = batch.expiresDay;
        }
        else if (expiresDay <= 0 && InventoryManager.Instance != null)
        {
            expiresDay = InventoryManager.Instance.GetNextExpiryDay(item.itemType);
        }

        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;
        bool expired = expiresDay > 0 && day >= expiresDay;
        string label = item.displayName;
        if (wrongStorage)
        {
            label += "\n<color=#FF9F1C><b>WRONG STORAGE</b></color>";
        }
        if (expired)
        {
            label += "\n<color=#" + ColorUtility.ToHtmlStringRGB(expiredLabelColor) +
                     "><b>" + (string.IsNullOrWhiteSpace(expiredLabel) ? "EXPIRED" : expiredLabel) +
                     "</b></color>";
        }

        ResolveLabelReferences();
        int remainingQuantity = ResolveRemainingQuantity(out bool hasAuthoritativeBatch);
        string quantityLabel = (quantityPrefix ?? string.Empty) + Mathf.Max(0, remainingQuantity);
        bool hasDedicatedQuantityLabels = HasQuantityLabels();
        for (int i = 0; i < itemNameTexts.Length; i++)
        {
            if (itemNameTexts[i] != null)
            {
                itemNameTexts[i].text = !hasDedicatedQuantityLabels && appendQuantityToItemName
                    ? label + "\n" + quantityLabel
                    : label;
            }
        }

        if (quantityTexts != null)
        {
            for (int i = 0; i < quantityTexts.Length; i++)
            {
                if (quantityTexts[i] != null)
                    quantityTexts[i].text = quantityLabel;
            }
        }

        if (removeContainerWhenEmpty && hasAuthoritativeBatch && remainingQuantity <= 0)
            RequestEmptyContainerRemoval();
    }

    public int DiscardTrackedStock()
    {
        if (item == null || InventoryManager.Instance == null)
            return 0;

        return InventoryManager.Instance.DiscardContainerStock(
            item.itemType,
            Mathf.Max(1, item.unitsPerBox),
            stockBatchID);
    }

    private void EnsureContainerID()
    {
        if (string.IsNullOrWhiteSpace(containerID))
            containerID = System.Guid.NewGuid().ToString("N");
    }

    private void SubscribeToInventory()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (subscribedInventory == inventory)
            return;

        UnsubscribeFromInventory();
        subscribedInventory = inventory;
        if (subscribedInventory != null)
            subscribedInventory.OnStockChanged += HandleStockChanged;
    }

    private void UnsubscribeFromInventory()
    {
        if (subscribedInventory != null)
            subscribedInventory.OnStockChanged -= HandleStockChanged;
        subscribedInventory = null;
    }

    private void HandleStockChanged(ItemType changedType, int _)
    {
        if (!emptyRemovalRequested && item != null && item.itemType == changedType)
            RefreshExpiryState();
    }

    private int ResolveRemainingQuantity(out bool hasAuthoritativeBatch)
    {
        hasAuthoritativeBatch = false;
        if (item == null)
            return 0;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return Mathf.Max(0, item.unitsPerBox);

        if (!string.IsNullOrWhiteSpace(stockBatchID))
        {
            if (inventory.TryGetBatch(stockBatchID, out InventoryStockBatchSaveEntry batch))
            {
                stockBatchObserved = true;
                hasAuthoritativeBatch = true;
                return Mathf.Max(0, batch.unitsRemaining);
            }

            // During scene/save restoration the physical box may bind before
            // InventoryManager has restored its batches. Missing is not the same
            // as empty until this exact batch has previously been observed.
            if (stockBatchObserved)
            {
                hasAuthoritativeBatch = true;
                return 0;
            }

            return Mathf.Max(0, item.unitsPerBox);
        }

        // Compatibility for authored/legacy containers that predate batch IDs.
        return Mathf.Min(
            Mathf.Max(0, item.unitsPerBox),
            Mathf.Max(0, inventory.GetStock(item.itemType)));
    }

    private bool HasQuantityLabels()
    {
        if (quantityTexts == null)
            return false;
        for (int i = 0; i < quantityTexts.Length; i++)
        {
            if (quantityTexts[i] != null)
                return true;
        }
        return false;
    }

    private void RequestEmptyContainerRemoval()
    {
        if (emptyRemovalRequested)
            return;
        emptyRemovalRequested = true;

        DraggableStorageBox draggable = GetComponent<DraggableStorageBox>();
        if (draggable != null)
        {
            draggable.RemoveEmptyContainer();
            return;
        }

        RestockOrderManager.Instance?.RemovePhysicalContainer(ContainerID);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(gameObject);
            return;
        }
#endif
        Destroy(gameObject);
    }

    private void ResolveLabelReferences()
    {
        if (itemNameTexts != null && itemNameTexts.Length > 0 &&
            itemIcons != null && itemIcons.Length > 0)
            return;

        List<TMP_Text> texts = new List<TMP_Text>();
        List<Image> icons = new List<Image>();
        Transform labelRoot = transform.Find("UI");
        if (labelRoot != null)
        {
            for (int i = 0; i < labelRoot.childCount; i++)
            {
                Transform child = labelRoot.GetChild(i);
                if (!IsItemLabelCanvas(child.name))
                    continue;

                TMP_Text text = child.GetComponentInChildren<TMP_Text>(true);
                Image icon = child.GetComponentInChildren<Image>(true);
                if (text != null)
                    texts.Add(text);
                if (icon != null)
                    icons.Add(icon);
            }
        }

        itemNameTexts = texts.ToArray();
        itemIcons = icons.ToArray();
    }

    private static bool IsItemLabelCanvas(string objectName)
    {
        return objectName == "Canvas" || objectName == "Canvas 2" || objectName == "Canvas2";
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int newline = value.IndexOf('\n');
        if (newline >= 0)
            value = value.Substring(0, newline);

        StringBuilder result = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetter(character))
                result.Append(character);
        }

        return result.ToString();
    }
}
