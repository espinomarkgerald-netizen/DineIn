using System.Collections.Generic;
using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance;

    private HashSet<string> unlockedRecipes = new HashSet<string>();

    private void Awake()
    {
        Instance = this;
    }

    public void UnlockRecipe(string recipeID)
    {
        unlockedRecipes.Add(recipeID);
    }

    public bool IsRecipeUnlocked(string recipeID)
    {
        return unlockedRecipes.Contains(recipeID);
    }
}

