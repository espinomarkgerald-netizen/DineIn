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

    private VerticalLayoutGroup verticalLayout;
    private LayoutElement contentHeightOverride;
    private ContentSizeFitter contentSizeFitter;
    private bool useCardLayout;
    private bool useEmbeddedPanelLayout;

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

    public void Open(string windowTitle)
    {
        gameObject.SetActive(true);
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

    public void RefreshContentLayout()
    {
        ApplyContentLayout();
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
            verticalLayout.padding = new RectOffset(12, 12, 12, 12);
            verticalLayout.spacing = 12f;
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

        const int rows = 2;
        const float gap = 12f;
        const float padding = 12f;
        Rect viewportRect = scrollRect != null && scrollRect.viewport != null
            ? scrollRect.viewport.rect
            : content.parent is RectTransform parentRect
                ? parentRect.rect
                : content.rect;
        float viewportWidth = Mathf.Max(320f, viewportRect.width);
        float viewportHeight = Mathf.Max(360f, viewportRect.height);
        float cardHeight = Application.isMobilePlatform
            ? Mathf.Clamp((viewportHeight - padding * 2f - gap) / rows, 228f, 270f)
            : Mathf.Clamp((viewportHeight - padding * 2f - gap) / rows, 190f, 236f);
        float cardWidth = Application.isMobilePlatform
            ? Mathf.Clamp(cardHeight * 0.82f, 190f, 222f)
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
        if (!Application.isMobilePlatform)
            return;

        ConfigureText(titleText, 30f, 40f);
        ConfigureText(messageText, 20f, 27f);
        ConfigureText(footerLabel, 21f, 28f);

        if (closeButton != null && closeButton.transform is RectTransform closeRect)
            closeRect.sizeDelta = new Vector2(92f, 82f);
        if (footerButton != null && footerButton.transform is RectTransform footerRect)
            footerRect.sizeDelta = new Vector2(280f, 82f);

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
            ConfigureText(text, buttonLabel ? 19f : 18f, buttonLabel ? 28f : 27f);
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
