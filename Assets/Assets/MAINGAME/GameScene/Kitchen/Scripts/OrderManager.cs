using System.Collections.Generic;
using UnityEngine;

public class OrderManager : MonoBehaviour {
    public static OrderManager Instance;

    [Header("Menu & Settings")]
    public List<PlatingRecipe> menu;
    public float timePerOrder = 60f;
    public float newOrderDelay = 15f;
    public int maxActiveOrders = 4;

    [System.Serializable]
    public class LiveTicket {
        public PlatingRecipe orderedRecipe;
        public float timeLeft;
    }

    [Header("Live Orders (Watch these timers in the Inspector!)")]
    public List<LiveTicket> activeOrders = new List<LiveTicket>();

    private float spawnTimer = 0f;

    void Awake() {
        Instance = this;
    }

    void Update() {
        // 1. Spawn new orders over time
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= newOrderDelay && activeOrders.Count < maxActiveOrders) {
            SpawnOrder();
            spawnTimer = 0f;
        }

        // 2. Count down the timers for all live tickets
        for (int i = activeOrders.Count - 1; i >= 0; i--) {
            activeOrders[i].timeLeft -= Time.deltaTime;

            if (activeOrders[i].timeLeft <= 0) {
                Debug.Log("FAILED ORDER: Customer left without their " + activeOrders[i].orderedRecipe.recipeName);
                activeOrders.RemoveAt(i);
            }
        }
    }

    private void SpawnOrder() {
        if (menu.Count == 0) return; // Failsafe so it doesn't crash if menu is empty!

        PlatingRecipe randomFood = menu[Random.Range(0, menu.Count)];

        LiveTicket newTicket = new LiveTicket();
        newTicket.orderedRecipe = randomFood;
        newTicket.timeLeft = timePerOrder;

        activeOrders.Add(newTicket);
        Debug.Log("NEW ORDER ARRIVED: " + randomFood.recipeName);
    }

    public bool TryDeliver(PlatingRecipe submittedFood) {
        // Force the submitted name to lowercase and remove accidental spaces
        string submittedName = submittedFood.recipeName.Trim().ToLower();

        for (int i = 0; i < activeOrders.Count; i++) {
            // Force the ticket name to lowercase and remove accidental spaces
            string orderedName = activeOrders[i].orderedRecipe.recipeName.Trim().ToLower();

            // THE FIX: Safe string comparison!
            if (orderedName == submittedName) {

                Debug.Log("SUCCESSFUL DELIVERY! You served a " + submittedFood.recipeName);
                activeOrders.RemoveAt(i);
                return true;
            }
        }

        Debug.Log("WRONG ORDER! Nobody ordered a " + submittedFood.recipeName + " right now.");
        return false;
    }
}