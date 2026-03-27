using UnityEngine;

public class Shelf : MonoBehaviour {
    [Tooltip("The prefab that will spawn when the player interacts with this shelf")]
    public GameObject ingredientToSpawn;
    public Transform standPoint; // <--- ADD THIS LINE

    public void Interact(PlayerHolding player) {
        // Only give the player an item if their hands are empty
        if (player.heldObject == null) {
            // Create a new ingredient in the world
            GameObject newIngredient = Instantiate(ingredientToSpawn);

            // Immediately force the player to pick it up
            player.PickUp(newIngredient);
        } else {
            Debug.Log("Player's hands are full!");
        }
    }
}