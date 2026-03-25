using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlatingRecipe {
    public string recipeName;
    public List<string> visualOrder;
}

public class Plate : MonoBehaviour {

    public Transform itemPlacementPoint;
    public float stackThickness = 0.2f;

    [Header("Recipe Book")]
    public List<PlatingRecipe> acceptedRecipes;

    private PlatingRecipe currentRecipe = null;
    private List<GameObject> itemsOnPlate = new List<GameObject>();

    public bool TryAddIngredient(GameObject foodItem) {
        string foodName = foodItem.name;

        // 1. Check if plate is empty and assign a recipe
        if (currentRecipe == null) {
            currentRecipe = FindRecipeFor(foodName);
            if (currentRecipe == null) {
                Debug.Log("This item doesn't belong on a plate!");
                return false;
            }
        }
        // 2. Or check if the item belongs to the current recipe
        else {
            if (!IsFoodInRecipe(foodName, currentRecipe)) {
                Debug.Log("Rejected! This plate is currently making a " + currentRecipe.recipeName);
                return false;
            }
        }

        // 3. Accept the item! Move it to the plate FIRST to prevent scale glitches
        foodItem.transform.parent = itemPlacementPoint;
        itemsOnPlate.Add(foodItem);
        foodItem.GetComponent<Collider>().enabled = false;

        // --- THE AUTO-BUN TRICK ---
        if (foodName.ToLower().Contains("bun")) {
            // Clone directly onto the plate so it copies the exact scale!
            GameObject extraBun = Instantiate(foodItem, itemPlacementPoint);
            extraBun.name = foodItem.name; // Keep name exact so math works
            extraBun.GetComponent<Collider>().enabled = false;
            itemsOnPlate.Add(extraBun);
            Debug.Log("AUTO-BUN TRIGGERED! Spawned the top bun perfectly!");
        }
        // --------------------------

        // 4. Sort everything beautifully
        RebuildVisualStack();
        return true;
    }

    private PlatingRecipe FindRecipeFor(string foodName) {
        foreach (PlatingRecipe recipe in acceptedRecipes) {
            foreach (string ingredient in recipe.visualOrder) {
                if (foodName.Contains(ingredient)) return recipe;
            }
        }
        return null;
    }

    private bool IsFoodInRecipe(string foodName, PlatingRecipe recipe) {
        foreach (string ingredient in recipe.visualOrder) {
            if (foodName.Contains(ingredient)) return true;
        }
        return false;
    }

    // --- THE SMART STACKING MATH ---
    private void RebuildVisualStack() {
        Dictionary<GameObject, int> targetLayer = new Dictionary<GameObject, int>();
        Dictionary<string, int> seenCounts = new Dictionary<string, int>();

        // Figure out exact layer for each item (counting duplicates like buns)
        foreach (GameObject item in itemsOnPlate) {
            string cleanName = item.name.Replace("(Clone)", "");

            if (!seenCounts.ContainsKey(cleanName)) {
                seenCounts[cleanName] = 0;
            }

            int index = FindSpecificLayer(cleanName, seenCounts[cleanName]);
            targetLayer[item] = index;

            seenCounts[cleanName]++;
        }

        // Sort them based on those assigned layers
        itemsOnPlate.Sort((a, b) => targetLayer[a].CompareTo(targetLayer[b]));

        // 4. Stack them physically in 3D space
        float currentY = 0f;
        foreach (GameObject item in itemsOnPlate) {

            item.transform.parent = itemPlacementPoint;
            item.transform.localPosition = new Vector3(0, currentY, 0);
            item.transform.localRotation = Quaternion.identity;

            currentY += stackThickness;
        }
    }

    private int FindSpecificLayer(string cleanName, int occurrenceNumber) {
        int matchCount = 0;
        for (int i = 0; i < currentRecipe.visualOrder.Count; i++) {
            if (cleanName.Contains(currentRecipe.visualOrder[i])) {
                if (matchCount == occurrenceNumber) {
                    return i;
                }
                matchCount++;
            }
        }
        return 999;
    }

    // This lets the Delivery Window see what recipe is on the plate!
    public PlatingRecipe GetRecipe() {
        return currentRecipe;
    }
}