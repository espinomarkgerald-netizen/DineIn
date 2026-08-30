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

    private bool cardPresentation;

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
        Image background = GetComponent<Image>();
        if (background != null)
            background.color = Color.white;

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
        float height = Application.isMobilePlatform
            ? asCard ? 270f : 168f
            : asCard ? 228f : 148f;
        if (layout != null)
        {
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.minWidth = asCard
                ? Application.isMobilePlatform ? 190f : 176f
                : -1f;
            layout.preferredWidth = asCard
                ? Application.isMobilePlatform ? 222f : 190f
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
