using System.Collections.Generic;
using UnityEngine;

public class OfficeStartButtons : MonoBehaviour
{
    [SerializeField]
    private List<InventoryEntry> kitchenRequirements;

    private List<string> GetStockIssues()
    {
        List<string> issues = new List<string>();

        foreach (var req in kitchenRequirements)
        {
            if (!IsRequiredByUnlockedRecipe(req.itemType))
                continue;

            int current = InventoryManager.Instance.GetStock(req.itemType);

            if (current < req.stock)
            {
                issues.Add($"{req.itemType}: {current}/{req.stock}");
            }
        }

        return issues;
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
            if (!string.IsNullOrEmpty(emp.assignedSlotName))
                return true;
        }

        Debug.Log("No employees assigned.");
        return false;
    }

    private bool HasRequiredEquipment()
    {
        // Define which equipment types are required to start the lobby
        string[] requiredIDs = {
        "booth01", "booth02", "booth03", "booth04", "booth05",
        "table01", "table02", "table03", "table04" }; // use itemID keywords or exact IDs from your Equipment ScriptableObjects

        foreach (var equip in EquipmentManager.Instance.AllEquipment)
        {
            // Skip if not purchased
            if (!EquipmentManager.Instance.Purchased(equip.itemID))
                continue;

            // Check if this equipment matches one of the required types
            foreach (var reqID in requiredIDs)
            {
                if (equip.itemID.Contains(reqID))
                    return true;
            }
        }

        // If no required equipment purchased, block start
        return false;
    }

    private List<string> GetStartBlockers()
    {
        List<string> issues = new List<string>();

        if (InventoryManager.Instance == null || EmployeeManager.Instance == null)
        {
            issues.Add("Missing core systems. Congratulations, you broke reality.");
            return issues;
        }

        var stockIssues = GetStockIssues();
        if (stockIssues.Count > 0)
        {
            issues.Add("Stock up required ingredients:");
            foreach (var s in stockIssues)
                issues.Add("• " + s);
        }

        if (!HasEmployeesAssigned())
            issues.Add("Assign at least one employee.");

        // NEW: Equipment check (your “buy seats” requirement)
        if (!HasRequiredEquipment())
            issues.Add("Buy and place required equipment (e.g., seats).");

        return issues;
    }

    public void StartLobby()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        var issues = GetStartBlockers();

        if (issues.Count > 0)
        {
            StartBlockedPanel.Instance?.Show(issues);
            return;
        }

        GameFlowManager.Instance.LoadLobbyScene();
    }

    public void StartKitchen()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("GameFlowManager not found.");
            return;
        }

        var issues = GetStartBlockers();

        if (issues.Count > 0)
        {
            StartBlockedPanel.Instance?.Show(issues);
            return;
        }

        GameFlowManager.Instance.StartKitchenShift();
    }
}