using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Waiter notepad. Menu choices are generated from MenuCatalog at runtime; no
/// product, drink, bundle, price, icon, or stock slot is serialized per item.
/// </summary>
public class OrderChecklistUI : MonoBehaviour
{
    public static OrderChecklistUI Instance { get; private set; }

    [Header("Order UI")]
    [SerializeField] private TMP_Text tableNumberText;
    [SerializeField] private TMP_Text customerMessageText;
    [SerializeField] private TMP_Text customerTypeText;
    [SerializeField] private Image customerImage;
    [SerializeField] private RectTransform requestedIconsRoot;
    [SerializeField] private RectTransform availableItemsRoot;

    [Header("Dynamic Menu Containers")]
    [SerializeField] private RectTransform foodContentRoot;
    [SerializeField] private RectTransform drinkContentRoot;
    [SerializeField] private ScrollRect foodScrollRect;
    [SerializeField] private ScrollRect drinkScrollRect;
    [Tooltip("Editable row prefab instantiated once for every menu product or bundle.")]
    [SerializeField] private NotepadMenuEntryUI menuEntryPrefab;
    [SerializeField] private GameObject foodPanel;
    [SerializeField] private GameObject drinkPanel;
    [SerializeField] private Button foodTabButton;
    [SerializeField] private Button drinkTabButton;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button exitButton;

    [Header("Scrollbar Alignment")]
    [Tooltip("Keeps the Food and Drinks scrollbars in the same right-side column as the exit button.")]
    [SerializeField] private bool alignScrollbarsWithExitButton = true;
    [Tooltip("Optional horizontal adjustment after aligning the scrollbar centers to the exit button.")]
    [SerializeField] private float scrollbarHorizontalOffset;

