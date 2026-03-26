using UnityEngine;

// We inherit from Counter so it works with your player's click system!
public class CupSpawner : Counter {

    [Header("Spawner Settings")]
    public GameObject emptyCupPrefab; // Drag your Empty Cup prefab here

    public override void Interact(PlayerHolding player) {

        // Check if the chef's hands are totally empty
        if (player.heldObject == null) {

            // Spawn a brand new empty cup
            GameObject newCup = Instantiate(emptyCupPrefab);

            // Keep the name clean so Unity math doesn't get confused
            newCup.name = emptyCupPrefab.name;

            // Put it directly into the player's hands!
            player.PickUp(newCup);

            Debug.Log("Grabbed an Empty Cup!");
        } else {
            Debug.Log("Your hands are full! Put down what you are holding first.");
        }
    }
}