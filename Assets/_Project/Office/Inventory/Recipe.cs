using UnityEngine;
using System.Collections.Generic;

public enum MenuProductCategory
{
    Food,
    Drink
}

[System.Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int amount;
}

[CreateAssetMenu(menuName = "Game/Recipe")]
public class Recipe : ScriptableObject
{
    [Header("Menu Product")]
    [Tooltip("Stable unique ID used by orders and save data. Never reuse an ID for a different product.")]
    public string recipeID;
    public string recipeName;
    public MenuProductCategory category = MenuProductCategory.Food;
    [Tooltip("Disabling this removes the product from customer orders, the notepad, and kitchen menu pools.")]
    public bool availableOnMenu = true;
    [Tooltip("Controls the product's order in menu UIs.")]
    public int menuSortOrder;

    [Header("Kitchen")]
    [Tooltip("The matching kitchen item type. Must be set for kitchen orders to spawn this recipe.")]
    public ItemTypeKitchen kitchenItemType = ItemTypeKitchen.None;

    [Header("Visuals")]
    public Sprite sprite;
    [Tooltip("Completed product visual used by trays and, later, kitchen/bar serving stations.")]
    public GameObject servingPrefab;

    [Header("Unlock")]
    public int dayToUnlock = 1;      // The day this recipe becomes available

    [Header("Ingredients")]
    public List<RecipeIngredient> ingredients;

    [Header("Economy")]
    public int sellPrice;

    [Header("Description")]
    public string descriptionText;

    public string ProductId => recipeID;
    public string DisplayName => recipeName;
    public int EffectiveSellPrice => MenuAvailabilityManager.GetProductPrice(this);

    public bool IsUnlocked
    {
        get
        {
            if (UnlockManager.Instance == null ||
                UnlockManager.Instance.IsRecipeUnlocked(recipeID))
                return true;

            // Direct scene play can have an empty UnlockManager because the Office
            // scene did not run RecipeManager yet. Day eligibility keeps those scenes
            // usable without changing the saved unlock state.
            int currentDay = GameFlowManager.Instance != null
                ? GameFlowManager.Instance.CurrentDay
                : 1;
            return dayToUnlock <= currentDay;
        }
    }

    private void OnValidate()
    {
        recipeID = recipeID != null ? recipeID.Trim() : string.Empty;
        recipeName = recipeName != null ? recipeName.Trim() : string.Empty;
        menuSortOrder = Mathf.Max(0, menuSortOrder);

        if (ingredients == null)
            ingredients = new List<RecipeIngredient>();
    }
}
