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
    }

    void Update() {
        if (isShiftActive) {
            currentShiftTime -= Time.deltaTime;

            if (currentShiftTime <= 0) {
                currentShiftTime = 0;
                isShiftActive = false;

                // --- NEW: TRIGGER THE END OF SHIFT REPORT! ---
                if (PerformanceManager.Instance != null) {
                    PerformanceManager.Instance.ShowReport();
                }
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

                // --- NEW: LOG A FAILED ORDER! ---
                PerformanceManager.AddFailedOrder();

                activeOrders.RemoveAt(i);
            }
        }
    }

    private void SpawnOrder() {
        if (foodOptions.Count == 0 || drinkOptions.Count == 0) return;
        ItemTypeKitchen randomFood = foodOptions[Random.Range(0, foodOptions.Count)];
        ItemTypeKitchen randomDrink = drinkOptions[Random.Range(0, drinkOptions.Count)];
        LiveTicket newTicket = new LiveTicket();
        newTicket.ticketName = randomFood.ToString() + " & " + randomDrink.ToString();
        newTicket.missingItems = new List<ItemTypeKitchen> { randomFood, randomDrink };
        newTicket.completedItems = new List<ItemTypeKitchen>();
        newTicket.timeLeft = timePerOrder;
        activeOrders.Add(newTicket);
    }

    public bool TryDeliver(ItemTypeKitchen submittedItem) {
        for (int i = 0; i < activeOrders.Count; i++) {
            for (int j = 0; j < activeOrders[i].missingItems.Count; j++) {
                if (activeOrders[i].missingItems[j] == submittedItem) {
                    activeOrders[i].missingItems.RemoveAt(j);
                    activeOrders[i].completedItems.Add(submittedItem);

                    if (activeOrders[i].missingItems.Count == 0) {

                        // --- NEW: LOG A COMPLETED ORDER! ---
                        PerformanceManager.AddCompletedOrder();

                        activeOrders.RemoveAt(i);
                        if (DeliveryFeedback.Instance != null) DeliveryFeedback.Instance.ShowSuccess("Order Completed!");
                    }
                    return true;
                }
            }
        }
        if (DeliveryFeedback.Instance != null) DeliveryFeedback.Instance.ShowRejection("No matching orders!");
        return false;
    }
}