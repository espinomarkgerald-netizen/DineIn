using UnityEngine;

public class TrashCan : MonoBehaviour {
    // This is the doormat so the player doesn't walk into the wall!
    public Transform standPoint;

    public void Interact(PlayerHolding player) {
        // Check if the player is actually holding something in their hands
        if (player.heldObject != null) {
            // 1. Destroy the 3D model of the food
            Destroy(player.heldObject);

            // 2. Tell the player's hands that they are officially empty again
            player.heldObject = null;
        }
    }
}