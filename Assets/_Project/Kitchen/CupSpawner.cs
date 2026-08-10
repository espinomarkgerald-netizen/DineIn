using UnityEngine;

public class CupSpawner : Counter {

    [Header("Spawner Settings")]
    public GameObject emptyCupPrefab;

    public override void Interact(PlayerHolding player) {
        if (player.heldObject != null) {
            Debug.Log("Your hands are full! Put down what you are holding first.");
            return;
        }

        GameObject newCup = Instantiate(emptyCupPrefab);
        newCup.name = emptyCupPrefab.name;
        player.PickUp(newCup);
        Debug.Log("Grabbed an Empty Cup!");
    }
}