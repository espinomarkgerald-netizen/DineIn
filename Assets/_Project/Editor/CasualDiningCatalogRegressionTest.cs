#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CasualDiningCatalogRegressionTest
{
    private const string CatalogPath =
        "Assets/_Project/Resources/CasualDiningMenuCatalog.asset";
    private const string MenuIconRoot =
        "Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/";
    private const string IngredientIconRoot =
        "Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/";

    private static readonly HashSet<string> ExpectedProducts =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Tomato Soup",
            "Roasted Chicken",
            "Pork Chop",
            "Garlic Butter Shrimp",
            "Fried Salmon",
            "Caesar Salad"
        };

    [MenuItem("Tools/Dine In/Validate Casual Dining Catalog %#F11")]
    public static void Run()
    {
        Validate();
        Debug.Log("[CasualDiningCatalogRegressionTest] PASS — 6 dishes, 13 ingredients, shared sprites/data, recipes, costs, prices, and restaurant catalog selection are valid.");
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
        Assert(catalog.Products.Count == 6,
            $"Expected 6 Casual Dining dishes, found {catalog.Products.Count}.");
        Assert(catalog.Ingredients.Count == 13,
            $"Expected 13 Casual Dining ingredients, found {catalog.Ingredients.Count}.");
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
            Assert(item.sprite != null &&
                   AssetDatabase.GetAssetPath(item.sprite).StartsWith(
                       IngredientIconRoot, StringComparison.Ordinal),
                $"{item.name} is not using its Casual Dining ingredient sprite.");
            Assert(item.unitsPerBox > 0 && item.boxCost > 0,
                $"{item.name} has invalid units or box cost.");
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
            Assert(product.category == MenuProductCategory.Food,
                $"{product.DisplayName} must be a Casual Dining dish.");
            Assert(product.sprite != null &&
                   AssetDatabase.GetAssetPath(product.sprite).StartsWith(
                       MenuIconRoot, StringComparison.Ordinal),
                $"{product.DisplayName} is not using its Casual Dining menu sprite.");
            Assert(product.servingPrefab == null,
                $"{product.DisplayName} has an unrelated permanent serving model assigned.");
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
            Assert(markup >= 2f && markup <= 4.75f,
                $"{product.DisplayName} has an unreasonable default markup ({markup:0.00}x).");
        }

        Assert(productNames.SetEquals(ExpectedProducts),
            "Casual Dining dish names do not match the supplied Menu icons.");
        Assert(usedIngredients.SetEquals(catalogIngredients),
            "The ingredient catalog contains ingredients unused by the six recipes.");

        ItemData salmon = FindIngredient(catalog, ItemType.SalmonFillet);
        ItemData shrimp = FindIngredient(catalog, ItemType.Shrimp);
        ItemData seasoning = FindIngredient(catalog, ItemType.Seasoning);
        ItemData oil = FindIngredient(catalog, ItemType.CookingOil);
        Assert(salmon.CostPerUnit > seasoning.CostPerUnit &&
               shrimp.CostPerUnit > oil.CostPerUnit,
            "Premium meat/seafood ingredients must cost more per serving than basics.");

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
