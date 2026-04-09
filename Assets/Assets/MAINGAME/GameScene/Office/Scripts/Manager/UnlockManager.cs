using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)] // Ensures this initializes first
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    private HashSet<string> unlockedRecipes = new HashSet<string>();
    private HashSet<string> unlockedEquipment = new HashSet<string>();
    private HashSet<ItemData> unlockedIngredients = new HashSet<ItemData>();
    private HashSet<ItemTypeKitchen> unlockedKitchenItems = new HashSet<ItemTypeKitchen>();

    // Events
    public static event Action<string> OnRecipeUnlocked;
    public static event Action<string> OnEquipmentUnlocked;
    public static event Action<ItemData> OnIngredientUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void UnlockRecipe(string recipeID, ItemTypeKitchen kitchenItem = ItemTypeKitchen.None)
    {
        if (unlockedRecipes.Add(recipeID))
        {
            if (kitchenItem != ItemTypeKitchen.None)
                unlockedKitchenItems.Add(kitchenItem);

            OnRecipeUnlocked?.Invoke(recipeID);
        }
    }

    public bool IsRecipeUnlocked(string recipeID) => unlockedRecipes.Contains(recipeID);

    /// <summary>Returns true if this kitchen item has a corresponding unlocked recipe.</summary>
    public bool IsKitchenItemUnlocked(ItemTypeKitchen item) => unlockedKitchenItems.Contains(item);

    /// <summary>Returns all unlocked kitchen item types.</summary>
    public IReadOnlyCollection<ItemTypeKitchen> GetUnlockedKitchenItems() => unlockedKitchenItems;

    public void UnlockEquipment(string itemID)
    {
        if (unlockedEquipment.Add(itemID))
            OnEquipmentUnlocked?.Invoke(itemID);
    }

    public bool IsEquipmentUnlocked(string itemID) => unlockedEquipment.Contains(itemID);

    public void UnlockIngredient(ItemData item)
    {
        if (unlockedIngredients.Add(item))
            OnIngredientUnlocked?.Invoke(item);
    }

    public bool IsIngredientUnlocked(ItemData item) => unlockedIngredients.Contains(item);

    /// <summary>
    /// Clears all unlock records. Call on a full run reset so the player
    /// re-earns recipes, equipment, and ingredients from Day 1.
    /// </summary>
    public void ResetAll()
    {
        unlockedRecipes.Clear();
        unlockedEquipment.Clear();
        unlockedIngredients.Clear();
        unlockedKitchenItems.Clear();
    }
}