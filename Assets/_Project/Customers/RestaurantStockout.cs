using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shared menu/stock policy. Queries do not reserve ingredients or create orders.
public static class RestaurantStockout
{
    public const float SpawnRate = 1f / 3f, WaitSeconds = 30f, PatientChance = 0.5f;
    private static InventoryManager inventory;
    private static MenuAvailabilityManager menu;
    private static float nextCheck;
    private static int shortage, scene, day;
    private static bool warned;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        if (!ReferenceEquals(inventory, null)) inventory.OnStockChanged -= StockChanged;
        if (!ReferenceEquals(menu, null)) menu.MenuChanged -= Invalidate;
        inventory = null; menu = null; nextCheck = -1f; shortage = scene = day = 0; warned = false;
    }
    private static void Invalidate() => nextCheck = -1f;
    private static void StockChanged(ItemType item, int count) => Invalidate();

    public static int Shortage
    {
        get
        {
            int currentScene = SceneManager.GetActiveScene().handle;
            int currentDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
            if (scene != currentScene || day != currentDay)
            { scene = currentScene; day = currentDay; warned = false; Invalidate(); }
            if (!ReferenceEquals(inventory, InventoryManager.Instance))
            {
                if (!ReferenceEquals(inventory, null)) inventory.OnStockChanged -= StockChanged;
                inventory = InventoryManager.Instance;
                if (inventory != null) inventory.OnStockChanged += StockChanged;
                Invalidate();
            }
            if (!ReferenceEquals(menu, MenuAvailabilityManager.Instance))
            {
                if (!ReferenceEquals(menu, null)) menu.MenuChanged -= Invalidate;
                menu = MenuAvailabilityManager.Instance;
                if (menu != null) menu.MenuChanged += Invalidate;
                Invalidate();
            }
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                shortage = Evaluate();
                if (shortage == 0) warned = false;
            }
            return shortage;
        }
    }

    private static int Evaluate()
    {
        var catalog = MenuCatalog.Default;
        var stock = LobbyStockBridge.Instance;
        if (catalog == null || stock == null) return 0; // Unconfigured/scripted scenes keep their normal flow.
        var meals = new List<List<Recipe>>();
        foreach (var food in catalog.GetProducts(MenuProductCategory.Food))
            meals.Add(new List<Recipe> { food });
        foreach (var bundle in catalog.GetFoodBundles()) meals.Add(new List<Recipe>(bundle.products));
        var drinks = catalog.GetProducts(MenuProductCategory.Drink);
        bool requiresDrink = catalog.GetProducts(MenuProductCategory.Drink, false).Count > 0;
        bool hasFood = false;
        foreach (var meal in meals)
        {
            if (!stock.HasOrderStock(meal)) continue;
            hasFood = true;
            if (!requiresDrink) return 0;
            foreach (var drink in drinks)
            {
                meal.Add(drink);
                bool available = stock.HasOrderStock(meal);
                meal.RemoveAt(meal.Count - 1);
                if (available) return 0;
            }
        }
        return hasFood ? 2 : 1;
    }

    public static void ShowLocalWarning(string message)
    {
        _ = Shortage;
        if (warned || WarningSlideUI.Instance == null) return;
        // Partial-order shortages retain existing feedback; global shortages are episode-deduplicated.
        warned = shortage != 0;
        WarningSlideUI.Instance.Show(message);
    }
}
