using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int amount;
}

[CreateAssetMenu(menuName = "Game/Recipe")]
public class Recipe : ScriptableObject
{
    public string recipeID;          // Unique ID for save/load
    public string recipeName;

    [Header("Kitchen")]
    [Tooltip("The matching kitchen item type. Must be set for kitchen orders to spawn this recipe.")]
    public ItemTypeKitchen kitchenItemType = ItemTypeKitchen.None;

    [Header("Visuals")]
    public Sprite sprite;

    [Header("Unlock")]
    public int dayToUnlock = 1;      // The day this recipe becomes available

    [Header("Ingredients")]
    public List<RecipeIngredient> ingredients;

    [Header("Economy")]
    public int sellPrice;

    [Header("Description")]
    public string descriptionText;
}