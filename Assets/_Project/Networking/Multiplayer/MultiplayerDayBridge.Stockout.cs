using System.Collections.Generic;
using UnityEngine;

public sealed partial class MultiplayerDayBridge
{
    private bool stockoutActive;
    private int stockoutEpisode, observedStockoutEpisode, stockoutDay;
    private string stockoutMessage;
    private List<Recipe> stockoutProducts;
    private float nextStockoutCheck;

    public static string ReportMissingStock(CustomerGroup group)
    {
        int shortage = MenuShortage();
        string thought = shortage == 1 ? "There are no foods available to order."
            : shortage == 2 ? "There are no drinks available to order."
            : "There aren't enough ingredients for our order.";
        string warning = shortage == 1 ? "RESTOCK NEEDED: No food can be prepared. Restock ingredients or enable stocked meals."
            : shortage == 2 ? "RESTOCK NEEDED: No drinks can be prepared. Restock drink ingredients."
            : "RESTOCK NEEDED: Insufficient ingredients for a customer's order. Check ingredient stocks.";
        if (!IsActive) { WarningSlideUI.Instance?.Show(warning); return thought; }
        var bridge = MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>();
        if (bridge == null || !bridge.session.IsAuthority) return thought;
        // Reevaluate before accepting a new incident, including stock recovered
        // and consumed again between periodic checks.
        bridge.TickStockout(true);
        if (!bridge.stockoutActive)
        {
            bridge.stockoutActive = true;
            bridge.stockoutEpisode++;
            bridge.stockoutDay = bridge.CurrentDay;
            bridge.stockoutMessage = warning;
            bridge.stockoutProducts = shortage == 0 && group?.currentOrder != null
                ? new List<Recipe>(group.currentOrder.ResolveProducts()) : null;
            if (shortage == 0 && bridge.stockoutProducts?.Count == 0 && group.LastUnavailableOrderProducts != null)
                bridge.stockoutProducts = new List<Recipe>(group.LastUnavailableOrderProducts);
            bridge.ShowStockoutNotice();
            bridge.PublishNow();
        }
        return thought;
    }

    // Match normal order generation: enabled/unlocked food or bundles and, if
    // the menu serves drinks, at least one stocked drink. No alternate stock math.
    internal static int MenuShortage()
    {
        var catalog = MenuCatalog.Default;
        if (catalog == null) return 1;
        var stock = LobbyStockBridge.Instance;
        bool food = false;
        foreach (var meal in catalog.GetProducts(MenuProductCategory.Food))
            if (stock == null || stock.HasProductStock(meal)) { food = true; break; }
        if (!food) foreach (var bundle in catalog.GetFoodBundles())
            if (stock == null || stock.HasOrderStock(bundle.products)) { food = true; break; }
        if (!food) return 1;
        if (catalog.GetProducts(MenuProductCategory.Drink, false).Count == 0) return 0;
        foreach (var drink in catalog.GetProducts(MenuProductCategory.Drink))
            if (stock == null || stock.HasProductStock(drink)) return 0;
        return 2;
    }

    private void TickStockout(bool force = false)
    {
        if (!session.IsAuthority) return;
        if (!force && Time.unscaledTime < nextStockoutCheck) return;
        nextStockoutCheck = Time.unscaledTime + 0.5f;
        if (stockoutActive && (stockoutDay != CurrentDay || MenuShortage() == 0
            && (stockoutProducts == null || stockoutProducts.Count == 0 || LobbyStockBridge.Instance == null
                || LobbyStockBridge.Instance.HasOrderStock(stockoutProducts))))
        { stockoutActive = false; stockoutProducts = null; PublishNow(); }
        ShowStockoutNotice();
    }
    private void ShowStockoutNotice()
    {
        if (!stockoutActive || stockoutEpisode <= observedStockoutEpisode
            || string.IsNullOrEmpty(stockoutMessage) || WarningSlideUI.Instance == null) return;
        observedStockoutEpisode = stockoutEpisode;
        WarningSlideUI.Instance.Show(stockoutMessage);
    }
}
