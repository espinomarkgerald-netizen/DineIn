using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    private HashSet<string> unlockedRecipes = new HashSet<string>();
    private HashSet<string> unlockedEquipment = new HashSet<string>();
    private HashSet<ItemData> unlockedIngredients = new HashSet<ItemData>();
    private HashSet<ItemTypeKitchen> unlockedKitchenItems = new HashSet<ItemTypeKitchen>();

    public static event Action<string> OnRecipeUnlocked;
    public static event Action<string> OnEquipmentUnlocked;
    public static event Action<ItemData> OnIngredientUnlocked;
    public static event Action OnStateRestored;

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
            GameSaveManager.Instance?.RequestSave();
        }
    }

    public bool IsRecipeUnlocked(string recipeID) => unlockedRecipes.Contains(recipeID);

    public bool IsKitchenItemUnlocked(ItemTypeKitchen item) => unlockedKitchenItems.Contains(item);

    public IReadOnlyCollection<ItemTypeKitchen> GetUnlockedKitchenItems() => unlockedKitchenItems;

    public void UnlockEquipment(string itemID)
    {
        if (unlockedEquipment.Add(itemID))
        {
            OnEquipmentUnlocked?.Invoke(itemID);
            GameSaveManager.Instance?.RequestSave();
        }
    }

    public bool IsEquipmentUnlocked(string itemID) => unlockedEquipment.Contains(itemID);

    public void UnlockIngredient(ItemData item)
    {
        if (unlockedIngredients.Add(item))
        {
            OnIngredientUnlocked?.Invoke(item);
            GameSaveManager.Instance?.RequestSave();
        }
    }

    public bool IsIngredientUnlocked(ItemData item) => unlockedIngredients.Contains(item);

    public void ResetAll()
    {
        unlockedRecipes.Clear();
        unlockedEquipment.Clear();
        unlockedIngredients.Clear();
        unlockedKitchenItems.Clear();
        GameSaveManager.Instance?.RequestSave();
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.unlockedRecipeIDs.Clear();
        data.unlockedEquipmentIDs.Clear();
        data.unlockedKitchenItems.Clear();

        data.unlockedRecipeIDs.AddRange(unlockedRecipes);
        data.unlockedEquipmentIDs.AddRange(unlockedEquipment);

        foreach (var item in unlockedKitchenItems)
            data.unlockedKitchenItems.Add((int)item);
    }

    // Install the full host state before notifying UI; presentation never grants an unlock.
    public bool ApplyMultiplayerState(GameSaveData data, bool announce)
    {
        if (data == null) return false;
        var previousRecipes = new HashSet<string>(unlockedRecipes);
        var previousEquipment = new HashSet<string>(unlockedEquipment);
        var previousIngredients = new HashSet<ItemData>(unlockedIngredients);
        var previousKitchen = new HashSet<ItemTypeKitchen>(unlockedKitchenItems);
        ApplySaveData(data);
        bool changed = !previousRecipes.SetEquals(unlockedRecipes) || !previousEquipment.SetEquals(unlockedEquipment)
            || !previousIngredients.SetEquals(unlockedIngredients) || !previousKitchen.SetEquals(unlockedKitchenItems);
        if (announce)
        {
            foreach (string id in unlockedRecipes) if (!previousRecipes.Contains(id)) OnRecipeUnlocked?.Invoke(id);
            foreach (string id in unlockedEquipment) if (!previousEquipment.Contains(id)) OnEquipmentUnlocked?.Invoke(id);
            foreach (var item in unlockedIngredients) if (!previousIngredients.Contains(item)) OnIngredientUnlocked?.Invoke(item);
        }
        if (changed && !announce) OnStateRestored?.Invoke();
        return changed;
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        unlockedRecipes.Clear();
        unlockedEquipment.Clear();
        bool restoreRestaurantIngredients = !CampaignSaveStore.ProtectedSession || MultiplayerProgressionContext.IsActive;
        if (restoreRestaurantIngredients) unlockedIngredients.Clear();
        unlockedKitchenItems.Clear();

        if (data.unlockedRecipeIDs != null)
        {
            foreach (string id in data.unlockedRecipeIDs)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    unlockedRecipes.Add(id);
            }
        }

        var catalog = MultiplayerProgressionContext.IsActive ? MultiplayerProgressionContext.Catalog
            : MenuCatalog.ForScene(CampaignSaveStore.RestaurantScene);
        if (restoreRestaurantIngredients && catalog != null)
            foreach (var recipe in catalog.Products)
                if (recipe != null && unlockedRecipes.Contains(recipe.recipeID) && recipe.ingredients != null)
                    foreach (var ingredient in recipe.ingredients)
                        if (ingredient.item != null) unlockedIngredients.Add(ingredient.item);

        if (data.unlockedEquipmentIDs != null)
        {
            foreach (string id in data.unlockedEquipmentIDs)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    unlockedEquipment.Add(id);
            }
        }

        if (data.unlockedKitchenItems != null)
        {
            foreach (int value in data.unlockedKitchenItems)
                unlockedKitchenItems.Add((ItemTypeKitchen)value);
        }
    }
}