    [Header("Presentation")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typeSpeed = 0.02f;
    [SerializeField] private TutorialHintTextUI tutorialHint;
    [SerializeField] private NotepadMenuVisualStyle menuStyle = new NotepadMenuVisualStyle();

    [Header("Order Check Panel")]
    [SerializeField] private GameObject reviewOverlay;
    [SerializeField] private Image reviewPanelImage;
    [SerializeField] private TMP_Text reviewTitleText;
    [SerializeField] private TMP_Text reviewSummaryText;
    [SerializeField] private Button reviewSubmitButton;
    [SerializeField] private Button reviewBackButton;
    [SerializeField] private Color correctPanelColor =
        new Color(0.045f, 0.27f, 0.20f, 0.98f);
    [SerializeField] private Color errorPanelColor =
        new Color(0.26f, 0.09f, 0.09f, 0.98f);
    [SerializeField] private Color correctTitleColor =
        new Color(0.35f, 1f, 0.54f, 1f);
    [SerializeField] private Color errorTitleColor =
        new Color(1f, 0.3f, 0.26f, 1f);
    [SerializeField] private Color errorSummaryColor =
        new Color(1f, 0.88f, 0.86f, 1f);

    private TMP_FontAsset notepadFont;
    private NotepadMenuEntryUI reviewFocusEntry;

    private readonly List<NotepadMenuEntryUI> menuEntries = new List<NotepadMenuEntryUI>();
    private readonly List<Recipe> requestedProducts = new List<Recipe>();
    private readonly List<string> requestedContents = new List<string>();
    private readonly List<CustomerGroup.OrderLine> requestedOrderLines =
        new List<CustomerGroup.OrderLine>();

    private MenuCatalog catalog;
    private CustomerGroup group;
    private Coroutine typingRoutine;
    private bool unlockEventSubscribed;
    private LobbyStockBridge subscribedStockBridge;
    private bool refreshingResponsiveLayout;

    private string cachedOpeningMessage;
    private string cachedCustomerTypeName;
    private Sprite cachedCustomerImage;

    private sealed class ExpectedOrderItem
    {
        public string displayName;
        public int quantity;
    }

    private sealed class OrderReviewResult
    {
        public readonly List<string> messages = new List<string>();
        public NotepadMenuEntryUI firstMismatch;
        public bool IsCorrect => messages.Count == 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InstallInactiveLayoutRefresh()
    {
        SceneManager.sceneLoaded -= RefreshInactiveNotepadsAfterSceneLoad;
        SceneManager.sceneLoaded += RefreshInactiveNotepadsAfterSceneLoad;
    }

    private static void RefreshInactiveNotepadsAfterSceneLoad(Scene _, LoadSceneMode __)
    {
        Canvas.ForceUpdateCanvases();
        OrderChecklistUI[] notepads = FindObjectsByType<OrderChecklistUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < notepads.Length; i++)
            notepads[i]?.RefreshResponsiveLayout();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Canvas.willRenderCanvases -= EnsureResponsiveCoverage;
        Canvas.willRenderCanvases += EnsureResponsiveCoverage;
        RefreshResponsiveLayout();
        catalog = MenuCatalog.Default;
        ResolveUIReferences();
        ResolveNotepadFont();
        BindStaticButtons();
        RebuildMenu();

        // Scene copies that start active should hide until an order opens them.
        // If Awake was triggered by Open activating an initially inactive object,
        // group is already assigned and the notepad must remain visible.
        if (group == null && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshResponsiveLayout();
        SubscribeToUnlocks();
        SubscribeToStock();
        RefreshMenuAvailability();
    }

    private void OnDisable()
    {
        UnsubscribeFromUnlocks();
        UnsubscribeFromStock();
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= EnsureResponsiveCoverage;
        UnsubscribeFromUnlocks();
        UnsubscribeFromStock();
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        typeSpeed = Mathf.Max(0f, typeSpeed);
        if (menuStyle == null)
            menuStyle = new NotepadMenuVisualStyle();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            RefreshResponsiveLayout();
    }

    private void EnsureResponsiveCoverage()
    {
        RectTransform root = transform as RectTransform;
        RectTransform parent = root != null ? root.parent as RectTransform : null;
        if (root == null || parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f)
            return;

        float renderedWidth = root.rect.width * Mathf.Abs(root.localScale.x);
        float renderedHeight = root.rect.height * Mathf.Abs(root.localScale.y);
        bool stretched = root.anchorMin == Vector2.zero && root.anchorMax == Vector2.one;
        if (!stretched || renderedWidth + 0.5f < parent.rect.width ||
            renderedHeight + 0.5f < parent.rect.height)
            RefreshResponsiveLayout();
    }

    /// <summary>
    /// The notepad artwork was authored at half scale. Keep that authored scale,
    /// but expand its stretched rect so the rendered background still reaches
    /// every edge of wider, taller, and mobile canvases.
    /// </summary>
    private void RefreshResponsiveLayout()
    {
        if (refreshingResponsiveLayout)
            return;

        RectTransform root = transform as RectTransform;
        RectTransform parent = root != null ? root.parent as RectTransform : null;
        if (root == null || parent == null)
            return;

        Vector2 parentSize = parent.rect.size;
        if (parentSize.x <= 0f || parentSize.y <= 0f)
            return;

        float scaleX = Mathf.Max(0.001f, Mathf.Abs(root.localScale.x));
        float scaleY = Mathf.Max(0.001f, Mathf.Abs(root.localScale.y));
        Vector2 requiredSizeDelta = new Vector2(
            parentSize.x / scaleX - parentSize.x,
            parentSize.y / scaleY - parentSize.y);

        refreshingResponsiveLayout = true;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        if ((root.sizeDelta - requiredSizeDelta).sqrMagnitude > 0.01f)
            root.sizeDelta = requiredSizeDelta;

        ApplyCustomerMessageBounds();
        FinalizeMenuLayout(foodContentRoot, foodScrollRect);
        FinalizeMenuLayout(drinkContentRoot, drinkScrollRect);
        AlignMenuScrollbars();
        refreshingResponsiveLayout = false;
    }

    private void AlignMenuScrollbars()
    {
        if (!alignScrollbarsWithExitButton || exitButton == null)
            return;

        RectTransform exitRect = exitButton.transform as RectTransform;
        RectTransform alignmentParent = exitRect != null
            ? exitRect.parent as RectTransform
            : null;
        if (exitRect == null || alignmentParent == null)
            return;

        Vector3 exitCenterWorld = exitRect.TransformPoint(exitRect.rect.center);
        AlignScrollbar(foodScrollRect, alignmentParent, exitCenterWorld);
        AlignScrollbar(drinkScrollRect, alignmentParent, exitCenterWorld);
    }

    private void AlignScrollbar(
        ScrollRect scrollRect,
        RectTransform alignmentParent,
        Vector3 targetCenterWorld)
    {
        if (scrollRect == null || scrollRect.verticalScrollbar == null)
            return;

        RectTransform scrollbarRect = scrollRect.verticalScrollbar.transform as RectTransform;
        if (scrollbarRect == null || alignmentParent == null)
            return;

        // The menu list itself is masked. Keep the scrollbar beside that mask,
        // under the shared panel, so moving it to the exit-button column cannot
        // clip it. Existing authored height and vertical placement are preserved.
        if (scrollbarRect.parent != alignmentParent)
        {
            Vector3[] worldCorners = new Vector3[4];
            scrollbarRect.GetWorldCorners(worldCorners);
            Vector3 lowerLeft = alignmentParent.InverseTransformPoint(worldCorners[0]);
            Vector3 upperRight = alignmentParent.InverseTransformPoint(worldCorners[2]);
            float preservedWidth = Mathf.Abs(upperRight.x - lowerLeft.x);
            float preservedHeight = Mathf.Abs(upperRight.y - lowerLeft.y);
            if (preservedWidth <= 0.01f || preservedHeight <= 0.01f)
                return;

            scrollbarRect.SetParent(alignmentParent, false);
            scrollbarRect.anchorMin = scrollbarRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollbarRect.pivot = new Vector2(0.5f, 0.5f);
            scrollbarRect.localRotation = Quaternion.identity;
            scrollbarRect.localScale = Vector3.one;
            scrollbarRect.sizeDelta = new Vector2(preservedWidth, preservedHeight);
            scrollbarRect.anchoredPosition = new Vector2(
                (lowerLeft.x + upperRight.x) * 0.5f,
                (lowerLeft.y + upperRight.y) * 0.5f);
        }

        Vector3 targetInParent = alignmentParent.InverseTransformPoint(targetCenterWorld);
        float anchorReferenceX = Mathf.Lerp(
            alignmentParent.rect.xMin,
            alignmentParent.rect.xMax,
            scrollbarRect.anchorMin.x);
        Vector2 position = scrollbarRect.anchoredPosition;
        position.x = targetInParent.x - anchorReferenceX + scrollbarHorizontalOffset;
        scrollbarRect.anchoredPosition = position;
    }

    private void ApplyCustomerMessageBounds()
    {
        if (customerMessageText == null)
            return;

        RectTransform messageRect = customerMessageText.rectTransform;
        messageRect.sizeDelta = new Vector2(280f, 150f);
        customerMessageText.margin = Vector4.zero;
        customerMessageText.enableAutoSizing = true;
        customerMessageText.fontSizeMin = 14f;
        customerMessageText.fontSizeMax = 20f;
        customerMessageText.textWrappingMode = TextWrappingModes.Normal;
        customerMessageText.overflowMode = TextOverflowModes.Ellipsis;
        customerMessageText.alignment = TextAlignmentOptions.MidlineLeft;
        customerMessageText.raycastTarget = false;
    }

    public void Open(CustomerGroup customerGroup)
    {
        if (customerGroup == null)
            return;

        if (!customerGroup.BeginPlayerOrderReview())
        {
            customerGroup.SetOrderTaskClaimedByStaff(false);
            RestaurantTaskClaim.ReleasePlayer(customerGroup);
            return;
        }

        group = customerGroup;
        cachedOpeningMessage = group.GetCustomerOpeningMessage();
        cachedCustomerTypeName = group.GetCustomerTypeName();
        cachedCustomerImage = group.GetCustomerTypeImage();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        ResolveUIReferences();
        ResolveNotepadFont();
        RebuildMenu();
        ResetSelection();
        HideReviewPanel(false);
        LoadRequestedOrder();
        RefreshRequestedOrderUI();
        ShowFoodTab();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnNotepadOpened(group);

        if (TutorialManager.Instance != null && tutorialHint != null)
            tutorialHint.Show("Read the order above. Match every meal, drink, and quantity below.");
    }

    public void Close()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (group != null)
        {
            group.EndPlayerOrderReview();
            if (group.state == CustomerGroup.GroupState.ReadyToOrder)
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                group.SetOrderTaskClaimedByStaff(false);
            }
        }

        group = null;
        requestedProducts.Clear();
        requestedContents.Clear();
        requestedOrderLines.Clear();
        cachedOpeningMessage = string.Empty;
        cachedCustomerTypeName = string.Empty;
        cachedCustomerImage = null;
        HideReviewPanel(false);
        ResetSelection();
        gameObject.SetActive(false);
    }

    public int GetPriceForItem(string item)
    {
        EnsureCatalog();
        Recipe product = catalog != null ? catalog.FindProduct(item) : null;
        return product != null ? product.EffectiveSellPrice : 0;
    }

    public bool TryGetBundleFoodPrice(List<string> contents, out int price)
    {
        price = 0;
        if (contents == null)
            return false;

        EnsureCatalog();
        if (catalog == null)
            return false;

        List<Recipe> foods = new List<Recipe>();
        List<Recipe> resolved = catalog.ResolveProducts(contents);
        for (int i = 0; i < resolved.Count; i++)
        {
            if (resolved[i].category == MenuProductCategory.Food)
                foods.Add(resolved[i]);
        }

        MenuBundle bundle = catalog.FindBundle(foods);
        if (bundle == null)
            return false;

        price = bundle.GetPrice();
        return true;
    }

