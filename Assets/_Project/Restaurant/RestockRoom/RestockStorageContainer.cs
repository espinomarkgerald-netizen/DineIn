using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime identity for a physical delivered box/crate.</summary>
public sealed class RestockStorageContainer : MonoBehaviour
{
    [SerializeField] private ItemData item;
    [Header("Editable Box Label")]
    [SerializeField] private TMP_Text[] itemNameTexts;
    [SerializeField] private Image[] itemIcons;
    [SerializeField, HideInInspector] private bool labelReferencesConfigured;
    public ItemData Item => item;
    public bool HasConfiguredLabels => labelReferencesConfigured;

    public void ConfigureLabels(TMP_Text[] configuredNameTexts, Image[] configuredIcons)
    {
        itemNameTexts = configuredNameTexts;
        itemIcons = configuredIcons;
        labelReferencesConfigured = true;
    }

    public void Bind(ItemData configuredItem)
    {
        item = configuredItem;
        if (item == null)
            return;

        gameObject.name = item.displayName + " Storage Box";

        ResolveLabelReferences();
        for (int i = 0; i < itemNameTexts.Length; i++)
        {
            if (itemNameTexts[i] != null)
                itemNameTexts[i].text = item.displayName;
        }

        for (int i = 0; i < itemIcons.Length; i++)
        {
            Image icon = itemIcons[i];
            if (icon == null)
                continue;

            icon.sprite = item.sprite;
            icon.enabled = item.sprite != null;
            icon.preserveAspect = true;
        }
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
}
