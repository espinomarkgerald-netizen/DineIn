using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefab-backed line used by the restock cart and menu ingredient list.</summary>
public sealed class ManagementComputerCheckoutLineUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private GameObject quantityControls;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private TMP_Text quantityText;

    public void ConfigureReferences(
        Image configuredIcon,
        TMP_Text configuredName,
        TMP_Text configuredTotal,
        GameObject configuredControls,
        Button configuredMinus,
        Button configuredPlus,
        TMP_Text configuredQuantity)
    {
        icon = configuredIcon;
        nameText = configuredName;
        totalText = configuredTotal;
        quantityControls = configuredControls;
        minusButton = configuredMinus;
        plusButton = configuredPlus;
        quantityText = configuredQuantity;
    }

    public void BindCart(
        ItemData item,
        int quantity,
        bool canIncrease,
        Action<ItemData, int> onQuantityChanged)
    {
        SetIcon(item != null ? item.sprite : null);
        SetText(nameText, item != null
            ? "<b>" + item.displayName + "</b>\n<size=80%>₱" +
              Mathf.Max(0, item.boxCost) + " each • " +
              Mathf.Max(1, item.unitsPerBox) + " units</size>"
            : "Missing item");
        SetText(totalText, item != null ? "₱" + (Mathf.Max(0, quantity) * Mathf.Max(0, item.boxCost)) : "₱0");
        SetText(quantityText, Mathf.Max(0, quantity).ToString());
        if (quantityControls != null)
            quantityControls.SetActive(true);
        BindButton(minusButton, item, -1, quantity > 0, onQuantityChanged);
        BindButton(plusButton, item, 1, canIncrease, onQuantityChanged);
    }

    public void BindIngredient(RecipeIngredient ingredient)
    {
        ItemData item = ingredient != null ? ingredient.item : null;
        SetIcon(item != null ? item.sprite : null);
        SetText(nameText, item != null
            ? "<b>" + item.displayName + "</b>"
            : "Unknown ingredient");
        SetText(totalText, "x" + Mathf.Max(1, ingredient != null ? ingredient.amount : 1));
        if (quantityControls != null)
            quantityControls.SetActive(false);
    }

    private void SetIcon(Sprite sprite)
    {
        if (icon == null)
            return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private static void BindButton(
        Button button,
        ItemData item,
        int delta,
        bool enabled,
        Action<ItemData, int> callback)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.interactable = enabled && item != null && callback != null;
        if (button.interactable)
            button.onClick.AddListener(() => callback(item, delta));
    }
}
