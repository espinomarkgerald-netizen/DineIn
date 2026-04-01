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

    public void UnlockRecipe(string recipeID)
    {
        if (unlockedRecipes.Add(recipeID))
            OnRecipeUnlocked?.Invoke(recipeID);
    }

    public bool IsRecipeUnlocked(string recipeID) => unlockedRecipes.Contains(recipeID);

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
}