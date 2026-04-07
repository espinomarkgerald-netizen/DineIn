using UnityEngine;
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
    /// <summary>Seconds since the drink menu was last opened. Used by KitchenPlayerMovement for the grace-period check.</summary>
    public float OpenTimer => openTimer;

    // Button RectTransforms resolved at runtime from drinkMenuPanel children.
    private RectTransform cokeButtonRect;
    private RectTransform pineappleButtonRect;
    private RectTransform icedTeaButtonRect;
    private Camera uiCamera;

    void Start() {
        if (drinkMenuPanel != null) {
            drinkMenuPanel.SetActive(false);

            // Cache button RectTransforms by name so we don't depend on Button.onClick
            // or the EventSystem at all — we do our own screen-point hit test instead.
            cokeButtonRect     = FindButtonRect("CokeButton");
            pineappleButtonRect = FindButtonRect("PineappleButton");
            icedTeaButtonRect  = FindButtonRect("IcedTeaButton");
        }

        if (popupCanvas != null)
            popupCanvas.SetActive(true);

        uiCamera = Camera.main;
    }

    private RectTransform FindButtonRect(string buttonName) {
        Transform t = drinkMenuPanel.transform.Find(buttonName);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    void Update() {
        if (drinkMenuPanel == null || !drinkMenuPanel.activeSelf) {
            openTimer = 0f;
        } else {
            openTimer += Time.deltaTime;
        }
    }

    /// <summary>Returns true when <paramref name="screenPos"/> falls inside the button's rect.</summary>
    private bool HitButton(RectTransform rect, Vector2 screenPos) {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, uiCamera);
    }

    /// <summary>
    /// Returns true if the screen position overlaps the drink menu panel.
    /// Called by KitchenPlayerMovement to decide whether to block world-click processing.
    /// Also selects the button that was tapped.
    /// </summary>
    public bool IsPointerOverMenu(Vector2 screenPos) {
        if (drinkMenuPanel == null || !drinkMenuPanel.activeSelf) return false;

        if (HitButton(cokeButtonRect, screenPos))      { Button_SelectCoke();      return true; }
        if (HitButton(pineappleButtonRect, screenPos)) { Button_SelectPineapple(); return true; }
        if (HitButton(icedTeaButtonRect, screenPos))   { Button_SelectIcedTea();   return true; }

        // Tapped inside the panel background but not a button.
        RectTransform panelRect = drinkMenuPanel.GetComponent<RectTransform>();
        return panelRect != null &&
               RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, uiCamera);
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