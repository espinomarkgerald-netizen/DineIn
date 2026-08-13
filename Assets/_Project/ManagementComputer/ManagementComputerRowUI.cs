using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Reusable, prefab-backed row used by every management computer app.</summary>
public sealed class ManagementComputerRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;

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
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }

        if (titleText != null) titleText.text = title ?? string.Empty;
        if (detailsText != null) detailsText.text = details ?? string.Empty;
        if (valueText != null) valueText.text = value ?? string.Empty;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            if (onAction != null)
                actionButton.onClick.AddListener(onAction);
            actionButton.interactable = actionEnabled && onAction != null;
            actionButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(action));
        }

        if (actionLabel != null) actionLabel.text = action ?? string.Empty;
    }
}
