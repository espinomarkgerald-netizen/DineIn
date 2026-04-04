using UnityEngine;

public class DrinkDispenser : Counter {

    // --- NEW: Global reference so the player movement knows this is open! ---
    public static DrinkDispenser activeDispenser;

    [Header("UI Setup")]
    public GameObject drinkMenuPanel;

    [Header("Drink Prefabs")]
    public GameObject cokePrefab;
    public GameObject pineapplePrefab;
    public GameObject icedTeaPrefab;

    private PlayerHolding interactingPlayer;

    void Start() {
        if (drinkMenuPanel != null) {
            drinkMenuPanel.SetActive(false);
        }
    }

    public override void Interact(PlayerHolding player) {
        if (player.heldObject != null) {

            string cleanName = player.heldObject.name.Replace(" ", "").ToLower();

            if (cleanName.Contains("emptycup")) {
                interactingPlayer = player;
                drinkMenuPanel.SetActive(true);

                // Tell the game this specific dispenser is currently open
                activeDispenser = this;

                Debug.Log("Drink Dispenser Menu Opened!");
                return;
            }
        }

        string holdingName = player.heldObject != null ? player.heldObject.name : "Nothing";
        Debug.Log("You need an Empty Cup to use the dispenser! You are holding: " + holdingName);
    }

    // --- BUTTON TRIGGERS ---

    public void Button_SelectCoke() { FillCup(cokePrefab); }
    public void Button_SelectPineapple() { FillCup(pineapplePrefab); }
    public void Button_SelectIcedTea() { FillCup(icedTeaPrefab); }

    public void CloseMenu() {
        if (drinkMenuPanel != null) {
            drinkMenuPanel.SetActive(false);
        }
        interactingPlayer = null;

        // Clear the global reference when closed
        if (activeDispenser == this) {
            activeDispenser = null;
        }
    }

    // --- FILL LOGIC ---

    private void FillCup(GameObject filledDrinkPrefab) {
        if (interactingPlayer == null || interactingPlayer.heldObject == null) return;

        Destroy(interactingPlayer.heldObject);

        GameObject newDrink = Instantiate(filledDrinkPrefab);
        newDrink.name = filledDrinkPrefab.name;

        interactingPlayer.PickUp(newDrink);

        CloseMenu();

        Debug.Log("Cup filled with " + newDrink.name + "!");
    }
}