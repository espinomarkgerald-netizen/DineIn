using UnityEngine;

public class Shelf : MonoBehaviour {
    [Tooltip("The prefab that will spawn when the player interacts with this shelf.")]
    public GameObject ingredientToSpawn;
    public Transform standPoint;

    public void Interact(PlayerHolding player) {
        if (player.heldObject != null) {
            Debug.Log("Player's hands are full!");
            return;
        }

        if (!TryDeductStock()) {
            Debug.Log($"[Shelf] Out of stock — cannot spawn {ingredientToSpawn.name}.");
            return;
        }

        GameObject newIngredient = Instantiate(ingredientToSpawn);
        player.PickUp(newIngredient);
    }

    /// <summary>
    /// Deducts one unit from InventoryManager.
    /// Returns false if stock is unavailable or no ItemType mapping is set.
    /// </summary>
    private bool TryDeductStock() {
        if (ingredientToSpawn == null) return false;

        IngredientComponent component = ingredientToSpawn.GetComponent<IngredientComponent>();
        if (component == null || component.ingredientData == null) return true;

        Ingredient data = component.ingredientData;
        ItemType itemType = data.itemType;

        if (InventoryManager.Instance == null) return true;

        if (InventoryManager.Instance.GetStock(itemType) <= 0) return false;

        InventoryManager.Instance.UseStock(itemType, 1);
        return true;
    }
}