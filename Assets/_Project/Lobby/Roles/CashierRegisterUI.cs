using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CashierRegisterUI : MonoBehaviour
{
    public static CashierRegisterUI Instance { get; private set; }

    public static event System.Action OnHidden;

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Current Order")]
    [SerializeField] private TMP_Text tableNumberText;

    [Header("Food")]
    [SerializeField] private Image foodImage;
    [SerializeField] private Image foodImage2;
    [SerializeField] private TMP_Text foodPriceText;

    [Header("Drink")]
    [SerializeField] private Image drinkImage;
    [SerializeField] private TMP_Text drinkPriceText;

    [Header("Totals")]
    [SerializeField] private TMP_Text receivedText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text changeText;

    [Header("Change Pad")]
    [SerializeField] private TMP_Text cashierChangeText;
    [SerializeField] private Button undoButton;

    [Header("Peso Buttons - Bills")]
    [SerializeField] private Button bill1000Button;
    [SerializeField] private Button bill500Button;
    [SerializeField] private Button bill200Button;
    [SerializeField] private Button bill100Button;
    [SerializeField] private Button bill50Button;

    [Header("Peso Buttons - Coins")]
    [SerializeField] private Button coin20Button;
    [SerializeField] private Button coin10Button;
    [SerializeField] private Button coin5Button;
    [SerializeField] private Button coin1Button;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;

    [Header("Input Colors")]
    [SerializeField] private Color normalInputColor = Color.white;
    [SerializeField] private Color wrongInputColor = Color.red;
    [SerializeField] private Color correctInputColor = Color.green;

    [SerializeField] private TutorialHintTextUI tutorialHint;

    [Header("Mobile Presentation (Editable)")]
    [Tooltip("Uses the alternate compact values on every platform so the Editor remains the source of truth.")]
    [SerializeField] private bool useAlternateCompactPresentation;
    [SerializeField, Min(80f)] private float mobileCompactItemsWidth = 146f;
    [SerializeField, Min(28f)] private float mobileCompactItemsHeight = 46f;
    [SerializeField, Min(28f)] private float mobileCompactMaximumCellWidth = 52f;
    [SerializeField, Min(48f)] private float mobileCompactPriceWidth = 76f;
    [Header("Desktop Compact Layout (Editable)")]
    [SerializeField, Min(80f)] private float desktopCompactItemsWidth = 136f;
    [SerializeField, Min(28f)] private float desktopCompactItemsHeight = 36f;
    [SerializeField, Min(28f)] private float desktopCompactMaximumCellWidth = 48f;
    [SerializeField, Min(48f)] private float desktopCompactPriceWidth = 82f;

    private int receivedAmount;
    private int totalAmount;
    private int expectedChange;
    private int inputChangeAmount;

    private CustomerGroup activeGroup;
    private bool isOpen;
    private bool buttonsBound;

    private RectTransform compactFoodRoot;
    private RectTransform compactDrinkRoot;

    // The Lobby1 Food/Drink rows are 230 px wide. Keep a dedicated price
    // column and a visible gap so a full four-person order cannot collide
    // with, or render outside, its row.
    private float CompactItemsWidth => useAlternateCompactPresentation
        ? mobileCompactItemsWidth
        : desktopCompactItemsWidth;
    private float CompactItemsHeight => useAlternateCompactPresentation
        ? mobileCompactItemsHeight
        : desktopCompactItemsHeight;
    private float CompactMaxCellWidth => useAlternateCompactPresentation
        ? mobileCompactMaximumCellWidth
        : desktopCompactMaximumCellWidth;
    private float CompactPriceWidth => useAlternateCompactPresentation
        ? mobileCompactPriceWidth
        : desktopCompactPriceWidth;

    private sealed class CompactOrderLine
    {
        public readonly List<Recipe> products = new List<Recipe>();
        public int quantity = 1;
    }

    // Tracks whether this register session ended with a successful Confirm().
    // Set to true by Confirm(), reset to false by OpenForPayment() and CloseRegister().
    // Used to detect abandoned sessions for cash error tracking.
    private bool sessionConfirmed;

    public bool IsOpen
    {
        get
        {
            ResolveRoot();

            if (root == null)
                return false;

            return isOpen && root.activeInHierarchy;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveRoot();
        BindButtons();
        ResetDisplay();
        HideImmediate();
    }

    private void LateUpdate()
    {
        ResolveRoot();

        if (root == null || !isOpen)
            return;

        // If the root went inactive while the register is open, something external
        // disabled it without going through Hide(). Log it and reset state cleanly.
        if (!root.activeInHierarchy)
        {
            Debug.LogError($"[CashierRegisterUI] Root '{root.name}' was deactivated externally while open. Closing register.", this);
            ForceClosedState(true);
        }
    }

    private void OnDisable()
    {
        // Fired when this GameObject itself is disabled externally while the register is open.
        // Log the full stack trace so the caller can be identified and fixed.
        if (isOpen)
        {
            Debug.LogError(
                $"[CashierRegisterUI] OnDisable — this GameObject was disabled while isOpen=true! Caller:\n{new System.Diagnostics.StackTrace(true)}",
                this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveRoot()
    {
        if (root == null || root == gameObject)
        {
            Transform pos = transform.Find("POS");
            if (pos != null)
            {
                root = pos.gameObject;
                return;
            }

            if (transform.childCount > 0)
            {
                root = transform.GetChild(0).gameObject;
                return;
            }

            root = gameObject;
        }
    }

    private void BindButtons()
    {
        if (buttonsBound)
            return;

        buttonsBound = true;

        BindMoneyButton(bill1000Button, 1000);
        BindMoneyButton(bill500Button, 500);
        BindMoneyButton(bill200Button, 200);
        BindMoneyButton(bill100Button, 100);
        BindMoneyButton(bill50Button, 50);

        BindMoneyButton(coin20Button, 20);
        BindMoneyButton(coin10Button, 10);
        BindMoneyButton(coin5Button, 5);
        BindMoneyButton(coin1Button, 1);

        if (undoButton != null)
        {
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(UndoLastInput);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }
    }

    private void BindMoneyButton(Button button, int value)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => AddChangeInput(value));
    }

    public void Show()
    {
        ResolveRoot();

        if (root != null)
        {
            root.SetActive(true);

            SetParentsActive(root.transform);
            SetHierarchyActive(root.transform);

            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 1f;
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }
        }

        if (transform.parent != null)
            transform.SetAsLastSibling();

        isOpen = true;
        Debug.Log($"[CashierRegisterUI] Show() called — root={root?.name ?? "NULL"} isOpen={isOpen}", this);
    }

    public void Hide()
    {
        ResolveRoot();

        if (root != null)
        {
            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 0f;
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
            }

            root.SetActive(false);
        }

        ForceClosedState(true);

        Debug.Log($"[CashierRegisterUI] Hide() called — root={root?.name ?? "NULL"} caller={new System.Diagnostics.StackTrace().ToString().Split('\n')[1].Trim()}", this);
    }

    private void HideImmediate()
    {
        ResolveRoot();

        if (root != null)
        {
            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 0f;
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
            }

            root.SetActive(false);
        }

        ForceClosedState(false);
    }

    private void ForceClosedState(bool invokeEvent)
    {
        bool wasOpen = isOpen;
        isOpen = false;

        if (invokeEvent && wasOpen)
            OnHidden?.Invoke();
    }

    private void SetParentsActive(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private void SetHierarchyActive(Transform target)
    {
        if (target == null)
            return;

        target.gameObject.SetActive(true);

        for (int i = 0; i < target.childCount; i++)
            SetHierarchyActive(target.GetChild(i));
    }

    public void OpenForPayment(CustomerGroup group, int received, int total)
    {
        activeGroup = group;
        receivedAmount = Mathf.Max(0, received);
        totalAmount = Mathf.Max(0, total);
        expectedChange = Mathf.Max(0, receivedAmount - totalAmount);
        inputChangeAmount = 0;
        sessionConfirmed = false;

        Show();

        // Refresh AFTER Show() so any OnEnable or tutorial callbacks
        // that run during Show() cannot overwrite the display values.
        RefreshOrderDisplay();
        RefreshTotalsDisplay();
        RefreshInputDisplay();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnCashierOpened(activeGroup, expectedChange);

        if (TutorialManager.Instance != null && tutorialHint != null)
            tutorialHint.Show($"Give the exact change: {expectedChange:0.00}");
    }

    public void CloseRegister()
    {
        // If this session was opened (a group was present) but never successfully confirmed,
        // it counts as a cash-handling error — the waiter abandoned the transaction.
        if (activeGroup != null && !sessionConfirmed)
            GameDayManager.Instance?.RegisterCashError();

        activeGroup = null;
        receivedAmount = 0;
        totalAmount = 0;
        expectedChange = 0;
        inputChangeAmount = 0;
        sessionConfirmed = false;

        ResetDisplay();
        Hide();
    }

    private void AddChangeInput(int value)
    {
        if (!IsOpen)
            return;

        inputChangeAmount += value;
        RefreshInputDisplay();
    }

    private void UndoLastInput()
    {
        if (!IsOpen)
            return;

        inputChangeAmount = 0;
        RefreshInputDisplay();
    }

    private void Confirm()
    {
        if (!IsOpen)
            return;

        if (inputChangeAmount != expectedChange)
            return;

        // Mark session as completed before CloseRegister() so the abandonment check is skipped.
        sessionConfirmed = true;
        var paidGroup = activeGroup;

        var hands = WaiterHands.ActivePlayerHands;
        if (hands != null)
            hands.ClearMoney();

        if (paidGroup != null)
        {
            int amountEarned = totalAmount;

            if (DailyFinanceBridge.Instance != null)
            {
                DailyFinanceBridge.Instance.AddEarnings(amountEarned);

                if (GameDayManager.Instance != null)
                    GameDayManager.Instance.RefreshRevenueUI();

                Debug.Log("[Finance] Earned ₱" + amountEarned +
                        " | Total = ₱" + DailyFinanceBridge.Instance.EarnedToday);
            }

            GameDayManager.Instance?.RegisterPaymentCompleted();

            if (paidGroup.IsTakeout)
            {
                TakeoutFlowManager.Instance?.NotifyPaymentCompleted(paidGroup);
            }
            else
            {
                paidGroup.PayAndLeave();
            }

        }

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnCashierConfirmed(paidGroup);

        CloseRegister();
    }

    /// <summary>
    /// Completes a valid restaurant payment without opening the player-facing change UI.
    /// This is used only by the autonomous Lobby service while no player role exists.
    /// </summary>
    public bool CompleteAutomatedPayment(CustomerGroup group)
    {
        if (group == null)
            return false;

        TakeoutFlowManager takeoutFlow = TakeoutFlowManager.Instance;
        bool validTakeoutPayment = group.IsTakeout &&
                                   takeoutFlow != null &&
                                   takeoutFlow.ActiveGroup == group &&
                                   takeoutFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment;
        bool validDineInPayment = !group.IsTakeout && group.state == CustomerGroup.GroupState.NeedsBill;

        if (!validTakeoutPayment && !validDineInPayment)
            return false;

        int amountEarned = GetAutomatedOrderTotal(group);
        if (amountEarned <= 0)
        {
            Debug.LogWarning($"[CashierRegisterUI] Automated payment skipped for {group.name}: order total is invalid.", this);
            return false;
        }

        DailyFinanceBridge.Instance?.AddEarnings(amountEarned, "Autonomous cashier payment");
        GameDayManager.Instance?.RefreshRevenueUI();
        GameDayManager.Instance?.RegisterPaymentCompleted();

        if (activeGroup == group)
        {
            sessionConfirmed = true;
            CloseRegister();
        }

        if (group.IsTakeout)
            takeoutFlow.NotifyPaymentCompleted(group);
        else
            group.PayAndLeave();

        return true;
    }

    private static int GetAutomatedOrderTotal(CustomerGroup group)
    {
        if (group == null)
            return 0;

        return group.GetCurrentOrderTotal();
    }

    private void RefreshOrderDisplay()
    {
        if (activeGroup == null || activeGroup.currentOrder == null)
        {
            SetText(tableNumberText, "-");
            ClearCompactOrderPictures();
            SetFoodDisplay(null, null, 0);
            SetDrinkDisplay(null, 0);
            return;
        }

        SetText(tableNumberText, activeGroup.currentOrderNumber.ToString());

        int foodPrice = activeGroup.GetCurrentOrderCategoryTotal(MenuProductCategory.Food);
        int drinkPrice = activeGroup.GetCurrentOrderCategoryTotal(MenuProductCategory.Drink);
        RebuildCompactOrderPictures(foodPrice, drinkPrice);
    }

    private void RebuildCompactOrderPictures(int foodPrice, int drinkPrice)
    {
        EnsureCompactOrderRoots();

        // These serialized images belonged to the old two-food/one-drink display.
        // Keep them as layout anchors, but let the quantity-aware strip render all
        // current order lines instead.
        if (foodImage != null) foodImage.enabled = false;
        if (foodImage2 != null) foodImage2.enabled = false;
        if (drinkImage != null) drinkImage.enabled = false;

        List<CompactOrderLine> foodLines = BuildCompactOrderLines(MenuProductCategory.Food);
        List<CompactOrderLine> drinkLines = BuildCompactOrderLines(MenuProductCategory.Drink);

        PopulateCompactOrderRoot(compactFoodRoot, foodLines);
        PopulateCompactOrderRoot(compactDrinkRoot, drinkLines);

        SetText(foodPriceText, foodLines.Count > 0 ? FormatMoney(foodPrice) : string.Empty);
        SetText(drinkPriceText, drinkLines.Count > 0 ? FormatMoney(drinkPrice) : string.Empty);
    }

    private List<CompactOrderLine> BuildCompactOrderLines(MenuProductCategory category)
    {
        List<CompactOrderLine> result = new List<CompactOrderLine>();
        if (activeGroup == null)
            return result;

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
            return result;

        IReadOnlyList<CustomerGroup.OrderLine> sourceLines = activeGroup.GetCurrentOrderLines();
        if (sourceLines.Count > 0)
        {
            for (int i = 0; i < sourceLines.Count; i++)
            {
                CustomerGroup.OrderLine sourceLine = sourceLines[i];
                if (sourceLine == null)
                    continue;

                List<Recipe> products = sourceLine.ResolveProducts(catalog);
                CompactOrderLine displayLine = new CompactOrderLine
                {
                    quantity = Mathf.Max(1, sourceLine.quantity)
                };

                for (int p = 0; p < products.Count; p++)
                {
                    Recipe product = products[p];
                    if (product != null && product.category == category)
                        displayLine.products.Add(product);
                }

                if (displayLine.products.Count > 0)
                    result.Add(displayLine);
            }

            return result;
        }

        // Compatibility for older runtime orders that predate quantity-aware lines.
        List<Recipe> legacyProducts = activeGroup.currentOrder.ResolveProducts(catalog);
        for (int i = 0; i < legacyProducts.Count; i++)
        {
            Recipe product = legacyProducts[i];
            if (product == null || product.category != category)
                continue;

            CompactOrderLine existing = result.Find(
                line => line.products.Count == 1 && line.products[0] != null &&
                    line.products[0].ProductId == product.ProductId);

            if (existing != null)
            {
                existing.quantity++;
                continue;
            }

            CompactOrderLine newLine = new CompactOrderLine();
            newLine.products.Add(product);
            result.Add(newLine);
        }

        return result;
    }

    private void EnsureCompactOrderRoots()
    {
        RectTransform foodContainer = foodImage != null
            ? foodImage.rectTransform.parent as RectTransform
            : foodPriceText != null ? foodPriceText.rectTransform.parent as RectTransform : null;
        RectTransform drinkContainer = drinkImage != null
            ? drinkImage.rectTransform.parent as RectTransform
            : drinkPriceText != null ? drinkPriceText.rectTransform.parent as RectTransform : null;

        if (compactFoodRoot == null && foodContainer != null)
            compactFoodRoot = CreateCompactOrderRoot("Quantity-Aware Food Pictures", foodContainer);
        if (compactDrinkRoot == null && drinkContainer != null)
            compactDrinkRoot = CreateCompactOrderRoot("Quantity-Aware Drink Pictures", drinkContainer);

        ConfigureCompactPriceText(foodPriceText, foodContainer);
        ConfigureCompactPriceText(drinkPriceText, drinkContainer);
    }

    private RectTransform CreateCompactOrderRoot(string objectName, RectTransform parent)
    {
        GameObject rootObject = new GameObject(
            objectName, typeof(RectTransform), typeof(RectMask2D));
        rootObject.layer = parent.gameObject.layer;

        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(3f, 0f);
        rect.sizeDelta = new Vector2(CompactItemsWidth, CompactItemsHeight);
        rect.localScale = Vector3.one;
        return rect;
    }

    private void ConfigureCompactPriceText(TMP_Text priceText, RectTransform container)
    {
        if (priceText == null || container == null)
            return;

        RectTransform rect = priceText.rectTransform;
        if (rect.parent != container)
            rect.SetParent(container, false);

        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-3f, 0f);
        rect.sizeDelta = new Vector2(CompactPriceWidth, CompactItemsHeight);
        rect.localScale = Vector3.one;
        priceText.alignment = TextAlignmentOptions.MidlineRight;
        priceText.fontSize = 14f;
        priceText.enableAutoSizing = true;
        priceText.fontSizeMin = 8f;
        priceText.fontSizeMax = 14f;
        priceText.margin = Vector4.zero;
        priceText.textWrappingMode = TextWrappingModes.NoWrap;
        priceText.overflowMode = TextOverflowModes.Truncate;
        rect.SetAsLastSibling();
    }

    private void PopulateCompactOrderRoot(
        RectTransform root,
        IReadOnlyList<CompactOrderLine> lines)
    {
        if (root == null)
            return;

        ClearCompactRoot(root);
        int count = lines != null ? lines.Count : 0;
        root.gameObject.SetActive(count > 0);
        if (count == 0)
            return;

        float cellWidth = Mathf.Min(CompactMaxCellWidth, CompactItemsWidth / count);
        for (int i = 0; i < count; i++)
        {
            CompactOrderLine line = lines[i];
            if (line == null || line.products.Count == 0)
                continue;

            GameObject cellObject = new GameObject($"Order Item {i + 1}", typeof(RectTransform));
            cellObject.layer = root.gameObject.layer;
            RectTransform cell = cellObject.GetComponent<RectTransform>();
            cell.SetParent(root, false);
            cell.anchorMin = new Vector2(0f, 0.5f);
            cell.anchorMax = new Vector2(0f, 0.5f);
            cell.pivot = new Vector2(0f, 0.5f);
            cell.anchoredPosition = new Vector2(i * cellWidth, 0f);
            cell.sizeDelta = new Vector2(cellWidth, CompactItemsHeight);

            float pictureWidth = Mathf.Max(4f, cellWidth - 12f);
            float maximumIconSize = useAlternateCompactPresentation ? 32f : 24f;
            float iconSize = Mathf.Min(maximumIconSize, pictureWidth / line.products.Count);
            float iconsWidth = iconSize * line.products.Count;
            float startX = Mathf.Max(0f, (pictureWidth - iconsWidth) * 0.5f);

            for (int p = 0; p < line.products.Count; p++)
            {
                Recipe product = line.products[p];
                GameObject iconObject = new GameObject(
                    product != null ? product.DisplayName : $"Picture {p + 1}",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = cellObject.layer;

                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(cell, false);
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(startX + p * iconSize, 0f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                Image image = iconObject.GetComponent<Image>();
                image.sprite = product != null ? product.sprite : null;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = image.sprite != null;
            }

            GameObject quantityObject = new GameObject(
                "Quantity", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            quantityObject.layer = cellObject.layer;
            RectTransform quantityRect = quantityObject.GetComponent<RectTransform>();
            quantityRect.SetParent(cell, false);
            quantityRect.anchorMin = new Vector2(1f, 0f);
            quantityRect.anchorMax = new Vector2(1f, 0f);
            quantityRect.pivot = new Vector2(1f, 0f);
            quantityRect.anchoredPosition = Vector2.zero;
            quantityRect.sizeDelta = new Vector2(Mathf.Min(20f, cellWidth), 15f);

            TextMeshProUGUI quantityText = quantityObject.GetComponent<TextMeshProUGUI>();
            quantityText.text = $"x{Mathf.Max(1, line.quantity)}";
            quantityText.fontSize = useAlternateCompactPresentation ? 14f : 11f;
            quantityText.enableAutoSizing = true;
            quantityText.fontSizeMin = 7f;
            quantityText.fontSizeMax = useAlternateCompactPresentation ? 14f : 11f;
            quantityText.fontStyle = FontStyles.Bold;
            quantityText.alignment = TextAlignmentOptions.BottomRight;
            quantityText.color = Color.white;
            quantityText.raycastTarget = false;
            quantityText.textWrappingMode = TextWrappingModes.NoWrap;
            quantityText.overflowMode = TextOverflowModes.Truncate;

            UnityEngine.UI.Outline outline =
                quantityObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void ClearCompactOrderPictures()
    {
        ClearCompactRoot(compactFoodRoot);
        ClearCompactRoot(compactDrinkRoot);
        if (compactFoodRoot != null) compactFoodRoot.gameObject.SetActive(false);
        if (compactDrinkRoot != null) compactDrinkRoot.gameObject.SetActive(false);
    }

    private static void ClearCompactRoot(RectTransform root)
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

    private void RefreshTotalsDisplay()
    {
        Debug.Log($"[CashierRegisterUI] RefreshTotalsDisplay — received={receivedAmount} total={totalAmount} change={expectedChange}", this);
        SetText(receivedText, FormatMoney(receivedAmount));
        SetText(totalText, FormatMoney(totalAmount));
        SetText(changeText, FormatMoney(expectedChange));
    }

    private void RefreshInputDisplay()
    {
        SetText(cashierChangeText, FormatMoney(inputChangeAmount));

        if (cashierChangeText == null)
            return;

        if (inputChangeAmount == 0)
            cashierChangeText.color = normalInputColor;
        else if (inputChangeAmount == expectedChange)
            cashierChangeText.color = correctInputColor;
        else
            cashierChangeText.color = wrongInputColor;
    }

    private void ResetDisplay()
    {
        Debug.Log($"[CashierRegisterUI] ResetDisplay called", this);
        SetText(tableNumberText, "-");
        ClearCompactOrderPictures();
        SetFoodDisplay(null, null, 0);
        SetDrinkDisplay(null, 0);

        SetText(receivedText, "0.00");
        SetText(totalText, "0.00");
        SetText(changeText, "0.00");
        SetText(cashierChangeText, "0.00");

        if (cashierChangeText != null)
            cashierChangeText.color = normalInputColor;
    }

    private void SetFoodDisplay(Sprite firstSprite, Sprite secondSprite, int sharedPrice)
    {
        if (foodImage != null)
        {
            foodImage.sprite = firstSprite;
            foodImage.enabled = firstSprite != null;
        }

        if (foodImage2 != null)
        {
            foodImage2.sprite = secondSprite;
            foodImage2.enabled = secondSprite != null;
        }

        if (foodPriceText != null)
        {
            if (firstSprite == null)
                foodPriceText.text = "";
            else
                foodPriceText.text = FormatMoney(sharedPrice);
        }
    }

    private void SetDrinkDisplay(Sprite sprite, int price)
    {
        if (drinkImage != null)
        {
            drinkImage.sprite = sprite;
            drinkImage.enabled = sprite != null;
        }

        if (drinkPriceText != null)
        {
            if (sprite == null)
                drinkPriceText.text = "";
            else
                drinkPriceText.text = FormatMoney(price);
        }
    }

    private void SetText(TMP_Text textComp, string value)
    {
        if (textComp != null)
            textComp.text = value;
    }

    private string FormatMoney(int value)
    {
        return value.ToString("N2");
    }

}
