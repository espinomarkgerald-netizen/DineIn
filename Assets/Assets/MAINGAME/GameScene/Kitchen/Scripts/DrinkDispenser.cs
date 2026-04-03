using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class DrinkDispenser : Counter {

    [Header("UI Setup")]
    public GameObject drinkMenuPanel;

    [Header("Drink Prefabs")]
    public GameObject cokePrefab;
    public GameObject pineapplePrefab;
    public GameObject icedTeaPrefab;

    private PlayerHolding interactingPlayer;
    private float openTimer = 0f;

    void Start() {
        if (drinkMenuPanel != null)
            drinkMenuPanel.SetActive(false);
    }

    void Update() {
        if (drinkMenuPanel != null && drinkMenuPanel.activeSelf) {
            openTimer += Time.deltaTime;

            if (openTimer > 0.1f) {
                bool clickedOutside = false;

                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue()))
                        clickedOutside = true;
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
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
                drinkMenuPanel.SetActive(true);
                openTimer = 0f;
                return;
            }
        }

        string holdingName = player.heldObject != null ? player.heldObject.name : "Nothing";
        Debug.Log("You need an Empty Cup to use the dispenser! You are holding: " + holdingName);
    }

    public void Button_SelectCoke()      { FillCup(cokePrefab,      ItemType.Coke);      }
    public void Button_SelectPineapple() { FillCup(pineapplePrefab, ItemType.Pineapple); }
    public void Button_SelectIcedTea()   { FillCup(icedTeaPrefab,   ItemType.IcedTea);   }

    public void CloseMenu() {
        if (drinkMenuPanel != null)
            drinkMenuPanel.SetActive(false);
        interactingPlayer = null;
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

        // Only gate on stock if this item was actually purchased and registered in inventory.
        // If it was never stocked, allow dispensing freely (e.g. testing or first-day play).
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