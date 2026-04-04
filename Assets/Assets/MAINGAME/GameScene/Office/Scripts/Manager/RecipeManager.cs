using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(0)]
public class RecipeManager : MonoBehaviour
{
    public static RecipeManager Instance { get; private set; }

    [Header("Assign in Inspector")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject unlockedPrefab;
    [SerializeField] private GameObject lockedPrefab;
    [SerializeField] private List<Recipe> allRecipes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Inspector safety
        if (!contentParent || !unlockedPrefab || !lockedPrefab)
            Debug.LogWarning($"[RecipeManager] Inspector references missing on {name}");
    }

    private void OnEnable()
    {
        UnlockManager.OnRecipeUnlocked += HandleRecipeUnlocked;
    }

    private void OnDisable()
    {
        UnlockManager.OnRecipeUnlocked -= HandleRecipeUnlocked;
    }

    private void Start()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        UnlockByDay(day);
        PopulateRecipesUI();
    }

    private void HandleRecipeUnlocked(string recipeID)
    {
        PopulateRecipesUI();
    }

    /// <summary>Returns the full recipe list for cross-system queries.</summary>
    public IReadOnlyList<Recipe> AllRecipes => allRecipes;

    /// <summary>Static accessor for cross-scene systems that need the recipe list without a direct reference.</summary>
    public static IReadOnlyList<Recipe> AllRecipesStatic
    {
        get
        {
            // Use Unity's overloaded == to correctly detect destroyed (fake-null) instances.
            if (Instance == null) return null;
            return Instance.allRecipes;
        }
    }

    public void UnlockByDay(int currentDay)
    {
        foreach (var r in allRecipes)
        {
            if (r.dayToUnlock <= currentDay && !UnlockManager.Instance.IsRecipeUnlocked(r.recipeID))
            {
                // Pass kitchenItemType so UnlockManager can answer kitchen queries directly.
                UnlockManager.Instance.UnlockRecipe(r.recipeID, r.kitchenItemType);

                foreach (var ing in r.ingredients)
                    UnlockManager.Instance.UnlockIngredient(ing.item);
            }
        }
    }

    public void PopulateRecipesUI()
    {
        if (!contentParent || !unlockedPrefab || !lockedPrefab) return;

        // Clear UI
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        // Sort: unlocked first
        var sorted = new List<Recipe>(allRecipes);
        sorted.Sort((a, b) =>
        {
            bool aUnlocked = UnlockManager.Instance.IsRecipeUnlocked(a.recipeID);
            bool bUnlocked = UnlockManager.Instance.IsRecipeUnlocked(b.recipeID);

            int unlockCompare = bUnlocked.CompareTo(aUnlocked);
            if (unlockCompare != 0)
                return unlockCompare;

            return a.dayToUnlock.CompareTo(b.dayToUnlock);
        });

        foreach (var r in sorted)
        {
            bool unlocked = UnlockManager.Instance.IsRecipeUnlocked(r.recipeID);
            GameObject prefab = unlocked ? unlockedPrefab : lockedPrefab;
            GameObject obj = Instantiate(prefab, contentParent);

            if (obj.TryGetComponent(out RecipeItemUI ui))
                ui.Setup(r, unlocked);
            else
                Debug.LogWarning($"[RecipeManager] Prefab missing RecipeItemUI: {r.recipeName}");
        }
    }
}