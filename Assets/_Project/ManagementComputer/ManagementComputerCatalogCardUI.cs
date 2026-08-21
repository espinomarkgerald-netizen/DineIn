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
    [SerializeField] private Color stockAccentColor = new Color(0.08f, 0.55f, 0.30f, 1f);
    [SerializeField] private Color expiryWarningColor = new Color(0.82f, 0.12f, 0.12f, 1f);

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
        SetRestockStatus(item, currentStock, unlocked);
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

    private void SetRestockStatus(ItemData item, int currentStock, bool unlocked)
    {
        if (statusText == null)
            return;

        if (!unlocked)
        {
            statusText.color = expiryWarningColor;
            statusText.text = "Unlocks Day " + (item != null ? item.dayToUnlock : 1);
            return;
        }

        int stock = Mathf.Max(0, currentStock);
        statusText.color = stock > 0 ? stockAccentColor : expiryWarningColor;
        if (item == null || InventoryManager.Instance == null || stock <= 0)
        {
            statusText.text = stock > 0 ? "IN STOCK  " + stock : "OUT OF STOCK";
            return;
        }

        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;
        int expired = InventoryManager.Instance.GetExpiredStock(item.itemType, day);
        int fresh = InventoryManager.Instance.GetFreshStock(item.itemType, day);
        int freshExpiryDay = InventoryManager.Instance.GetNextFreshExpiryDay(item.itemType, day);
        string stockColor = ColorUtility.ToHtmlStringRGB(stockAccentColor);
        string expiredColor = ColorUtility.ToHtmlStringRGB(expiryWarningColor);

        statusText.color = Color.white;
        statusText.text = "<color=#" + stockColor + "><b>IN STOCK  " + stock + "</b></color>";
        if (fresh > 0)
        {
            statusText.text += "\n<color=#" + stockColor + ">FRESH " + fresh +
                               (freshExpiryDay > 0 ? " • Expires Day " + freshExpiryDay : string.Empty) +
                               "</color>";
        }

        if (expired > 0)
        {
            statusText.text += "\n<color=#" + expiredColor + "><b>EXPIRED " + expired +
                               " • THROW AWAY</b></color>";
        }
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
