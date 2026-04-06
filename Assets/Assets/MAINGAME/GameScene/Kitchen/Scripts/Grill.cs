using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Grill : Counter {
    private float currentCookTime = 0f;

    [Header("UI Settings")]
    public GameObject progressBarCanvas;
    public Image progressFill;

    [Header("Smart UI: Direct Orders")]
    [Tooltip("If this ticket exists, show this popup.")]
    public ItemTypeKitchen targetOrder1 = ItemTypeKitchen.None;
    public GameObject popup1;

    [Tooltip("Optional 2nd order (e.g. for a station that cooks two things)")]
    public ItemTypeKitchen targetOrder2 = ItemTypeKitchen.None;
    public GameObject popup2;

    [Header("Smart UI: Kitchen Radar")]
    [Tooltip("Turn this on for the Fryer so it waits for the Breader!")]
    public bool useRadar = false;
    public string radarSearchTerm = "breaded";
    public GameObject radarPopup;

    [Header("Smart UI: Trash")]
    public GameObject trashPopup;

    [Header("Station Rules")]
    public List<Ingredient> acceptedIngredients;

    void Start() {
        if (progressBarCanvas != null) progressBarCanvas.SetActive(false);
    }

    void Update() {
        // --- COOKING LOGIC ---
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

        // --- THE UI OBSERVER ---
        UpdateSmartUI();
    }

    private void UpdateSmartUI() {
        bool showP1 = false;
        bool showP2 = false;
        bool showRadar = false;
        bool showTrash = false;

        if (currentItem != null) {
            // If the item is burnt, ONLY show the trash popup
            if (currentItem.name.ToLower().Contains("burnt")) {
                showTrash = true;
            }
        } else {
            // Check Tickets
            if (OrderManagerKitchen.Instance != null) {
                foreach (var ticket in OrderManagerKitchen.Instance.activeOrders) {
                    if (targetOrder1 != ItemTypeKitchen.None && ticket.missingItems.Contains(targetOrder1)) showP1 = true;
                    if (targetOrder2 != ItemTypeKitchen.None && ticket.missingItems.Contains(targetOrder2)) showP2 = true;
                }
            }

            // Check Radar (For the Fryer!)
            if (useRadar && IsItemInKitchen(radarSearchTerm)) {
                showRadar = true;
            }
        }

        // Apply visual states
        if (popup1 != null) popup1.SetActive(showP1);
        if (popup2 != null) popup2.SetActive(showP2);
        if (radarPopup != null) radarPopup.SetActive(showRadar);
        if (trashPopup != null) trashPopup.SetActive(showTrash);
    }

    private void CookItem(IngredientComponent rawItem) {
        bool burntIt = false;

        // If it was already cooked/fried/breaded, we burnt it!
        string rawName = rawItem.gameObject.name.ToLower();
        if (rawName.Contains("cooked") || rawName.Contains("fried") || rawName.Contains("breaded")) {
            PerformanceManager.AddBurnedItem();
            Debug.Log("Item Burnt on Station!");
            burntIt = true;
        }

        GameObject cookedPrefab = rawItem.ingredientData.processedForm.prefab;
        Destroy(rawItem.gameObject);

        GameObject cookedObj = Instantiate(cookedPrefab);

        // Tag it with "Burnt" so the UI Observer knows to trigger the Trash Canvas!
        if (burntIt) cookedObj.name = "Burnt " + cookedPrefab.name;
        else cookedObj.name = cookedPrefab.name;

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

    // --- GLOBAL RADAR FUNCTION ---
    private bool IsItemInKitchen(string searchTerm) {
        Counter[] allCounters = FindObjectsOfType<Counter>();
        foreach (Counter c in allCounters) {
            if (c.currentItem != null && c.currentItem.name.ToLower().Contains(searchTerm.ToLower())) return true;
        }

        PlayerHolding[] allPlayers = FindObjectsOfType<PlayerHolding>();
        foreach (PlayerHolding p in allPlayers) {
            if (p.heldObject != null && p.heldObject.name.ToLower().Contains(searchTerm.ToLower())) return true;
        }
        return false;
    }
}