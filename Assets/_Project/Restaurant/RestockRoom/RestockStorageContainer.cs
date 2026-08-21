using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime identity for a physical delivered box/crate.</summary>
public sealed class RestockStorageContainer : MonoBehaviour
{
    [SerializeField] private ItemData item;
    [SerializeField, HideInInspector] private string stockBatchID;
    [SerializeField, HideInInspector] private int expiresDay;
    [Header("Editable Box Label")]
    [SerializeField] private TMP_Text[] itemNameTexts;
    [SerializeField] private Image[] itemIcons;
    [SerializeField, HideInInspector] private bool labelReferencesConfigured;
    [Header("Expiry Presentation")]
    [SerializeField] private string expiredLabel = "EXPIRED";
    [SerializeField] private Color expiredLabelColor = new Color(0.94f, 0.16f, 0.16f, 1f);
    public ItemData Item => item;
    public string StockBatchID => stockBatchID;
    public int ExpiresDay => expiresDay;
    public bool HasConfiguredLabels => labelReferencesConfigured;

    public void ConfigureLabels(TMP_Text[] configuredNameTexts, Image[] configuredIcons)
    {
        itemNameTexts = configuredNameTexts;
        itemIcons = configuredIcons;
        labelReferencesConfigured = true;
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
        item = configuredItem;
        stockBatchID = configuredBatchID ?? string.Empty;
        expiresDay = Mathf.Max(0, configuredExpiresDay);
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
        if (expired)
        {
            label += "\n<color=#" + ColorUtility.ToHtmlStringRGB(expiredLabelColor) +
                     "><b>" + (string.IsNullOrWhiteSpace(expiredLabel) ? "EXPIRED" : expiredLabel) +
                     "</b></color>";
        }

        ResolveLabelReferences();
        for (int i = 0; i < itemNameTexts.Length; i++)
        {
            if (itemNameTexts[i] != null)
                itemNameTexts[i].text = label;
        }
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
