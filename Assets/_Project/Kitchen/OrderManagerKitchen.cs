using System.Collections.Generic;
using UnityEngine;

public class OrderManagerKitchen : MonoBehaviour {
    public static OrderManagerKitchen Instance;

    [Header("Shift Settings")]
    public float shiftDuration = 180f;
    public bool isShiftActive = false;
    public float currentShiftTime;

    [Header("Menu Pools")]
    public List<ItemTypeKitchen> foodOptions;
    public List<ItemTypeKitchen> drinkOptions;

    [Header("Settings")]
    public float timePerOrder = 90f;
    public float newOrderDelay = 15f;
    public int maxActiveOrders = 4;

    [Header("Spawn Difficulty Scaling")]
    [Tooltip("The day number treated as the difficulty ceiling (scales from Day 1 up to this day).")]
    [SerializeField] private int maxScalingDay = 20;

    [Tooltip("X = normalized day (0–1). Y = max simultaneous active tickets.")]
    [SerializeField] private AnimationCurve maxActiveOrdersCurve = new AnimationCurve(
        new Keyframe(0f, 2f),
        new Keyframe(0.25f, 3f),
        new Keyframe(0.6f, 4f),
        new Keyframe(1f, 5f));

    [Tooltip("X = normalized day (0–1). Y = seconds between new ticket spawns.")]
    [SerializeField] private AnimationCurve newOrderDelayCurve = new AnimationCurve(
        new Keyframe(0f, 30f),
        new Keyframe(0.25f, 22f),
        new Keyframe(0.6f, 15f),
        new Keyframe(1f, 8f));

    [Tooltip("X = normalized day (0–1). Y = seconds a ticket stays alive before failing.")]
    [SerializeField] private AnimationCurve timePerOrderCurve = new AnimationCurve(
        new Keyframe(0f, 120f),
        new Keyframe(0.25f, 105f),
        new Keyframe(0.6f, 90f),
        new Keyframe(1f, 70f));

    [System.Serializable]
    public class LiveTicket {
        public string ticketName;
        public List<ItemTypeKitchen> missingItems;
        public List<ItemTypeKitchen> completedItems;
        public float timeLeft;
    }

    [Header("Live Orders")]
    public List<LiveTicket> activeOrders = new List<LiveTicket>();
    private float spawnTimer = 0f;

    private Dictionary<ItemTypeKitchen, int> priceMap;

    void Awake() { Instance = this; }

    void Start() {
        BuildPriceMap();
        ApplyDifficultyScaling();

        currentShiftTime = shiftDuration;
        isShiftActive = true;
        Time.timeScale = 1f;

        SpawnOrder();
    }

    /// <summary>
    /// Reads the current day from GameFlowManager and evaluates each AnimationCurve
    /// to scale ticket spawn settings before the shift starts.
    /// </summary>
    private void ApplyDifficultyScaling() {
        if (GameFlowManager.Instance == null || maxScalingDay <= 1) return;

        int day = GameFlowManager.Instance.CurrentDay;
        float t = Mathf.Clamp01((float)(day - 1) / (maxScalingDay - 1));

        maxActiveOrders = Mathf.Max(1, Mathf.RoundToInt(maxActiveOrdersCurve.Evaluate(t)));
        newOrderDelay   = Mathf.Max(1f, newOrderDelayCurve.Evaluate(t));
        timePerOrder    = Mathf.Max(10f, timePerOrderCurve.Evaluate(t));

        Debug.Log($"[OrderManagerKitchen] Day {day} (t={t:F2}) — " +
                  $"maxActiveOrders={maxActiveOrders}, newOrderDelay={newOrderDelay:F1}s, timePerOrder={timePerOrder:F1}s");
    }

    private bool pendingReport = false;

    void Update() {
        if (isShiftActive) {
            currentShiftTime -= Time.deltaTime;

            if (currentShiftTime <= 0) {
                currentShiftTime = 0;
                isShiftActive = false;
                OnShiftTimerExpired();
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= newOrderDelay && activeOrders.Count < maxActiveOrders) {
                SpawnOrder();
                spawnTimer = 0f;
            }
        }

        for (int i = activeOrders.Count - 1; i >= 0; i--) {
            activeOrders[i].timeLeft -= Time.deltaTime;
            if (activeOrders[i].timeLeft <= 0) {
                PerformanceManager.AddFailedOrder();
                AlienApprovalManager.Instance?.RegisterGroupResult(CustomerGroup.FinalResult.Angry);
                activeOrders.RemoveAt(i);
                DailyRevenueTracker.Instance?.RecordOrderFailed();
            }
        }

        if (pendingReport && activeOrders.Count == 0)
            ShowDailyReport();
    }

    /// <summary>Called once when the shift timer reaches zero. Triggers the report
    /// immediately if the board is already clear, otherwise waits for remaining tickets.</summary>
    private void OnShiftTimerExpired() {
        GameFlowManager.Instance?.EndOfDayFinance();

        if (activeOrders.Count == 0)
            ShowDailyReport();
        else
            pendingReport = true;
    }

    private void ShowDailyReport() {
        pendingReport = false;

        if (DailyReportUI.Instance != null)
            DailyReportUI.Instance.Show();
        else
            Debug.LogWarning("[OrderManagerKitchen] DailyReportUI not found.");
    }

