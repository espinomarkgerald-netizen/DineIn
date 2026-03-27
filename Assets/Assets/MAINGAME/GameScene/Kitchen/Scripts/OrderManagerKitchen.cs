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
        // Start the shift clock exactly when the game begins!
        currentShiftTime = shiftDuration;
        isShiftActive = true;
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
        newTicket.timeLeft = timePerOrder;

        activeOrders.Add(newTicket);
        Debug.Log("NEW ORDER ARRIVED: " + newTicket.ticketName);
    }

    public bool TryDeliver(ItemTypeKitchen submittedItem) {
        for (int i = 0; i < activeOrders.Count; i++) {
            for (int j = 0; j < activeOrders[i].missingItems.Count; j++) {

                if (activeOrders[i].missingItems[j] == submittedItem) {
                    Debug.Log("GOOD DROP OFF! Delivered a " + submittedItem.ToString());
                    activeOrders[i].missingItems.RemoveAt(j);

                    if (activeOrders[i].missingItems.Count == 0) {
                        Debug.Log("COMBO COMPLETE!");
                        activeOrders.RemoveAt(i);
                    }
                    return true;
                }
            }
        }

        Debug.Log("REJECTED! Nobody is waiting for a " + submittedItem.ToString());

        // --- WIRED THE FEEDBACK BUBBLE HERE! ---
        if (DeliveryFeedback.Instance != null) {
            DeliveryFeedback.Instance.ShowRejection("No matching orders!");
        }

        return false;
    }
}