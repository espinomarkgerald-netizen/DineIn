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

    [Header("Catalog Categories (Runtime-authored from this prefab)")]
    [SerializeField, Min(44f)] private float categoryTabHeight = 48f;
    [SerializeField, Min(210f)] private float categoryTabsWidth = 250f;

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
    [SerializeField] private Vector2 preferredCardSize = new Vector2(248f, 316f);
    [SerializeField, Min(1)] private int preferredColumns = 2;
    [SerializeField, Min(0f)] private float cardSpacing = 14f;
    [SerializeField, Min(220f)] private float rightRailPreferredWidth = 380f;

    [Header("Menu Layout (Editable)")]
    [SerializeField] private Vector2 menuCardSize = new Vector2(248f, 228f);
    [SerializeField, Range(1, 5)] private int menuMaximumColumns = 4;
    [SerializeField, Range(0.22f, 0.5f)] private float menuRightRailProportion = 0.3f;
    [SerializeField] private Vector2 menuRightRailWidthRange = new Vector2(360f, 480f);

    [Header("Restock Layout (Editable)")]
    [SerializeField] private Vector2 restockCardSize = new Vector2(248f, 316f);
    [SerializeField, Range(1, 4)] private int restockMaximumColumns = 3;
    [SerializeField, Range(0.28f, 0.55f)] private float restockRightRailProportion = 0.39f;
    [SerializeField] private Vector2 restockRightRailWidthRange = new Vector2(440f, 580f);

    [Header("Mobile Catalog Layout (Editable)")]
    [SerializeField, Range(0.3f, 0.6f)] private float mobileRightRailProportion = 0.42f;
    [SerializeField] private Vector2 mobileRightRailWidthRange = new Vector2(440f, 560f);
    [SerializeField, Min(44f)] private float mobileControlHeight = 68f;
    [SerializeField, Min(44f)] private float mobileSmallButtonWidth = 112f;
    [SerializeField, Min(44f)] private float mobileMenuIconSize = 104f;

    private readonly List<Recipe> menuProducts = new List<Recipe>();
    private readonly Dictionary<Recipe, ManagementComputerCatalogCardUI> menuCards =
        new Dictionary<Recipe, ManagementComputerCatalogCardUI>();
    private readonly List<ItemData> restockItems = new List<ItemData>();
    private readonly Dictionary<ItemData, ManagementComputerCatalogCardUI> restockCards =
        new Dictionary<ItemData, ManagementComputerCatalogCardUI>();
    private readonly Dictionary<ItemData, int> cart = new Dictionary<ItemData, int>();
    private readonly Dictionary<ItemData, int> restockProgressionDays =
        new Dictionary<ItemData, int>();
    private readonly HashSet<ItemData> foodRestockItems = new HashSet<ItemData>();
    private readonly HashSet<ItemData> drinkRestockItems = new HashSet<ItemData>();

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
    private bool showingMenu = true;
    private ItemData extraOrderArmedItem;
    private Vector2 lastPanelSize;
    private InventoryManager subscribedInventory;
    private MenuProductCategory activeCategory = MenuProductCategory.Food;
    private RectTransform categoryTabsRoot;
    private Button foodTabButton;
    private Button drinksTabButton;
    private TMP_Text foodTabLabel;
    private TMP_Text drinksTabLabel;
    private bool catalogScrollOffsetsCaptured;
    private Vector2 authoredCatalogScrollOffsetMin;
    private Vector2 authoredCatalogScrollOffsetMax;

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
        StretchToAvailableParent();
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
        activeCategory = MenuProductCategory.Food;
        EnsureCategoryTabs();
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

        menuProducts.Sort(CompareMenuProgression);
        SetText(contextText, "Select a menu item to view its price and recipe details.");
        BuildMenuCards();
        SelectRecipe(FindFirstMenuProduct(activeCategory));
        ApplyCategoryFilter(false);
        UpdateCategoryTabVisuals();
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
        activeCategory = MenuProductCategory.Food;
        EnsureCategoryTabs();
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

        BuildRestockProgressionMetadata();
        restockItems.Sort(CompareRestockProgression);

        if (orderManager != null)
            orderManager.OrdersChanged -= HandleOrdersChanged;
        orderManager = configuredOrders;
        if (orderManager != null)
            orderManager.OrdersChanged += HandleOrdersChanged;

        SetText(contextText,
            $"Expected visitors today: {expectedCustomers}. Choose how many boxes to order, then review your cart.");
        BuildRestockCards();
        RefreshRestockView();
        ApplyCategoryFilter(false);
        UpdateCategoryTabVisuals();
        ApplyResponsiveLayout();
    }

    private void SetMode(bool menu)
    {
        showingMenu = menu;
        if (menuDetailsRoot != null)
            menuDetailsRoot.SetActive(menu);
        if (restockCartRoot != null)
            restockCartRoot.SetActive(!menu);
        SetMessage(string.Empty);
    }

    private void EnsureCategoryTabs()
    {
        if (categoryTabsRoot != null || catalogScroll == null)
            return;

        RectTransform scrollRect = catalogScroll.transform as RectTransform;
        RectTransform catalogRoot = scrollRect != null ? scrollRect.parent as RectTransform : null;
        if (scrollRect == null || catalogRoot == null)
            return;

        if (!catalogScrollOffsetsCaptured)
        {
            authoredCatalogScrollOffsetMin = scrollRect.offsetMin;
            authoredCatalogScrollOffsetMax = scrollRect.offsetMax;
            catalogScrollOffsetsCaptured = true;
        }

        float height = Mathf.Clamp(categoryTabHeight, 44f, 52f);
        GameObject tabs = new GameObject("CatalogCategoryTabs", typeof(RectTransform));
        tabs.transform.SetParent(catalogRoot, false);
        categoryTabsRoot = tabs.transform as RectTransform;
        categoryTabsRoot.anchorMin = new Vector2(0f, 1f);
        categoryTabsRoot.anchorMax = new Vector2(0f, 1f);
        categoryTabsRoot.pivot = new Vector2(0f, 1f);
        categoryTabsRoot.anchoredPosition = new Vector2(16f, -66f);
        categoryTabsRoot.sizeDelta = new Vector2(Mathf.Max(210f, categoryTabsWidth), height);
        categoryTabsRoot.SetSiblingIndex(Mathf.Min(1, catalogRoot.childCount - 1));

        foodTabButton = CreateCategoryTab(
            "FoodTab",
            "FOOD",
            new Vector2(0f, 0f),
            new Vector2(0.48f, 1f),
            out foodTabLabel);
        drinksTabButton = CreateCategoryTab(
            "DrinksTab",
            "DRINKS",
            new Vector2(0.52f, 0f),
            new Vector2(1f, 1f),
            out drinksTabLabel);

        foodTabButton.onClick.AddListener(() => SetCategory(MenuProductCategory.Food));
        drinksTabButton.onClick.AddListener(() => SetCategory(MenuProductCategory.Drink));

        Vector2 scrollOffsetMax = authoredCatalogScrollOffsetMax;
        scrollOffsetMax.y -= height + 8f;
        scrollRect.offsetMin = authoredCatalogScrollOffsetMin;
        scrollRect.offsetMax = scrollOffsetMax;
        UpdateCategoryTabVisuals();
    }

    private Button CreateCategoryTab(
        string objectName,
        string labelValue,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out TMP_Text label)
    {
        GameObject tab = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        tab.transform.SetParent(categoryTabsRoot, false);
        RectTransform rect = tab.transform as RectTransform;
        SetAnchors(rect, anchorMin, anchorMax);

        Image image = tab.GetComponent<Image>();
        ManagementComputerResponsiveLayout responsive =
            GetComponentInParent<ManagementComputerResponsiveLayout>(true);
        if (responsive != null && responsive.WideButtonSprite != null)
        {
            image.sprite = responsive.WideButtonSprite;
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = true;
        Button button = tab.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.88f, 0.92f, 0.96f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        tab.AddComponent<UISubtlePressFeedback>();

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tab.transform, false);
        label = textObject.GetComponent<TextMeshProUGUI>();
        if (contextText != null && contextText.font != null)
            label.font = contextText.font;
        label.text = labelValue;
        label.fontSize = 17f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    private void SetCategory(MenuProductCategory category)
    {
        if (activeCategory == category)
            return;

        activeCategory = category;
        UpdateCategoryTabVisuals();
        ApplyCategoryFilter(true);

        if (showingMenu)
            SelectRecipe(FindFirstMenuProduct(activeCategory));
        else
            RefreshRestockView();

        if (catalogScroll != null)
            catalogScroll.verticalNormalizedPosition = 1f;
        if (cardContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent);
    }

    private void ApplyCategoryFilter(bool animateNewCards)
    {
        int revealIndex = 0;
        if (showingMenu)
        {
            foreach (KeyValuePair<Recipe, ManagementComputerCatalogCardUI> entry in menuCards)
            {
                bool visible = entry.Key != null && entry.Key.category == activeCategory;
                SetCardVisibility(entry.Value, visible, animateNewCards, revealIndex);
                if (visible)
                    revealIndex++;
            }
        }
        else
        {
            foreach (KeyValuePair<ItemData, ManagementComputerCatalogCardUI> entry in restockCards)
            {
                bool visible = IsRestockItemInCategory(entry.Key, activeCategory);
                SetCardVisibility(entry.Value, visible, animateNewCards, revealIndex);
                if (visible)
                    revealIndex++;
            }
        }

        if (cardContent != null)
            LayoutRebuilder.MarkLayoutForRebuild(cardContent);
    }

    private static void SetCardVisibility(
        ManagementComputerCatalogCardUI card,
        bool visible,
        bool animate,
        int revealIndex)
    {
        if (card == null)
            return;

        bool wasVisible = card.gameObject.activeSelf;
        if (wasVisible != visible)
            card.gameObject.SetActive(visible);
        if (visible)
            card.RestoreExpectedVisualState();
        if (visible && animate && !wasVisible)
            card.GetComponent<UIRevealAnimation>()?.Play(Mathf.Min(0.1f, revealIndex * 0.018f));
    }

    private void UpdateCategoryTabVisuals()
    {
        SetCategoryTabVisual(foodTabButton, foodTabLabel,
            activeCategory == MenuProductCategory.Food);
        SetCategoryTabVisual(drinksTabButton, drinksTabLabel,
            activeCategory == MenuProductCategory.Drink);
    }

    private static void SetCategoryTabVisual(Button button, TMP_Text label, bool selected)
    {
        if (button != null && button.targetGraphic is Image image)
            image.color = selected
                ? new Color(0.12f, 0.57f, 0.84f, 1f)
                : new Color(0.22f, 0.34f, 0.48f, 1f);
        if (label != null)
        {
            SetText(label, button == null || button.name.StartsWith("Food", StringComparison.Ordinal)
                ? "FOOD"
                : "DRINKS");
            label.color = Color.white;
        }
    }

    private Recipe FindFirstMenuProduct(MenuProductCategory category)
    {
        for (int i = 0; i < menuProducts.Count; i++)
            if (menuProducts[i] != null && menuProducts[i].category == category)
                return menuProducts[i];
        return null;
    }

    private static int CompareMenuProgression(Recipe a, Recipe b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int day = Mathf.Max(1, a.dayToUnlock).CompareTo(Mathf.Max(1, b.dayToUnlock));
        if (day != 0)
            return day;
        int authored = a.menuSortOrder.CompareTo(b.menuSortOrder);
        return authored != 0
            ? authored
            : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
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
            bool visible = product.category == activeCategory;
            card.gameObject.SetActive(visible);
            menuCards[product] = card;
            if (visible)
                card.GetComponent<UIRevealAnimation>()?.Play(Mathf.Min(0.12f, i * 0.018f));
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
        MenuPriceGuidance guidance = MenuPriceValueService.GetGuidance(product);
        SetText(menuDescriptionText,
            description + $" • Unlock day {Mathf.Max(1, product.dayToUnlock)}" +
            $"\n<b>Ingredient Cost:</b> ₱{Mathf.CeilToInt(guidance.CostPerServing)}" +
            $"\n<b>Suggested Price:</b> ₱{guidance.RecommendedMinimum} – ₱{guidance.RecommendedMaximum}");
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
            bool visible = IsRestockItemInCategory(item, activeCategory);
            card.gameObject.SetActive(visible);
            restockCards[item] = card;
            if (visible)
                card.GetComponent<UIRevealAnimation>()?.Play(Mathf.Min(0.12f, i * 0.018f));
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
        ApplyCategoryFilter(false);
    }

    private void BuildRestockProgressionMetadata()
    {
        restockProgressionDays.Clear();
        foodRestockItems.Clear();
        drinkRestockItems.Clear();

        MenuCatalog catalog = MenuCatalog.Default;
        IReadOnlyList<Recipe> products = catalog != null ? catalog.Products : null;
        if (products != null)
        {
            for (int productIndex = 0; productIndex < products.Count; productIndex++)
            {
                Recipe product = products[productIndex];
                if (product == null || product.ingredients == null)
                    continue;

                int day = Mathf.Max(1, product.dayToUnlock);
                for (int ingredientIndex = 0;
                     ingredientIndex < product.ingredients.Count;
                     ingredientIndex++)
                {
                    ItemData item = product.ingredients[ingredientIndex]?.item;
                    if (item == null)
                        continue;

                    if (!restockProgressionDays.TryGetValue(item, out int existingDay) ||
                        day < existingDay)
                        restockProgressionDays[item] = day;

                    if (product.category == MenuProductCategory.Drink)
                        drinkRestockItems.Add(item);
                    else
                        foodRestockItems.Add(item);
                }
            }
        }

        for (int i = 0; i < restockItems.Count; i++)
        {
            ItemData item = restockItems[i];
            if (item == null)
                continue;
            if (!restockProgressionDays.ContainsKey(item))
                restockProgressionDays[item] = Mathf.Max(1, item.dayToUnlock);
            if (!foodRestockItems.Contains(item) && !drinkRestockItems.Contains(item))
                foodRestockItems.Add(item);
        }
    }

    private int CompareRestockProgression(ItemData a, ItemData b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int day = GetIngredientProgressionDay(a).CompareTo(GetIngredientProgressionDay(b));
        return day != 0
            ? day
            : string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
    }

    private int GetIngredientProgressionDay(ItemData item)
    {
        if (item != null && restockProgressionDays.TryGetValue(item, out int day))
            return Mathf.Max(1, day);
        return item != null ? Mathf.Max(1, item.dayToUnlock) : int.MaxValue;
    }

    private bool IsRestockItemInCategory(ItemData item, MenuProductCategory category)
    {
        if (item == null)
            return false;
        return category == MenuProductCategory.Drink
            ? drinkRestockItems.Contains(item)
            : foodRestockItems.Contains(item);
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
        return GetIngredientProgressionDay(item) <= day ||
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

        StretchToAvailableParent();
        bool mobile = UsesMobileLayout;
        ApplyFullHeightContentLayout(mobile);
        lastPanelSize = root.rect.size;
        float width = Mathf.Max(480f, lastPanelSize.x);
        Vector2 authoredRailRange = showingMenu
            ? menuRightRailWidthRange
            : restockRightRailWidthRange;
        Vector2 activeRailRange = mobile
            ? new Vector2(
                Mathf.Max(authoredRailRange.x, mobileRightRailWidthRange.x),
                Mathf.Max(authoredRailRange.y, mobileRightRailWidthRange.y))
            : authoredRailRange;
        float railMinimum = Mathf.Max(280f, Mathf.Min(activeRailRange.x, activeRailRange.y));
        float railMaximum = Mathf.Max(
            railMinimum,
            Mathf.Max(
                Mathf.Max(activeRailRange.x, activeRailRange.y),
                rightRailPreferredWidth));
        float railProportion = showingMenu
            ? menuRightRailProportion
            : restockRightRailProportion;
        if (mobile)
            railProportion = Mathf.Max(railProportion, mobileRightRailProportion);
        float railWidth = Mathf.Clamp(
            width * railProportion,
            railMinimum,
            railMaximum);
        if (rightRailLayout != null)
        {
            rightRailLayout.minWidth = mobile ? railMinimum : 250f;
            rightRailLayout.preferredWidth = railWidth;
        }

        if (cardGrid == null)
        {
            ApplyMobileControls(mobile);
            return;
        }

        Vector2 authoredCardSize = showingMenu ? menuCardSize : restockCardSize;
        if (authoredCardSize.x <= 0f || authoredCardSize.y <= 0f)
            authoredCardSize = preferredCardSize;
        Vector2 targetCardSize = authoredCardSize;
        float estimatedLeftWidth = Mathf.Max(220f, width - railWidth - 52f);
        int configuredMaximum = showingMenu ? menuMaximumColumns : restockMaximumColumns;
        int maximumColumns = Mathf.Max(
            1,
            configuredMaximum > 0 ? configuredMaximum : preferredColumns);
        int columnsThatFit = Mathf.Max(
            1,
            Mathf.FloorToInt((estimatedLeftWidth + cardSpacing) /
                             (Mathf.Max(160f, targetCardSize.x) + cardSpacing)));
        int columns = Mathf.Min(maximumColumns, columnsThatFit);
        float usableWidth = estimatedLeftWidth - cardGrid.padding.left - cardGrid.padding.right -
                            Mathf.Max(0, columns - 1) * cardSpacing;
        float cardWidth = Mathf.Min(targetCardSize.x, usableWidth / columns);
        cardWidth = Mathf.Max(220f, cardWidth);
        float cardHeight = targetCardSize.y *
                           (cardWidth / Mathf.Max(1f, targetCardSize.x));
        cardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cardGrid.constraintCount = columns;
        cardGrid.cellSize = new Vector2(cardWidth, cardHeight);
        cardGrid.spacing = Vector2.one * cardSpacing;
        cardGrid.childAlignment = TextAnchor.UpperLeft;

        if (cardContent != null)
            LayoutRebuilder.MarkLayoutForRebuild(cardContent);

        ApplyMobileControls(mobile);
    }

    private void StretchToAvailableParent()
    {
        RectTransform root = transform as RectTransform;
        if (root == null || root.parent is not RectTransform)
            return;

        // Menu and Restock are embedded panels. Their parent owns the management
        // window bounds, so the catalog should consume those bounds instead of
        // retaining the prefab's 1000 x 620 authoring size.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
    }

    private void ApplyFullHeightContentLayout(bool mobile)
    {
        if (catalogScroll != null && catalogScroll.transform.parent is RectTransform catalogRoot)
        {
            LayoutElement catalogLayout = catalogRoot.GetComponent<LayoutElement>();
            if (catalogLayout != null)
                catalogLayout.flexibleHeight = 1f;

            if (catalogScrollOffsetsCaptured)
            {
                float tabHeight = Mathf.Clamp(categoryTabHeight, 44f, 52f);
                RectTransform scrollRect = catalogScroll.transform as RectTransform;
                scrollRect.offsetMin = new Vector2(
                    authoredCatalogScrollOffsetMin.x,
                    Mathf.Max(8f, authoredCatalogScrollOffsetMin.y));
                scrollRect.offsetMax = new Vector2(
                    authoredCatalogScrollOffsetMax.x,
                    authoredCatalogScrollOffsetMax.y - tabHeight - 8f);
            }
        }

        if (rightRailLayout != null)
            rightRailLayout.flexibleHeight = 1f;

        RectTransform details = menuDetailsRoot != null
            ? menuDetailsRoot.transform as RectTransform
            : null;
        if (details != null)
        {
            details.anchorMin = Vector2.zero;
            details.anchorMax = Vector2.one;
            details.offsetMin = Vector2.zero;
            details.offsetMax = new Vector2(0f, -50f);
        }

        RectTransform ingredientScroll = menuDetailsRoot != null
            ? menuDetailsRoot.transform.Find("IngredientScroll") as RectTransform
            : null;
        if (ingredientScroll != null)
        {
            ingredientScroll.anchorMin = Vector2.zero;
            ingredientScroll.anchorMax = Vector2.one;
            ingredientScroll.pivot = new Vector2(0.5f, 0.5f);
            ingredientScroll.offsetMin = new Vector2(12f, 76f);
            ingredientScroll.offsetMax = new Vector2(-12f, -314f);
        }

        RectTransform availability = menuAvailabilityButton != null
            ? menuAvailabilityButton.transform as RectTransform
            : null;
        float bottomControlHeight = mobile
            ? Mathf.Max(54f, mobileControlHeight)
            : 54f;
        float lowerContentTop = 10f + bottomControlHeight + 12f;
        if (availability != null)
        {
            availability.anchorMin = new Vector2(0f, 0f);
            availability.anchorMax = new Vector2(1f, 0f);
            availability.pivot = new Vector2(0.5f, 0f);
            availability.anchoredPosition = new Vector2(0f, 10f);
            availability.sizeDelta = new Vector2(-24f, bottomControlHeight);
        }

        if (ingredientScroll != null)
            ingredientScroll.offsetMin = new Vector2(12f, lowerContentTop);

        RectTransform cart = restockCartRoot != null
            ? restockCartRoot.transform as RectTransform
            : null;
        if (cart != null)
        {
            cart.anchorMin = Vector2.zero;
            cart.anchorMax = Vector2.one;
            cart.offsetMin = Vector2.zero;
            cart.offsetMax = new Vector2(0f, -50f);
        }

        RectTransform cartScroll = restockCartRoot != null
            ? restockCartRoot.transform.Find("CartScroll") as RectTransform
            : null;
        if (cartScroll != null)
        {
            cartScroll.anchorMin = Vector2.zero;
            cartScroll.anchorMax = Vector2.one;
            cartScroll.pivot = new Vector2(0.5f, 0.5f);
            cartScroll.offsetMin = new Vector2(10f, lowerContentTop + 96f);
            cartScroll.offsetMax = new Vector2(-10f, -8f);
        }

        RectTransform cartSummary = cartSummaryText != null
            ? cartSummaryText.rectTransform
            : null;
        if (cartSummary != null)
        {
            cartSummary.anchorMin = new Vector2(0f, 0f);
            cartSummary.anchorMax = new Vector2(1f, 0f);
            cartSummary.pivot = new Vector2(0.5f, 0f);
            cartSummary.anchoredPosition = new Vector2(0f, lowerContentTop);
            cartSummary.sizeDelta = new Vector2(-24f, 88f);
        }
    }

    private bool UsesMobileLayout
    {
        get
        {
            ManagementComputerResponsiveLayout responsive =
                GetComponentInParent<ManagementComputerResponsiveLayout>(true);
            return responsive != null
                ? responsive.UsesMobileLayout
                : false;
        }
    }

    private void ApplyMobileControls(bool mobile)
    {
        if (!mobile)
            return;

        ResizeControl(menuPriceInput != null ? menuPriceInput.transform as RectTransform : null,
            -1f, mobileControlHeight);
        ResizeControl(savePriceButton != null ? savePriceButton.transform as RectTransform : null,
            mobileSmallButtonWidth, mobileControlHeight);
        ResizeControl(menuAvailabilityButton != null
                ? menuAvailabilityButton.transform as RectTransform
                : null,
            -1f, mobileControlHeight);
        ResizeControl(primaryCartButton != null ? primaryCartButton.transform as RectTransform : null,
            -1f, mobileControlHeight);
        ResizeControl(secondaryCartButton != null ? secondaryCartButton.transform as RectTransform : null,
            -1f, mobileControlHeight);

        if (menuIcon != null && menuIcon.transform is RectTransform iconRect)
            iconRect.sizeDelta = Vector2.one * mobileMenuIconSize;
    }

    private static void ResizeControl(RectTransform rect, float minimumWidth, float minimumHeight)
    {
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        if (minimumWidth > 0f && Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x))
            size.x = Mathf.Max(size.x, minimumWidth);
        if (minimumHeight > 0f && Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            size.y = Mathf.Max(size.y, minimumHeight);
        rect.sizeDelta = size;

        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            return;
        if (minimumWidth > 0f)
            layout.minWidth = Mathf.Max(layout.minWidth, minimumWidth);
        if (minimumHeight > 0f)
            layout.minHeight = Mathf.Max(layout.minHeight, minimumHeight);
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
        SetText(rightMessageText, message);
        rightMessageText.color = warning
            ? new Color(0.78f, 0.18f, 0.18f)
            : new Color(0.18f, 0.28f, 0.38f);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;

        if (!target.gameObject.activeSelf)
            target.gameObject.SetActive(true);
        target.enabled = true;
        Color color = target.color;
        color.a = 1f;
        target.color = color;
        target.canvasRenderer.SetAlpha(1f);
        target.text = value ?? string.Empty;
        target.SetAllDirty();
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
            return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
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
