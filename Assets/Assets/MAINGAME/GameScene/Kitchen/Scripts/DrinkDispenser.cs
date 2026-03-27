using UnityEngine;
using UnityEngine.InputSystem; // Needed to track the mouse/screen taps
using UnityEngine.EventSystems; // Needed to check if the mouse is touching the UI

public class DrinkDispenser : Counter {

    [Header("UI Setup")]
    public GameObject drinkMenuPanel;

    [Header("Drink Prefabs")]
    public GameObject cokePrefab;
    public GameObject pineapplePrefab;
    public GameObject icedTeaPrefab;

    private PlayerHolding interactingPlayer;
    private float openTimer = 0f; // Protects from same-frame accidental closing

    void Start() {
        if (drinkMenuPanel != null) {
            drinkMenuPanel.SetActive(false);
        }
    }

    void Update() {
        // 1. Only run this background check if the menu is actively open on the screen
        if (drinkMenuPanel != null && drinkMenuPanel.activeSelf) {

            openTimer += Time.deltaTime;

            // Wait a tiny fraction of a second before allowing click-to-close 
            // so the click that opened the menu doesn't instantly trigger a close!
            if (openTimer > 0.1f) {
                bool clickedOutside = false;

                // Check Mobile Tap
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue())) {
                        clickedOutside = true;
                    }
                }
                // Check PC Click
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject()) {
                        clickedOutside = true;
                    }
                }

                // If they clicked the 3D world (not the UI), close the menu!
                if (clickedOutside) {
                    CloseMenu();
                }
            }
        } else {
            openTimer = 0f; // Reset the safety timer when the menu is closed
        }
    }

    public override void Interact(PlayerHolding player) {
        if (player.heldObject != null) {

            string cleanName = player.heldObject.name.Replace(" ", "").ToLower();

            if (cleanName.Contains("emptycup")) {
                interactingPlayer = player;
                drinkMenuPanel.SetActive(true);
                openTimer = 0f; // Start the safety timer!
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

    // We renamed this to CloseMenu since it happens automatically now!
    public void CloseMenu() {
        if (drinkMenuPanel != null) {
            drinkMenuPanel.SetActive(false);
        }
        interactingPlayer = null;
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