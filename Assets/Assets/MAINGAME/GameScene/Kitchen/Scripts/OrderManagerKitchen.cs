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

    void Awake() { Instance = this; }

    void Start() {
        currentShiftTime = shiftDuration;
        isShiftActive = true;
        Time.timeScale = 1f; // Make sure time isn't frozen from the last shift!

        // Spawn one order immediately so the board is never empty at game start.
        SpawnOrder();
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
        List<ItemTypeKitchen> unlockedFood = GetUnlockedItems(foodOptions);
        List<ItemTypeKitchen> unlockedDrinks = GetUnlockedItems(drinkOptions);

        if (unlockedFood.Count == 0 || unlockedDrinks.Count == 0) {
            Debug.LogWarning("[OrderManager] No unlocked food or drink recipes available to spawn an order.");
            return;
        }

        ItemTypeKitchen randomFood = unlockedFood[Random.Range(0, unlockedFood.Count)];
        ItemTypeKitchen randomDrink = unlockedDrinks[Random.Range(0, unlockedDrinks.Count)];
        LiveTicket newTicket = new LiveTicket();
        newTicket.ticketName = randomFood.ToString() + " & " + randomDrink.ToString();
        newTicket.missingItems = new List<ItemTypeKitchen> { randomFood, randomDrink };
        newTicket.completedItems = new List<ItemTypeKitchen>();
        newTicket.timeLeft = timePerOrder;
        activeOrders.Add(newTicket);
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
                Debug.Log($"[OrderManager] Delivered {item} for '{ticket.ticketName}'. Remaining: {ticket.missingItems.Count}");

                if (ticket.missingItems.Count == 0) {
                    int revenue = GetOrderRevenue(ticket.ticketName);
                    Debug.Log($"[OrderManager] ORDER COMPLETE: '{ticket.ticketName}' — +₱{revenue}");
                    DailyRevenueTracker.Instance?.RecordOrderCompleted();
                    DailyFinanceBridge.Instance?.AddEarnings(revenue, "Kitchen Order");
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
    /// Looks up the combined sell price of all items in the order ticket name
    /// by matching each token against Recipe.recipeName.
    /// </summary>
    private int GetOrderRevenue(string ticketName) {
        IReadOnlyList<Recipe> recipes = RecipeManager.AllRecipesStatic;
        if (recipes == null || recipes.Count == 0) return 0;

        int total = 0;
        string[] tokens = ticketName.Split('&');
        foreach (string token in tokens) {
            string clean = token.Trim();
            foreach (Recipe recipe in recipes) {
                if (string.Equals(recipe.recipeName.Replace(" ", ""), clean, System.StringComparison.OrdinalIgnoreCase)) {
                    total += recipe.sellPrice;
                    break;
                }
            }
        }
        return total;
    }
}