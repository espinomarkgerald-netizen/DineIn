using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RecipeIngredient
{
    public ItemType item;
    public int amount;
}

[CreateAssetMenu(menuName = "Game/Recipe")]
public class Recipe : ScriptableObject
{
    public string recipeID;          // Unique ID for save/load
    public string recipeName;

    [Header("Unlock")]
    public int dayToUnlock = 1;      // The day this recipe becomes available

    [Header("Ingredients")]
    public List<RecipeIngredient> ingredients;

    [Header("Economy")]
    public int sellPrice;
}