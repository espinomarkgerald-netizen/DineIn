using UnityEngine;
using System; // We need this to magically translate the text!

public class DeliveryCounter : Counter {

    public override void Interact(PlayerHolding player) {

        // --- NEW: THE TUTORIAL SAFETY NET ---
        // If the Order Manager is missing, just report the click to the tutorial and stop!
        if (OrderManagerKitchen.Instance == null) {
            if (KitchenTutorialManager.Instance != null) {
                KitchenTutorialManager.Instance.ReportInteraction(transform);
            }
            return; // This completely stops the crash!
        }
        // ------------------------------------

        if (player.heldObject != null) {
            bool success = false;

            // --- SCENARIO 1: They are delivering a Plate ---
            if (player.heldObject.TryGetComponent(out Plate playerPlate)) {

                // Read your teammate's standard string recipe
                PlatingRecipe foodOnPlate = playerPlate.GetRecipe();

                if (foodOnPlate != null) {
                    // TRANSLATE IT! Turn their string into your new Enum
                    ItemTypeKitchen plateEnum = ConvertRecipeToEnum(foodOnPlate.recipeName);

                    // Hand the translated Enum to your Order Manager
                    if (plateEnum != ItemTypeKitchen.None) {
                        success = OrderManagerKitchen.Instance.TryDeliver(plateEnum);
                    }
                }
            }
            // --- SCENARIO 2: They are delivering a loose cup ---
            else if (player.heldObject.TryGetComponent(out ItemIdentity identity)) {
                // Cups already have the Enum, so hand it straight in!
                success = OrderManagerKitchen.Instance.TryDeliver(identity.itemType);
            } else {
                Debug.Log("This item cannot be delivered here!");
            }

            if (success) {
                Destroy(player.heldObject);
                player.heldObject = null;
            }
        }
    }

    // --- THE TRANSLATOR METHOD ---
    private ItemTypeKitchen ConvertRecipeToEnum(string recipeName) {
        // 1. Strip out spaces so "Iced Tea" becomes "IcedTea"
        string cleanName = recipeName.Replace(" ", "");

        // 2. Automatically try to match their text to your dropdown list!
        if (Enum.TryParse(cleanName, true, out ItemTypeKitchen result)) {
            return result;
        }

        Debug.Log("WARNING: Your teammate's recipe name '" + recipeName + "' doesn't match any of your ItemTypeKitchen Enums!");
        return ItemTypeKitchen.None;
    }
}