using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one ShopItemData on the ShopItem prefab and reports Buy clicks
/// back to whoever bound it.
///
/// Single Responsibility: this script only fills in the icon/name/price
/// labels and forwards the Buy button click - it never talks to PlayFab,
/// never decides whether a purchase is allowed, and never knows which
/// category it's showing (ShopPanelPopulator decides all of that and just
/// hands this a ShopItemData + a callback).
/// </summary>
public class ShopItemView : MonoBehaviour
{
    [Header("Prefab References")]
    [Tooltip("-> Panel/ImageItemFrame/Image")]
    [SerializeField] private Image iconImage;
    [Tooltip("-> Panel/ItemName")]
    [SerializeField] private TMP_Text itemNameText;
    [Tooltip("-> Panel/ItemValue. Shows what the player receives (e.g. the NM reward for a Currency pack). Hidden automatically for categories that don't have a meaningful value to show.")]
    [SerializeField] private TMP_Text itemValueText;
    [Tooltip("-> Panel/BuyButton/Text (TMP)")]
    [SerializeField] private TMP_Text priceText;
    [Tooltip("-> Panel/BuyButton")]
    [SerializeField] private Button buyButton;

    [Header("Display Formatting")]
    [Tooltip("Prefix shown before the Normal Money reward amount for Currency items, e.g. \"+2500 NM\".")]
    [SerializeField] private string moneyRewardPrefix = "+";
    [Tooltip("Suffix shown after the Normal Money reward amount for Currency items.")]
    [SerializeField] private string moneyRewardSuffix = " NM";

    /// <summary>The ShopItemData currently bound to this view, if any.</summary>
    public ShopItemData Data { get; private set; }

    private Action<ShopItemData> onBuyClicked;

    /// <summary>
    /// Fills in the icon/name/price labels from itemData and wires the Buy
    /// button to invoke onBuyClickedCallback(itemData). Safe to call again
    /// on a reused/pooled instance - listeners are cleared first.
    /// </summary>
    public void Bind(ShopItemData itemData, Action<ShopItemData> onBuyClickedCallback)
    {
        if (itemData == null)
        {
            Debug.LogWarning("ShopItemView: Bind() called with a null ShopItemData.");
            return;
        }

        Data = itemData;
        onBuyClicked = onBuyClickedCallback;

        if (iconImage != null)
            iconImage.sprite = itemData.icon;

        if (itemNameText != null)
            itemNameText.text = itemData.displayName;

        ApplyItemValueText(itemData);

        if (priceText != null)
            priceText.text = itemData.price + " " + itemData.priceCurrencyCode;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyButtonClicked);
        }
        else
        {
            Debug.LogWarning("ShopItemView: buyButton is not assigned on prefab '" + name + "'.");
        }
    }

    private void ApplyItemValueText(ShopItemData itemData)
    {
        if (itemValueText == null)
            return;

        // Only Currency items have a meaningful "value received" to show
        // right now (the Normal Money reward). Other categories (e.g.
        // future Cosmetics) simply hide the label rather than show a
        // stale/blank value - update this switch when a cosmetic has its
        // own value to display (a rarity tier, a stat bonus, etc.).
        switch (itemData.category)
        {
            case ShopCategory.Currency:
                itemValueText.gameObject.SetActive(true);
                itemValueText.text = moneyRewardPrefix + itemData.normalMoneyReward + moneyRewardSuffix;
                break;

            default:
                itemValueText.gameObject.SetActive(false);
                break;
        }
    }

    private void HandleBuyButtonClicked()
    {
        if (Data == null)
        {
            Debug.LogWarning("ShopItemView: Buy clicked but no ShopItemData is bound.");
            return;
        }

        onBuyClicked?.Invoke(Data);
    }
}