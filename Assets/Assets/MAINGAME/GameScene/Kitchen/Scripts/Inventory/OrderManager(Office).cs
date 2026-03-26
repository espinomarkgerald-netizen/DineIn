using UnityEngine;

public class OrderManagerOffice : MonoBehaviour
{
    public bool TryMakeRecipe(Recipe recipe)
    {
        // Check ingredients
        foreach (var ing in recipe.ingredients)
        {
            if (InventoryManager.Instance.GetStock(ing.item) < ing.amount)
                return false;
        }

        // Deduct ingredients
        foreach (var ing in recipe.ingredients)
        {
            InventoryManager.Instance.UseStock(ing.item, ing.amount);
        }

        // Add money
        MoneyManager.Instance.Earn(recipe.sellPrice);

        return true;
    }
}