using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class NotepadMenuVisualStyle
{
    public float entryHeight = 218f;
    public Sprite entryBackgroundSprite;
    public Color entryColor = new Color(0.404f, 0.667f, 0.808f, 1f);
    public Color disabledColor = new Color(0.46f, 0.49f, 0.52f, 0.9f);
    public Color selectedColor = new Color(0.22f, 0.72f, 0.38f, 0.9f);
    public Color primaryTextColor = Color.white;
    public Color secondaryTextColor = new Color(0.92f, 0.96f, 1f, 1f);
    public float nameFontSize = 24f;
    public float detailFontSize = 18f;

    [NonSerialized] public TMP_FontAsset fontAsset;
}

/// <summary>
/// Runtime view for one product or bundle in the waiter notepad. The view does
/// not know any concrete menu items; all displayed data comes from Recipe and
/// MenuBundle instances supplied by OrderChecklistUI.
/// </summary>
public sealed class NotepadMenuEntryUI : MonoBehaviour
{
    public enum EntryKind
    {
        Product,
        Bundle
    }

    public enum ReviewState
    {
        None,
        Correct,
        Missing,
        TooFew,
        TooMany,
        WrongItem
    }

    private static readonly Color ReviewCorrectColor =
        new Color(0.3f, 0.95f, 0.48f, 1f);
    private static readonly Color ReviewMissingColor =
        new Color(1f, 0.74f, 0.18f, 1f);
    private static readonly Color ReviewErrorColor =
        new Color(1f, 0.24f, 0.22f, 1f);

    private readonly List<Image> icons = new List<Image>();

    [Header("Editable Prefab References")]
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image background;
    [SerializeField] private Image selectionMark;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Toggle toggle;

    [Header("Editable Prefab State Colors")]
    [SerializeField] private Color disabledBackgroundColor =
        new Color(0.46f, 0.49f, 0.52f, 0.9f);
    [SerializeField] private Color selectedAccentColor =
        new Color(0.22f, 0.72f, 0.38f, 0.9f);
    private NotepadMenuVisualStyle style;
    private int selectedQuantity;
    private int availableQuantity;
    private bool canSelect;
    private ReviewState reviewState;
    private int expectedQuantity;
    private Coroutine feedbackAnimation;
    private bool feedbackPending;
    private Vector2 authoredCardSize;

    public EntryKind Kind { get; private set; }
    public Recipe Product { get; private set; }
    public MenuBundle Bundle { get; private set; }
    public MenuProductCategory Category => Product != null
        ? Product.category
        : MenuProductCategory.Food;
    public bool IsOn => toggle != null && toggle.isOn;
    public int SelectedQuantity => selectedQuantity;
    public Vector2 AuthoredCardSize => authoredCardSize.x > 0f && authoredCardSize.y > 0f
        ? authoredCardSize
        : GetCurrentCardSize();
    public string DisplayName => Kind == EntryKind.Bundle
        ? Bundle != null ? Bundle.displayName : "Missing Bundle"
        : Product != null ? Product.DisplayName : "Missing Product";
    public string ItemId => Kind == EntryKind.Bundle
        ? Bundle != null ? Bundle.bundleId : string.Empty
        : Product != null ? Product.ProductId : string.Empty;

    public event Action<NotepadMenuEntryUI, bool> ValueChanged;
    public event Action<NotepadMenuEntryUI, int> QuantityChanged;

    private void Awake()
    {
        CaptureAuthoredCardSize();
        ResolvePrefabReferences();
        if (HasRequiredReferences())
        {
            CapturePrefabStyle();
            BindControls();
        }
    }

    public static NotepadMenuEntryUI Create(
        NotepadMenuEntryUI prefab,
        Transform parent,
        NotepadMenuVisualStyle fallbackStyle)
    {
        if (prefab == null)
            return Create(parent, fallbackStyle);

        NotepadMenuEntryUI view = Instantiate(prefab, parent, false);
        view.ResolvePrefabReferences();
        if (!view.HasRequiredReferences())
        {
            Debug.LogError(
                $"[NotepadMenuEntryUI] Prefab '{prefab.name}' is missing one or more UI references.",
                prefab);
            Destroy(view.gameObject);
            return Create(parent, fallbackStyle);
        }

        view.CapturePrefabStyle();
        view.BindControls();
        return view;
    }

