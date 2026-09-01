using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Large, editable equipment-store card authored as a prefab.</summary>
public sealed class ManagementEquipmentCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text availabilityText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Image ownedBadge;
    [SerializeField] private ManagementItemCardFeedback feedback;

    [Header("State Colours")]
    [SerializeField] private Color availableColor = new Color(0.05f, 0.40f, 0.67f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.46f, 0.52f, 0.60f, 1f);
    [SerializeField] private Color ownedColor = new Color(0.08f, 0.56f, 0.31f, 1f);

    public void ConfigureReferences(
        Image configuredIcon,
        TMP_Text configuredTitle,
        TMP_Text configuredDescription,
        TMP_Text configuredAvailability,
        TMP_Text configuredPrice,
        Button configuredAction,
        TMP_Text configuredActionLabel,
        Image configuredOwnedBadge)
    {
        icon = configuredIcon;
        titleText = configuredTitle;
        descriptionText = configuredDescription;
        availabilityText = configuredAvailability;
        priceText = configuredPrice;
        actionButton = configuredAction;
        actionLabel = configuredActionLabel;
        ownedBadge = configuredOwnedBadge;
    }

    public void Bind(
        Equipment equipment,
        string description,
        bool unlocked,
        bool purchased,
        bool storeEditable,
        bool canBuy,
        UnityAction purchaseAction)
    {
        if (equipment == null)
            return;

        if (icon != null)
        {
            icon.sprite = equipment.sprite;
            icon.enabled = equipment.sprite != null;
            icon.preserveAspect = true;
        }
        if (titleText != null)
            titleText.text = equipment.displayName;
        if (descriptionText != null)
            descriptionText.text = description ?? string.Empty;
        if (availabilityText != null)
        {
            availabilityText.text = purchased
                ? "OWNED"
                : unlocked ? "AVAILABLE NOW" : "UNLOCKS DAY " + equipment.dayToUnlock;
            availabilityText.color = purchased
                ? ownedColor
                : unlocked ? availableColor : lockedColor;
        }
        if (priceText != null)
        {
            priceText.text = purchased ? "PURCHASED" : unlocked ? "₱" + equipment.cost : "LOCKED";
            priceText.color = purchased
                ? ownedColor
                : unlocked ? availableColor : lockedColor;
        }
        if (ownedBadge != null)
            ownedBadge.gameObject.SetActive(purchased);

        ManagementItemCardFeedback cardFeedback = GetFeedback();
        if (cardFeedback != null)
        {
            string state = purchased
                ? "Owned and ready to use"
                : unlocked ? "Available for ₱" + equipment.cost :
                    "Unlocks on day " + Mathf.Max(1, equipment.dayToUnlock);
            cardFeedback.SetTooltip(
                equipment.displayName,
                (description ?? string.Empty) + "\n" + state);
            cardFeedback.SetSelected(purchased);
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            if (purchaseAction != null)
                actionButton.onClick.AddListener(() =>
                {
                    ManagementItemCardFeedback activeFeedback = GetFeedback();
                    if (activeFeedback != null)
                        activeFeedback.PlaySuccessFeedback(purchaseAction);
                    else
                        purchaseAction.Invoke();
                });
            actionButton.interactable = storeEditable && canBuy && !purchased && unlocked && purchaseAction != null;
            actionButton.gameObject.SetActive(!purchased);
        }
        if (actionLabel != null)
            actionLabel.text = !unlocked
                ? "LOCKED"
                : !storeEditable
                    ? "SERVICE ACTIVE"
                    : canBuy ? "BUY" : "NOT ENOUGH";
    }

    private ManagementItemCardFeedback GetFeedback()
    {
        if (feedback == null)
            feedback = GetComponent<ManagementItemCardFeedback>();
        return feedback;
    }
}
