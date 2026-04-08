using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class DrinkDispenser : Counter {

    // --- Global reference so the player movement knows this is open! ---
    public static DrinkDispenser activeDispenser;

    [Header("UI Setup")]
    public GameObject drinkMenuPanel;

    // --- NEW: Slot for the White Circle Popup! ---
    public GameObject popupCanvas;

    [Header("Drink Prefabs")]
    public GameObject cokePrefab;
    public GameObject pineapplePrefab;
    public GameObject icedTeaPrefab;

    private PlayerHolding interactingPlayer;
    private float openTimer = 0f;

    void Start() {
        if (drinkMenuPanel != null)
            drinkMenuPanel.SetActive(false);

        // Make sure the popup is visible when the game starts!
        if (popupCanvas != null)
            popupCanvas.SetActive(true);
    }

    void Update() {
        if (drinkMenuPanel != null && drinkMenuPanel.activeSelf) {
            openTimer += Time.deltaTime;

            if (openTimer > 0.1f) {
                bool clickedOutside = false;

                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue()))
                        clickedOutside = true;
                } else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                        clickedOutside = true;
                }

                if (clickedOutside)
                    CloseMenu();
            }
        } else {
            openTimer = 0f;
        }
    }

    public override void Interact(PlayerHolding player) {
        if (player.heldObject != null) {
            string cleanName = player.heldObject.name.Replace(" ", "").ToLower();
            if (cleanName.Contains("emptycup")) {
                interactingPlayer = player;

                // --- NEW: Hide the white circle, show the menu! ---
                if (popupCanvas != null) popupCanvas.SetActive(false);
                drinkMenuPanel.SetActive(true);

                openTimer = 0f;

                // Tell the game this specific dispenser is currently open.
                activeDispenser = this;

                Debug.Log("Drink Dispenser Menu Opened!");
                return;
            }
        }

        string holdingName = player.heldObject != null ? player.heldObject.name : "Nothing";
        Debug.Log("You need an Empty Cup to use the dispenser! You are holding: " + holdingName);
    }

    public void Button_SelectCoke() { FillCup(cokePrefab, ItemType.Coke); }
    public void Button_SelectPineapple() { FillCup(pineapplePrefab, ItemType.Pineapple); }
    public void Button_SelectIcedTea() { FillCup(icedTeaPrefab, ItemType.IcedTea); }

    public void CloseMenu() {
        if (drinkMenuPanel != null) drinkMenuPanel.SetActive(false);

        // --- NEW: Bring the white circle back when the menu closes! ---
        if (popupCanvas != null) popupCanvas.SetActive(true);

        interactingPlayer = null;

        // Clear the global reference when closed
        if (activeDispenser == this) {
            activeDispenser = null;
        }
    }

    private void FillCup(GameObject filledDrinkPrefab, ItemType drinkType) {
        if (interactingPlayer == null || interactingPlayer.heldObject == null) return;

        if (!TryDeductDrinkStock(drinkType)) {
            Debug.Log($"[DrinkDispenser] Out of stock: {drinkType}");
            CloseMenu();
            return;
        }

        Destroy(interactingPlayer.heldObject);

        GameObject newDrink = Instantiate(filledDrinkPrefab);
        newDrink.name = filledDrinkPrefab.name;
        interactingPlayer.PickUp(newDrink);

        CloseMenu();
        Debug.Log("Cup filled with " + newDrink.name + "!");
    }

    /// <summary>Deducts one unit of drink stock and records its cost. Returns false if out of stock.</summary>
    private bool TryDeductDrinkStock(ItemType drinkType) {
        if (InventoryManager.Instance == null) return true;

        if (InventoryManager.Instance.IsTracked(drinkType) && InventoryManager.Instance.GetStock(drinkType) <= 0) {
            return false;
        }

        InventoryManager.Instance.UseStock(drinkType, 1);

        float unitCost = GetUnitCost(drinkType);
        if (unitCost > 0f && DailyRevenueTracker.Instance != null)
            DailyRevenueTracker.Instance.RecordIngredientCost(Mathf.RoundToInt(unitCost));

        return true;
    }

    private float GetUnitCost(ItemType itemType) {
        if (InventoryManager.Instance == null) return 0f;
        foreach (ItemData item in InventoryManager.Instance.Items) {
            if (item.itemType == itemType)
                return item.CostPerUnit;
        }
        return 0f;
    }
}