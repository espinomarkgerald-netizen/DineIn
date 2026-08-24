using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Large two-column catalog window. The left side is a mobile-friendly card
/// grid; the right side switches between menu details and the restock cart.
/// All repeated visual elements are prefab instances.
/// </summary>
public sealed class ManagementComputerCatalogPanelUI : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private TMP_Text contextText;
    [SerializeField] private ScrollRect catalogScroll;
    [SerializeField] private RectTransform cardContent;
    [SerializeField] private GridLayoutGroup cardGrid;
    [SerializeField] private ManagementComputerCatalogCardUI cardPrefab;

    [Header("Right Rail")]
    [SerializeField] private LayoutElement rightRailLayout;
    [SerializeField] private TMP_Text rightHeaderText;
    [SerializeField] private TMP_Text rightMessageText;

    [Header("Menu Details")]
    [SerializeField] private GameObject menuDetailsRoot;
    [SerializeField] private Image menuIcon;
    [SerializeField] private TMP_Text menuNameText;
    [SerializeField] private TMP_Text menuDescriptionText;
    [SerializeField] private TMP_InputField menuPriceInput;
    [SerializeField] private Button savePriceButton;
    [SerializeField] private Button menuAvailabilityButton;
    [SerializeField] private TMP_Text menuAvailabilityLabel;
    [SerializeField] private RectTransform menuIngredientContent;

    [Header("Restock Cart")]
    [SerializeField] private GameObject restockCartRoot;
    [SerializeField] private RectTransform cartLineContent;
    [SerializeField] private TMP_Text cartSummaryText;
    [SerializeField] private Button primaryCartButton;
    [SerializeField] private TMP_Text primaryCartLabel;
    [SerializeField] private Button secondaryCartButton;
    [SerializeField] private TMP_Text secondaryCartLabel;

    [Header("Repeated Prefabs")]
    [SerializeField] private ManagementComputerCheckoutLineUI checkoutLinePrefab;

    [Header("Responsive Card Grid")]
    [SerializeField] private Vector2 preferredCardSize = new Vector2(250f, 300f);
    [SerializeField, Min(1)] private int preferredColumns = 2;
    [SerializeField, Min(0f)] private float cardSpacing = 12f;
    [SerializeField, Min(220f)] private float rightRailPreferredWidth = 380f;

    private readonly List<Recipe> menuProducts = new List<Recipe>();
    private readonly Dictionary<Recipe, ManagementComputerCatalogCardUI> menuCards =
        new Dictionary<Recipe, ManagementComputerCatalogCardUI>();
    private readonly List<ItemData> restockItems = new List<ItemData>();
    private readonly Dictionary<ItemData, ManagementComputerCatalogCardUI> restockCards =
        new Dictionary<ItemData, ManagementComputerCatalogCardUI>();
    private readonly Dictionary<ItemData, int> cart = new Dictionary<ItemData, int>();

    private Recipe selectedRecipe;
    private bool menuEditable;
    private Func<Recipe, bool, bool> setMenuAvailability;
    private Func<Recipe, int, bool> setMenuPrice;

    private RestaurantStorageConfig storageConfig;
    private RestockOrderManager orderManager;
    private int expectedCustomers;
    private Func<IReadOnlyList<RestockCartLine>, bool> confirmOrder;
    private bool reviewMode;
    private bool committingOrder;
    private ItemData extraOrderArmedItem;
    private Vector2 lastPanelSize;
    private InventoryManager subscribedInventory;

    public void ConfigureReferences(
        TMP_Text configuredContext,
        ScrollRect configuredCatalogScroll,
        RectTransform configuredCardContent,
        GridLayoutGroup configuredCardGrid,
        ManagementComputerCatalogCardUI configuredCardPrefab,
        LayoutElement configuredRightRailLayout,
        TMP_Text configuredRightHeader,
        TMP_Text configuredRightMessage,
        GameObject configuredMenuRoot,
        Image configuredMenuIcon,
        TMP_Text configuredMenuName,
        TMP_Text configuredMenuDescription,
        TMP_InputField configuredMenuPriceInput,
        Button configuredSavePrice,
        Button configuredAvailabilityButton,
        TMP_Text configuredAvailabilityLabel,
        RectTransform configuredIngredientContent,
        GameObject configuredRestockRoot,
        RectTransform configuredCartContent,
        TMP_Text configuredCartSummary,
        Button configuredPrimaryButton,
        TMP_Text configuredPrimaryLabel,
        Button configuredSecondaryButton,
        TMP_Text configuredSecondaryLabel,
        ManagementComputerCheckoutLineUI configuredLinePrefab)
    {
        contextText = configuredContext;
        catalogScroll = configuredCatalogScroll;
        cardContent = configuredCardContent;
        cardGrid = configuredCardGrid;
        cardPrefab = configuredCardPrefab;
        rightRailLayout = configuredRightRailLayout;
        rightHeaderText = configuredRightHeader;
        rightMessageText = configuredRightMessage;
        menuDetailsRoot = configuredMenuRoot;
        menuIcon = configuredMenuIcon;
        menuNameText = configuredMenuName;
        menuDescriptionText = configuredMenuDescription;
        menuPriceInput = configuredMenuPriceInput;
        savePriceButton = configuredSavePrice;
        menuAvailabilityButton = configuredAvailabilityButton;
        menuAvailabilityLabel = configuredAvailabilityLabel;
        menuIngredientContent = configuredIngredientContent;
        restockCartRoot = configuredRestockRoot;
        cartLineContent = configuredCartContent;
        cartSummaryText = configuredCartSummary;
        primaryCartButton = configuredPrimaryButton;
        primaryCartLabel = configuredPrimaryLabel;
        secondaryCartButton = configuredSecondaryButton;
        secondaryCartLabel = configuredSecondaryLabel;
        checkoutLinePrefab = configuredLinePrefab;
    }

    private void OnEnable()
    {
        ApplyResponsiveLayout();
    }

    private void Update()
    {
        RectTransform root = transform as RectTransform;
        Vector2 size = root != null ? root.rect.size : Vector2.zero;
        if ((size - lastPanelSize).sqrMagnitude > 1f)
            ApplyResponsiveLayout();
    }

    private void OnDestroy()
    {
        if (orderManager != null)
            orderManager.OrdersChanged -= HandleOrdersChanged;
        UnsubscribeInventory();
    }

    public void BindMenu(
        IReadOnlyList<Recipe> products,
        bool editable,
        Func<Recipe, bool, bool> availabilitySetter,
        Func<Recipe, int, bool> priceSetter)
    {
        UnsubscribeInventory();
        SetMode(menu: true);
        menuEditable = editable;
        setMenuAvailability = availabilitySetter;
        setMenuPrice = priceSetter;
        menuProducts.Clear();

        if (products != null)
        {
            for (int i = 0; i < products.Count; i++)
            {
                if (products[i] != null)
                    menuProducts.Add(products[i]);
            }
        }

        menuProducts.Sort((a, b) => a.menuSortOrder.CompareTo(b.menuSortOrder));
        SetText(contextText, "Select a menu item to view its price and recipe details.");
        BuildMenuCards();
        SelectRecipe(menuProducts.Count > 0 ? menuProducts[0] : null);
        ApplyResponsiveLayout();
    }

    public void BindRestock(
        IReadOnlyList<ItemData> items,
        RestaurantStorageConfig configuredStorage,
        RestockOrderManager configuredOrders,
        int configuredExpectedCustomers,
        Func<IReadOnlyList<RestockCartLine>, bool> orderConfirmation)
    {
        SubscribeInventory();
        SetMode(menu: false);
        storageConfig = configuredStorage;
        confirmOrder = orderConfirmation;
        expectedCustomers = Mathf.Max(1, configuredExpectedCustomers);
        reviewMode = false;
        committingOrder = false;
        extraOrderArmedItem = null;
        cart.Clear();
        restockItems.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    restockItems.Add(items[i]);
            }
        }

        restockItems.Sort((a, b) => string.Compare(
            a.displayName,
            b.displayName,
            StringComparison.OrdinalIgnoreCase));

        if (orderManager != null)
            orderManager.OrdersChanged -= HandleOrdersChanged;
        orderManager = configuredOrders;
        if (orderManager != null)
            orderManager.OrdersChanged += HandleOrdersChanged;

        SetText(contextText,
            $"Expected visitors today: {expectedCustomers}. Choose container quantities, then review your order.");
        BuildRestockCards();
        RefreshRestockView();
        ApplyResponsiveLayout();
    }

    private void SetMode(bool menu)
    {
        if (menuDetailsRoot != null)
            menuDetailsRoot.SetActive(menu);
        if (restockCartRoot != null)
            restockCartRoot.SetActive(!menu);
        SetMessage(string.Empty);
    }

    private void BuildMenuCards()
    {
        ClearChildren(cardContent);
        menuCards.Clear();

        if (cardPrefab == null || cardContent == null)
            return;

        for (int i = 0; i < menuProducts.Count; i++)
        {
            Recipe product = menuProducts[i];
            ManagementComputerCatalogCardUI card = Instantiate(cardPrefab, cardContent);
            card.gameObject.SetActive(true);
            menuCards[product] = card;
        }
    }

    private void RefreshMenuCards()
    {
        foreach (KeyValuePair<Recipe, ManagementComputerCatalogCardUI> entry in menuCards)
        {
            if (entry.Key != null && entry.Value != null)
                entry.Value.BindMenu(entry.Key, entry.Key == selectedRecipe, SelectRecipe);
        }
    }

    private void SelectRecipe(Recipe product)
    {
        selectedRecipe = product;
        RefreshMenuCards();
        ClearChildren(menuIngredientContent);

        if (product == null)
        {
            SetText(rightHeaderText, "Menu Details");
            SetText(menuNameText, "No menu items available");
            SetText(menuDescriptionText, string.Empty);
            SetMenuIcon(null);
            if (menuPriceInput != null)
                menuPriceInput.text = string.Empty;
            ConfigureMenuButtons(false);
            return;
        }

        SetText(rightHeaderText, "Menu Item Details");
        SetText(menuNameText, product.DisplayName);
        string description = string.IsNullOrWhiteSpace(product.descriptionText)
            ? product.category + " menu item"
            : product.descriptionText;
        SetText(menuDescriptionText,
            description + $"\nUnlock day {Mathf.Max(1, product.dayToUnlock)}");
        SetMenuIcon(product.sprite);

        if (menuPriceInput != null)
        {
            menuPriceInput.text = product.EffectiveSellPrice.ToString();
            menuPriceInput.interactable = menuEditable && product.IsUnlocked;
        }

        if (checkoutLinePrefab != null && menuIngredientContent != null && product.ingredients != null)
        {
            for (int i = 0; i < product.ingredients.Count; i++)
            {
                RecipeIngredient ingredient = product.ingredients[i];
                if (ingredient?.item == null)
                    continue;

                ManagementComputerCheckoutLineUI line = Instantiate(
                    checkoutLinePrefab,
                    menuIngredientContent);
                line.gameObject.SetActive(true);
                line.BindIngredient(ingredient);
            }
        }

        ConfigureMenuButtons(product.IsUnlocked);
        SetMessage(menuEditable
            ? "Price and menu availability are saved for this restaurant."
            : "Menu editing is locked while the shift is running.");
    }

    private void ConfigureMenuButtons(bool unlocked)
    {
        if (savePriceButton != null)
        {
            savePriceButton.onClick.RemoveAllListeners();
            savePriceButton.interactable = menuEditable && unlocked && selectedRecipe != null;
            if (savePriceButton.interactable)
                savePriceButton.onClick.AddListener(SaveSelectedPrice);
        }

        if (menuAvailabilityButton != null)
        {
            menuAvailabilityButton.onClick.RemoveAllListeners();
            bool authored = selectedRecipe != null && selectedRecipe.availableOnMenu;
            menuAvailabilityButton.interactable = menuEditable && unlocked && authored;
            if (menuAvailabilityButton.interactable)
                menuAvailabilityButton.onClick.AddListener(ToggleSelectedAvailability);
        }

        bool available = MenuAvailabilityManager.IsProductAvailable(selectedRecipe);
        SetText(menuAvailabilityLabel, !unlocked
            ? "LOCKED"
            : selectedRecipe != null && !selectedRecipe.availableOnMenu
                ? "AUTHOR DISABLED"
                : available ? "REMOVE FROM MENU" : "ADD TO MENU");
    }

    private void SaveSelectedPrice()
    {
        if (!menuEditable || selectedRecipe == null || menuPriceInput == null)
            return;

        if (!int.TryParse(menuPriceInput.text, out int price) || price < 0)
        {
            SetMessage("Enter a valid whole-number price.", true);
            return;
        }

        if (setMenuPrice == null || !setMenuPrice(selectedRecipe, price))
        {
            if (selectedRecipe.EffectiveSellPrice == price)
                SetMessage("Price is already set to ₱" + price + ".");
            else
                SetMessage("The price could not be saved.", true);
            return;
        }

        SelectRecipe(selectedRecipe);
        SetMessage("Saved " + selectedRecipe.DisplayName + " at ₱" + price + ".");
    }

    private void ToggleSelectedAvailability()
    {
        if (!menuEditable || selectedRecipe == null || setMenuAvailability == null)
            return;

        bool next = !MenuAvailabilityManager.IsProductAvailable(selectedRecipe);
        if (!setMenuAvailability(selectedRecipe, next))
        {
            SetMessage("Menu availability did not change.", true);
            return;
        }

        SelectRecipe(selectedRecipe);
    }

    private void BuildRestockCards()
    {
        ClearChildren(cardContent);
        restockCards.Clear();

        if (cardPrefab == null || cardContent == null)
            return;

        for (int i = 0; i < restockItems.Count; i++)
        {
            ItemData item = restockItems[i];
            ManagementComputerCatalogCardUI card = Instantiate(cardPrefab, cardContent);
            card.gameObject.SetActive(true);
            restockCards[item] = card;
        }
    }

    private void RefreshRestockView()
    {
        foreach (KeyValuePair<ItemData, ManagementComputerCatalogCardUI> entry in restockCards)
        {
            ItemData item = entry.Key;
            ManagementComputerCatalogCardUI card = entry.Value;
            if (item == null || card == null)
                continue;

            int requested = GetCartQuantity(item);
            bool unlocked = IsItemUnlocked(item);
            RestockStockProjection projection = RestockStockProjection.Calculate(
                item,
                expectedCustomers,
                orderManager);
            card.BindRestock(
                item,
                projection,
                requested,
                unlocked,
                unlocked && GetAvailableCapacity(item.requiredStorage) > 0,
                ChangeCartQuantity);
        }

        RebuildCartLines();
        RefreshCartSummary();
        ConfigureCartButtons();
    }

    private void RebuildCartLines()
    {
        ClearChildren(cartLineContent);
        if (checkoutLinePrefab == null || cartLineContent == null)
            return;

        for (int i = 0; i < restockItems.Count; i++)
        {
            ItemData item = restockItems[i];
            int quantity = GetCartQuantity(item);
            if (quantity <= 0)
                continue;

            ManagementComputerCheckoutLineUI line = Instantiate(
                checkoutLinePrefab,
                cartLineContent);
            line.gameObject.SetActive(true);
            line.BindCart(
                item,
                quantity,
                GetAvailableCapacity(item.requiredStorage) > 0,
                ChangeCartQuantity);
        }
    }

    private void ChangeCartQuantity(ItemData item, int delta)
    {
        if (item == null || delta == 0 || committingOrder || reviewMode)
            return;

        int current = GetCartQuantity(item);
        if (delta > 0 && current == 0)
        {
            RestockStockProjection projection = RestockStockProjection.Calculate(
                item,
                expectedCustomers,
                orderManager);
            if (projection.IsCoveredByIncoming && extraOrderArmedItem != item)
            {
                extraOrderArmedItem = item;
                SetMessage(
                    "✓ " + item.displayName.ToUpperInvariant() + " COVERED   →   " +
                    projection.PendingContainers + " BOX" +
                    (projection.PendingContainers == 1 ? string.Empty : "ES") +
                    " " + projection.GetDeliveryStageLabel() +
                    "\nTap + again only for EXTRA stock.");
                return;
            }
        }

        if (delta > 0 && GetAvailableCapacity(item.requiredStorage) <= 0)
        {
            SetMessage(
                "Not enough " + item.requiredStorage.ToString().ToLowerInvariant() +
                " storage for another box.",
                true);
            return;
        }

        int next = Mathf.Max(0, current + delta);
        if (next == 0)
            cart.Remove(item);
        else
            cart[item] = next;

        extraOrderArmedItem = null;
        SetMessage(string.Empty);
        RefreshRestockView();
    }

    private void ConfigureCartButtons()
    {
        if (primaryCartButton != null)
        {
            primaryCartButton.onClick.RemoveAllListeners();
            primaryCartButton.interactable = !committingOrder && GetTotalBoxes() > 0;
            if (primaryCartButton.interactable)
                primaryCartButton.onClick.AddListener(reviewMode ? CommitOrder : EnterReview);
        }

        if (secondaryCartButton != null)
        {
            secondaryCartButton.onClick.RemoveAllListeners();
            secondaryCartButton.interactable = !committingOrder && (reviewMode || GetTotalBoxes() > 0);
            if (secondaryCartButton.interactable)
                secondaryCartButton.onClick.AddListener(reviewMode ? LeaveReview : ClearCart);
        }

        SetText(rightHeaderText, reviewMode ? "Review Order" : "Shopping Cart");
        SetText(primaryCartLabel, reviewMode ? "ORDER NOW" : "CHECKOUT");
        SetText(secondaryCartLabel, reviewMode ? "BACK" : "CLEAR");
    }

    private void EnterReview()
    {
        string validation = ValidateCart();
        if (!string.IsNullOrEmpty(validation))
        {
            SetMessage(validation, true);
            return;
        }

        reviewMode = true;
        SetMessage("Review every quantity. Money is spent only after ORDER NOW.");
        RefreshRestockView();
    }

    private void LeaveReview()
    {
        reviewMode = false;
        SetMessage(string.Empty);
        RefreshRestockView();
    }

    private void ClearCart()
    {
        cart.Clear();
        reviewMode = false;
        SetMessage("Cart cleared.");
        RefreshRestockView();
    }

    private void CommitOrder()
    {
        if (committingOrder)
            return;

        string validation = ValidateCart();
        if (!string.IsNullOrEmpty(validation))
        {
            reviewMode = false;
            SetMessage(validation, true);
            RefreshRestockView();
            return;
        }

        List<RestockCartLine> lines = BuildCartLines();
        committingOrder = true;
        ConfigureCartButtons();
        bool success = confirmOrder != null && confirmOrder(lines);
        committingOrder = false;

        if (!success)
        {
            SetMessage("Order could not be placed. No cart quantities were removed.", true);
            ConfigureCartButtons();
            return;
        }

        cart.Clear();
        reviewMode = false;
        SetMessage("Order placed. The containers are reserved for delivery.");
        RefreshRestockView();
    }

    private string ValidateCart()
    {
        int boxes = GetTotalBoxes();
        if (boxes <= 0)
            return "Add at least one box before checkout.";

        int total = GetTotalCost();
        if (MoneyManager.Instance == null)
            return "Restaurant money is unavailable.";
        if (!MoneyManager.Instance.HasEnough(total))
            return "Not enough money. You need ₱" + total + ".";

        foreach (RestockStorageType storageType in Enum.GetValues(typeof(RestockStorageType)))
        {
            int capacity = storageConfig != null ? storageConfig.GetCapacity(storageType) : 0;
            int afterOrder = GetUsedCapacityBeforeCart(storageType) + GetCartCount(storageType);
            if (afterOrder > capacity)
            {
                int room = Mathf.Max(0, capacity - GetUsedCapacityBeforeCart(storageType));
                return $"Not enough {storageType.ToString().ToLowerInvariant()} storage. " +
                       $"Only {room} more boxes fit.";
            }
        }

        return string.Empty;
    }

    private void RefreshCartSummary()
    {
        int dryCapacity = storageConfig != null ? storageConfig.DryCapacity : 0;
        int frozenCapacity = storageConfig != null ? storageConfig.FrozenCapacity : 0;
        int dryAfter = GetUsedCapacityBeforeCart(RestockStorageType.Dry) +
                       GetCartCount(RestockStorageType.Dry);
        int frozenAfter = GetUsedCapacityBeforeCart(RestockStorageType.Frozen) +
                          GetCartCount(RestockStorageType.Frozen);

        SetText(cartSummaryText,
            $"Boxes: {GetTotalBoxes()}\n" +
            $"Dry after order: {dryAfter} / {dryCapacity}\n" +
            $"Frozen after order: {frozenAfter} / {frozenCapacity}\n" +
            $"TOTAL: ₱{GetTotalCost()}");
    }

    private int GetAvailableCapacity(RestockStorageType type)
    {
        int capacity = storageConfig != null ? storageConfig.GetCapacity(type) : 0;
        return Mathf.Max(0, capacity - GetUsedCapacityBeforeCart(type) - GetCartCount(type));
    }

    private int GetUsedCapacityBeforeCart(RestockStorageType type)
    {
        int storedEstimate = 0;
        for (int i = 0; i < restockItems.Count; i++)
        {
            ItemData item = restockItems[i];
            if (item == null || item.requiredStorage != type)
                continue;

            int unitsPerBox = Mathf.Max(1, item.unitsPerBox);
            storedEstimate += Mathf.CeilToInt(GetCurrentStock(item) / (float)unitsPerBox);
        }

        int reserved = orderManager != null
            ? orderManager.GetReservedContainers(type, restockItems)
            : 0;
        return storedEstimate + reserved;
    }

    private int GetCartCount(RestockStorageType type)
    {
        int total = 0;
        foreach (KeyValuePair<ItemData, int> entry in cart)
        {
            if (entry.Key != null && entry.Key.requiredStorage == type)
                total += Mathf.Max(0, entry.Value);
        }
        return total;
    }

    private int GetCartQuantity(ItemData item)
    {
        return item != null && cart.TryGetValue(item, out int quantity)
            ? Mathf.Max(0, quantity)
            : 0;
    }

    private int GetCurrentStock(ItemData item)
    {
        return item != null && InventoryManager.Instance != null
            ? Mathf.Max(0, InventoryManager.Instance.GetStock(item.itemType))
            : 0;
    }

    private int GetRecommendedContainers(ItemData item)
    {
        return RestockStockProjection.Calculate(item, expectedCustomers, orderManager)
            .RecommendedContainers;
    }

    private bool IsItemUnlocked(ItemData item)
    {
        if (item == null)
            return false;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        return item.dayToUnlock <= day ||
               (UnlockManager.Instance != null && UnlockManager.Instance.IsIngredientUnlocked(item));
    }

    private int GetTotalBoxes()
    {
        int total = 0;
        foreach (int quantity in cart.Values)
            total += Mathf.Max(0, quantity);
        return total;
    }

    private int GetTotalCost()
    {
        int total = 0;
        foreach (KeyValuePair<ItemData, int> entry in cart)
        {
            if (entry.Key != null)
                total += Mathf.Max(0, entry.Value) *
                         CasualDiningPolishManager.GetCurrentBoxCostOrBase(entry.Key);
        }
        return total;
    }

    private List<RestockCartLine> BuildCartLines()
    {
        List<RestockCartLine> result = new List<RestockCartLine>();
        for (int i = 0; i < restockItems.Count; i++)
        {
            ItemData item = restockItems[i];
            int quantity = GetCartQuantity(item);
            if (item != null && quantity > 0)
                result.Add(new RestockCartLine { item = item, quantity = quantity });
        }
        return result;
    }

    private void HandleOrdersChanged()
    {
        extraOrderArmedItem = null;
        if (isActiveAndEnabled && restockCartRoot != null && restockCartRoot.activeSelf)
            RefreshRestockView();
    }

    private void HandleStockChanged(ItemType _, int __)
    {
        if (isActiveAndEnabled && restockCartRoot != null && restockCartRoot.activeSelf)
            RefreshRestockView();
    }

    private void SubscribeInventory()
    {
        UnsubscribeInventory();
        subscribedInventory = InventoryManager.Instance;
        if (subscribedInventory != null)
            subscribedInventory.OnStockChanged += HandleStockChanged;
    }

    private void UnsubscribeInventory()
    {
        if (subscribedInventory != null)
            subscribedInventory.OnStockChanged -= HandleStockChanged;
        subscribedInventory = null;
    }

    private void ApplyResponsiveLayout()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        lastPanelSize = root.rect.size;
        float width = Mathf.Max(480f, lastPanelSize.x);
        float railWidth = Mathf.Clamp(
            width * 0.34f,
            300f,
            Mathf.Max(300f, rightRailPreferredWidth));
        if (rightRailLayout != null)
            rightRailLayout.preferredWidth = railWidth;

        if (cardGrid == null)
            return;

        float estimatedLeftWidth = Mathf.Max(220f, width - railWidth - 52f);
        int columns = estimatedLeftWidth >= preferredCardSize.x * 2f + cardSpacing * 3f
            ? Mathf.Max(1, preferredColumns)
            : 1;
        float usableWidth = estimatedLeftWidth - cardGrid.padding.left - cardGrid.padding.right -
                            Mathf.Max(0, columns - 1) * cardSpacing;
        float cardWidth = Mathf.Clamp(
            usableWidth / columns,
            204f,
            preferredCardSize.x * 1.35f);
        float cardHeight = Mathf.Clamp(
            cardWidth * (preferredCardSize.y / Mathf.Max(1f, preferredCardSize.x)),
            248f,
            preferredCardSize.y * 1.12f);
        cardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cardGrid.constraintCount = columns;
        cardGrid.cellSize = new Vector2(cardWidth, cardHeight);
        cardGrid.spacing = Vector2.one * cardSpacing;

        if (cardContent != null)
            LayoutRebuilder.MarkLayoutForRebuild(cardContent);
    }

    private void SetMenuIcon(Sprite sprite)
    {
        if (menuIcon == null)
            return;
        menuIcon.sprite = sprite;
        menuIcon.enabled = sprite != null;
        menuIcon.preserveAspect = true;
    }

    private void SetMessage(string message, bool warning = false)
    {
        if (rightMessageText == null)
            return;
        rightMessageText.text = message ?? string.Empty;
        rightMessageText.color = warning
            ? new Color(0.78f, 0.18f, 0.18f)
            : new Color(0.18f, 0.28f, 0.38f);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
            return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
