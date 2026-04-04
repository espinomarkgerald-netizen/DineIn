using UnityEngine;

public class Fryer : Counter {

    [Header("Fryer Settings")]
    public GameObject cookedFriesPrefab;
    public float cookTime = 4f;

    private float cookTimer = 0f;
    private bool isCooking = false;

    void Update() {
        if (currentItem != null) {
            // Check if it's Raw OR Cooked (if you want them to burn!)
            if ((currentItem.name.Contains("Raw Fries") || currentItem.name.Contains("Cooked Fries")) && !isCooking) {
                isCooking = true;
                cookTimer = 0f;
            }
        } else {
            isCooking = false;
            cookTimer = 0f;
        }

        if (isCooking) {
            cookTimer += Time.deltaTime;

            if (cookTimer >= cookTime) {
                FinishCooking();
            }
        }
    }

    private void FinishCooking() {
        isCooking = false;

        // --- NEW: TRACK BURNING ---
        // If it was already cooked, and it finished a timer again, it burnt!
        if (currentItem.name.Contains("Cooked Fries")) {
            PerformanceManager.AddBurnedItem();
            Debug.Log("Fries Burnt!");
            // NOTE: If you have a burntFriesPrefab, you would instantiate it here instead!
        }

        Destroy(currentItem);

        GameObject cookedFries = Instantiate(cookedFriesPrefab);
        currentItem = cookedFries;
        currentItem.transform.parent = itemPlacementPoint;
        currentItem.transform.localPosition = Vector3.zero;
        currentItem.transform.localRotation = Quaternion.identity;
    }
}