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
            {
                Debug.Log($"Not enough {req.itemType}");
                return false;
            }
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

    private bool HasEmployeesAssigned()
    {
        foreach (var emp in EmployeeManager.Instance.allEmployees)
        {
            if (emp.assignedSlot != null)
                return true;
        }

        Debug.Log("No employees assigned.");
        return false;
    }

    private bool CanStart()
    {
        if (InventoryManager.Instance == null || EmployeeManager.Instance == null)
        {
            Debug.LogError("Missing managers.");
            return false;
        }

        if (!HasEnoughStock())
            return false;

        if (!HasEmployeesAssigned())
            return false;

        return true;
    }

    public void StartLobby()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }
        if (!CanStart())
            return; // Abort if stock or employees fail

        GameFlowManager.Instance.LoadLobbyScene();
    }

    public void StartKitchen()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }
        if (!CanStart())
            return; // Abort if stock or employees fail

        GameFlowManager.Instance.StartKitchenShift();
    }
}