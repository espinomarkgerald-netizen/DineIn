using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum ReadinessVisualState
{
    Ready,
    Incoming,
    Warning,
    Blocked
}

public enum ManagementRowCategory
{
    Neutral,
    Mandatory,
    Secondary,
    Bonus
}

public enum FinanceRowKind
{
    Header,
    Section,
    Balance,
    Income,
    Expense,
    Detail,
    Total,
    NetPositive,
    NetNegative
}

/// <summary>Reusable, prefab-backed row used by every management computer app.</summary>
public sealed class ManagementComputerRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;

    [Header("Editor Preview")]
    [SerializeField] private bool previewPortraitCard = true;

    [Header("Section Card Colours (Editable)")]
    [SerializeField] private Color neutralPanelColor = new Color(0.91f, 0.96f, 1f, 1f);
    [SerializeField] private Color neutralAccentColor = new Color(0.08f, 0.36f, 0.62f, 1f);
    [SerializeField] private Color mandatoryPanelColor = new Color(1f, 0.89f, 0.88f, 1f);
    [SerializeField] private Color mandatoryAccentColor = new Color(0.78f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color secondaryPanelColor = new Color(0.88f, 0.95f, 1f, 1f);
    [SerializeField] private Color secondaryAccentColor = new Color(0.08f, 0.47f, 0.76f, 1f);
    [SerializeField] private Color bonusPanelColor = new Color(1f, 0.96f, 0.79f, 1f);
    [SerializeField] private Color bonusAccentColor = new Color(0.76f, 0.48f, 0.04f, 1f);

    [Header("Finance Receipt Style (Editable)")]
    [SerializeField] private Color financePaperColor = new Color(0.985f, 0.98f, 0.95f, 1f);
    [SerializeField] private Color financeHeaderColor = new Color(0.86f, 0.94f, 0.99f, 1f);
    [SerializeField] private Color financeSectionColor = new Color(0.93f, 0.96f, 0.98f, 1f);
    [SerializeField] private Color financeInkColor = new Color(0.10f, 0.16f, 0.22f, 1f);
    [SerializeField] private Color financeMutedColor = new Color(0.34f, 0.40f, 0.47f, 1f);
    [SerializeField] private Color financeIncomeColor = new Color(0.07f, 0.48f, 0.27f, 1f);
    [SerializeField] private Color financeExpenseColor = new Color(0.76f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color financeDividerColor = new Color(0.58f, 0.67f, 0.74f, 0.42f);
    [SerializeField, Min(48f)] private float financeHeaderHeight = 108f;
    [SerializeField, Min(40f)] private float financeSectionHeight = 58f;
    [SerializeField, Min(64f)] private float financeRowHeight = 88f;
    [SerializeField, Min(64f)] private float financeTotalHeight = 98f;

    private bool cardPresentation;
    private Image financeDivider;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            ApplyPresentation(previewPortraitCard);
    }
#endif

    public void ConfigureReferences(
        Image configuredIcon,
        TMP_Text configuredTitle,
        TMP_Text configuredDetails,
        TMP_Text configuredValue,
        Button configuredAction,
        TMP_Text configuredActionLabel)
    {
        icon = configuredIcon;
        titleText = configuredTitle;
        detailsText = configuredDetails;
        valueText = configuredValue;
        actionButton = configuredAction;
        actionLabel = configuredActionLabel;
    }

    public void Bind(
        Sprite sprite,
        string title,
        string details,
        string value,
        string action,
        UnityAction onAction,
        bool actionEnabled = true)
    {
        if (financeDivider != null)
            financeDivider.gameObject.SetActive(false);

        Image background = GetComponent<Image>();
        if (background != null)
            background.color = sprite != null ? neutralPanelColor : Color.white;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }

        if (titleText != null) titleText.text = title ?? string.Empty;
        if (detailsText != null) detailsText.text = details ?? string.Empty;
        if (valueText != null) valueText.text = value ?? string.Empty;
        if (valueText != null) valueText.color = new Color(0.08f, 0.14f, 0.22f, 1f);

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            if (onAction != null)
                actionButton.onClick.AddListener(onAction);
            actionButton.interactable = actionEnabled && onAction != null;
            actionButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(action));
        }

        if (actionLabel != null) actionLabel.text = action ?? string.Empty;
        ApplyActionState(!string.IsNullOrWhiteSpace(action));
    }

    public void BindFinance(
        string title,
        string details,
        string amount,
        FinanceRowKind kind)
    {
        Bind(null, title, details, amount, string.Empty, null, false);
        ApplyFinanceLayout(kind);
    }

    public void BindCategory(
        Sprite sprite,
        string title,
        string details,
        ManagementRowCategory category)
    {
        Bind(sprite, title, details, string.Empty, string.Empty, null, false);

        Color panel = neutralPanelColor;
        Color accent = neutralAccentColor;
        switch (category)
        {
            case ManagementRowCategory.Mandatory:
                panel = mandatoryPanelColor;
                accent = mandatoryAccentColor;
                break;
            case ManagementRowCategory.Secondary:
                panel = secondaryPanelColor;
                accent = secondaryAccentColor;
                break;
            case ManagementRowCategory.Bonus:
                panel = bonusPanelColor;
                accent = bonusAccentColor;
                break;
        }

        Image background = GetComponent<Image>();
        if (background != null)
            background.color = panel;
        if (titleText != null)
        {
            titleText.color = accent;
            titleText.fontStyle = FontStyles.Bold;
        }
        if (detailsText != null)
            detailsText.color = new Color(0.16f, 0.23f, 0.31f, 1f);
    }

    public void BindReadiness(
        Sprite sprite,
        string title,
        string details,
        ReadinessVisualState state,
        string action,
        UnityAction onAction)
    {
        string symbol;
        Color accent;
        Color panel;
        switch (state)
        {
            case ReadinessVisualState.Incoming:
                symbol = "→";
                accent = new Color(0.08f, 0.42f, 0.78f, 1f);
                panel = new Color(0.84f, 0.93f, 1f, 1f);
                break;
            case ReadinessVisualState.Warning:
                symbol = "!";
                accent = new Color(0.88f, 0.48f, 0.04f, 1f);
                panel = new Color(1f, 0.94f, 0.78f, 1f);
                break;
            case ReadinessVisualState.Blocked:
                symbol = "×";
                accent = new Color(0.82f, 0.12f, 0.12f, 1f);
                panel = new Color(1f, 0.86f, 0.84f, 1f);
                break;
            default:
                symbol = "✓";
                accent = new Color(0.08f, 0.55f, 0.30f, 1f);
                panel = new Color(0.84f, 0.96f, 0.88f, 1f);
                break;
        }

        Bind(sprite, title, details, symbol, action, onAction, onAction != null);

        Image background = GetComponent<Image>();
        if (background != null)
            background.color = panel;
        if (valueText != null)
        {
            valueText.color = accent;
            valueText.fontStyle = FontStyles.Bold;
            valueText.fontSizeMin = 34f;
            valueText.fontSizeMax = 48f;
        }
        if (titleText != null)
        {
            titleText.color = accent;
            titleText.fontStyle = FontStyles.Bold;
        }
    }

    public void ApplyPresentation(bool asCard)
    {
        cardPresentation = asCard;

        LayoutElement layout = GetComponent<LayoutElement>();
        RectTransform root = transform as RectTransform;
        bool alternateTouchPresentation = UsesAlternateTouchPresentation;
        float height = alternateTouchPresentation
            ? asCard ? 270f : 168f
            : asCard ? 228f : 148f;
        if (layout != null)
        {
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.minWidth = asCard
                ? alternateTouchPresentation ? 190f : 176f
                : -1f;
            layout.preferredWidth = asCard
                ? alternateTouchPresentation ? 222f : 190f
                : -1f;
            layout.flexibleWidth = asCard ? 0f : 1f;
        }
        if (root != null)
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (asCard)
            ApplyCardLayout();
        else
            ApplyWideRowLayout(true);
    }

    private bool UsesAlternateTouchPresentation
    {
        get
        {
            ManagementComputerResponsiveLayout responsive =
                GetComponentInParent<ManagementComputerResponsiveLayout>(true);
            return responsive != null && responsive.UsesMobileLayout;
        }
    }

    private void ApplyCardLayout()
    {
        SetFixed(icon != null ? icon.rectTransform : null,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -43f), new Vector2(74f, 74f));
        SetTopStretch(titleText != null ? titleText.rectTransform : null,
            12f, 12f, 82f, 30f);
        SetTopStretch(valueText != null ? valueText.rectTransform : null,
            12f, 12f, 111f, 25f);
        SetStretch(detailsText != null ? detailsText.rectTransform : null,
            12f, 12f, 62f, 136f);
        SetBottomStretch(actionButton != null
                ? actionButton.transform as RectTransform
                : null,
            12f, 12f, 10f, 46f);

        if (titleText != null) titleText.alignment = TextAlignmentOptions.Center;
        if (detailsText != null) detailsText.alignment = TextAlignmentOptions.Center;
        if (valueText != null) valueText.alignment = TextAlignmentOptions.Center;

        ConfigureAutosizing(titleText, 16f, 21f, TextWrappingModes.NoWrap);
        ConfigureAutosizing(detailsText, 12f, 16f, TextWrappingModes.Normal);
        ConfigureAutosizing(valueText, 14f, 18f, TextWrappingModes.NoWrap);
        ConfigureAutosizing(actionLabel, 15f, 20f, TextWrappingModes.NoWrap);
    }

    private void ApplyWideRowLayout(bool hasAction)
    {
        if (titleText != null) titleText.alignment = TextAlignmentOptions.MidlineLeft;
        if (detailsText != null) detailsText.alignment = TextAlignmentOptions.MidlineLeft;
        if (valueText != null) valueText.alignment = TextAlignmentOptions.Center;

        SetFixed(icon != null ? icon.rectTransform : null,
            new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(58f, 0f), new Vector2(88f, 88f));

        float textRight = hasAction ? 356f : 188f;
        SetStretch(titleText != null ? titleText.rectTransform : null,
            104f, textRight, 62f, 10f);
        SetStretch(detailsText != null ? detailsText.rectTransform : null,
            104f, textRight, 12f, 66f);

        SetFixed(valueText != null ? valueText.rectTransform : null,
            new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(hasAction ? -270f : -92f, 0f), new Vector2(150f, 88f));
        SetFixed(actionButton != null
                ? actionButton.transform as RectTransform
                : null,
            new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-96f, 0f), new Vector2(168f, 76f));

        ConfigureAutosizing(titleText, 20f, 28f, TextWrappingModes.NoWrap);
        ConfigureAutosizing(detailsText, 15f, 20f, TextWrappingModes.Normal);
        ConfigureAutosizing(valueText, 17f, 23f, TextWrappingModes.Normal);
        ConfigureAutosizing(actionLabel, 18f, 24f, TextWrappingModes.NoWrap);
    }

    private void ApplyActionState(bool hasAction)
    {
        if (!cardPresentation)
            ApplyWideRowLayout(hasAction);
    }

    private void ApplyFinanceLayout(FinanceRowKind kind)
    {
        bool header = kind == FinanceRowKind.Header;
        bool section = kind == FinanceRowKind.Section;
        bool total = kind == FinanceRowKind.Total ||
                     kind == FinanceRowKind.NetPositive ||
                     kind == FinanceRowKind.NetNegative;
        float height = header
            ? financeHeaderHeight
            : section ? financeSectionHeight : total ? financeTotalHeight : financeRowHeight;

        LayoutElement layout = GetComponent<LayoutElement>();
        RectTransform root = transform as RectTransform;
        if (layout != null)
        {
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.minWidth = -1f;
            layout.preferredWidth = -1f;
            layout.flexibleWidth = 1f;
        }
        if (root != null)
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (icon != null)
            icon.gameObject.SetActive(false);
        if (actionButton != null)
            actionButton.gameObject.SetActive(false);

        Image background = GetComponent<Image>();
        if (background != null)
            background.color = header
                ? financeHeaderColor
                : section ? financeSectionColor : financePaperColor;

        if (section)
        {
            SetStretch(titleText != null ? titleText.rectTransform : null, 24f, 24f, 8f, 8f);
            if (titleText != null)
            {
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.color = neutralAccentColor;
                titleText.fontStyle = FontStyles.Bold;
            }
            if (detailsText != null) detailsText.gameObject.SetActive(false);
            if (valueText != null) valueText.gameObject.SetActive(false);
            ConfigureAutosizing(titleText, 19f, 25f, TextWrappingModes.NoWrap);
        }
        else if (header)
        {
            SetTopStretch(titleText != null ? titleText.rectTransform : null, 24f, 24f, 15f, 38f);
            SetTopStretch(detailsText != null ? detailsText.rectTransform : null, 24f, 24f, 55f, 30f);
            if (valueText != null) valueText.gameObject.SetActive(false);
            if (titleText != null)
            {
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.color = neutralAccentColor;
                titleText.fontStyle = FontStyles.Bold;
            }
            if (detailsText != null)
            {
                detailsText.gameObject.SetActive(true);
                detailsText.alignment = TextAlignmentOptions.Center;
                detailsText.color = financeMutedColor;
                detailsText.fontStyle = FontStyles.Normal;
            }
            ConfigureAutosizing(titleText, 22f, 31f, TextWrappingModes.NoWrap);
            ConfigureAutosizing(detailsText, 15f, 20f, TextWrappingModes.NoWrap);
        }
        else
        {
            if (detailsText != null) detailsText.gameObject.SetActive(true);
            if (valueText != null) valueText.gameObject.SetActive(true);
            SetStretch(titleText != null ? titleText.rectTransform : null, 24f, 252f, 42f, 10f);
            SetStretch(detailsText != null ? detailsText.rectTransform : null, 24f, 252f, 10f, 48f);
            SetFixed(valueText != null ? valueText.rectTransform : null,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(218f, height - 20f));

            if (titleText != null)
            {
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.color = financeInkColor;
                titleText.fontStyle = total ? FontStyles.Bold : FontStyles.Normal;
            }
            if (detailsText != null)
            {
                detailsText.alignment = TextAlignmentOptions.MidlineLeft;
                detailsText.color = financeMutedColor;
                detailsText.fontStyle = FontStyles.Normal;
            }
            if (valueText != null)
            {
                valueText.alignment = TextAlignmentOptions.MidlineRight;
                valueText.fontStyle = FontStyles.Bold;
                valueText.color = kind == FinanceRowKind.Income || kind == FinanceRowKind.NetPositive
                    ? financeIncomeColor
                    : kind == FinanceRowKind.Expense || kind == FinanceRowKind.Detail ||
                      kind == FinanceRowKind.NetNegative
                        ? financeExpenseColor
                        : neutralAccentColor;
            }

            ConfigureAutosizing(titleText, total ? 19f : 17f, total ? 25f : 23f,
                TextWrappingModes.NoWrap);
            ConfigureAutosizing(detailsText, 14f, 18f, TextWrappingModes.NoWrap);
            ConfigureAutosizing(valueText, total ? 20f : 17f, total ? 27f : 23f,
                TextWrappingModes.NoWrap);
        }

        EnsureFinanceDivider();
        if (financeDivider != null)
        {
            financeDivider.gameObject.SetActive(!header);
            financeDivider.color = total ? neutralAccentColor : financeDividerColor;
        }
    }

    private void EnsureFinanceDivider()
    {
        if (financeDivider != null)
            return;

        Transform existing = transform.Find("Finance Divider");
        if (existing != null)
            financeDivider = existing.GetComponent<Image>();
        if (financeDivider == null)
        {
            GameObject dividerObject = new GameObject(
                "Finance Divider",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dividerObject.transform.SetParent(transform, false);
            financeDivider = dividerObject.GetComponent<Image>();
            financeDivider.raycastTarget = false;
        }

        RectTransform divider = financeDivider.rectTransform;
        divider.anchorMin = new Vector2(0f, 0f);
        divider.anchorMax = new Vector2(1f, 0f);
        divider.pivot = new Vector2(0.5f, 0f);
        divider.anchoredPosition = Vector2.zero;
        divider.sizeDelta = new Vector2(0f, 2f);
        financeDivider.transform.SetAsLastSibling();
    }

    private static void ConfigureAutosizing(
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

    private static void SetFixed(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopStretch(
        RectTransform rect,
        float left,
        float right,
        float top,
        float height)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void SetBottomStretch(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float height)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, bottom);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void SetStretch(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
