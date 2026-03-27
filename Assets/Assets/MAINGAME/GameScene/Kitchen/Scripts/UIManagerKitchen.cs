using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManagerKitchen : MonoBehaviour {

    [Header("Ticket UI References")]
    public GameObject ticketPrefab;
    public Transform ticketContainer;

    // --- NEW CLOCK VARIABLE ---
    [Header("Shift Clock UI")]
    public TextMeshProUGUI shiftTimerText;

    private List<GameObject> spawnedTickets = new List<GameObject>();

    void Update() {

        // --- THE MASTER CLOCK LOGIC ---
        if (shiftTimerText != null) {
            float timeRemaining = OrderManagerKitchen.Instance.currentShiftTime;

            if (timeRemaining > 0) {
                // Convert raw seconds into standard Minutes:Seconds
                int minutes = Mathf.FloorToInt(timeRemaining / 60F);
                int seconds = Mathf.FloorToInt(timeRemaining - minutes * 60);

                // Format it nicely so "3 minutes and 5 seconds" looks like "03:05"
                shiftTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

                // Turn the clock RED if there are 30 seconds or less left!
                if (timeRemaining <= 30f) {
                    shiftTimerText.color = Color.red;
                } else {
                    shiftTimerText.color = Color.white; // Default color
                }
            } else {
                shiftTimerText.text = "CLOSED";
                shiftTimerText.color = Color.red;
            }
        }
        // ------------------------------

        // --- EXISTING TICKET LOGIC (Untouched so Combos still work!) ---
        if (spawnedTickets.Count != OrderManagerKitchen.Instance.activeOrders.Count) {
            RebuildTicketUI();
        }

        for (int i = 0; i < spawnedTickets.Count; i++) {

            var orderData = OrderManagerKitchen.Instance.activeOrders[i];
            TextMeshProUGUI[] texts = spawnedTickets[i].GetComponentsInChildren<TextMeshProUGUI>();

            foreach (var textItem in texts) {
                if (textItem.gameObject.name == "RecipeNameText") {

                    string missingListText = "";
                    foreach (var item in orderData.missingItems) {
                        missingListText += "\n+ " + item.ToString();
                    }

                    textItem.text = orderData.ticketName + "\n<size=60%>" + missingListText + "</size>";
                } else if (textItem.gameObject.name == "TimerText") {
                    textItem.text = Mathf.CeilToInt(orderData.timeLeft).ToString() + "s";
                }
            }
        }
    }

    private void RebuildTicketUI() {
        foreach (GameObject ticket in spawnedTickets) {
            Destroy(ticket);
        }
        spawnedTickets.Clear();

        for (int i = 0; i < OrderManagerKitchen.Instance.activeOrders.Count; i++) {
            GameObject newTicket = Instantiate(ticketPrefab, ticketContainer);
            spawnedTickets.Add(newTicket);
        }
    }
}