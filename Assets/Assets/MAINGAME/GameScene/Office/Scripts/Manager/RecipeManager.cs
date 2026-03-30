using System.Collections.Generic;
using UnityEngine;

public class RecipeManager : MonoBehaviour
{
    public static RecipeManager Instance;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject recipePrefab;
    [SerializeField] private List<Recipe> allRecipes;
    private HashSet<string> unlockedRecipes = new HashSet<string>();

    private void Awake() => Instance = this;

    public void UnlockByDay(int currentDay)
    {
        foreach (var r in allRecipes)
            if (!unlockedRecipes.Contains(r.recipeID) && r.dayToUnlock <= currentDay)
                unlockedRecipes.Add(r.recipeID);
    }

    public bool IsUnlocked(string recipeID) => unlockedRecipes.Contains(recipeID);
    public List<Recipe> GetUnlockedRecipes() => allRecipes.FindAll(r => unlockedRecipes.Contains(r.recipeID));
}