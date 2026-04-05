using UnityEngine;

public class Shelf : MonoBehaviour {
    [Tooltip("The prefab that will spawn when the player interacts with this shelf.")]
    public GameObject ingredientToSpawn;
    public Transform standPoint;

    [Tooltip("How many units are grabbed per shelf interaction.")]
    [SerializeField] private int grabQuantity = 5;

    private PlayerHolding interactingPlayer;

    /// <summary>Called by the proximity trigger to register the nearby player.</summary>
    public void Interact(PlayerHolding player) {
        interactingPlayer = player;
    }

    /// <summary>Called by the Take Stack button on the shelf.</summary>
    public void Button_TakeStack() {
        PlayerHolding player = interactingPlayer ?? FindNearestPlayer();

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
        interactingPlayer = null;
    }

    private PlayerHolding FindNearestPlayer() {
        PlayerHolding[] all = FindObjectsByType<PlayerHolding>(FindObjectsSortMode.None);
        PlayerHolding nearest = null;
        float minDist = float.MaxValue;

        foreach (PlayerHolding p in all) {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDist) {
                minDist = dist;
                nearest = p;
            }
        }

        return nearest;
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