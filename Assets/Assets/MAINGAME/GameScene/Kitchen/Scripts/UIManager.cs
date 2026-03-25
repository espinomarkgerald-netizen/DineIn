using System.Collections.Generic;
using UnityEngine;
using TMPro; // We need this to talk to TextMeshPro!

public class UIManager : MonoBehaviour {

    [Header("UI References")]
    public GameObject ticketPrefab; // Drag your OrderTicket prefab here
    public Transform ticketContainer; // Drag your TicketContainer panel here

    private List<GameObject> spawnedTickets = new List<GameObject>();

    void Update() {
        // 1. If the number of UI tickets doesn't match the actual orders, rebuild the board!
        if (spawnedTickets.Count != OrderManager.Instance.activeOrders.Count) {
            RebuildTicketUI();
        }

        // 2. Continually update the text on the tickets that are currently on screen
        for (int i = 0; i < spawnedTickets.Count; i++) {

            // Grab the real order data from the OrderManager
            var orderData = OrderManager.Instance.activeOrders[i];

            // Search the UI ticket for our specific Text objects
            TextMeshProUGUI[] texts = spawnedTickets[i].GetComponentsInChildren<TextMeshProUGUI>();

            foreach (var textItem in texts) {
                if (textItem.gameObject.name == "RecipeNameText") {
                    textItem.text = orderData.orderedRecipe.recipeName;
                } else if (textItem.gameObject.name == "TimerText") {
                    // Mathf.CeilToInt rounds the timer up so it doesn't show crazy decimals!
                    textItem.text = Mathf.CeilToInt(orderData.timeLeft).ToString() + "s";
                }
            }
        }
    }

    private void RebuildTicketUI() {
        // Wipe the board clean
        foreach (GameObject ticket in spawnedTickets) {
            Destroy(ticket);
        }
        spawnedTickets.Clear();

        // Spawn a brand new ticket for every active order
        for (int i = 0; i < OrderManager.Instance.activeOrders.Count; i++) {
            GameObject newTicket = Instantiate(ticketPrefab, ticketContainer);
            spawnedTickets.Add(newTicket);
        }
    }
}