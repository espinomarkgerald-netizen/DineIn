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
    private bool useCardLayout;

    public RectTransform Content => content;
    public Button FooterButton => footerButton;
    public float VerticalNormalizedPosition =>
        scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;
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
        SetMessage(string.Empty);
        SetFooter(string.Empty, null, false);

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    public void SetContentLayout(bool cards)
    {
        useCardLayout = cards;
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

        if (verticalLayout == null)
            verticalLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentHeightOverride == null)
            contentHeightOverride = content.GetComponent<LayoutElement>();
        if (contentHeightOverride == null)
            contentHeightOverride = content.gameObject.AddComponent<LayoutElement>();

        if (verticalLayout != null)
        {
            verticalLayout.enabled = !useCardLayout;
            verticalLayout.padding = new RectOffset(12, 12, 12, 12);
            verticalLayout.spacing = 12f;
        }

        if (!useCardLayout)
        {
            contentHeightOverride.minHeight = -1f;
            contentHeightOverride.preferredHeight = -1f;
            return;
        }

        const float gap = 14f;
        const float padding = 12f;
        const float cardHeight = 190f;
        float availableWidth = Mathf.Max(280f, content.rect.width - padding * 2f);
        int columns = availableWidth >= 500f ? 2 : 1;
        float cellWidth = (availableWidth - gap * (columns - 1)) / columns;
        cellWidth = Mathf.Max(240f, cellWidth);

        int visibleIndex = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            int column = visibleIndex % columns;
            int row = visibleIndex / columns;
            child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = new Vector2(cellWidth, cardHeight);
            child.anchoredPosition = new Vector2(
                padding + cellWidth * 0.5f + column * (cellWidth + gap),
                -(padding + cardHeight * 0.5f + row * (cardHeight + gap)));
            visibleIndex++;
        }

        int rows = Mathf.CeilToInt(visibleIndex / (float)columns);
        float preferredHeight = padding * 2f + rows * cardHeight +
                                Mathf.Max(0, rows - 1) * gap;
        contentHeightOverride.minHeight = preferredHeight;
        contentHeightOverride.preferredHeight = preferredHeight;
        LayoutRebuilder.MarkLayoutForRebuild(content);
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
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = normalizedPosition;
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
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
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
