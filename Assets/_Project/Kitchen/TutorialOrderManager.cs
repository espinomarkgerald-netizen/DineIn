using UnityEngine;
using System.Collections.Generic;

public class TutorialOrderManager : MonoBehaviour {
    public static TutorialOrderManager Instance;

    // A simplified fake ticket just for the tutorial
    [System.Serializable]
    public class TutorialTicket {
        public string ticketName;
        public List<ItemTypeKitchen> missingItems;
    }

    public List<TutorialTicket> activeOrders = new List<TutorialTicket>();

    void Awake() {
        Instance = this;
    }

    // The TutorialManager will call this to force a ticket to appear!
    public void SpawnSpecificOrder(ItemTypeKitchen foodTarget) {
        TutorialTicket newTicket = new TutorialTicket();
        newTicket.ticketName = "Tutorial " + foodTarget.ToString();
        newTicket.missingItems = new List<ItemTypeKitchen> { foodTarget };

        activeOrders.Add(newTicket);
        Debug.Log("Tutorial Order Spawned: " + foodTarget.ToString());
    }

    // Call this to wipe the board clean
    public void ClearOrders() {
        activeOrders.Clear();
    }
}