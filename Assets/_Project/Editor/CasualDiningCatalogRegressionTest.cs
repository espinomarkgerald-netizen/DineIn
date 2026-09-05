#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CasualDiningCatalogRegressionTest
{
    private const string CatalogPath =
        "Assets/_Project/Resources/CasualDiningMenuCatalog.asset";
    private const string FoodIconRoot =
        "Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/";
    private const string FoodIngredientIconRoot =
        "Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/";
    private const string DrinkIconRoot =
        "Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/";
    private const string PowderIconRoot =
        "Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/";

    private static readonly Dictionary<string, int> ExpectedFoods =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Tomato Soup", 1 },
            { "Caesar Salad", 3 },
            { "Pork Chop", 6 },
            { "Fried Salmon", 10 },
            { "Garlic Butter Shrimp", 15 },
            { "Roasted Chicken", 20 }
        };

    private static readonly Dictionary<string, int> ExpectedDrinks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Iced Tea Pitcher", 1 },
            { "Orange Juice Pitcher", 2 },
            { "Pineapple Juice Pitcher", 4 },
            { "Cucumber Lemonade Pitcher", 7 },
            { "Mango Juice Pitcher", 12 },
            { "Four Seasons Juice Pitcher", 18 }
        };

    private static readonly HashSet<ItemType> ColdIngredients = new HashSet<ItemType>
        {
            ItemType.Butter,
            ItemType.Dressing,
            ItemType.Lemon,
            ItemType.Lettuce,
            ItemType.PorkChop,
            ItemType.SalmonFillet,
            ItemType.Shrimp,
            ItemType.WholeChicken
        };

    private static readonly HashSet<ItemType> CratedIngredients = new HashSet<ItemType>
        {
            ItemType.Garlic,
            ItemType.Lemon,
            ItemType.Lettuce
        };

    [MenuItem("Tools/Dine In/Validate Casual Dining Catalog %#F11")]
    public static void Run()
    {
        Validate();
        Debug.Log("[CasualDiningCatalogRegressionTest] PASS — all 15 Casual Dining food, drink, icon, unlock, recipe, storage, container, price, and shared-catalog checks passed.");
    }

    public static void RunBatch()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Validate()
    {
        MenuCatalog catalog = AssetDatabase.LoadAssetAtPath<MenuCatalog>(CatalogPath);
        Assert(catalog != null, "Casual Dining catalog asset is missing.");
        Assert(catalog.RestaurantType == RestaurantType.CasualDining,
            "Catalog is not marked Casual Dining.");
        int foodCount = 0;
        int drinkCount = 0;
        for (int i = 0; i < catalog.Products.Count; i++)
        {
            if (catalog.Products[i] != null &&
                catalog.Products[i].category == MenuProductCategory.Drink)
                drinkCount++;
            else if (catalog.Products[i] != null)
                foodCount++;
        }
        Assert(foodCount == 6,
            $"Expected 6 Casual Dining foods, found {foodCount}.");
        Assert(drinkCount == 6,
            $"Expected 6 Casual Dining drinks, found {drinkCount}.");
        Assert(catalog.Products.Count == 12,
            $"Expected 12 Casual Dining products, found {catalog.Products.Count}.");
        Assert(catalog.Ingredients.Count == 19,
            $"Expected 19 Casual Dining ingredients, found {catalog.Ingredients.Count}.");
        Assert(catalog.FoodBundles.Count == 0,
            "Casual Dining must not retain Fast Food bundles.");

        HashSet<string> productNames = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> productIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<ItemData> catalogIngredients = new HashSet<ItemData>();
        HashSet<ItemData> usedIngredients = new HashSet<ItemData>();
        HashSet<ItemTypeKitchen> kitchenItems = new HashSet<ItemTypeKitchen>();

        for (int i = 0; i < catalog.Ingredients.Count; i++)
        {
            ItemData item = catalog.Ingredients[i];
            Assert(item != null, $"Ingredient slot {i} is empty.");
            Assert(item.restaurantType == RestaurantType.CasualDining,
                $"{item.name} is assigned to the wrong restaurant.");
            bool powder = (int)item.itemType >= (int)ItemType.IcedTeaPowder;
            string expectedIconRoot = powder ? PowderIconRoot : FoodIngredientIconRoot;
            Assert(item.sprite != null && AssetDatabase.GetAssetPath(item.sprite).StartsWith(
                       expectedIconRoot, StringComparison.Ordinal),
                $"{item.name} is not using its correct Casual Dining " +
                (powder ? "powder" : "food ingredient") + " sprite.");
            Assert(item.unitsPerBox > 0 && item.boxCost > 0,
                $"{item.name} has invalid units or box cost.");
            RestockStorageType expectedStorage = ColdIngredients.Contains(item.itemType)
                ? RestockStorageType.Frozen
                : RestockStorageType.Dry;
            RestockContainerType expectedContainer = CratedIngredients.Contains(item.itemType)
                ? RestockContainerType.Crate
                : RestockContainerType.CardboardBox;
            Assert(item.requiredStorage == expectedStorage,
                $"{item.name} routes to the wrong storage area.");
            Assert(item.containerType == expectedContainer,
                $"{item.name} uses the wrong physical container type.");
            string containerPath = AssetDatabase.GetAssetPath(item.worldContainerPrefab);
            Assert(expectedContainer == RestockContainerType.Crate
                    ? containerPath.EndsWith("/crate.prefab", StringComparison.OrdinalIgnoreCase)
                    : containerPath.EndsWith("/CardboardBox.prefab", StringComparison.OrdinalIgnoreCase),
                $"{item.name} is not linked to the correct box/crate prefab.");
            Assert(catalogIngredients.Add(item),
                $"{item.name} is duplicated in the ingredient catalog.");
        }

        for (int i = 0; i < catalog.Products.Count; i++)
        {
            Recipe product = catalog.Products[i];
            Assert(product != null, $"Product slot {i} is empty.");
            Assert(productNames.Add(product.DisplayName),
                $"Duplicate product name: {product.DisplayName}.");
            Assert(productIds.Add(product.ProductId),
                $"Duplicate product ID: {product.ProductId}.");
            Assert(product.restaurantType == RestaurantType.CasualDining,
                $"{product.DisplayName} is assigned to the wrong restaurant.");
            Dictionary<string, int> expectedProducts =
                product.category == MenuProductCategory.Food
                    ? ExpectedFoods
                    : ExpectedDrinks;
            Assert(expectedProducts.TryGetValue(product.DisplayName, out int unlockDay),
                $"Unexpected Casual Dining product: {product.DisplayName}.");
            Assert(product.dayToUnlock == unlockDay,
                $"{product.DisplayName} unlocks on day {product.dayToUnlock}, expected day {unlockDay}.");
            string expectedProductIconRoot = product.category == MenuProductCategory.Food
                ? FoodIconRoot
                : DrinkIconRoot;
            Assert(product.sprite != null &&
                   AssetDatabase.GetAssetPath(product.sprite).StartsWith(
                       expectedProductIconRoot, StringComparison.Ordinal),
                $"{product.DisplayName} is not using its Casual Dining " +
                (product.category == MenuProductCategory.Food ? "food" : "pitcher") + " sprite.");
            if (product.category == MenuProductCategory.Food)
                Assert(product.servingPrefab != null &&
                       AssetDatabase.GetAssetPath(product.servingPrefab).StartsWith(
                           "Assets/_Project/Art/Models/Foods/Casual Dining/", StringComparison.Ordinal),
                    $"{product.DisplayName} is missing its Casual Dining serving model.");
            Assert(product.ingredients != null && product.ingredients.Count > 0,
                $"{product.DisplayName} has no recipe ingredients.");
            Assert(kitchenItems.Add(product.kitchenItemType),
                $"{product.DisplayName} shares a kitchen identity with another dish.");

            for (int ingredientIndex = 0;
                 ingredientIndex < product.ingredients.Count;
                 ingredientIndex++)
            {
                RecipeIngredient requirement = product.ingredients[ingredientIndex];
                Assert(requirement != null && requirement.item != null &&
                       requirement.amount > 0,
                    $"{product.DisplayName} has an invalid recipe requirement.");
                Assert(catalogIngredients.Contains(requirement.item),
                    $"{product.DisplayName} references an ingredient outside Casual Dining.");
                usedIngredients.Add(requirement.item);
            }

            MenuPriceGuidance guidance = MenuPriceValueService.GetGuidance(product);
            Assert(guidance.CostPerServing > 0f,
                $"{product.DisplayName} has no ingredient cost.");
            Assert(product.sellPrice > guidance.CostPerServing,
                $"{product.DisplayName} default price does not cover ingredients.");
            Assert(product.sellPrice >= guidance.RecommendedMinimum &&
                   product.sellPrice <= guidance.RecommendedMaximum,
                $"{product.DisplayName} default price is outside its suggested range.");
            float markup = product.sellPrice / guidance.CostPerServing;
            float maximumMarkup = product.category == MenuProductCategory.Drink ? 9f : 4.75f;
            Assert(markup >= 2f && markup <= maximumMarkup,
                $"{product.DisplayName} has an unreasonable default markup ({markup:0.00}x).");
        }

        HashSet<string> expectedProductNames = new HashSet<string>(ExpectedFoods.Keys, StringComparer.Ordinal);
        expectedProductNames.UnionWith(ExpectedDrinks.Keys);
        Assert(productNames.SetEquals(expectedProductNames),
            "Casual Dining product names do not match the supplied food and pitcher icons.");
        Assert(usedIngredients.SetEquals(catalogIngredients),
            "The ingredient catalog contains ingredients unused by the twelve recipes.");

        ItemData salmon = FindIngredient(catalog, ItemType.SalmonFillet);
        ItemData shrimp = FindIngredient(catalog, ItemType.Shrimp);
        ItemData seasoning = FindIngredient(catalog, ItemType.Seasoning);
        ItemData oil = FindIngredient(catalog, ItemType.CookingOil);
        Assert(salmon.CostPerUnit > seasoning.CostPerUnit &&
               shrimp.CostPerUnit > oil.CostPerUnit,
            "Premium meat/seafood ingredients must cost more per serving than basics.");

        for (int i = 0; i < catalog.Products.Count; i++)
        {
            Recipe product = catalog.Products[i];
            if (product == null || product.category != MenuProductCategory.Drink)
                continue;

            Assert(product.ingredients.Count == 1 &&
                   (int)product.ingredients[0].item.itemType >= (int)ItemType.IcedTeaPowder,
                $"{product.DisplayName} does not consume its raw drink powder.");
        }

        MenuCatalog.SetActiveRestaurantType(RestaurantType.CasualDining);
        try
        {
            Assert(MenuCatalog.Default == catalog,
                "The active Casual Dining restaurant does not resolve its catalog.");
        }
        finally
        {
            MenuCatalog.ClearActiveRestaurantOverride();
        }
    }

    private static ItemData FindIngredient(MenuCatalog catalog, ItemType itemType)
    {
        for (int i = 0; i < catalog.Ingredients.Count; i++)
            if (catalog.Ingredients[i] != null && catalog.Ingredients[i].itemType == itemType)
                return catalog.Ingredients[i];

        throw new InvalidOperationException($"Missing Casual Dining ingredient {itemType}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