    public int GetFoodTotalFromContents(List<string> contents)
    {
        if (contents == null)
            return 0;

        if (TryGetBundleFoodPrice(contents, out int bundlePrice))
            return bundlePrice;

        EnsureCatalog();
        if (catalog == null)
            return 0;

        int total = 0;
        List<Recipe> products = catalog.ResolveProducts(contents);
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i].category == MenuProductCategory.Food)
                total += products[i].EffectiveSellPrice;
        }

        return total;
    }

    public int GetDrinkTotalFromContents(List<string> contents)
    {
        if (contents == null)
            return 0;

        EnsureCatalog();
        if (catalog == null)
            return 0;

        int total = 0;
        List<Recipe> products = catalog.ResolveProducts(contents);
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i].category == MenuProductCategory.Drink)
                total += products[i].EffectiveSellPrice;
        }

        return total;
    }

    public int GetOrderTotalFromContents(List<string> contents)
    {
        EnsureCatalog();
        return catalog != null
            ? catalog.GetOrderTotal(contents)
            : GetFoodTotalFromContents(contents) + GetDrinkTotalFromContents(contents);
    }

    private void EnsureCatalog()
    {
        if (catalog == null)
            catalog = MenuCatalog.Default;
    }

    private void ResolveUIReferences()
    {
        if (tableNumberText == null)
            tableNumberText = FindText("Table Number");
        if (customerMessageText == null)
            customerMessageText = FindText("CustomerMessage");
        if (customerTypeText == null)
            customerTypeText = FindText("CustomerType");
        if (customerImage == null)
            customerImage = FindImage("CustomerImage");
        if (tutorialHint == null)
            tutorialHint = GetComponent<TutorialHintTextUI>();

        if (confirmButton == null)
            confirmButton = FindButton("Check Order") ?? FindButton("Send to Cashier");
        if (exitButton == null)
            exitButton = FindButton("ExitButton");
        if (foodTabButton == null)
            foodTabButton = FindButton("FoodTab");
        if (drinkTabButton == null)
            drinkTabButton = FindButton("DrinksTab");

        ScrollRect[] scrollRects = GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scroll = scrollRects[i];
            string objectName = scroll.gameObject.name.ToLowerInvariant();
            if (foodScrollRect == null && objectName.Contains("food"))
                foodScrollRect = scroll;
            else if (drinkScrollRect == null && objectName.Contains("drink"))
                drinkScrollRect = scroll;
        }

        if (foodContentRoot == null && foodScrollRect != null)
            foodContentRoot = foodScrollRect.content;
        if (drinkContentRoot == null && drinkScrollRect != null)
            drinkContentRoot = drinkScrollRect.content;

        // Compatibility with the older single-player notepad, which predates
        // the ScrollRect-based role scenes.
        if (foodContentRoot == null)
            foodContentRoot = FindRectTransform("Food");
        if (drinkContentRoot == null)
            drinkContentRoot = FindRectTransform("Drinks");

        if (foodPanel == null)
            foodPanel = foodScrollRect != null
                ? foodScrollRect.gameObject
                : foodContentRoot != null ? foodContentRoot.gameObject : null;
        if (drinkPanel == null)
            drinkPanel = drinkScrollRect != null
                ? drinkScrollRect.gameObject
                : drinkContentRoot != null ? drinkContentRoot.gameObject : null;

        if (requestedIconsRoot == null)
            requestedIconsRoot = FindRectTransform("OrderPicture");
        if (availableItemsRoot == null)
            availableItemsRoot = FindRectTransform("AvailableItems");

        ResolveNotepadFont();
    }

    private void BindStaticButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.RemoveListener(CheckOrder);
            confirmButton.onClick.AddListener(CheckOrder);
            SetButtonLabel(confirmButton, "CHECK ORDER");
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
            exitButton.onClick.AddListener(Close);
        }

        if (foodTabButton != null)
        {
            foodTabButton.onClick.RemoveListener(ShowFoodTab);
            foodTabButton.onClick.AddListener(ShowFoodTab);
        }

        if (drinkTabButton != null)
        {
            drinkTabButton.onClick.RemoveListener(ShowDrinkTab);
            drinkTabButton.onClick.AddListener(ShowDrinkTab);
        }
    }

    private void RebuildMenu()
    {
        EnsureCatalog();
        ResolveUIReferences();
        ResolveNotepadFont();
        BindStaticButtons();
        HideReviewPanel(false);
        menuEntries.Clear();

        ClearChildren(foodContentRoot);
        ClearChildren(drinkContentRoot);
        ClearChildren(availableItemsRoot);
        EnsureMenuLayout(foodContentRoot, foodScrollRect);
        EnsureMenuLayout(drinkContentRoot, drinkScrollRect);

        if (catalog == null)
        {
            Debug.LogError("[OrderChecklistUI] MenuCatalog is missing from Resources.", this);
            if (confirmButton != null)
                confirmButton.interactable = false;
            return;
        }

        if (confirmButton != null)
            confirmButton.interactable = true;

        List<Recipe> foods = catalog.GetProducts(MenuProductCategory.Food, false);
        List<MenuBundle> bundles = catalog.GetFoodBundles(false);
        List<Recipe> drinks = catalog.GetProducts(MenuProductCategory.Drink, false);

        for (int i = 0; i < foods.Count; i++)
            CreateProductEntry(foodContentRoot, foods[i]);

        for (int i = 0; i < bundles.Count; i++)
            CreateBundleEntry(foodContentRoot, bundles[i]);

        for (int i = 0; i < drinks.Count; i++)
            CreateProductEntry(drinkContentRoot, drinks[i]);

        FinalizeMenuLayout(foodContentRoot, foodScrollRect);
        FinalizeMenuLayout(drinkContentRoot, drinkScrollRect);

        RebuildAvailableItems(foods);
        RefreshMenuAvailability();
        Canvas.ForceUpdateCanvases();
        AlignMenuScrollbars();

        if (foodScrollRect != null)
            foodScrollRect.verticalNormalizedPosition = 1f;
        if (drinkScrollRect != null)
            drinkScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreateProductEntry(RectTransform parent, Recipe product)
    {
        if (parent == null || product == null)
            return;

        NotepadMenuEntryUI entry = NotepadMenuEntryUI.Create(
            menuEntryPrefab,
            parent,
            menuStyle);
        entry.Bind(product);
        entry.QuantityChanged += HandleEntryQuantityChanged;
        menuEntries.Add(entry);
    }

    private void CreateBundleEntry(RectTransform parent, MenuBundle bundle)
    {
        if (parent == null || bundle == null)
            return;

        NotepadMenuEntryUI entry = NotepadMenuEntryUI.Create(
            menuEntryPrefab,
            parent,
            menuStyle);
        entry.Bind(bundle);
        entry.QuantityChanged += HandleEntryQuantityChanged;
        menuEntries.Add(entry);
    }

    private void RefreshMenuAvailability()
    {
        for (int i = 0; i < menuEntries.Count; i++)
            menuEntries[i]?.RefreshAvailability();
    }

    private void ResetSelection()
    {
        for (int i = 0; i < menuEntries.Count; i++)
        {
            menuEntries[i]?.ClearReview();
            menuEntries[i]?.SetQuantityWithoutNotify(0);
        }
    }

    private void HandleEntryQuantityChanged(NotepadMenuEntryUI entry, int quantity)
    {
        if (reviewOverlay != null && reviewOverlay.activeSelf)
            HideReviewPanel(false);
    }

    private void ShowFoodTab()
    {
        if (foodTabButton == null && drinkTabButton == null)
        {
            if (foodPanel != null)
                foodPanel.SetActive(true);
            if (drinkPanel != null)
                drinkPanel.SetActive(true);
            return;
        }

        if (foodPanel != null)
            foodPanel.SetActive(true);
        if (drinkPanel != null && drinkPanel != foodPanel)
            drinkPanel.SetActive(false);
        if (foodScrollRect != null)
            foodScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ShowDrinkTab()
    {
        if (foodPanel != null && foodPanel != drinkPanel)
            foodPanel.SetActive(false);
        if (drinkPanel != null)
            drinkPanel.SetActive(true);
        if (drinkScrollRect != null)
            drinkScrollRect.verticalNormalizedPosition = 1f;
    }

    private void SubscribeToUnlocks()
    {
        if (unlockEventSubscribed)
            return;

        UnlockManager.OnRecipeUnlocked += HandleRecipeUnlocked;
        unlockEventSubscribed = true;
    }

    private void UnsubscribeFromUnlocks()
    {
        if (!unlockEventSubscribed)
            return;

        UnlockManager.OnRecipeUnlocked -= HandleRecipeUnlocked;
        unlockEventSubscribed = false;
    }

    private void HandleRecipeUnlocked(string recipeId)
    {
        RefreshMenuAvailability();
    }

    private void SubscribeToStock()
    {
        if (subscribedStockBridge == LobbyStockBridge.Instance)
            return;

        UnsubscribeFromStock();
        subscribedStockBridge = LobbyStockBridge.Instance;
        if (subscribedStockBridge != null)
            subscribedStockBridge.OnProductStockChanged += HandleProductStockChanged;
    }

    private void UnsubscribeFromStock()
    {
        if (subscribedStockBridge != null)
            subscribedStockBridge.OnProductStockChanged -= HandleProductStockChanged;

        subscribedStockBridge = null;
    }

    private void HandleProductStockChanged(Recipe _, int __)
    {
        RefreshMenuAvailability();
        if (catalog != null)
            RebuildAvailableItems(catalog.GetProducts(MenuProductCategory.Food, false));
    }

    private void LoadRequestedOrder()
    {
        requestedProducts.Clear();
        requestedContents.Clear();
        requestedOrderLines.Clear();
        EnsureCatalog();

        if (group == null || catalog == null)
            return;

        requestedProducts.AddRange(catalog.ResolveProducts(group.GetCurrentOrderProductIds()));
        if (requestedProducts.Count == 0)
            requestedProducts.AddRange(catalog.ResolveProducts(group.GetCurrentOrderContents()));

        requestedContents.AddRange(catalog.GetDisplayNames(requestedProducts));

        IReadOnlyList<CustomerGroup.OrderLine> sourceLines = group.GetCurrentOrderLines();
        for (int i = 0; i < sourceLines.Count; i++)
        {
            if (sourceLines[i] != null)
                requestedOrderLines.Add(sourceLines[i].Clone());
        }
    }

    private void RefreshRequestedOrderUI()
    {
        RefreshTableText();
        RefreshCustomerTypeUI();
        RefreshMessageFromRequestedOrder();
        RebuildRequestedIcons();
    }

    private void RefreshTableText()
    {
        if (tableNumberText == null)
            return;

        int number = group != null ? group.currentOrderNumber : 0;
        tableNumberText.text = number > 0 ? $"Table {number}" : "Table -";
    }

    private void RefreshCustomerTypeUI()
    {
        if (customerTypeText != null)
        {
            customerTypeText.text = string.IsNullOrWhiteSpace(cachedCustomerTypeName)
                ? "Customer Type: Regular"
                : $"Customer Type: {cachedCustomerTypeName}";
        }

        if (customerImage != null)
        {
            customerImage.sprite = cachedCustomerImage;
            customerImage.enabled = cachedCustomerImage != null;
            customerImage.gameObject.SetActive(cachedCustomerImage != null);
        }
    }

    private void RefreshMessageFromRequestedOrder()
    {
        if (customerMessageText == null)
            return;

        string sentence = GenerateSentence(requestedOrderLines, requestedProducts);
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (useTypewriter && gameObject.activeInHierarchy)
            typingRoutine = StartCoroutine(TypeSentence(sentence));
        else
            customerMessageText.text = sentence;
    }

    private IEnumerator TypeSentence(string sentence)
    {
        customerMessageText.text = string.Empty;
        for (int i = 0; i < sentence.Length; i++)
        {
            customerMessageText.text += sentence[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        typingRoutine = null;
    }

    private string GenerateSentence(
        IReadOnlyList<CustomerGroup.OrderLine> orderLines,
        IReadOnlyList<Recipe> fallbackProducts)
    {
        List<string> phrases = new List<string>();

        if (orderLines != null && orderLines.Count > 0)
        {
            for (int i = 0; i < orderLines.Count; i++)
            {
                CustomerGroup.OrderLine line = orderLines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.displayName))
                    continue;

                phrases.Add(FormatOrderLinePhrase(line));
            }
        }
        else if (fallbackProducts != null)
        {
            for (int i = 0; i < fallbackProducts.Count; i++)
            {
                Recipe product = fallbackProducts[i];
                if (product != null)
                    phrases.Add($"1 {product.DisplayName}");
            }
        }

        string orderSentence = phrases.Count > 0
            ? $"Can we have {JoinNaturally(phrases)}?"
            : "Order not found.";

        return string.IsNullOrWhiteSpace(cachedOpeningMessage)
            ? orderSentence
            : $"{cachedOpeningMessage} {orderSentence}";
    }

    private string FormatOrderLinePhrase(CustomerGroup.OrderLine line)
    {
        int quantity = Mathf.Max(1, line.quantity);
        if (!line.isBundle || catalog == null)
            return $"{quantity} {line.displayName}";

        List<Recipe> products = line.ResolveProducts(catalog);
        List<string> productNames = catalog.GetDisplayNames(products);
        if (productNames.Count == 2)
            return $"{quantity} {productNames[0]} bundled with {productNames[1]}";

        return $"{quantity} {line.displayName}";
    }

    private static string JoinNaturally(IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
            return string.Empty;
        if (values.Count == 1)
            return values[0];
        if (values.Count == 2)
            return $"{values[0]} and {values[1]}";

        string result = string.Empty;
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                result += i == values.Count - 1 ? ", and " : ", ";
            result += values[i];
        }

        return result;
    }

    private void RebuildRequestedIcons()
    {
        if (requestedIconsRoot == null)
            return;

        ClearChildren(requestedIconsRoot);
        HorizontalLayoutGroup layout = requestedIconsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = requestedIconsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        List<CustomerGroup.OrderLine> displayLines = new List<CustomerGroup.OrderLine>();
        if (requestedOrderLines.Count > 0)
        {
            displayLines.AddRange(requestedOrderLines);
        }
        else
        {
            for (int i = 0; i < requestedProducts.Count; i++)
            {
                Recipe product = requestedProducts[i];
                if (product == null)
                    continue;

                CustomerGroup.OrderLine existing = displayLines.Find(
                    line => line != null && !line.isBundle &&
                        string.Equals(line.itemId, product.ProductId,
                            System.StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.quantity++;
                    continue;
                }

                CustomerGroup.OrderLine line = new CustomerGroup.OrderLine();
                line.SetProduct(product);
                displayLines.Add(line);
            }
        }

        for (int i = 0; i < displayLines.Count; i++)
            CreateRequestedOrderLine(displayLines[i]);
    }

    private void CreateRequestedOrderLine(CustomerGroup.OrderLine line)
    {
        if (line == null || requestedIconsRoot == null)
            return;

        List<Recipe> products = line.ResolveProducts(catalog);
        if (products.Count == 0)
            return;

        float iconSize = products.Count > 1 ? 42f : 54f;
        float iconsWidth = products.Count * iconSize + Mathf.Max(0, products.Count - 1) * 2f;
        float entryWidth = Mathf.Max(78f, iconsWidth + 28f);

        GameObject entryObject = new GameObject($"Order Line - {line.displayName}",
            typeof(RectTransform), typeof(LayoutElement));
        entryObject.layer = requestedIconsRoot.gameObject.layer;
        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.SetParent(requestedIconsRoot, false);
        entryRect.sizeDelta = new Vector2(entryWidth, 76f);

        LayoutElement element = entryObject.GetComponent<LayoutElement>();
        element.minWidth = entryWidth;
        element.preferredWidth = entryWidth;
        element.minHeight = 76f;
        element.preferredHeight = 76f;

        float startX = -iconsWidth * 0.5f + iconSize * 0.5f - 8f;
        for (int i = 0; i < products.Count; i++)
        {
            GameObject iconObject = new GameObject($"Icon - {products[i].DisplayName}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = entryObject.layer;
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(entryRect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(startX + i * (iconSize + 2f), 6f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            Image image = iconObject.GetComponent<Image>();
            image.sprite = products[i].sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
        }

        GameObject quantityObject = new GameObject("Quantity",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        quantityObject.layer = entryObject.layer;
        RectTransform quantityRect = quantityObject.GetComponent<RectTransform>();
        quantityRect.SetParent(entryRect, false);
        quantityRect.anchorMin = new Vector2(1f, 0f);
        quantityRect.anchorMax = new Vector2(1f, 0f);
        quantityRect.pivot = new Vector2(1f, 0f);
        quantityRect.anchoredPosition = Vector2.zero;
        quantityRect.sizeDelta = new Vector2(38f, 28f);

        TextMeshProUGUI quantityText = quantityObject.GetComponent<TextMeshProUGUI>();
        ApplyNotepadFont(quantityText);
        quantityText.text = $"x{Mathf.Max(1, line.quantity)}";
        quantityText.fontSize = 20f;
        quantityText.fontStyle = FontStyles.Bold;
        quantityText.alignment = TextAlignmentOptions.BottomRight;
        quantityText.color = Color.white;
        quantityText.raycastTarget = false;
        quantityText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RebuildAvailableItems(IReadOnlyList<Recipe> foods)
    {
        if (availableItemsRoot == null)
            return;

        ClearChildren(availableItemsRoot);

        HorizontalLayoutGroup layout = availableItemsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = availableItemsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < foods.Count; i++)
        {
            Recipe product = foods[i];
            int stock = LobbyStockBridge.Instance != null
                ? LobbyStockBridge.Instance.GetProductStock(product)
                : 0;

            GameObject item = new GameObject($"Stock - {product.DisplayName}",
                typeof(RectTransform), typeof(LayoutElement));
            item.layer = availableItemsRoot.gameObject.layer;
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(availableItemsRoot, false);
            rect.sizeDelta = new Vector2(118f, 42f);

            LayoutElement element = item.GetComponent<LayoutElement>();
            element.preferredWidth = 118f;
            element.preferredHeight = 42f;

            TextMeshProUGUI text = item.AddComponent<TextMeshProUGUI>();
            ApplyNotepadFont(text);
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = $"{product.DisplayName}: {Mathf.Max(0, stock)}";
        }
    }

    private void CheckOrder()
    {
        if (!CanReviewCurrentOrder())
            return;

        EnsureReviewPanel();
        OrderReviewResult review = EvaluateOrderSelection(true);
        ShowReviewPanel(review);
    }

    private bool CanReviewCurrentOrder()
    {
        if (group == null)
            return false;

        if (!group.IsPlayerReviewingOrder ||
            group.state != CustomerGroup.GroupState.ReadyToOrder)
        {
            ShowWarning("This order is no longer waiting for your confirmation.");
            Close();
            return false;
        }

        return true;
    }

    private OrderReviewResult EvaluateOrderSelection(bool applyVisuals)
    {
        OrderReviewResult result = new OrderReviewResult();
        Dictionary<string, ExpectedOrderItem> expected = BuildExpectedOrderMap();
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < menuEntries.Count; i++)
        {
            NotepadMenuEntryUI entry = menuEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                continue;

            string key = BuildOrderKey(
                entry.Kind == NotepadMenuEntryUI.EntryKind.Bundle,
                entry.ItemId);
            expected.TryGetValue(key, out ExpectedOrderItem expectedItem);
            int expectedQuantity = expectedItem != null ? expectedItem.quantity : 0;
            int selectedQuantity = entry.SelectedQuantity;
            NotepadMenuEntryUI.ReviewState state =
                NotepadMenuEntryUI.ClassifyReview(expectedQuantity, selectedQuantity);

            if (applyVisuals)
                entry.ApplyReview(expectedQuantity);

            visited.Add(key);
            if (state == NotepadMenuEntryUI.ReviewState.None ||
                state == NotepadMenuEntryUI.ReviewState.Correct)
            {
                continue;
            }

            if (result.firstMismatch == null)
                result.firstMismatch = entry;

            string displayName = expectedItem != null &&
                !string.IsNullOrWhiteSpace(expectedItem.displayName)
                ? expectedItem.displayName
                : entry.DisplayName;
            result.messages.Add(FormatReviewMessage(
                displayName, expectedQuantity, selectedQuantity));
        }

        foreach (KeyValuePair<string, ExpectedOrderItem> pair in expected)
        {
            if (visited.Contains(pair.Key))
                continue;

            ExpectedOrderItem item = pair.Value;
            result.messages.Add($"Missing {item.displayName} x{item.quantity}.");
        }

        return result;
    }

    private Dictionary<string, ExpectedOrderItem> BuildExpectedOrderMap()
    {
        Dictionary<string, ExpectedOrderItem> result =
            new Dictionary<string, ExpectedOrderItem>(StringComparer.OrdinalIgnoreCase);

        if (requestedOrderLines.Count > 0)
        {
            for (int i = 0; i < requestedOrderLines.Count; i++)
            {
                CustomerGroup.OrderLine line = requestedOrderLines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.itemId))
                    continue;

                AddExpectedOrderItem(result, BuildOrderKey(line.isBundle, line.itemId),
                    line.displayName, Mathf.Max(1, line.quantity));
            }

            return result;
        }

        // Compatibility for older orders created before quantity-aware lines.
        for (int i = 0; i < requestedProducts.Count; i++)
        {
            Recipe product = requestedProducts[i];
            if (product == null)
                continue;

            AddExpectedOrderItem(result, BuildOrderKey(false, product.ProductId),
                product.DisplayName, 1);
        }

        return result;
    }

    private static void AddExpectedOrderItem(
        IDictionary<string, ExpectedOrderItem> destination,
        string key,
        string displayName,
        int quantity)
    {
        if (destination.TryGetValue(key, out ExpectedOrderItem existing))
        {
            existing.quantity += Mathf.Max(1, quantity);
            return;
        }

        destination[key] = new ExpectedOrderItem
        {
            displayName = displayName,
            quantity = Mathf.Max(1, quantity)
        };
    }

    private static string BuildOrderKey(bool isBundle, string itemId)
    {
        return $"{(isBundle ? "bundle" : "product")}:{itemId?.Trim()}";
    }

    private static string FormatReviewMessage(
        string displayName,
        int expectedQuantity,
        int selectedQuantity)
    {
        if (expectedQuantity <= 0)
            return $"{displayName} was not ordered. Remove x{selectedQuantity}.";
        if (selectedQuantity <= 0)
            return $"Missing {displayName} x{expectedQuantity}.";
        if (selectedQuantity < expectedQuantity)
            return $"{displayName}: selected x{selectedQuantity}, needs x{expectedQuantity}.";
        return $"{displayName}: selected x{selectedQuantity}, needs x{expectedQuantity}.";
    }

    private void Confirm()
    {
        if (!CanReviewCurrentOrder())
            return;

        OrderReviewResult review = EvaluateOrderSelection(false);
        if (!review.IsCorrect)
        {
            EnsureReviewPanel();
            EvaluateOrderSelection(true);
            ShowReviewPanel(review);
            return;
        }

        if (!TryBuildSelection(
            out List<CustomerGroup.OrderLine> selectedLines,
            out List<Recipe> selectedProducts,
            out string orderName,
            out int unitPrice,
            out CustomerGroup.FoodType mainFood,
            out CustomerGroup.DrinkType selectedDrink))
            return;

        if (LobbyStockBridge.Instance != null)
        {
            if (!LobbyStockBridge.Instance.HasOrderStock(selectedProducts))
            {
                ShowWarning("One or more products in this order are no longer available.");
                RebuildMenu();
                return;
            }

            if (!LobbyStockBridge.Instance.TryUseOrderStock(selectedProducts))
            {
                ShowWarning("Stock changed before the order could be submitted. Please try again.");
                RebuildMenu();
                return;
            }
        }

        if (group.submittedOrder == null)
            group.submittedOrder = new CustomerGroup.SimpleOrder();

        group.submittedOrder.SetLines(selectedLines, catalog);
        group.submittedOrder.name = orderName;
        group.submittedOrder.unitPrice = unitPrice;

        if (!group.ConfirmPlayerReviewedOrder(mainFood, selectedDrink))
        {
            ShowWarning("The customer order changed before it could be confirmed.");
            Close();
            return;
        }
        RestaurantTaskClaim.Complete(group);

        if (group.IsTakeout)
        {
            ProcessingBillIndicatorUI.Instance?.ShowForSeconds(
                "Order confirmed — awaiting payment", 2f);
        }
        else
        {
            // The player-review lock has been released only by the successful
            // confirmation above, so this is the first valid point at which a
            // manager-assisted order may enter the kitchen.
            KitchenManager kitchen = FindFirstObjectByType<KitchenManager>();
            if (kitchen == null || !kitchen.ProcessOrder(group))
            {
                ShowWarning("The kitchen could not start this order.");
                Close();
                return;
            }

            ProcessingBillIndicatorUI.Instance?.ShowForSeconds(
                "Order Sent to Kitchen", 2f);
        }

        TutorialManager.Instance?.OnOrderConfirmed(group);
        Close();
    }

    private bool TryBuildSelection(
        out List<CustomerGroup.OrderLine> selectedLines,
        out List<Recipe> selectedProducts,
        out string orderName,
        out int unitPrice,
        out CustomerGroup.FoodType mainFood,
        out CustomerGroup.DrinkType selectedDrink)
    {
        selectedLines = new List<CustomerGroup.OrderLine>();
        selectedProducts = new List<Recipe>();
        orderName = string.Empty;
        unitPrice = 0;
        mainFood = CustomerGroup.FoodType.Chicken;
        selectedDrink = CustomerGroup.DrinkType.Coke;

        EnsureCatalog();
        if (catalog == null)
        {
            ShowWarning("The menu catalog could not be loaded.");
            return false;
        }

        int mealCount = 0;
        int drinkCount = 0;
        bool foundMainFood = false;
        bool foundDrink = false;

        for (int i = 0; i < menuEntries.Count; i++)
        {
            NotepadMenuEntryUI entry = menuEntries[i];
            int lineQuantity = entry != null ? entry.SelectedQuantity : 0;
            if (entry == null || lineQuantity <= 0)
                continue;

            CustomerGroup.OrderLine line = new CustomerGroup.OrderLine();
            List<Recipe> lineProducts = new List<Recipe>();

            if (entry.Kind == NotepadMenuEntryUI.EntryKind.Bundle)
            {
                MenuBundle bundle = entry.Bundle;
                if (bundle == null || bundle.products.Count == 0)
                    continue;

                line.SetBundle(bundle, lineQuantity);
                lineProducts.AddRange(bundle.products);
                mealCount += lineQuantity;

                if (!foundMainFood && bundle.products[0] != null)
                {
                    mainFood = ToLegacyFoodType(bundle.products[0]);
                    foundMainFood = true;
                }
            }
            else
            {
                Recipe product = entry.Product;
                if (product == null)
                    continue;

                line.SetProduct(product, lineQuantity);
                lineProducts.Add(product);

                if (product.category == MenuProductCategory.Drink)
                {
                    drinkCount += lineQuantity;
                    if (!foundDrink)
                    {
                        selectedDrink = ToLegacyDrinkType(product);
                        foundDrink = true;
                    }
                }
                else
                {
                    mealCount += lineQuantity;
                    if (!foundMainFood)
                    {
                        mainFood = ToLegacyFoodType(product);
                        foundMainFood = true;
                    }
                }
            }

            selectedLines.Add(line);
            unitPrice += line.TotalPrice;
            for (int copy = 0; copy < lineQuantity; copy++)
                selectedProducts.AddRange(lineProducts);
        }

        int expectedMeals = Mathf.Max(1, group != null ? group.Size : 1);
        if (mealCount != expectedMeals)
        {
            ShowWarning($"Select exactly {expectedMeals} meal{(expectedMeals == 1 ? string.Empty : "s")} for this group.");
            return false;
        }

        if (drinkCount != expectedMeals)
        {
            ShowWarning($"Select exactly {expectedMeals} drink{(expectedMeals == 1 ? string.Empty : "s")} for this group.");
            return false;
        }

        orderName = selectedLines.Count == 1
            ? selectedLines[0].displayName
            : "Group Order";
        return selectedProducts.Count > 0;
    }

    private static CustomerGroup.FoodType ToLegacyFoodType(Recipe product)
    {
        if (product == null)
            return CustomerGroup.FoodType.Chicken;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Fries: return CustomerGroup.FoodType.Fries;
            case ItemTypeKitchen.Burger: return CustomerGroup.FoodType.Burger;
            default: return CustomerGroup.FoodType.Chicken;
        }
    }

    private static CustomerGroup.DrinkType ToLegacyDrinkType(Recipe product)
    {
        if (product == null)
            return CustomerGroup.DrinkType.Coke;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Pineapple: return CustomerGroup.DrinkType.Pineapple;
            case ItemTypeKitchen.IcedTea: return CustomerGroup.DrinkType.IceTea;
            default: return CustomerGroup.DrinkType.Coke;
        }
    }

    private void EnsureReviewPanel()
    {
        if (reviewOverlay != null)
        {
            ResolveReviewPanelReferences();
            BindReviewPanelButtons();
            return;
        }

        RectTransform parent = transform as RectTransform;
        if (parent == null)
            return;

        reviewOverlay = new GameObject("Order Check Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        reviewOverlay.layer = gameObject.layer;
        RectTransform overlayRect = reviewOverlay.GetComponent<RectTransform>();
        overlayRect.SetParent(parent, false);
        ConfigureRuntimeRect(overlayRect, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image overlayImage = reviewOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
        overlayImage.raycastTarget = true;

        GameObject panel = new GameObject("Short Confirmation Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = gameObject.layer;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(overlayRect, false);
        ConfigureRuntimeRect(panelRect, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f), new Vector2(570f, 330f));
        reviewPanelImage = panel.GetComponent<Image>();
        reviewPanelImage.color = new Color(0.055f, 0.22f, 0.34f, 0.98f);
        reviewPanelImage.sprite = menuStyle.entryBackgroundSprite;
        reviewPanelImage.type = reviewPanelImage.sprite != null
            ? Image.Type.Sliced
            : Image.Type.Simple;

        reviewTitleText = CreateRuntimeText("Title", panelRect, 34f,
            FontStyles.Bold, TextAlignmentOptions.Center);
        ConfigureRuntimeRect(reviewTitleText.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -20f),
            new Vector2(520f, 54f));

        reviewSummaryText = CreateRuntimeText("Summary", panelRect, 22f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        ConfigureRuntimeRect(reviewSummaryText.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 9f),
            new Vector2(500f, 166f));
        reviewSummaryText.textWrappingMode = TextWrappingModes.Normal;
        reviewSummaryText.overflowMode = TextOverflowModes.Ellipsis;

        reviewBackButton = CreateRuntimeButton("Fix Order", panelRect,
            "FIX ORDER", new Color(0.16f, 0.42f, 0.66f, 1f));
        ConfigureRuntimeRect(reviewBackButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-132f, 22f),
            new Vector2(230f, 60f));

        reviewSubmitButton = CreateRuntimeButton("Confirm Correct Order", panelRect,
            "CONFIRM ORDER", new Color(0.18f, 0.66f, 0.34f, 1f));
        ConfigureRuntimeRect(reviewSubmitButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(132f, 22f),
            new Vector2(230f, 60f));
        BindReviewPanelButtons();

        reviewOverlay.SetActive(false);
    }

    private void ResolveReviewPanelReferences()
    {
        if (reviewOverlay == null)
            return;

        Image[] images = reviewOverlay.GetComponentsInChildren<Image>(true);
        TMP_Text[] texts = reviewOverlay.GetComponentsInChildren<TMP_Text>(true);
        Button[] buttons = reviewOverlay.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < images.Length && reviewPanelImage == null; i++)
        {
            if (images[i].gameObject.name == "Short Confirmation Panel")
                reviewPanelImage = images[i];
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (reviewTitleText == null && texts[i].gameObject.name == "Title")
                reviewTitleText = texts[i];
            else if (reviewSummaryText == null && texts[i].gameObject.name == "Summary")
                reviewSummaryText = texts[i];
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (reviewBackButton == null && buttons[i].gameObject.name == "Fix Order")
                reviewBackButton = buttons[i];
            else if (reviewSubmitButton == null &&
                buttons[i].gameObject.name == "Confirm Correct Order")
            {
                reviewSubmitButton = buttons[i];
            }
        }
    }

    private void BindReviewPanelButtons()
    {
        if (reviewBackButton != null)
        {
            reviewBackButton.onClick.RemoveListener(BackToOrder);
            reviewBackButton.onClick.AddListener(BackToOrder);
        }

        if (reviewSubmitButton != null)
        {
            reviewSubmitButton.onClick.RemoveListener(Confirm);
            reviewSubmitButton.onClick.AddListener(Confirm);
        }
    }

    private void BackToOrder()
    {
        HideReviewPanel(true);
    }

    private void ShowReviewPanel(OrderReviewResult review)
    {
        if (reviewOverlay == null || review == null)
            return;

        reviewFocusEntry = review.firstMismatch;
        bool correct = review.IsCorrect;
        reviewTitleText.text = correct ? "ORDER MATCHES" : "ORDER NEEDS FIXING";
        reviewTitleText.color = correct ? correctTitleColor : errorTitleColor;
        reviewPanelImage.color = correct ? correctPanelColor : errorPanelColor;

        if (correct)
        {
            reviewSummaryText.alignment = TextAlignmentOptions.Center;
            reviewSummaryText.text =
                "Everything matches the customer's order.\n\nConfirm to send it forward.";
            reviewSummaryText.color = Color.white;
            reviewSubmitButton.interactable = true;
            SetButtonLabel(reviewSubmitButton, "CONFIRM ORDER");
            SetButtonLabel(reviewBackButton, "BACK");
        }
        else
        {
            reviewSummaryText.alignment = TextAlignmentOptions.TopLeft;
            reviewSummaryText.color = errorSummaryColor;
            reviewSummaryText.text = BuildReviewSummary(review.messages);
            reviewSubmitButton.interactable = false;
            SetButtonLabel(reviewSubmitButton, "FIX ERRORS FIRST");
            SetButtonLabel(reviewBackButton, "FIX ORDER");
        }

        reviewOverlay.SetActive(true);
        reviewOverlay.transform.SetAsLastSibling();
    }

    private static string BuildReviewSummary(IReadOnlyList<string> messages)
    {
        if (messages == null || messages.Count == 0)
            return "No mismatch details were found.";

        int visibleCount = Mathf.Min(messages.Count, 6);
        string result = string.Empty;
        for (int i = 0; i < visibleCount; i++)
            result += $"- {messages[i]}{(i < visibleCount - 1 ? "\n" : string.Empty)}";

        if (messages.Count > visibleCount)
            result += $"\n- Plus {messages.Count - visibleCount} more item(s).";
        return result;
    }

    private void HideReviewPanel(bool showMismatchFeedback)
    {
        if (reviewOverlay != null)
            reviewOverlay.SetActive(false);

        if (!showMismatchFeedback)
        {
            reviewFocusEntry = null;
            return;
        }

        OrderReviewResult review = EvaluateOrderSelection(true);
        reviewFocusEntry = review.firstMismatch;
        FocusFirstMismatch();
    }

    private void FocusFirstMismatch()
    {
        if (reviewFocusEntry == null)
            return;

        if (reviewFocusEntry.Category == MenuProductCategory.Drink)
            ShowDrinkTab();
        else
            ShowFoodTab();
    }

    private void ResolveNotepadFont()
    {
        if (notepadFont == null && customerMessageText != null)
            notepadFont = customerMessageText.font;
        if (notepadFont == null && tableNumberText != null)
            notepadFont = tableNumberText.font;
        if (notepadFont == null && confirmButton != null)
        {
            TMP_Text label = confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                notepadFont = label.font;
        }

        if (menuStyle != null)
            menuStyle.fontAsset = notepadFont;
    }

    private TMP_Text CreateRuntimeText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject child = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.layer = gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.font = notepadFont != null ? notepadFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private Button CreateRuntimeButton(
        string objectName,
        Transform parent,
        string label,
        Color color)
    {
        GameObject child = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        child.layer = gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        if (confirmButton != null && confirmButton.image != null)
        {
            image.sprite = confirmButton.image.sprite;
            image.type = confirmButton.image.type;
        }

        Button button = child.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.34f, 0.34f, 0.34f, 0.75f);
        button.colors = colors;

        TMP_Text text = CreateRuntimeText("Label", rect, 23f,
            FontStyles.Bold, TextAlignmentOptions.Center);
        ConfigureRuntimeRect(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -6f));
        text.text = label;
        return button;
    }

    private void ApplyNotepadFont(TMP_Text text)
    {
        if (text != null && notepadFont != null)
            text.font = notepadFont;
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;

        text.text = label;
        ApplyNotepadFont(text);
    }

    private static void ConfigureRuntimeRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void EnsureMenuLayout(RectTransform root, ScrollRect scrollRect)
    {
        if (root == null)
            return;

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.enabled = false;

        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = Vector2.zero;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;

        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalNormalizedPosition = 1f;
            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(true);
        }
    }

    private static void FinalizeMenuLayout(RectTransform root, ScrollRect scrollRect)
    {
        if (root == null)
            return;

        Rect viewport = GetMenuViewportRect(scrollRect, root);
        const float padding = 12f;
        const float spacing = 12f;

        Vector2 authoredCardSize = GetAuthoredCardSize(root);
        float cardWidth = authoredCardSize.x;
        float cardHeight = authoredCardSize.y;

        float availableWidth = Mathf.Max(cardWidth, viewport.width - padding * 2f);
        int columnCount = Mathf.Clamp(
            Mathf.FloorToInt((availableWidth + spacing) / (cardWidth + spacing)),
            1,
            3);
        float gridWidth = columnCount * cardWidth + Mathf.Max(0, columnCount - 1) * spacing;
        float horizontalInset = Mathf.Max(padding, (viewport.width - gridWidth) * 0.5f);

        int itemCount = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform card = root.GetChild(i) as RectTransform;
            if (card == null)
                continue;

            int column = itemCount % columnCount;
            int row = itemCount / columnCount;
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(
                horizontalInset + column * (cardWidth + spacing),
                -padding - row * (cardHeight + spacing));
            card.sizeDelta = new Vector2(cardWidth, cardHeight);
            card.localScale = Vector3.one;

            itemCount++;
        }

        int rowCount = Mathf.CeilToInt(itemCount / (float)columnCount);
        float requiredHeight = padding * 2f +
                               rowCount * cardHeight +
                               Mathf.Max(0, rowCount - 1) * spacing;
        root.sizeDelta = new Vector2(
            viewport.width,
            Mathf.Max(viewport.height, requiredHeight));
    }

    private static Vector2 GetAuthoredCardSize(RectTransform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            NotepadMenuEntryUI entry = root.GetChild(i).GetComponent<NotepadMenuEntryUI>();
            if (entry == null)
                continue;

            Vector2 size = entry.AuthoredCardSize;
            if (size.x > 0f && size.y > 0f)
                return size;
        }

        return new Vector2(174f, 218f);
    }

    private static Rect GetMenuViewportRect(ScrollRect scrollRect, RectTransform fallback)
    {
        if (scrollRect != null)
        {
            if (scrollRect.viewport != null)
                return scrollRect.viewport.rect;

            if (scrollRect.transform is RectTransform scrollRectTransform)
                return scrollRectTransform.rect;
        }

        return fallback != null ? fallback.rect : new Rect(0f, 0f, 600f, 400f);
    }

    private void CreateSectionHeader(RectTransform parent, string title)
    {
        if (parent == null)
            return;

        GameObject header = new GameObject($"Section - {title}",
            typeof(RectTransform), typeof(LayoutElement));
        header.layer = parent.gameObject.layer;
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(450f, 34f);

        LayoutElement element = header.GetComponent<LayoutElement>();
        element.minHeight = 34f;
        element.preferredHeight = 34f;

        TextMeshProUGUI text = header.AddComponent<TextMeshProUGUI>();
        ApplyNotepadFont(text);
        text.text = title;
        text.fontSize = 22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private TMP_Text FindText(string objectName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == objectName)
                return texts[i];
        }

        return null;
    }

    private Image FindImage(string objectName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].gameObject.name == objectName)
                return images[i];
        }

        return null;
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].gameObject.name == objectName)
                return buttons[i];
        }

        return null;
    }

    private RectTransform FindRectTransform(string objectName)
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].gameObject.name == objectName)
                return rects[i];
        }

        return null;
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void ShowWarning(string message)
    {
        Debug.Log("[OrderChecklistUI] " + message);
        WarningSlideUI popup = FindFirstObjectByType<WarningSlideUI>();
        popup?.Show(message);
    }
}
