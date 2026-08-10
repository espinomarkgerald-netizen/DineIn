/// <summary>
/// Which section of the shop a ShopItemData belongs in. ShopPanelPopulator
/// uses this to sort items into their category container, and to decide
/// which purchase manager (CurrencyExchangeManager, or a future
/// CosmeticPurchaseManager) should handle a Buy click.
/// </summary>
public enum ShopCategory
{
    Currency,
    Cosmetic
}