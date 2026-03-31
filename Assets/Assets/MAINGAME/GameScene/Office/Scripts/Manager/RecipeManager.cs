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

    void Start()
    {
        UnlockByDay(1); // or whatever your starting day is
        PopulateRecipesUI();
    }
 
    public void UnlockByDay(int currentDay)
    {
        foreach (var r in allRecipes)
            if (!unlockedRecipes.Contains(r.recipeID) && r.dayToUnlock <= currentDay)
                unlockedRecipes.Add(r.recipeID);
    }

    public void PopulateRecipesUI()
    {
        // Clear old UI
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        // Spawn new UI
        foreach (var recipe in allRecipes)
        {
            GameObject obj = Instantiate(recipePrefab, contentParent);

            RecipeItemUI ui = obj.GetComponent<RecipeItemUI>();
            ui.Setup(recipe);
        }
    }

    public bool IsUnlocked(string recipeID) => unlockedRecipes.Contains(recipeID);
    public List<Recipe> GetUnlockedRecipes() => allRecipes.FindAll(r => unlockedRecipes.Contains(r.recipeID));
}