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

    public RectTransform Content => content;
    public Button FooterButton => footerButton;

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

    public void Close() => gameObject.SetActive(false);

    public void ClearRows()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
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
