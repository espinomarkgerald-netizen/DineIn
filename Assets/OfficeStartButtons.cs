using System.Collections.Generic;
using UnityEngine;

public class OfficeStartButtons : MonoBehaviour
{
    [SerializeField]
    private List<InventoryEntry> kitchenRequirements;

    private bool HasEnoughStock()
    {
        foreach (var req in kitchenRequirements)
        {
            if (!IsRequiredByUnlockedRecipe(req.itemType))
                continue;

            int current = InventoryManager.Instance.GetStock(req.itemType);

            if (current < req.stock)
                WarningSlideUI.Instance?.Show($"Low stock: {req.itemType}");
        }

        return true;
    }

    /// <summary>
    /// Returns true if at least one unlocked recipe uses this ingredient.
    /// Locked-recipe ingredients are skipped from stock requirements.
    /// </summary>
    private bool IsRequiredByUnlockedRecipe(ItemType itemType)
    {
        if (RecipeManager.Instance == null) return true;

        foreach (var recipe in RecipeManager.Instance.AllRecipes)
        {
            if (!UnlockManager.Instance.IsRecipeUnlocked(recipe.recipeID))
                continue;

            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.item != null && ingredient.item.itemType == itemType)
                    return true;
            }
        }

        return false;
    }

    private void WarnIfNoEmployees()
    {
        foreach (var emp in EmployeeManager.Instance.allEmployees)
            if (!string.IsNullOrEmpty(emp.assignedSlotName))
                return;

        WarningSlideUI.Instance?.Show("No employees assigned — shift may be unproductive.");
    }

    private bool CanStart()
    {
        if (InventoryManager.Instance == null || EmployeeManager.Instance == null)
        {
            Debug.LogError("[OfficeStartButtons] Missing managers.");
            return false;
        }
        return true;
    }

    public void StartLobby()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }
        if (!CanStart()) return;

        HasEnoughStock();       // warns per low item, no longer blocks
        WarnIfNoEmployees(); 

        GameFlowManager.Instance.LoadLobbyScene();
    }

    public void StartKitchen()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }
        if (!CanStart()) return;

        HasEnoughStock();       // warns per low item, no longer blocks
        WarnIfNoEmployees(); 

        GameFlowManager.Instance.StartKitchenShift();
    }
}