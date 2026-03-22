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
    public string recipeName;

    [Header("Ingredients")]
    public List<RecipeIngredient> ingredients;

    [Header("Economy")]
    public int sellPrice;
}