using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable portrait card shared by the Menu and Restock apps.</summary>
public sealed class ManagementComputerCatalogCardUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Button cardButton;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text metaText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private GameObject quantityRoot;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private TMP_Text quantityText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.96f, 0.97f, 1f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.72f, 0.93f, 1f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.82f, 0.84f, 0.88f, 1f);

    public ItemData BoundItem { get; private set; }
    public Recipe BoundProduct { get; private set; }
    public Button MinusButton => minusButton;
    public Button PlusButton => plusButton;

    public void ConfigureReferences(
        Image configuredBackground,
        Button configuredCardButton,
        Image configuredIcon,
        TMP_Text configuredTitle,
        TMP_Text configuredMeta,
        TMP_Text configuredStatus,
        TMP_Text configuredPrice,
        GameObject configuredQuantityRoot,
        Button configuredMinus,
        Button configuredPlus,
        TMP_Text configuredQuantity)
    {
        background = configuredBackground;
        cardButton = configuredCardButton;
        icon = configuredIcon;
        titleText = configuredTitle;
        metaText = configuredMeta;
        statusText = configuredStatus;
        priceText = configuredPrice;
        quantityRoot = configuredQuantityRoot;
        minusButton = configuredMinus;
        plusButton = configuredPlus;
        quantityText = configuredQuantity;
    }

    public void BindMenu(Recipe product, bool selected, Action<Recipe> onSelected)
    {
        BoundProduct = product;
        BoundItem = null;
        bool unlocked = product != null && product.IsUnlocked;
        SetIcon(product != null ? product.sprite : null);
        SetText(titleText, product != null ? product.DisplayName : "Missing product");
        SetText(metaText, product != null ? product.category.ToString() : string.Empty);
        SetText(statusText, !unlocked
            ? "Unlocks Day " + (product != null ? product.dayToUnlock : 1)
            : MenuAvailabilityManager.IsProductAvailable(product) ? "ON MENU" : "OFF MENU");
        SetText(priceText, product != null ? "₱" + product.EffectiveSellPrice : "₱0");

        if (quantityRoot != null)
            quantityRoot.SetActive(false);

        if (cardButton != null)
        {
            cardButton.enabled = true;
            cardButton.interactable = product != null;
            cardButton.onClick.RemoveAllListeners();
            if (product != null && onSelected != null)
                cardButton.onClick.AddListener(() => onSelected(product));
        }

        if (background != null)
            background.color = !unlocked ? lockedColor : selected ? selectedColor : normalColor;
    }

    public void BindRestock(
        ItemData item,
        int currentStock,
        int pendingContainers,
        int recommendedContainers,
        int requestedContainers,
        bool unlocked,
        bool canIncrease,
        Action<ItemData, int> onQuantityChanged)
    {
        BoundItem = item;
        BoundProduct = null;
        SetIcon(item != null ? item.sprite : null);
        SetText(titleText, item != null ? item.displayName : "Missing item");
        SetText(metaText, item != null
            ? $"{Mathf.Max(1, item.unitsPerBox)} units • {item.requiredStorage}"
            : string.Empty);
        SetText(statusText, !unlocked
            ? "Unlocks Day " + (item != null ? item.dayToUnlock : 1)
            : $"Stock {Mathf.Max(0, currentStock)} • Pending {Mathf.Max(0, pendingContainers)}\nRecommended {Mathf.Max(0, recommendedContainers)} boxes");
        SetText(priceText, item != null ? "₱" + Mathf.Max(0, item.boxCost) + " / box" : "₱0");

        if (quantityRoot != null)
            quantityRoot.SetActive(true);
        if (quantityText != null)
            quantityText.text = Mathf.Max(0, requestedContainers).ToString();

        // The restock card body is informational. Only the explicit minus/plus
        // controls change cart values, preventing missed taps from adding stock.
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.enabled = false;
        }

        BindQuantityButton(
            minusButton,
            unlocked && requestedContainers > 0,
            item,
            -1,
            onQuantityChanged);
        BindQuantityButton(
            plusButton,
            unlocked && canIncrease,
            item,
            1,
            onQuantityChanged);

        if (background != null)
            background.color = unlocked ? normalColor : lockedColor;
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

    private static void BindQuantityButton(
        Button button,
        bool interactable,
        ItemData item,
        int delta,
        Action<ItemData, int> callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = interactable && item != null && callback != null;
        if (button.interactable)
            button.onClick.AddListener(() => callback(item, delta));
    }
}