    public static NotepadMenuEntryUI Create(Transform parent, NotepadMenuVisualStyle visualStyle)
    {
        GameObject root = new GameObject(
            "Menu Entry",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement),
            typeof(Toggle),
            typeof(NotepadMenuEntryUI));

        root.layer = parent != null ? parent.gameObject.layer : 5;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(174f, Mathf.Max(218f, visualStyle.entryHeight));

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.minWidth = 174f;
        layout.preferredWidth = 174f;
        layout.minHeight = Mathf.Max(218f, visualStyle.entryHeight);
        layout.preferredHeight = Mathf.Max(218f, visualStyle.entryHeight);
        layout.flexibleWidth = 0f;

        NotepadMenuEntryUI view = root.GetComponent<NotepadMenuEntryUI>();
        view.BuildVisuals(visualStyle);
        view.StretchToContainer();
        view.CaptureAuthoredCardSize();
        return view;
    }

    private void StretchToContainer()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(174f, 218f);
    }

    public void Bind(Recipe product)
    {
        Product = product;
        Bundle = null;
        Kind = EntryKind.Product;
        gameObject.name = product != null ? $"Product - {product.DisplayName}" : "Product - Missing";

        nameText.text = product != null ? product.DisplayName : "Missing Product";
        priceText.text = product != null ? FormatPrice(product.EffectiveSellPrice) : string.Empty;

        List<Recipe> products = new List<Recipe>();
        if (product != null)
            products.Add(product);

        SetIcons(products);
        RefreshAvailability();
        SetIsOnWithoutNotify(false);
    }

    public void Bind(MenuBundle bundle)
    {
        Product = null;
        Bundle = bundle;
        Kind = EntryKind.Bundle;
        gameObject.name = bundle != null ? $"Bundle - {bundle.displayName}" : "Bundle - Missing";

        nameText.text = bundle != null ? bundle.displayName : "Missing Bundle";
        priceText.text = bundle != null ? FormatPrice(bundle.GetPrice()) : string.Empty;
        SetIcons(bundle != null ? bundle.products : null);
        RefreshAvailability();
        SetIsOnWithoutNotify(false);
    }

    public void RefreshAvailability()
    {
        bool unlocked = true;
        bool available = true;
        int stock = int.MaxValue;

        if (Kind == EntryKind.Product)
        {
            unlocked = Product != null && Product.IsUnlocked;
            available = MenuAvailabilityManager.IsProductAvailable(Product);
            stock = GetProductStock(Product);
        }
        else
        {
            unlocked = MenuAvailabilityManager.IsBundleAvailable(Bundle);
            available = MenuAvailabilityManager.IsBundleAvailable(Bundle);

            if (Bundle != null)
            {
                for (int i = 0; i < Bundle.products.Count; i++)
                {
                    Recipe product = Bundle.products[i];
                    if (!MenuAvailabilityManager.IsProductAvailable(product))
                    {
                        available = false;
                        stock = 0;
                        continue;
                    }

                    unlocked &= product.IsUnlocked;
                }

                stock = available && LobbyStockBridge.Instance != null
                    ? LobbyStockBridge.Instance.GetOrderStock(Bundle.products)
                    : 0;
            }
        }

        if (stock == int.MaxValue)
            stock = 0;

        availableQuantity = Mathf.Max(0, stock);
        bool inStock = availableQuantity > 0;
        canSelect = unlocked && available && inStock;
        // The card body is display-only. Quantity can only be changed with the
        // explicit minus and plus buttons, which avoids accidental mobile taps.
        toggle.interactable = false;
        if (decreaseButton != null)
            decreaseButton.interactable = canSelect && selectedQuantity > 0;
        if (increaseButton != null)
            increaseButton.interactable = canSelect && selectedQuantity < availableQuantity;

        if (!canSelect)
            SetQuantityWithoutNotify(0);
        else if (selectedQuantity > availableQuantity)
            SetQuantityWithoutNotify(availableQuantity);

        background.color = canSelect ? style.entryColor : style.disabledColor;
        nameText.color = style.primaryTextColor;
        quantityText.color = style.primaryTextColor;
        statusText.color = style.secondaryTextColor;
        selectionMark.color = style.selectedColor;
        selectionMark.enabled = selectedQuantity > 0;
        // Stock is intentionally shown once in the Products Availability area.
        // Keeping it off the order card prevents it being confused with the
        // quantity the customer requested through the - / quantity / + controls.
        stockText.text = string.Empty;

        if (!available)
            statusText.text = "Unavailable";
        else if (!unlocked)
            statusText.text = "Locked";
        else if (!inStock)
            statusText.text = "Out of stock";
        else
            statusText.text = GetCardSubtitle();

        ApplyReviewAppearance();
    }

    public void SetIsOnWithoutNotify(bool value)
    {
        SetQuantityWithoutNotify(value ? 1 : 0);
    }

    public void SetQuantityWithoutNotify(int value)
    {
        ApplyQuantity(value, false);
    }

    public static ReviewState ClassifyReview(int expected, int selected)
    {
        expected = Mathf.Max(0, expected);
        selected = Mathf.Max(0, selected);

        if (expected == selected)
            return expected > 0 ? ReviewState.Correct : ReviewState.None;
        if (expected == 0)
            return ReviewState.WrongItem;
        if (selected == 0)
            return ReviewState.Missing;
        return selected < expected ? ReviewState.TooFew : ReviewState.TooMany;
    }

    public static bool ShouldAnimateReview(ReviewState state, bool isActive)
    {
        return isActive && IsMismatchState(state);
    }

    public void ApplyReview(int expected)
    {
        expectedQuantity = Mathf.Max(0, expected);
        reviewState = ClassifyReview(expectedQuantity, selectedQuantity);
        feedbackPending = false;
        StopFeedbackAnimation();
        ApplyReviewAppearance();

        if (HasMismatch())
        {
            if (ShouldAnimateReview(reviewState,
                isActiveAndEnabled && gameObject.activeInHierarchy))
                feedbackAnimation = StartCoroutine(AnimateMismatch());
            else
                feedbackPending = true;
        }
    }

    public void ClearReview()
    {
        reviewState = ReviewState.None;
        expectedQuantity = 0;
        feedbackPending = false;
        StopFeedbackAnimation();
        RefreshAvailability();
    }

    private void IncreaseQuantity()
    {
        if (availableQuantity <= 0)
            return;

        ApplyQuantity(Mathf.Min(selectedQuantity + 1, availableQuantity), true);
    }

    private void DecreaseQuantity()
    {
        ApplyQuantity(Mathf.Max(0, selectedQuantity - 1), true);
    }

    private void ApplyQuantity(int value, bool notify)
    {
        int maximum = availableQuantity > 0 ? availableQuantity : int.MaxValue;
        int previousQuantity = selectedQuantity;
        selectedQuantity = Mathf.Clamp(value, 0, maximum);
        bool clearedReview = reviewState != ReviewState.None &&
            selectedQuantity != previousQuantity;

        if (clearedReview)
        {
            reviewState = ReviewState.None;
            expectedQuantity = 0;
            feedbackPending = false;
            StopFeedbackAnimation();
        }

        if (toggle != null)
            toggle.SetIsOnWithoutNotify(selectedQuantity > 0);
        if (selectionMark != null)
            selectionMark.enabled = selectedQuantity > 0;
        if (quantityText != null)
            quantityText.text = $"x{selectedQuantity}";
        if (decreaseButton != null)
            decreaseButton.interactable = canSelect && selectedQuantity > 0;
        if (increaseButton != null)
            increaseButton.interactable = canSelect &&
                availableQuantity > 0 && selectedQuantity < availableQuantity;

        if (reviewState == ReviewState.None)
        {
            if (nameText != null) nameText.color = style.primaryTextColor;
            if (quantityText != null) quantityText.color = style.primaryTextColor;
            if (selectionMark != null)
            {
                selectionMark.color = style.selectedColor;
                selectionMark.enabled = selectedQuantity > 0;
            }
        }

        if (clearedReview)
            RefreshAvailability();

        if (notify)
        {
            ValueChanged?.Invoke(this, selectedQuantity > 0);
            QuantityChanged?.Invoke(this, selectedQuantity);
        }
    }

    private void BuildVisuals(NotepadMenuVisualStyle visualStyle)
    {
        style = visualStyle ?? new NotepadMenuVisualStyle();
        disabledBackgroundColor = style.disabledColor;
        selectedAccentColor = style.selectedColor;
        background = GetComponent<Image>();
        background.sprite = style.entryBackgroundSprite;
        background.type = style.entryBackgroundSprite != null
            ? Image.Type.Sliced
            : Image.Type.Simple;
        background.color = style.entryColor;
        background.raycastTarget = true;

        toggle = GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.transition = Selectable.Transition.None;
        toggle.interactable = false;
        toggle.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 1f, 1f, 0.9f),
            pressedColor = new Color(0.82f, 0.9f, 1f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };

        iconRoot = CreateRect("Icons", transform);
        ConfigureRect(iconRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(112f, 106f));

        nameText = CreateText("Name", transform, style.nameFontSize, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft, style.primaryTextColor);
        ConfigureRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(132f, 32f), new Vector2(190f, 42f));

        statusText = CreateText("Status", transform, style.detailFontSize, FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft, style.secondaryTextColor);
        ConfigureRect(statusText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(132f, 0f), new Vector2(180f, 28f));

        priceText = CreateText("Price", transform, style.detailFontSize, FontStyles.Bold,
            TextAlignmentOptions.MidlineRight, style.primaryTextColor);
        ConfigureRect(priceText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-16f, 38f), new Vector2(105f, 30f));

        stockText = CreateText("Stock", transform, style.detailFontSize, FontStyles.Normal,
            TextAlignmentOptions.MidlineRight, style.secondaryTextColor);
        ConfigureRect(stockText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(105f, 28f));

        decreaseButton = CreateQuantityButton("Decrease Quantity", transform, "−");
        ConfigureRect(decreaseButton.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-116f, -36f), new Vector2(44f, 44f));

        quantityText = CreateText("Selected Quantity", transform, style.detailFontSize,
            FontStyles.Bold, TextAlignmentOptions.Center, style.primaryTextColor);
        ConfigureRect(quantityText.rectTransform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-64f, -36f), new Vector2(48f, 44f));
        quantityText.text = "x0";

        increaseButton = CreateQuantityButton("Increase Quantity", transform, "+");
        ConfigureRect(increaseButton.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, -36f), new Vector2(44f, 44f));

        RectTransform markRect = CreateRect("Selected", transform);
        ConfigureRect(markRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 106f));
        selectionMark = markRect.gameObject.AddComponent<Image>();
        selectionMark.color = style.selectedColor;
        selectionMark.raycastTarget = false;
        selectionMark.enabled = false;
        toggle.graphic = selectionMark;

        ApplyFont(style.fontAsset);
        ApplyFallbackCardLayout();
        BindControls();
    }

    private void ApplyFallbackCardLayout()
    {
        const float cardWidth = 174f;
        const float cardHeight = 218f;
        LayoutElement layout = GetComponent<LayoutElement>();
        RectTransform root = transform as RectTransform;
        if (layout != null)
        {
            layout.minWidth = cardWidth;
            layout.preferredWidth = cardWidth;
            layout.minHeight = cardHeight;
            layout.preferredHeight = cardHeight;
            layout.flexibleWidth = 0f;
        }
        if (root != null)
        {
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cardHeight);
        }

        ConfigureRect(iconRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(112f, 78f));
        ConfigureRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(-20f, 29f));
        ConfigureRect(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-20f, 21f));
        ConfigureRect(priceText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(-22f, 19f));
        ConfigureRect(decreaseButton.transform as RectTransform,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(12f, 12f), new Vector2(46f, 46f));
        ConfigureRect(quantityText.rectTransform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 12f), new Vector2(54f, 46f));
        ConfigureRect(increaseButton.transform as RectTransform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-12f, 12f), new Vector2(46f, 46f));
        ConfigureRect(selectionMark.rectTransform,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(3f, 0f), new Vector2(6f, -10f));

        nameText.alignment = TextAlignmentOptions.Center;
        statusText.alignment = TextAlignmentOptions.Center;
        priceText.alignment = TextAlignmentOptions.Center;
        stockText.gameObject.SetActive(false);

        ConfigureText(nameText, 16f, 21f, TextWrappingModes.NoWrap);
        ConfigureText(statusText, 12f, 15f, TextWrappingModes.NoWrap);
        ConfigureText(priceText, 14f, 18f, TextWrappingModes.NoWrap);
        ConfigureText(quantityText, 17f, 21f, TextWrappingModes.NoWrap);
    }

    private void CaptureAuthoredCardSize()
    {
        authoredCardSize = GetCurrentCardSize();
    }

    private Vector2 GetCurrentCardSize()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return new Vector2(174f, 218f);

        Vector2 size = rect.rect.size;
        if (size.x <= 0f)
            size.x = Mathf.Abs(rect.sizeDelta.x);
        if (size.y <= 0f)
            size.y = Mathf.Abs(rect.sizeDelta.y);

        return new Vector2(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y));
    }

    private string GetCardSubtitle()
    {
        if (Kind == EntryKind.Bundle)
            return "Combo";

        return Product != null && Product.category == MenuProductCategory.Drink
            ? "Drink"
            : "Menu item";
    }

    private static void ConfigureText(
        TMP_Text text,
        float minimum,
        float maximum,
        TextWrappingModes wrapping)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.textWrappingMode = wrapping;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void BindControls()
    {
        if (decreaseButton != null)
        {
            decreaseButton.onClick.RemoveListener(DecreaseQuantity);
            decreaseButton.onClick.AddListener(DecreaseQuantity);
        }

        if (increaseButton != null)
        {
            increaseButton.onClick.RemoveListener(IncreaseQuantity);
            increaseButton.onClick.AddListener(IncreaseQuantity);
        }

        if (toggle != null)
        {
            toggle.transition = Selectable.Transition.None;
            toggle.interactable = false;
        }
    }

    private void CapturePrefabStyle()
    {
        LayoutElement layout = GetComponent<LayoutElement>();
        RectTransform rect = transform as RectTransform;
        float authoredHeight = layout != null && layout.preferredHeight > 0f
            ? layout.preferredHeight
            : rect != null ? rect.rect.height : 92f;

        style = new NotepadMenuVisualStyle
        {
            entryHeight = authoredHeight,
            entryBackgroundSprite = background != null ? background.sprite : null,
            entryColor = background != null ? background.color : Color.white,
            disabledColor = disabledBackgroundColor,
            selectedColor = selectedAccentColor,
            primaryTextColor = nameText != null ? nameText.color : Color.white,
            secondaryTextColor = stockText != null ? stockText.color : Color.white,
            nameFontSize = nameText != null ? nameText.fontSize : 24f,
            detailFontSize = stockText != null ? stockText.fontSize : 18f,
            fontAsset = nameText != null ? nameText.font : null
        };
    }

    private bool HasRequiredReferences()
    {
        return iconRoot != null && background != null && selectionMark != null &&
               nameText != null && priceText != null && stockText != null &&
               statusText != null && quantityText != null &&
               decreaseButton != null && increaseButton != null && toggle != null;
    }

    private void ResolvePrefabReferences()
    {
        if (background == null)
            background = GetComponent<Image>();
        if (toggle == null)
            toggle = GetComponent<Toggle>();
        if (iconRoot == null)
            iconRoot = FindDescendant<RectTransform>("Icons");
        if (selectionMark == null)
            selectionMark = FindDescendant<Image>("Selected");
        if (nameText == null)
            nameText = FindDescendant<TMP_Text>("Name");
        if (priceText == null)
            priceText = FindDescendant<TMP_Text>("Price");
        if (stockText == null)
            stockText = FindDescendant<TMP_Text>("Stock");
        if (statusText == null)
            statusText = FindDescendant<TMP_Text>("Status");
        if (quantityText == null)
            quantityText = FindDescendant<TMP_Text>("Selected Quantity");
        if (decreaseButton == null)
            decreaseButton = FindDescendant<Button>("Decrease Quantity");
        if (increaseButton == null)
            increaseButton = FindDescendant<Button>("Increase Quantity");
    }

    private T FindDescendant<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].gameObject.name == objectName)
                return components[i];
        }

        return null;
    }

    private void ApplyFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            texts[i].font = fontAsset;
    }

    private void ApplyReviewAppearance()
    {
        if (reviewState == ReviewState.None)
            return;

        if (reviewState == ReviewState.Correct)
        {
            background.color = Color.Lerp(style.entryColor, ReviewCorrectColor, 0.24f);
            nameText.color = style.primaryTextColor;
            quantityText.color = ReviewCorrectColor;
            statusText.color = ReviewCorrectColor;
            statusText.text = $"Correct x{expectedQuantity}";
            selectionMark.color = ReviewCorrectColor;
            selectionMark.enabled = true;
            return;
        }

        bool missing = reviewState == ReviewState.Missing ||
            reviewState == ReviewState.TooFew;
        Color accent = missing ? ReviewMissingColor : ReviewErrorColor;
        background.color = Color.Lerp(style.entryColor, accent, missing ? 0.35f : 0.48f);
        // Every mismatch uses red item/quantity text; the amber accent on
        // missing/short entries helps distinguish "add this" from "remove this".
        nameText.color = ReviewErrorColor;
        quantityText.color = ReviewErrorColor;
        statusText.color = accent;
        selectionMark.color = accent;
        selectionMark.enabled = true;

        switch (reviewState)
        {
            case ReviewState.Missing:
                statusText.text = $"Missing x{expectedQuantity}";
                break;
            case ReviewState.TooFew:
                statusText.text = $"Too few - expected x{expectedQuantity}";
                break;
            case ReviewState.TooMany:
                statusText.text = $"Too many - expected x{expectedQuantity}";
                break;
            case ReviewState.WrongItem:
                statusText.text = $"Not ordered - remove x{selectedQuantity}";
                break;
        }
    }

    private IEnumerator AnimateMismatch()
    {
        RectTransform target = quantityText != null ? quantityText.rectTransform : transform as RectTransform;
        if (target == null)
            yield break;

        const float duration = 0.65f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Max(0f, Mathf.Sin(progress * Mathf.PI * 4f));
            target.localScale = Vector3.one * (1f + pulse * 0.24f);
            yield return null;
        }

        target.localScale = Vector3.one;
        feedbackPending = false;
        feedbackAnimation = null;
    }

    private void StopFeedbackAnimation()
    {
        if (feedbackAnimation != null)
        {
            StopCoroutine(feedbackAnimation);
            feedbackAnimation = null;
        }

        if (quantityText != null)
            quantityText.rectTransform.localScale = Vector3.one;
    }

    private bool HasMismatch()
    {
        return IsMismatchState(reviewState);
    }

    private static bool IsMismatchState(ReviewState state)
    {
        return state == ReviewState.Missing ||
            state == ReviewState.TooFew ||
            state == ReviewState.TooMany ||
            state == ReviewState.WrongItem;
    }

    private void OnEnable()
    {
        if (!feedbackPending || !HasMismatch())
            return;

        feedbackPending = false;
        feedbackAnimation = StartCoroutine(AnimateMismatch());
    }

    private void OnDisable()
    {
        if (HasMismatch())
            feedbackPending = true;
        StopFeedbackAnimation();
    }

    private void SetIcons(IReadOnlyList<Recipe> products)
    {
        for (int i = iconRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = iconRoot.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        icons.Clear();
        if (products == null || products.Count == 0)
            return;

        int count = products.Count;
        float iconSize = count <= 1 ? 68f : Mathf.Clamp(86f / count, 26f, 40f);
        float spacing = count <= 1 ? 0f : 4f;
        float totalWidth = count * iconSize + (count - 1) * spacing;
        float start = -totalWidth * 0.5f + iconSize * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = CreateRect($"Icon {i + 1}", iconRoot);
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(start + i * (iconSize + spacing), 0f),
                new Vector2(iconSize, iconSize));

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = products[i] != null ? products[i].sprite : null;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
            icons.Add(image);
        }
    }

    private static int GetProductStock(Recipe product)
    {
        if (product == null)
            return 0;

        return LobbyStockBridge.Instance != null
            ? LobbyStockBridge.Instance.GetProductStock(product)
            : 0;
    }

    private static string FormatPrice(int price)
    {
        return $"{Mathf.Max(0, price):0.00}";
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent != null ? parent.gameObject.layer : 5;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    private static Button CreateQuantityButton(
        string objectName,
        Transform parent,
        string label)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.38f, 0.62f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", rect, 24f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        ConfigureRect(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.text = label;
        return button;
    }

    private static void ConfigureRect(
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
}
