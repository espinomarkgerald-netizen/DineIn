using System;
using System.Collections.Generic;
using UnityEngine;

public class LobbyStockBridge : MonoBehaviour
{
    public static LobbyStockBridge Instance { get; private set; }

    private readonly Dictionary<string, int> productStock =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private InventoryManager boundInventory;

    public event Action<Recipe, int> OnProductStockChanged;

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

    private void Start()
    {
        BindInventory();
        RefreshProductStocks(false);
    }

    private void OnDestroy()
    {
        if (boundInventory != null)
            boundInventory.OnStockChanged -= HandleIngredientStockChanged;

        if (Instance == this)
            Instance = null;
    }

    private void BindInventory()
    {
        if (boundInventory == InventoryManager.Instance)
            return;

        if (boundInventory != null)
            boundInventory.OnStockChanged -= HandleIngredientStockChanged;

        boundInventory = InventoryManager.Instance;
        if (boundInventory != null)
            boundInventory.OnStockChanged += HandleIngredientStockChanged;
    }

    private void HandleIngredientStockChanged(ItemType _, int __)
    {
        RefreshProductStocks(true);
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
        if (!MenuAvailabilityManager.IsProductAvailable(product))
            return false;

        return GetProductStock(product) >= Mathf.Max(1, quantity);
    }

    public bool TryUseProductStock(Recipe product, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        int requestedQuantity = Mathf.Max(1, quantity);

        if (inv == null || product == null)
            return false;

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

        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public int GetProductStock(Recipe product)
    {
        if (!MenuAvailabilityManager.IsProductAvailable(product))
            return 0;

        var inv = InventoryManager.Instance;
        if (inv == null || product == null)
            return 0;

        if (product.ingredients == null || product.ingredients.Count == 0)
            return 0;

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

        return hasTrackedIngredient ? Mathf.Max(0, available) : 0;
    }

    public bool HasOrderStock(IReadOnlyList<Recipe> products, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || products == null || products.Count == 0)
            return false;

        Dictionary<ItemType, int> requirements = BuildOrderRequirements(products, quantity);
        if (requirements.Count == 0)
            return false;
        foreach (KeyValuePair<ItemType, int> requirement in requirements)
        {
            if (inv.GetStock(requirement.Key) < requirement.Value)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns how many complete copies of an order can be made from the current
    /// inventory. Shared ingredients are aggregated before the result is
    /// calculated, so bundle availability cannot be overstated.
    /// </summary>
    public int GetOrderStock(IReadOnlyList<Recipe> products)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || products == null || products.Count == 0)
            return 0;

        Dictionary<ItemType, int> requirements = BuildOrderRequirements(products, 1);
        if (requirements.Count == 0)
            return 0;

        int available = int.MaxValue;
        foreach (KeyValuePair<ItemType, int> requirement in requirements)
        {
            if (requirement.Value <= 0)
                continue;

            available = Mathf.Min(
                available,
                inv.GetStock(requirement.Key) / requirement.Value);
        }

        return available == int.MaxValue ? 0 : Mathf.Max(0, available);
    }

    public bool TryUseOrderStock(IReadOnlyList<Recipe> products, int quantity = 1)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || products == null || products.Count == 0)
            return false;

        Dictionary<ItemType, int> requirements = BuildOrderRequirements(products, quantity);
        if (requirements.Count == 0)
            return false;
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

        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    private void RefreshProductStocks(bool notifyOutOfStock)
    {
        BindInventory();
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
            return;

        for (int i = 0; i < catalog.Products.Count; i++)
        {
            Recipe product = catalog.Products[i];
            if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                continue;

            int current = GetProductStock(product);
            bool hadPrevious = productStock.TryGetValue(product.ProductId, out int previous);
            productStock[product.ProductId] = current;

            if (!hadPrevious || previous == current)
                continue;

            OnProductStockChanged?.Invoke(product, current);
            bool applyingSave = GameSaveManager.Instance != null &&
                                GameSaveManager.Instance.IsApplyingSave;
            if (notifyOutOfStock && !applyingSave && previous > 0 && current == 0)
            {
                string message = product.DisplayName +
                    " is out of stock. Restock it from the management computer.";
                WarningSlideUI.Instance?.Show(message);
                Debug.LogWarning("[LobbyStockBridge] " + message, this);
            }
        }
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
