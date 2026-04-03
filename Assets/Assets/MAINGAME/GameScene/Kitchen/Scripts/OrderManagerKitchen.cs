using System.Collections.Generic;
using UnityEngine;

public class OrderManagerKitchen : MonoBehaviour {
    public static OrderManagerKitchen Instance;

    // --- THE NEW SHIFT TIMER ---
    [Header("Shift Settings")]
    [Tooltip("Total shift time in seconds. 180 = 3 minutes.")]
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
        public float timeLeft;
    }

    [Header("Live Orders")]
    public List<LiveTicket> activeOrders = new List<LiveTicket>();

    private float spawnTimer = 0f;

    void Awake() { Instance = this; }

    void Start() {
        currentShiftTime = shiftDuration;
        isShiftActive = true;

        // Spawn one order immediately so the board is never empty at game start.
        SpawnOrder();
    }

    void Update() {
        // 1. RUN THE MASTER SHIFT CLOCK
        if (isShiftActive) {
            currentShiftTime -= Time.deltaTime;

            // Did we run out of time?
            if (currentShiftTime <= 0) {
                currentShiftTime = 0;
                isShiftActive = false;
                Debug.Log("SHIFT OVER! The restaurant is closed. No new orders!");
                EndShift();
            }

            // 2. ONLY SPAWN NEW TICKETS IF THE SHIFT IS STILL ACTIVE
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= newOrderDelay && activeOrders.Count < maxActiveOrders) {
                SpawnOrder();
                spawnTimer = 0f;
            }
        }

        // 3. EXISTING TICKETS KEEP TICKING DOWN (You still have to finish the board!)
        for (int i = activeOrders.Count - 1; i >= 0; i--) {
            activeOrders[i].timeLeft -= Time.deltaTime;
            if (activeOrders[i].timeLeft <= 0) {
                Debug.Log("FAILED ORDER: Customer left waiting for " + activeOrders[i].ticketName);
                activeOrders.RemoveAt(i);
                DailyRevenueTracker.Instance?.RecordOrderFailed();
            }
        }
    }

    private void EndShift()
    {
        GameFlowManager.Instance?.EndOfDayFinance();

        if (DailyReportUI.Instance != null)
        {
            DailyReportUI.Instance.Show();
        }
        else
        {
            Debug.LogWarning("DailyReportUI not found.");
        }
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
        newTicket.timeLeft = timePerOrder;

        activeOrders.Add(newTicket);
        Debug.Log("NEW ORDER ARRIVED: " + newTicket.ticketName);
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
                    Debug.Log($"[OrderManager] ORDER COMPLETE: '{ticket.ticketName}' — +${revenue}");
                    DailyRevenueTracker.Instance?.RecordOrderCompleted();
                    DailyRevenueTracker.Instance?.RecordRevenue(revenue);
                    activeOrders.Remove(ticket);
                }
                return true;
            }
        }

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