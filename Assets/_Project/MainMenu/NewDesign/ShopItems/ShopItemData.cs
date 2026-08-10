using UnityEngine;

/// <summary>
/// Reusable data definition for a single shop item - a Gold Coin/Normal
/// Money exchange package today, a cosmetic tomorrow, without needing a new
/// prefab or a new UI script per category.
///
/// Single Responsibility: this asset only describes an item. It knows
/// nothing about UI (ShopItemView reads it, it never reads back), and
/// nothing about how a purchase is executed (CurrencyExchangeManager /
/// a future CosmeticPurchaseManager own that). One class is used for every
/// category rather than a subclass per category - category-specific fields
/// (normalMoneyReward, cosmeticUnlockId) simply go unused outside their own
/// category, which is easier to author in the Inspector than an inheritance
/// hierarchy and easy to extend later.
///
/// Create instances via Assets > Create > Shop > Shop Item. Suggested
/// location: Assets/MainMenu/Data/ShopItems/.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Shop Item", fileName = "NewShopItem")]
public class ShopItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique, stable id for this item (e.g. \"currency_small_pack\"). Hand-typed so it stays stable across renames/reordering.")]
    public string itemId;
    [Tooltip("Shown as the ItemName label on the ShopItem prefab.")]
    public string displayName;
    [Tooltip("Shown in the ShopItem prefab's icon image.")]
    public Sprite icon;

    [Header("Category")]
    [Tooltip("Which shop section this item appears under, and which purchase manager handles buying it.")]
    public ShopCategory category = ShopCategory.Currency;

    [Header("Cost")]
    [Tooltip("Amount charged when this item is purchased.")]
    public int price;
    [Tooltip("Which wallet currency is spent - matches PlayFabWalletManager's currency codes, e.g. \"GC\" or \"NM\".")]
    public string priceCurrencyCode = "GC";

    [Header("Currency-Exchange-Specific (used only when category == Currency)")]
    [Tooltip("Normal Money granted when this pack is bought. Ignored for Cosmetic items.")]
    public int normalMoneyReward;

    [Header("Cosmetic-Specific (used only when category == Cosmetic) - reserved for future use")]
    [Tooltip("Id a future cosmetic/inventory system would check or grant. Unused for Currency items.")]
    public string cosmeticUnlockId;
}