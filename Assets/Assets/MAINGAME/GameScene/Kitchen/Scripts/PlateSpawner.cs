using UnityEngine;

// By inheriting from Counter, your movement script already knows 
// how to click this and walk over to it!
public class PlateSpawner : Counter {

    [Header("Plate Spawner Settings")]
    public GameObject platePrefab; // Drag your Plate Prefab here in the Inspector!

    public override void Interact(PlayerHolding player) {
        // Only dispense a plate if the player's hands are completely empty
        if (player.heldObject == null) {

            // 1. Spawn a brand new plate out of thin air
            GameObject newPlate = Instantiate(platePrefab);

            // 2. Put it in the player's hands!
            // IMPORTANT: Look at your Shelf.cs script to see how you parented food to the player!
            // You might need to change 'player.transform' to something like 'player.holdPoint'
            newPlate.transform.parent = player.transform;

            // Adjust these numbers so the plate rests perfectly in your chef's hands
            newPlate.transform.localPosition = new Vector3(0, 1.5f, 1.5f);
            newPlate.transform.localRotation = Quaternion.identity;

            // 3. Tell the player script that it is officially holding the plate
            player.heldObject = newPlate;

        } else {
            Debug.Log("Chef's hands are full! Can't carry another plate.");
        }
    }
}