    private void SpawnOrder() {
        List<ItemTypeKitchen> unlockedFood = GetMenuItems(MenuProductCategory.Food, foodOptions);
        List<ItemTypeKitchen> unlockedDrinks = GetMenuItems(MenuProductCategory.Drink, drinkOptions);

        if (unlockedFood.Count == 0 || unlockedDrinks.Count == 0) {
            Debug.LogWarning("[OrderManager] No unlocked food or drink recipes available to spawn an order.");
            return;
        }

        ItemTypeKitchen randomFood = unlockedFood[Random.Range(0, unlockedFood.Count)];
        ItemTypeKitchen randomDrink = unlockedDrinks[Random.Range(0, unlockedDrinks.Count)];
        MenuCatalog catalog = MenuCatalog.Default;
        Recipe foodProduct = catalog != null ? catalog.FindByKitchenItem(randomFood) : null;
        Recipe drinkProduct = catalog != null ? catalog.FindByKitchenItem(randomDrink) : null;
        LiveTicket newTicket = new LiveTicket();
        string foodName = foodProduct != null ? foodProduct.DisplayName : randomFood.ToString();
        string drinkName = drinkProduct != null ? drinkProduct.DisplayName : randomDrink.ToString();
        newTicket.ticketName = foodName + " & " + drinkName;
        newTicket.missingItems = new List<ItemTypeKitchen> { randomFood, randomDrink };
        newTicket.completedItems = new List<ItemTypeKitchen>();
        newTicket.timeLeft = timePerOrder;
        activeOrders.Add(newTicket);
    }

    private List<ItemTypeKitchen> GetMenuItems(
        MenuProductCategory category,
        List<ItemTypeKitchen> legacyPool) {
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
            return GetUnlockedItems(legacyPool);

        List<ItemTypeKitchen> result = new List<ItemTypeKitchen>();
        List<Recipe> menuProducts = catalog.GetProducts(category);
        foreach (Recipe product in menuProducts) {
            if (product.kitchenItemType != ItemTypeKitchen.None &&
                !result.Contains(product.kitchenItemType))
                result.Add(product.kitchenItemType);
        }

        return result;
    }

    /// <summary>Filters a pool of kitchen items to only those with an unlocked recipe.
    /// When UnlockManager is absent (direct scene launch) the full pool is returned.
    /// No string-name matching — UnlockManager tracks ItemTypeKitchen directly.</summary>
    private List<ItemTypeKitchen> GetUnlockedItems(List<ItemTypeKitchen> pool) {
        if (UnlockManager.Instance == null)
            return new List<ItemTypeKitchen>(pool);

        List<ItemTypeKitchen> unlocked = new List<ItemTypeKitchen>();
        foreach (ItemTypeKitchen item in pool) {
            if (UnlockManager.Instance.IsKitchenItemUnlocked(item))
                unlocked.Add(item);
        }

        // If nothing is unlocked yet (e.g. direct kitchen scene launch without Office),
        // fall back to the full pool so the shift is never broken.
        return unlocked.Count > 0 ? unlocked : new List<ItemTypeKitchen>(pool);
    }

    /// <summary>
    /// Attempts to fulfill an item in the oldest matching active order.
    /// Returns true if the item was accepted, false if no order needs it.
    /// </summary>
    public bool TryDeliver(ItemTypeKitchen item) {
        foreach (LiveTicket ticket in activeOrders) {
            if (ticket.missingItems.Contains(item)) {
                ticket.missingItems.Remove(item);
                ticket.completedItems.Add(item);
                Debug.Log($"[OrderManager] Delivered {item} for '{ticket.ticketName}'. Remaining: {ticket.missingItems.Count}");

                if (ticket.missingItems.Count == 0) {
                    int revenue = GetOrderRevenue(ticket.completedItems);
                    Debug.Log($"[OrderManager] ORDER COMPLETE: '{ticket.ticketName}' — +₱{revenue}");
                    DailyRevenueTracker.Instance?.RecordOrderCompleted();
                    DailyFinanceBridge.Instance?.AddEarnings(revenue, "Kitchen Order");
                    AlienApprovalManager.Instance?.RegisterGroupResult(CustomerGroup.FinalResult.Happy);
                    activeOrders.Remove(ticket);
                    if (DeliveryFeedback.Instance != null) DeliveryFeedback.Instance.ShowSuccess("Order Completed!");
                }
                return true;
            }
        }

        if (DeliveryFeedback.Instance != null) DeliveryFeedback.Instance.ShowRejection("No matching orders!");
        Debug.Log($"[OrderManager] No active order needs '{item}'.");
        return false;
    }

    /// <summary>
    /// Reads prices from the shared MenuCatalog at shift start.
    /// </summary>
    private void BuildPriceMap() {
        priceMap = new Dictionary<ItemTypeKitchen, int>();

        MenuCatalog catalog = MenuCatalog.Default;
        IReadOnlyList<Recipe> recipes = catalog != null
            ? catalog.Products
            : RecipeManager.AllRecipesStatic;
        if (recipes == null || recipes.Count == 0) {
            Debug.LogError("[OrderManagerKitchen] MenuCatalog is unavailable; kitchen orders have no price data.");
            return;
        }

        foreach (Recipe recipe in recipes) {
            if (recipe.kitchenItemType != ItemTypeKitchen.None)
                priceMap[recipe.kitchenItemType] = recipe.EffectiveSellPrice;
        }

        Debug.Log("[OrderManagerKitchen] Price map built from MenuCatalog.");
    }

    /// <summary>
    /// Sums the sell price of each ItemTypeKitchen on the completed ticket
    /// using the price map built at shift start.
    /// </summary>
    private int GetOrderRevenue(List<ItemTypeKitchen> items) {
        int total = 0;
        foreach (ItemTypeKitchen item in items) {
            if (priceMap.TryGetValue(item, out int price))
                total += price;
            else
                Debug.LogWarning($"[OrderManagerKitchen] No price for {item} — item not counted.");
        }
        return total;
    }
}
