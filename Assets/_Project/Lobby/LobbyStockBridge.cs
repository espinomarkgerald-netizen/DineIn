using UnityEngine;
using System.Collections.Generic;

public class LobbyStockBridge : MonoBehaviour
{
    public static LobbyStockBridge Instance { get; private set; }

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

    public bool HasFoodStock(CustomerGroup.FoodType foodType)
    {
        return HasProductStock(GetLegacyFoodProduct(foodType));
    }

    public bool HasDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        return HasProductStock(GetLegacyDrinkProduct(drinkType));
    }

    public bool TryUseFoodStock(CustomerGroup.FoodType foodType)
    {
        return TryUseProductStock(GetLegacyFoodProduct(foodType));
    }

    public bool TryUseDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        return TryUseProductStock(GetLegacyDrinkProduct(drinkType));
    }

    public int GetFoodStock(CustomerGroup.FoodType foodType)
    {
        return GetProductStock(GetLegacyFoodProduct(foodType));
    }

    public int GetDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        return GetProductStock(GetLegacyDrinkProduct(drinkType));
    }

    public bool HasProductStock(Recipe product, int quantity = 1)
    {
        if (product == null || !product.availableOnMenu)
            return false;

        return GetProductStock(product) >= Mathf.Max(1, quantity);
    }

    public bool TryUseProductStock(Recipe product, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        int requestedQuantity = Mathf.Max(1, quantity);

        if (inv == null)
            return true;

        if (!HasProductStock(product, requestedQuantity))
            return false;

        if (product.ingredients == null)
            return true;

        // Availability is checked for every ingredient before anything is deducted,
        // so a partial recipe can never consume stock.
        for (int i = 0; i < product.ingredients.Count; i++)
        {
            RecipeIngredient requirement = product.ingredients[i];
            if (requirement == null || requirement.item == null || requirement.amount <= 0)
                continue;

            ItemType itemType = requirement.item.itemType;
            if (!inv.IsTracked(itemType))
                continue;

            int amount = requirement.amount * requestedQuantity;
            if (!inv.UseStock(itemType, amount))
            {
                Debug.LogError($"[LobbyStockBridge] Stock changed while consuming {product.DisplayName}.");
                return false;
            }
        }

        return true;
    }

    public int GetProductStock(Recipe product)
    {
        if (product == null || !product.availableOnMenu)
            return 0;

        var inv = InventoryManager.Instance;
        if (inv == null)
            return 999;

        if (product.ingredients == null || product.ingredients.Count == 0)
            return 999;

        int available = int.MaxValue;
        bool hasTrackedIngredient = false;

        for (int i = 0; i < product.ingredients.Count; i++)
        {
            RecipeIngredient requirement = product.ingredients[i];
            if (requirement == null || requirement.item == null || requirement.amount <= 0)
                continue;

            ItemType itemType = requirement.item.itemType;
            if (!inv.IsTracked(itemType))
                continue;

            hasTrackedIngredient = true;
            int ingredientAvailability = inv.GetStock(itemType) / requirement.amount;
            available = Mathf.Min(available, ingredientAvailability);
        }

        return hasTrackedIngredient ? Mathf.Max(0, available) : 999;
    }

    public bool HasOrderStock(IReadOnlyList<Recipe> products, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
            return true;

        Dictionary<ItemType, int> requirements = BuildOrderRequirements(products, quantity);
        foreach (KeyValuePair<ItemType, int> requirement in requirements)
        {
            if (inv.GetStock(requirement.Key) < requirement.Value)
                return false;
        }

        return true;
    }

    public bool TryUseOrderStock(IReadOnlyList<Recipe> products, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
            return true;

        Dictionary<ItemType, int> requirements = BuildOrderRequirements(products, quantity);
        foreach (KeyValuePair<ItemType, int> requirement in requirements)
        {
            if (inv.GetStock(requirement.Key) < requirement.Value)
                return false;
        }

        foreach (KeyValuePair<ItemType, int> requirement in requirements)
        {
            if (!inv.UseStock(requirement.Key, requirement.Value))
            {
                Debug.LogError($"[LobbyStockBridge] Stock changed while consuming order ingredient {requirement.Key}.");
                return false;
            }
        }

        return true;
    }

    private static Dictionary<ItemType, int> BuildOrderRequirements(
        IReadOnlyList<Recipe> products,
        int quantity)
    {
        Dictionary<ItemType, int> requirements = new Dictionary<ItemType, int>();
        var inv = InventoryManager.Instance;

        if (products == null || inv == null)
            return requirements;

        int multiplier = Mathf.Max(1, quantity);
        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product == null || product.ingredients == null)
                continue;

            for (int r = 0; r < product.ingredients.Count; r++)
            {
                RecipeIngredient ingredient = product.ingredients[r];
                if (ingredient == null || ingredient.item == null || ingredient.amount <= 0)
                    continue;

                ItemType itemType = ingredient.item.itemType;
                if (!inv.IsTracked(itemType))
                    continue;

                requirements.TryGetValue(itemType, out int current);
                requirements[itemType] = current + ingredient.amount * multiplier;
            }
        }

        return requirements;
    }

    private static Recipe GetLegacyFoodProduct(CustomerGroup.FoodType foodType)
    {
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null) return null;

        switch (foodType)
        {
            case CustomerGroup.FoodType.Chicken: return catalog.FindByKitchenItem(ItemTypeKitchen.Chicken);
            case CustomerGroup.FoodType.Fries:   return catalog.FindByKitchenItem(ItemTypeKitchen.Fries);
            case CustomerGroup.FoodType.Burger:  return catalog.FindByKitchenItem(ItemTypeKitchen.Burger);
            default:                             return null;
        }
    }

    private static Recipe GetLegacyDrinkProduct(CustomerGroup.DrinkType drinkType)
    {
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null) return null;

        switch (drinkType)
        {
            case CustomerGroup.DrinkType.Coke:      return catalog.FindByKitchenItem(ItemTypeKitchen.Coke);
            case CustomerGroup.DrinkType.Pineapple: return catalog.FindByKitchenItem(ItemTypeKitchen.Pineapple);
            case CustomerGroup.DrinkType.IceTea:    return catalog.FindByKitchenItem(ItemTypeKitchen.IcedTea);
            default:                                return null;
        }
    }
}
