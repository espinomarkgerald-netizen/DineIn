using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Editable Windows-style application window shared by all apps.</summary>
public sealed class ManagementComputerWindow : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button footerButton;
    [SerializeField] private TMP_Text footerLabel;

    [Header("Mobile Window Style (Editable)")]
    [SerializeField, Min(8f)] private float mobileTitleMinimum = 32f;
    [SerializeField, Min(8f)] private float mobileTitleMaximum = 42f;
    [SerializeField, Min(8f)] private float mobileMessageMinimum = 21f;
    [SerializeField, Min(8f)] private float mobileMessageMaximum = 29f;
    [SerializeField, Min(8f)] private float mobileBodyMinimum = 19f;
    [SerializeField, Min(8f)] private float mobileBodyMaximum = 27f;
    [SerializeField, Min(8f)] private float mobileButtonMinimum = 20f;
    [SerializeField, Min(8f)] private float mobileButtonMaximum = 29f;
    [SerializeField] private Vector2 mobileCloseButtonSize = new Vector2(96f, 86f);
    [SerializeField] private Vector2 mobileFooterButtonSize = new Vector2(300f, 86f);
    [SerializeField, Min(0f)] private float mobileContentPadding = 14f;
    [SerializeField, Min(0f)] private float mobileContentSpacing = 14f;
    [SerializeField, Min(1)] private int mobileCardRows = 2;
    [SerializeField] private Vector2 mobileCardSizeRange = new Vector2(236f, 282f);

    [Header("Finance Statement Layout (Editable)")]
    [SerializeField, Min(0f)] private float financeHorizontalPadding = 28f;
    [SerializeField, Min(0f)] private float financeVerticalPadding = 18f;
    [SerializeField, Min(0f)] private float financeRowSpacing = 2f;

    private VerticalLayoutGroup verticalLayout;
    private LayoutElement contentHeightOverride;
    private ContentSizeFitter contentSizeFitter;
    private bool useCardLayout;
    private bool useEmbeddedPanelLayout;
    private bool useFinanceStatementLayout;
    private bool initialScrollResetPending;

    public RectTransform Content => content;
    public Button FooterButton => footerButton;
    public float VerticalNormalizedPosition =>
        scrollRect == null
            ? 1f
            : useCardLayout
                ? 1f - scrollRect.horizontalNormalizedPosition
                : scrollRect.verticalNormalizedPosition;
    public bool UsesCardLayout => useCardLayout;

    public void ConfigureReferences(
        TMP_Text configuredTitle,
        Button configuredClose,
        ScrollRect configuredScroll,
        RectTransform configuredContent,
        TMP_Text configuredMessage,
        Button configuredFooter,
        TMP_Text configuredFooterLabel)
    {
        titleText = configuredTitle;
        closeButton = configuredClose;
        scrollRect = configuredScroll;
        content = configuredContent;
        messageText = configuredMessage;
        footerButton = configuredFooter;
        footerLabel = configuredFooterLabel;
    }

    public void Initialize(UnityAction closeAction)
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(closeAction);
        }
    }

    public void SetTypography(
        TMP_FontAsset displayFont,
        TMP_FontAsset readableFont,
        TMP_FontAsset readableBoldFont)
    {
        if (titleText != null && displayFont != null)
            titleText.font = displayFont;
        if (messageText != null && readableFont != null)
            messageText.font = readableFont;
        if (footerLabel != null)
            footerLabel.font = readableBoldFont != null ? readableBoldFont : readableFont;
    }

    public void Open(string windowTitle)
    {
        gameObject.SetActive(true);
        initialScrollResetPending = true;
        GetComponent<UIRevealAnimation>()?.Play();
        if (titleText != null) titleText.text = windowTitle;
        ApplyMobileChromeAndTypography();
        SetMessage(string.Empty);
        SetFooter(string.Empty, null, false);

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    public void SetContentLayout(bool cards)
    {
        useCardLayout = cards;
        ApplyContentLayout();
    }

    public void SetEmbeddedPanelLayout(bool embedded)
    {
        useEmbeddedPanelLayout = embedded;
        ApplyContentLayout();
    }

    public void SetFinanceStatementLayout(bool financeStatement)
    {
        useFinanceStatementLayout = financeStatement;
        ApplyContentLayout();
    }

    public void RefreshContentLayout()
    {
        ApplyContentLayout();
        if (initialScrollResetPending)
            ResetAllScrollRectsToStart();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            ApplyContentLayout();
    }

    // Reflows card apps when a device rotates or its safe area changes.
    private void ApplyContentLayout()
    {
        if (content == null)
            return;

        ApplyMobileChromeAndTypography();

        if (verticalLayout == null)
            verticalLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentHeightOverride == null)
            contentHeightOverride = content.GetComponent<LayoutElement>();
        if (contentHeightOverride == null)
            contentHeightOverride = content.gameObject.AddComponent<LayoutElement>();
        if (contentSizeFitter == null)
            contentSizeFitter = content.GetComponent<ContentSizeFitter>();

        if (scrollRect != null)
        {
            scrollRect.horizontal = useCardLayout && !useEmbeddedPanelLayout;
            scrollRect.vertical = !useCardLayout && !useEmbeddedPanelLayout;
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                scrollRect.horizontalNormalizedPosition);
            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(
                    !useCardLayout && !useEmbeddedPanelLayout);
        }

        if (verticalLayout != null)
        {
            verticalLayout.enabled = !useCardLayout && !useEmbeddedPanelLayout;
            int horizontalPadding = Mathf.RoundToInt(useFinanceStatementLayout
                ? financeHorizontalPadding
                : UsesMobileLayout ? mobileContentPadding : 12f);
            int verticalPadding = Mathf.RoundToInt(useFinanceStatementLayout
                ? financeVerticalPadding
                : UsesMobileLayout ? mobileContentPadding : 12f);
            verticalLayout.padding = new RectOffset(
                horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            verticalLayout.spacing = useFinanceStatementLayout
                ? financeRowSpacing
                : UsesMobileLayout ? mobileContentSpacing : 12f;
        }

        if (useEmbeddedPanelLayout)
        {
            if (contentSizeFitter != null)
                contentSizeFitter.enabled = false;

            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            contentHeightOverride.minWidth = -1f;
            contentHeightOverride.preferredWidth = -1f;
            contentHeightOverride.minHeight = -1f;
            contentHeightOverride.preferredHeight = -1f;

            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                    continue;
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.pivot = new Vector2(0.5f, 0.5f);
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
            }

            LayoutRebuilder.MarkLayoutForRebuild(content);
            return;
        }

        if (!useCardLayout)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-16f, 0f);

            if (contentSizeFitter != null)
            {
                contentSizeFitter.enabled = true;
                contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            contentHeightOverride.minWidth = -1f;
            contentHeightOverride.preferredWidth = -1f;
            contentHeightOverride.minHeight = -1f;
            contentHeightOverride.preferredHeight = -1f;
            return;
        }

        if (contentSizeFitter != null)
            contentSizeFitter.enabled = false;

        int rows = UsesMobileLayout ? Mathf.Max(1, mobileCardRows) : 2;
        float gap = UsesMobileLayout ? mobileContentSpacing : 12f;
        float padding = UsesMobileLayout ? mobileContentPadding : 12f;
        Rect viewportRect = scrollRect != null && scrollRect.viewport != null
            ? scrollRect.viewport.rect
            : content.parent is RectTransform parentRect
                ? parentRect.rect
                : content.rect;
        float viewportWidth = Mathf.Max(320f, viewportRect.width);
        float viewportHeight = Mathf.Max(360f, viewportRect.height);
        float mobileCardMin = Mathf.Min(mobileCardSizeRange.x, mobileCardSizeRange.y);
        float mobileCardMax = Mathf.Max(mobileCardSizeRange.x, mobileCardSizeRange.y);
        float cardHeight = UsesMobileLayout
            ? Mathf.Clamp((viewportHeight - padding * 2f - gap * (rows - 1)) / rows,
                mobileCardMin, mobileCardMax)
            : Mathf.Clamp((viewportHeight - padding * 2f - gap) / rows, 190f, 236f);
        float cardWidth = UsesMobileLayout
            ? Mathf.Clamp(cardHeight * 0.84f, mobileCardMin * 0.84f, mobileCardMax * 0.84f)
            : Mathf.Clamp(cardHeight * 0.8f, 176f, 194f);

        int visibleIndex = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            int row = visibleIndex % rows;
            int column = visibleIndex / rows;
            child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = new Vector2(cardWidth, cardHeight);
            child.anchoredPosition = new Vector2(
                padding + cardWidth * 0.5f + column * (cardWidth + gap),
                -(padding + cardHeight * 0.5f + row * (cardHeight + gap)));
            visibleIndex++;
        }

        int columns = Mathf.CeilToInt(visibleIndex / (float)rows);
        float preferredWidth = padding * 2f + columns * cardWidth +
                               Mathf.Max(0, columns - 1) * gap;
        preferredWidth = Mathf.Max(viewportWidth, preferredWidth);

        content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(preferredWidth, viewportHeight);
        contentHeightOverride.minWidth = preferredWidth;
        contentHeightOverride.preferredWidth = preferredWidth;
        contentHeightOverride.minHeight = viewportHeight;
        contentHeightOverride.preferredHeight = viewportHeight;
        LayoutRebuilder.MarkLayoutForRebuild(content);
    }

    private void ApplyMobileChromeAndTypography()
    {
        if (!UsesMobileLayout)
            return;

        ConfigureText(titleText, mobileTitleMinimum, mobileTitleMaximum);
        ConfigureText(messageText, mobileMessageMinimum, mobileMessageMaximum);
        ConfigureText(footerLabel, mobileButtonMinimum + 1f, mobileButtonMaximum + 1f);

        if (closeButton != null && closeButton.transform is RectTransform closeRect)
            closeRect.sizeDelta = mobileCloseButtonSize;
        if (footerButton != null && footerButton.transform is RectTransform footerRect)
            footerRect.sizeDelta = mobileFooterButtonSize;

        // App panels are populated after the window opens. Re-running this from the
        // layout refresh gives every generated row/card readable type without
        // changing its wording or blindly scaling the complete window.
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || text == titleText || text == messageText || text == footerLabel)
                continue;

            bool buttonLabel = text.GetComponentInParent<Button>() != null;
            ConfigureText(
                text,
                buttonLabel ? mobileButtonMinimum : mobileBodyMinimum,
                buttonLabel ? mobileButtonMaximum : mobileBodyMaximum);
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

    private static void ConfigureText(TMP_Text text, float minimum, float maximum)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(text.fontSizeMin, minimum);
        text.fontSizeMax = Mathf.Max(text.fontSizeMax, maximum);
        text.fontSize = Mathf.Max(text.fontSize, minimum);
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    public void Close() => gameObject.SetActive(false);

    public void RestoreVerticalNormalizedPositionNextFrame(float normalizedPosition)
    {
        if (scrollRect == null)
            return;

        initialScrollResetPending = false;
        normalizedPosition = Mathf.Clamp01(normalizedPosition);
        Canvas.ForceUpdateCanvases();
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        SetNormalizedPosition(normalizedPosition);
        StartCoroutine(RestoreVerticalNormalizedPositionRoutine(normalizedPosition));
    }

    private IEnumerator RestoreVerticalNormalizedPositionRoutine(float normalizedPosition)
    {
        // ClearRows uses Destroy, so wait for the old rows to leave the layout
        // before restoring the position on the rebuilt content.
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        SetNormalizedPosition(normalizedPosition);
    }

    private void SetNormalizedPosition(float normalizedPosition)
    {
        if (scrollRect == null)
            return;

        normalizedPosition = Mathf.Clamp01(normalizedPosition);
        scrollRect.StopMovement();
        if (useCardLayout)
            scrollRect.horizontalNormalizedPosition = 1f - normalizedPosition;
        else
            scrollRect.verticalNormalizedPosition = normalizedPosition;
    }

    /// <summary>
    /// Keeps newly populated ScrollRects at their authored starting edge while
    /// nested layout groups publish their final sizes over the first two frames.
    /// </summary>
    public void ResetInitialScrollsAfterLayout(bool complete)
    {
        if (!initialScrollResetPending)
            return;

        ResetAllScrollRectsToStart();
        if (complete)
            initialScrollResetPending = false;
    }

    private void ResetAllScrollRectsToStart()
    {
        ScrollRect[] scrolls = GetComponentsInChildren<ScrollRect>(false);
        for (int i = 0; i < scrolls.Length; i++)
        {
            ScrollRect activeScroll = scrolls[i];
            if (activeScroll == null || !activeScroll.gameObject.activeInHierarchy)
                continue;

            if (activeScroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(activeScroll.content);
            activeScroll.StopMovement();
            if (activeScroll.horizontal)
                activeScroll.horizontalNormalizedPosition = 0f;
            if (activeScroll.vertical)
                activeScroll.verticalNormalizedPosition = 1f;
        }
    }

    public void ClearRows()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject oldRow = content.GetChild(i).gameObject;
            oldRow.SetActive(false);
            Destroy(oldRow);
        }
    }

    public void SetMessage(string message, bool warning = false)
    {
        if (messageText == null)
            return;

        messageText.text = message ?? string.Empty;
        messageText.color = warning
            ? new Color(0.85f, 0.24f, 0.22f)
            : new Color(0.19f, 0.25f, 0.33f);
        messageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    public void SetFooter(string label, UnityAction action, bool interactable = true)
    {
        if (footerButton == null)
            return;

        footerButton.onClick.RemoveAllListeners();
        if (action != null)
            footerButton.onClick.AddListener(action);
        footerButton.interactable = interactable && action != null;
        footerButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(label));
        if (footerLabel != null) footerLabel.text = label ?? string.Empty;
    }
}
