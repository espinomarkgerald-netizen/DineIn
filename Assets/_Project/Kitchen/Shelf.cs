using UnityEngine;

public class Shelf : MonoBehaviour {
    [Tooltip("The prefab that will spawn when the player interacts with this shelf.")]
    public GameObject ingredientToSpawn;
    public Transform standPoint;

    [Tooltip("How many units are grabbed per shelf interaction.")]
    [SerializeField] private int grabQuantity = 5;


    /// <summary>Called by the world-space shelf button. Finds the active PrepCook and
    /// sends them to this shelf's stand point. The ingredient is given on arrival.</summary>
    public void Button_TakeStack() {
        KitchenPlayerMovement prep = FindActivePrepCook();

        if (prep == null) {
            Debug.Log("[Shelf] No active Prep Cook found.");
            return;
        }

        if (prep.myRole != KitchenRole.PrepCook) {
            if (DeliveryFeedback.Instance != null)
                DeliveryFeedback.Instance.ShowRejection("Only Prep Cook can take from shelves!");
            return;
        }

        prep.SetTargetPublic(transform, standPoint);
    }

    /// <summary>Called by KitchenPlayerMovement when the prep cook arrives at this shelf.
    /// Automatically gives the ingredient stack.</summary>
    public void Interact(PlayerHolding player) {
        if (player == null || player.heldObject != null) return;

        int amount = TryDeductStock(grabQuantity);

        if (amount <= 0) {
            Debug.Log($"[Shelf] Out of stock — cannot spawn {ingredientToSpawn.name}.");
            return;
        }

        GameObject newIngredient = Instantiate(ingredientToSpawn);
        newIngredient.name = ingredientToSpawn.name;

        IngredientStack stack = newIngredient.AddComponent<IngredientStack>();
        stack.Init(ingredientToSpawn, amount);

        player.PickUp(newIngredient);
    }

    /// <summary>Finds the KitchenPlayerMovement marked as active player with the PrepCook role.</summary>
    private KitchenPlayerMovement FindActivePrepCook() {
        KitchenPlayerMovement[] all = FindObjectsByType<KitchenPlayerMovement>(FindObjectsSortMode.None);
        foreach (KitchenPlayerMovement k in all) {
            if (k.isActivePlayer && k.myRole == KitchenRole.PrepCook)
                return k;
        }
        return null;
    }


    /// <summary>
    /// Deducts up to <paramref name="requested"/> units from InventoryManager.
    /// Returns the actual amount deducted, or 0 if out of stock.
    /// </summary>
    private int TryDeductStock(int requested) {
        if (ingredientToSpawn == null) return 0;

        IngredientComponent component = ingredientToSpawn.GetComponent<IngredientComponent>();
        if (component == null || component.ingredientData == null) return requested;

        Ingredient data = component.ingredientData;
        ItemType itemType = data.itemType;

        if (InventoryManager.Instance == null) return requested;

        int available = InventoryManager.Instance.GetStock(itemType);
        if (available <= 0) return 0;

        int amount = Mathf.Min(requested, available);
        InventoryManager.Instance.UseStock(itemType, amount);
        return amount;
    }
}