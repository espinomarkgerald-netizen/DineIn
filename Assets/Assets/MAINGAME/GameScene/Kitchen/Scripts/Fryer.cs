using UnityEngine;

public class Fryer : Counter {

    [Header("Fryer Settings")]
    public GameObject cookedFriesPrefab;
    public float cookTime = 4f; // Seconds to cook

    private float cookTimer = 0f;
    private bool isCooking = false;

    void Update() {
        // 1. Is there something sitting in the fryer?
        if (currentItem != null) {

            // 2. Is it Raw Fries, and are we NOT cooking yet?
            if (currentItem.name.Contains("Raw Fries") && !isCooking) {
                isCooking = true;
                cookTimer = 0f;
                Debug.Log("Fryer started cooking!");
            }
        } else {
            // If the player picks up the fries early, reset the fryer
            isCooking = false;
            cookTimer = 0f;
        }

        // 3. The Timer Logic
        if (isCooking) {
            cookTimer += Time.deltaTime;

            if (cookTimer >= cookTime) {
                FinishCooking();
            }
        }
    }

    private void FinishCooking() {
        isCooking = false;

        // Destroy the raw fries
        Destroy(currentItem);

        // Spawn the golden cooked fries
        GameObject cookedFries = Instantiate(cookedFriesPrefab);

        // Put the cooked fries exactly where the raw ones were sitting
        currentItem = cookedFries;
        currentItem.transform.parent = itemPlacementPoint;
        currentItem.transform.localPosition = Vector3.zero;
        currentItem.transform.localRotation = Quaternion.identity;

        Debug.Log("Fries are done!");
    }
}