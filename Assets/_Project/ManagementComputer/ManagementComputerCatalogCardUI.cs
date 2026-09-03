using System;
using System.Collections;
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
    [SerializeField] private Image statusBackground;
    [SerializeField] private GameObject restockStatsRoot;
    [SerializeField] private TMP_Text inStockLabelText;
    [SerializeField] private TMP_Text inStockValueText;
    [SerializeField] private TMP_Text neededTodayLabelText;
    [SerializeField] private TMP_Text neededTodayValueText;
    [SerializeField] private GameObject quantityRoot;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private ManagementItemCardFeedback feedback;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.89f, 0.95f, 0.99f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.76f, 0.90f, 0.98f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.84f, 0.88f, 0.91f, 1f);
    [SerializeField] private Color primaryTextColor = new Color(0.025f, 0.08f, 0.14f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.12f, 0.21f, 0.28f, 1f);
    [SerializeField] private Color priceTextColor = new Color(0.02f, 0.32f, 0.62f, 1f);
    [SerializeField] private Color stockAccentColor = new Color(0.02f, 0.38f, 0.18f, 1f);
    [SerializeField] private Color expiryWarningColor = new Color(0.68f, 0.06f, 0.08f, 1f);
    [SerializeField] private Color incomingAccentColor = new Color(0.03f, 0.30f, 0.60f, 1f);
    [SerializeField] private Color warningAccentColor = new Color(0.60f, 0.32f, 0.02f, 1f);
    [SerializeField] private Color bodyTextColor = new Color(0.035f, 0.10f, 0.17f, 1f);
    [SerializeField] private Color readyBackgroundColor = new Color(0.82f, 0.94f, 0.85f, 1f);
    [SerializeField] private Color lowBackgroundColor = new Color(1f, 0.84f, 0.86f, 1f);
    [SerializeField] private Color warningBackgroundColor = new Color(1f, 0.91f, 0.76f, 1f);
    [SerializeField] private Color neutralStatusBackgroundColor = new Color(0.86f, 0.90f, 0.93f, 1f);

    private Coroutine quantityRoutine;
    private Coroutine statusRoutine;
    private Coroutine statsRoutine;
    private int displayedQuantity = -1;
    private string displayedStatus;
    private int displayedStock = int.MinValue;
    private int displayedNeededToday = int.MinValue;

    public ItemData BoundItem { get; private set; }
    public Recipe BoundProduct { get; private set; }
    public Button MinusButton => minusButton;
    public Button PlusButton => plusButton;

    private void OnEnable()
    {
        RestoreExpectedVisualState();
    }

    public void RestoreExpectedVisualState()
    {
        RestoreTextVisuals();
        GetComponent<UIRevealAnimation>()?.RestoreVisibleStateIfIdle();
    }

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
        RestoreExpectedVisualState();
        BoundProduct = product;
        BoundItem = null;
        bool unlocked = product != null && product.IsUnlocked;
        bool onMenu = unlocked && MenuAvailabilityManager.IsProductAvailable(product);
        ApplyMenuReadability(unlocked, onMenu);
        SetIcon(product != null ? product.sprite : null);
        SetText(titleText, product != null ? product.DisplayName : "Missing product");
        SetText(metaText, product != null ? product.category.ToString() : string.Empty);
        SetStatusText(!unlocked
            ? "LOCKED • DAY " + Mathf.Max(1, product.dayToUnlock)
            : onMenu ? "ON MENU" : "NOT ON MENU");
        SetText(priceText, product != null ? "₱" + product.EffectiveSellPrice : "₱0");

        ManagementItemCardFeedback cardFeedback = GetFeedback();
        if (cardFeedback != null)
        {
            string details = product == null
                ? "No product information."
                : (string.IsNullOrWhiteSpace(product.descriptionText)
                    ? product.category + " menu item"
                    : product.descriptionText) +
                  "\n" + (!unlocked
                      ? "Unlocks on day " + Mathf.Max(1, product.dayToUnlock)
                      : MenuAvailabilityManager.IsProductAvailable(product)
                          ? "Currently on the menu"
                          : "Currently off the menu") +
                  "  •  ₱" + product.EffectiveSellPrice;
            cardFeedback.SetTooltip(product != null ? product.DisplayName : "MENU ITEM", details);
            cardFeedback.SetSelected(selected);
        }

        if (quantityRoot != null)
            quantityRoot.SetActive(false);

        if (cardButton != null)
        {
            cardButton.enabled = true;
            cardButton.interactable = product != null;
            cardButton.onClick.RemoveAllListeners();
            if (product != null && onSelected != null)
                cardButton.onClick.AddListener(() =>
                {
                    GetFeedback()?.PlaySelectionFeedback();
                    onSelected(product);
                });
        }

        if (background != null)
            background.color = !unlocked ? lockedColor : selected ? selectedColor : normalColor;
    }

    public void BindRestock(
        ItemData item,
        RestockStockProjection projection,
        int requestedContainers,
        bool unlocked,
        bool canIncrease,
        Action<ItemData, int> onQuantityChanged)
    {
        RestoreExpectedVisualState();
        BoundItem = item;
        BoundProduct = null;
        projection ??= RestockStockProjection.Calculate(item, 1);
        ApplyRestockReadability();
        SetIcon(item != null ? item.sprite : null);
        SetText(titleText, item != null
            ? $"{item.displayName} ×{Mathf.Max(1, item.unitsPerBox)}"
            : "Missing item");
        SetText(metaText, item != null
            ? $"{Mathf.Max(1, item.unitsPerBox)} units • {item.requiredStorage}"
            : string.Empty);
        SetRestockStatus(
            item,
            projection,
            unlocked);
        SetText(priceText, item != null
            ? "₱" + CasualDiningPolishManager.EnsureInstance().GetCurrentBoxCost(item) + " / box"
            : "₱0 / box");

        ManagementItemCardFeedback cardFeedback = GetFeedback();
        if (cardFeedback != null)
        {
            string details = "No ingredient information.";
            if (item != null)
            {
                details = Mathf.Max(1, item.unitsPerBox) + " units per box  •  " +
                          item.requiredStorage + " storage\nIN STOCK " + projection.OnHandUnits +
                          "  •  NEEDED TODAY " + projection.TargetUnits + "  •  " +
                          CasualDiningPolishManager.EnsureInstance().GetMarketTrendLabel(item) + " / box";
                if (projection.PendingContainers > 0)
                    details += "\n" + projection.PendingContainers + " box" +
                               (projection.PendingContainers == 1 ? string.Empty : "es") + " " +
                               projection.GetDeliveryStageLabel();
                if (projection.ExpiredUnits > 0)
                    details += "  •  " + projection.ExpiredUnits + " expired";
            }
            cardFeedback.SetTooltip(item != null ? item.displayName : "INGREDIENT", details);
            cardFeedback.SetSelected(requestedContainers > 0);
        }

        if (quantityRoot != null)
            quantityRoot.SetActive(true);
        SetQuantity(Mathf.Max(0, requestedContainers));

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
            onQuantityChanged,
            () => GetFeedback()?.PlayValueFeedback(false));
        BindQuantityButton(
            plusButton,
            unlocked && canIncrease,
            item,
            1,
            onQuantityChanged,
            () => GetFeedback()?.PlayValueFeedback(true));

        if (background != null)
            background.color = unlocked ? normalColor : lockedColor;
    }

    private void SetRestockStatus(
        ItemData item,
        RestockStockProjection projection,
        bool unlocked)
    {
        if (statusText == null)
            return;

        projection ??= RestockStockProjection.Calculate(item, 1);
        SetStatValues(projection.OnHandUnits, projection.TargetUnits);

        if (!unlocked)
        {
            statusText.color = expiryWarningColor;
            SetStatusBackground(neutralStatusBackgroundColor);
            SetStatusText("LOCKED • DAY " + (item != null ? item.dayToUnlock : 1));
            return;
        }

        string headline;
        Color headlineColor;
        Color bandColor;
        switch (projection.State)
        {
            case RestockCoverageState.CoveredByDelivery:
                headline = "READY • DELIVERY COVERED";
                headlineColor = incomingAccentColor;
                bandColor = readyBackgroundColor;
                break;
            case RestockCoverageState.Low:
                headline = "LOW • NEED " + projection.RecommendedContainers + " BOX" +
                           (projection.RecommendedContainers == 1 ? string.Empty : "ES");
                headlineColor = expiryWarningColor;
                bandColor = lowBackgroundColor;
                break;
            case RestockCoverageState.StillLow:
                headline = "LOW • NEED " + projection.RecommendedContainers + " MORE BOX" +
                           (projection.RecommendedContainers == 1 ? string.Empty : "ES");
                headlineColor = warningAccentColor;
                bandColor = warningBackgroundColor;
                break;
            case RestockCoverageState.Overstocked:
                headline = "OVERSTOCKED";
                headlineColor = warningAccentColor;
                bandColor = warningBackgroundColor;
                break;
            case RestockCoverageState.SpoilageRisk:
                headline = "OVERSTOCKED • SPOILAGE RISK";
                headlineColor = expiryWarningColor;
                bandColor = lowBackgroundColor;
                break;
            default:
                headline = "READY";
                headlineColor = stockAccentColor;
                bandColor = readyBackgroundColor;
                break;
        }

        statusText.color = headlineColor;
        SetStatusBackground(bandColor);
        SetStatusText(headline);
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
        if (target == null)
            return;

        RestoreTextVisual(target);
        target.text = value ?? string.Empty;
        target.SetAllDirty();
        target.ForceMeshUpdate(true, true);
    }

    private void RestoreTextVisuals()
    {
        RestoreTextVisual(titleText);
        RestoreTextVisual(metaText);
        RestoreTextVisual(statusText);
        RestoreTextVisual(priceText);
        RestoreTextVisual(inStockLabelText);
        RestoreTextVisual(inStockValueText);
        RestoreTextVisual(neededTodayLabelText);
        RestoreTextVisual(neededTodayValueText);
        RestoreTextVisual(quantityText);
    }

    private static void RestoreTextVisual(TMP_Text text)
    {
        if (text == null)
            return;

        Transform parent = text.transform.parent;
        ManagementComputerCatalogCardUI card = text.GetComponentInParent<ManagementComputerCatalogCardUI>();
        while (parent != null && card != null && parent != card.transform)
        {
            CanvasGroup group = parent.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;
            parent = parent.parent;
        }

        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);
        text.enabled = true;
        Color color = text.color;
        color.a = 1f;
        text.color = color;
        text.canvasRenderer.SetAlpha(1f);
        text.canvasRenderer.cull = false;
        text.UpdateMeshPadding();
        text.RecalculateMasking();
        text.RecalculateClipping();
        text.SetAllDirty();
    }

    private static void BindQuantityButton(
        Button button,
        bool interactable,
        ItemData item,
        int delta,
        Action<ItemData, int> callback,
        Action feedbackAction)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = interactable && item != null && callback != null;
        if (button.interactable)
            button.onClick.AddListener(() =>
            {
                feedbackAction?.Invoke();
                callback(item, delta);
            });
    }

    private ManagementItemCardFeedback GetFeedback()
    {
        if (feedback == null)
            feedback = GetComponent<ManagementItemCardFeedback>();
        return feedback;
    }

    private void ApplyMenuReadability(bool unlocked, bool onMenu)
    {
        EnsureModeVisuals();
        if (restockStatsRoot != null)
            restockStatsRoot.SetActive(false);
        ApplyMenuLayout();
        StyleText(titleText, primaryTextColor, 17f, 23f, FontStyles.Bold);
        StyleText(metaText, secondaryTextColor, 13f, 16f, FontStyles.Normal);
        StyleText(statusText,
            !unlocked ? expiryWarningColor : onMenu ? stockAccentColor : incomingAccentColor,
            14f,
            17f,
            FontStyles.Bold);
        StyleText(priceText, priceTextColor, 16f, 21f, FontStyles.Bold);
        SetStatusBackground(!unlocked
            ? neutralStatusBackgroundColor
            : onMenu ? readyBackgroundColor : neutralStatusBackgroundColor);
    }

    private void ApplyRestockReadability()
    {
        EnsureModeVisuals();
        if (restockStatsRoot != null)
            restockStatsRoot.SetActive(true);
        ApplyRestockLayout();
        StyleText(titleText, primaryTextColor, 17f, 23f, FontStyles.Bold);
        StyleText(metaText, secondaryTextColor, 13f, 16f, FontStyles.Normal);
        StyleText(statusText, bodyTextColor, 13.5f, 16f, FontStyles.Bold);
        StyleText(priceText, priceTextColor, 16f, 20f, FontStyles.Bold);
        StyleText(quantityText, primaryTextColor, 18f, 22f, FontStyles.Bold);
        StyleText(inStockLabelText, secondaryTextColor, 10.5f, 12.5f, FontStyles.Normal);
        StyleText(neededTodayLabelText, secondaryTextColor, 10.5f, 12.5f, FontStyles.Normal);
        StyleText(inStockValueText, primaryTextColor, 14f, 17f, FontStyles.Bold);
        StyleText(neededTodayValueText, primaryTextColor, 14f, 17f, FontStyles.Bold);
    }

    private void EnsureModeVisuals()
    {
        if (statusBackground == null)
        {
            Transform band = FindPart("StatusBand") ?? FindPart("StatusPill");
            if (band == null)
            {
                GameObject bandObject = new GameObject("StatusBand", typeof(RectTransform));
                bandObject.transform.SetParent(transform, false);
                band = bandObject.transform;
            }

            band.name = "StatusBand";
            statusBackground = band.GetComponent<Image>() ?? band.gameObject.AddComponent<Image>();
            statusBackground.sprite = background != null ? background.sprite : null;
            statusBackground.type = statusBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            statusBackground.raycastTarget = false;
            band.gameObject.SetActive(true);
            if (statusText != null)
                band.SetSiblingIndex(Mathf.Max(0, statusText.transform.GetSiblingIndex()));
        }

        if (restockStatsRoot == null)
        {
            Transform existing = FindPart("RestockStats");
            if (existing == null)
            {
                GameObject statsObject = new GameObject("RestockStats", typeof(RectTransform));
                statsObject.transform.SetParent(transform, false);
                existing = statsObject.transform;
            }
            restockStatsRoot = existing.gameObject;
        }

        EnsureStatCell(
            "InStockCell",
            "IN STOCK",
            out inStockLabelText,
            out inStockValueText,
            new Vector2(0f, 0f),
            new Vector2(0.48f, 1f));
        EnsureStatCell(
            "NeededTodayCell",
            "NEEDED TODAY",
            out neededTodayLabelText,
            out neededTodayValueText,
            new Vector2(0.52f, 0f),
            new Vector2(1f, 1f));
    }

    private void EnsureStatCell(
        string cellName,
        string labelValue,
        out TMP_Text label,
        out TMP_Text value,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform cell = FindPart(cellName);
        if (cell == null)
        {
            GameObject cellObject = new GameObject(cellName, typeof(RectTransform));
            cellObject.transform.SetParent(restockStatsRoot.transform, false);
            cell = cellObject.transform;
        }

        SetAnchors(cell as RectTransform, anchorMin, anchorMax);
        Image panel = cell.GetComponent<Image>() ?? cell.gameObject.AddComponent<Image>();
        panel.sprite = background != null ? background.sprite : null;
        panel.type = panel.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panel.color = new Color(1f, 1f, 1f, 0.88f);
        panel.raycastTarget = false;

        string labelName = cellName == "InStockCell" ? "InStockLabel" : "NeededTodayLabel";
        string valueName = cellName == "InStockCell" ? "InStockValue" : "NeededTodayValue";
        label = EnsureTextPart(cell, labelName, labelValue,
            new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.92f));
        value = EnsureTextPart(cell, valueName, "0",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.56f));
    }

    private TMP_Text EnsureTextPart(
        Transform parent,
        string objectName,
        string initialValue,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform part = FindPart(objectName);
        TMP_Text text = part != null ? part.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        if (titleText != null && titleText.font != null)
            text.font = titleText.font;
        text.text = string.IsNullOrEmpty(text.text) ? initialValue : text.text;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        SetAnchors(text.rectTransform, anchorMin, anchorMax);
        return text;
    }

    private void ApplyMenuLayout()
    {
        SetAnchors(GetIconPanelRect(), new Vector2(0.055f, 0.46f), new Vector2(0.945f, 0.94f));
        SetAnchors(titleText != null ? titleText.rectTransform : null,
            new Vector2(0.055f, 0.33f), new Vector2(0.945f, 0.45f));
        SetAnchors(metaText != null ? metaText.rectTransform : null,
            new Vector2(0.07f, 0.26f), new Vector2(0.93f, 0.33f));
        SetAnchors(statusBackground != null ? statusBackground.rectTransform : null,
            new Vector2(0.055f, 0.055f), new Vector2(0.59f, 0.195f));
        SetAnchors(statusText != null ? statusText.rectTransform : null,
            new Vector2(0.075f, 0.065f), new Vector2(0.57f, 0.185f));
        SetAnchors(priceText != null ? priceText.rectTransform : null,
            new Vector2(0.62f, 0.055f), new Vector2(0.945f, 0.195f));
        if (statusText != null)
        {
            statusText.alignment = TextAlignmentOptions.MidlineLeft;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (priceText != null)
            priceText.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void ApplyRestockLayout()
    {
        SetAnchors(GetIconPanelRect(), new Vector2(0.055f, 0.60f), new Vector2(0.945f, 0.95f));
        SetAnchors(titleText != null ? titleText.rectTransform : null,
            new Vector2(0.055f, 0.50f), new Vector2(0.945f, 0.59f));
        SetAnchors(metaText != null ? metaText.rectTransform : null,
            new Vector2(0.07f, 0.445f), new Vector2(0.93f, 0.50f));
        SetAnchors(statusBackground != null ? statusBackground.rectTransform : null,
            new Vector2(0.055f, 0.335f), new Vector2(0.945f, 0.425f));
        SetAnchors(statusText != null ? statusText.rectTransform : null,
            new Vector2(0.075f, 0.345f), new Vector2(0.925f, 0.415f));
        SetAnchors(restockStatsRoot != null ? restockStatsRoot.transform as RectTransform : null,
            new Vector2(0.055f, 0.195f), new Vector2(0.945f, 0.325f));
        SetAnchors(priceText != null ? priceText.rectTransform : null,
            new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.195f));
        SetAnchors(quantityRoot != null ? quantityRoot.transform as RectTransform : null,
            new Vector2(0.055f, 0.015f), new Vector2(0.945f, 0.13f));
        if (statusText != null)
        {
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (priceText != null)
            priceText.alignment = TextAlignmentOptions.Center;
    }

    private RectTransform GetIconPanelRect()
    {
        return icon != null && icon.transform.parent != null
            ? icon.transform.parent as RectTransform
            : null;
    }

    private Transform FindPart(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }
        return null;
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

    private void SetStatusBackground(Color color)
    {
        if (statusBackground != null)
            statusBackground.color = color;
    }

    private void SetStatValues(int inStock, int neededToday)
    {
        bool changed = displayedStock != int.MinValue &&
                       (displayedStock != inStock || displayedNeededToday != neededToday);
        displayedStock = inStock;
        displayedNeededToday = neededToday;
        SetText(inStockLabelText, "IN STOCK");
        SetText(neededTodayLabelText, "NEEDED TODAY");
        SetText(inStockValueText, Mathf.Max(0, inStock).ToString());
        SetText(neededTodayValueText, Mathf.Max(0, neededToday).ToString());

        if (!changed || restockStatsRoot == null || !isActiveAndEnabled ||
            LevelOneUIAccessibility.ReducedMotion)
            return;
        if (statsRoutine != null)
            StopCoroutine(statsRoutine);
        statsRoutine = StartCoroutine(AnimateStatsChange());
    }

    private static void StyleText(
        TMP_Text text,
        Color color,
        float minimumSize,
        float maximumSize,
        FontStyles style)
    {
        if (text == null)
            return;

        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.fontStyle = style;
        RestoreTextVisual(text);
    }

    private void SetQuantity(int value)
    {
        if (quantityText == null)
            return;

        bool changed = displayedQuantity >= 0 && displayedQuantity != value;
        displayedQuantity = value;
        SetText(quantityText, value.ToString());
        if (!changed || !isActiveAndEnabled || LevelOneUIAccessibility.ReducedMotion)
            return;

        if (quantityRoutine != null)
            StopCoroutine(quantityRoutine);
        quantityRoutine = StartCoroutine(AnimateQuantityChange());
    }

    private void SetStatusText(string value)
    {
        if (statusText == null)
            return;

        value ??= string.Empty;
        bool changed = displayedStatus != null && displayedStatus != value;
        displayedStatus = value;
        SetText(statusText, value);
        if (!changed || !isActiveAndEnabled || LevelOneUIAccessibility.ReducedMotion)
            return;

        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(AnimateStatusChange());
    }

    private IEnumerator AnimateQuantityChange()
    {
        RectTransform rect = quantityText.rectTransform;
        Vector3 authoredScale = Vector3.one;
        rect.localScale = authoredScale * 1.14f;
        float elapsed = 0f;
        const float duration = 0.13f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.localScale = Vector3.LerpUnclamped(authoredScale * 1.14f, authoredScale, t);
            yield return null;
        }

        rect.localScale = authoredScale;
        quantityRoutine = null;
    }

    private IEnumerator AnimateStatusChange()
    {
        RectTransform rect = statusText.rectTransform;
        rect.localScale = Vector3.one * 1.035f;
        float elapsed = 0f;
        const float duration = 0.12f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.localScale = Vector3.LerpUnclamped(Vector3.one * 1.035f, Vector3.one, t);
            yield return null;
        }

        rect.localScale = Vector3.one;
        statusRoutine = null;
    }

    private IEnumerator AnimateStatsChange()
    {
        RectTransform rect = restockStatsRoot.transform as RectTransform;
        if (rect == null)
        {
            statsRoutine = null;
            yield break;
        }

        rect.localScale = Vector3.one * 1.025f;
        float elapsed = 0f;
        const float duration = 0.12f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rect.localScale = Vector3.LerpUnclamped(Vector3.one * 1.025f, Vector3.one, t);
            yield return null;
        }

        rect.localScale = Vector3.one;
        statsRoutine = null;
    }

    private void OnDisable()
    {
        if (quantityRoutine != null)
            StopCoroutine(quantityRoutine);
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        if (statsRoutine != null)
            StopCoroutine(statsRoutine);
        quantityRoutine = null;
        statusRoutine = null;
        statsRoutine = null;
        if (quantityText != null)
            quantityText.rectTransform.localScale = Vector3.one;
        if (statusText != null)
            statusText.rectTransform.localScale = Vector3.one;
        if (restockStatsRoot != null)
            restockStatsRoot.transform.localScale = Vector3.one;
    }

}
