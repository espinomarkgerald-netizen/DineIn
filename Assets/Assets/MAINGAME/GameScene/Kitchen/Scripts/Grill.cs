using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Grill : Counter {
    private float currentCookTime = 0f;

    [Header("UI Settings")]
    public GameObject progressBarCanvas;
    public Image progressFill;

    [Header("Station Rules")]
    public List<Ingredient> acceptedIngredients;

    void Start() {
        if (progressBarCanvas != null) progressBarCanvas.SetActive(false);
    }

    void Update() {
        if (currentItem != null) {
            if (currentItem.TryGetComponent(out IngredientComponent itemData)) {

                bool isAllowedHere = false;

                if (acceptedIngredients.Count == 0) {
                    isAllowedHere = true;
                } else {
                    if (acceptedIngredients.Contains(itemData.ingredientData)) {
                        isAllowedHere = true;
                    }
                }

                if (itemData.ingredientData.processedForm != null && isAllowedHere) {
                    if (progressBarCanvas != null) progressBarCanvas.SetActive(true);

                    currentCookTime += Time.deltaTime;

                    if (progressFill != null) {
                        progressFill.fillAmount = currentCookTime / itemData.ingredientData.cookTime;
                    }

                    if (currentCookTime >= itemData.ingredientData.cookTime) {
                        CookItem(itemData);
                    }
                } else {
                    if (progressBarCanvas != null) progressBarCanvas.SetActive(false);
                }
            }
        } else {
            if (progressBarCanvas != null) progressBarCanvas.SetActive(false);
        }
    }

    private void CookItem(IngredientComponent rawItem) {
        // --- NEW: CHECK IF WE JUST BURNT IT ---
        // If the item we are about to destroy was already cooked, then we just burnt it!
        if (rawItem.gameObject.name.ToLower().Contains("cooked") || rawItem.gameObject.name.ToLower().Contains("fried")) {
            PerformanceManager.AddBurnedItem();
            Debug.Log("Item Burnt on Grill!");
        }

        GameObject cookedPrefab = rawItem.ingredientData.processedForm.prefab;
        Destroy(rawItem.gameObject);

        GameObject cookedObj = Instantiate(cookedPrefab);
        currentItem = cookedObj;
        currentItem.transform.parent = itemPlacementPoint;
        currentItem.transform.localPosition = Vector3.zero;

        currentCookTime = 0f;
        if (progressFill != null) progressFill.fillAmount = 0f;
    }

    public override void Interact(PlayerHolding player) {
        base.Interact(player);
        currentCookTime = 0f;
        if (progressFill != null) progressFill.fillAmount = 0f;
    }
}