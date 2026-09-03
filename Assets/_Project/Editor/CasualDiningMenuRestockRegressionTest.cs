#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CasualDiningMenuRestockRegressionTest
{
    private const string CatalogPath =
        "Assets/_Project/Resources/CasualDiningMenuCatalog.asset";
    private const string ConfigPath =
        "Assets/_Project/Resources/ManagementComputerCatalogUIConfig.asset";
    private const string CardPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogCard.prefab";

    private static readonly string[] FoodOrder =
    {
        "Tomato Soup", "Caesar Salad", "Pork Chop", "Fried Salmon",
        "Garlic Butter Shrimp", "Roasted Chicken"
    };

    private static readonly int[] FoodDays = { 1, 3, 6, 10, 15, 20 };

    private static readonly string[] DrinkOrder =
    {
        "Iced Tea Pitcher", "Orange Juice Pitcher", "Pineapple Juice Pitcher",
        "Cucumber Lemonade Pitcher", "Mango Juice Pitcher", "Four Seasons Juice Pitcher"
    };

    private static readonly int[] DrinkDays = { 1, 2, 4, 7, 12, 18 };

    private static readonly string[] PowderOrder =
    {
        "Iced Tea Powder", "Orange Juice Powder", "Pineapple Juice Powder",
        "Cucumber Lemonade Powder", "Mango Juice Powder", "Four Seasons Juice Powder"
    };

    [MenuItem("Tools/Dine In/Validate Casual Dining Menu Restock %#&v")]
    public static void Run()
    {
        MenuCatalog catalog = AssetDatabase.LoadAssetAtPath<MenuCatalog>(CatalogPath);
        Assert(catalog != null, "Casual Dining catalog is missing.");
        Assert(catalog.RestaurantType == RestaurantType.CasualDining,
            "The active content catalog is not Casual Dining.");

        ValidateProducts(catalog);
        ValidateIngredientProgression(catalog);
        ValidateCardTextRecovery(catalog);
        ValidateCategoryPanels(catalog);
        ValidatePitcherPipeline(catalog);

        Debug.Log("[CasualDiningMenuRestockRegressionTest] PASS — all 23 Menu, Restock, text recovery, progression, cart, pitcher quantity, shared-order, and compatibility checks passed.");
    }

    private static void ValidateProducts(MenuCatalog catalog)
    {
        List<Recipe> foods = GetProducts(catalog, MenuProductCategory.Food);
        List<Recipe> drinks = GetProducts(catalog, MenuProductCategory.Drink);
        Assert(foods.Count == 6, "Food tab must contain exactly six Casual Dining foods.");
        Assert(drinks.Count == 6, "Drinks tab must contain exactly six pitcher drinks.");
        ValidateProgression(foods, FoodOrder, FoodDays);
        ValidateProgression(drinks, DrinkOrder, DrinkDays);

        for (int i = 1; i < foods.Count; i++)
            Assert(!foods[i].IsUnlocked,
                foods[i].DisplayName + " is usable before its unlock day.");
        for (int i = 1; i < drinks.Count; i++)
            Assert(!drinks[i].IsUnlocked,
                drinks[i].DisplayName + " is usable before its unlock day.");

        MenuCatalog.SetActiveRestaurantType(RestaurantType.CasualDining);
        try
        {
            Assert(MenuCatalog.Default == catalog,
                "Casual Dining does not resolve the shared catalog.");
            List<Recipe> unlockedFoods = catalog.GetProducts(MenuProductCategory.Food);
            List<Recipe> unlockedDrinks = catalog.GetProducts(MenuProductCategory.Drink);
            Assert(unlockedFoods.Count == 1 && unlockedFoods[0].DisplayName == FoodOrder[0],
                "Day-one food ordering includes locked products.");
            Assert(unlockedDrinks.Count == 1 && unlockedDrinks[0].DisplayName == DrinkOrder[0],
                "Day-one drink ordering includes locked products.");
        }
        finally
        {
            MenuCatalog.ClearActiveRestaurantOverride();
        }
    }

    private static void ValidateIngredientProgression(MenuCatalog catalog)
    {
        Dictionary<ItemData, int> earliestDay = new Dictionary<ItemData, int>();
        HashSet<ItemData> foodItems = new HashSet<ItemData>();
        HashSet<ItemData> drinkItems = new HashSet<ItemData>();
        for (int i = 0; i < catalog.Products.Count; i++)
        {
            Recipe product = catalog.Products[i];
            if (product == null || product.ingredients == null)
                continue;
            for (int ingredientIndex = 0; ingredientIndex < product.ingredients.Count; ingredientIndex++)
            {
                ItemData item = product.ingredients[ingredientIndex]?.item;
                if (item == null)
                    continue;
                int day = Mathf.Max(1, product.dayToUnlock);
                if (!earliestDay.TryGetValue(item, out int current) || day < current)
                    earliestDay[item] = day;
                if (product.category == MenuProductCategory.Drink)
                    drinkItems.Add(item);
                else
                    foodItems.Add(item);
            }
        }

        Assert(foodItems.Count == 13, "Food Restock must contain the 13 recipe ingredients.");
        Assert(drinkItems.Count == 6, "Drink Restock must contain the six powders.");
        Assert(foodItems.Count + drinkItems.Count == catalog.Ingredients.Count,
            "Restock contains content outside the Casual Dining recipes.");

        List<ItemData> powders = new List<ItemData>(drinkItems);
        powders.Sort((a, b) => earliestDay[a].CompareTo(earliestDay[b]));
        for (int i = 0; i < PowderOrder.Length; i++)
        {
            Assert(powders[i].displayName == PowderOrder[i],
                "Drink powders are not ordered by pitcher unlock progression.");
            Assert(earliestDay[powders[i]] == DrinkDays[i],
                powders[i].displayName + " has the wrong effective Restock day.");
        }

        foreach (ItemData item in foodItems)
        {
            int expected = int.MaxValue;
            for (int productIndex = 0; productIndex < catalog.Products.Count; productIndex++)
            {
                Recipe product = catalog.Products[productIndex];
                if (product == null || product.category != MenuProductCategory.Food ||
                    product.ingredients == null)
                    continue;
                if (product.ingredients.Exists(requirement => requirement != null && requirement.item == item))
                    expected = Mathf.Min(expected, Mathf.Max(1, product.dayToUnlock));
            }
            Assert(earliestDay[item] == expected,
                item.displayName + " is not keyed to its earliest food recipe unlock day.");
        }
    }

    private static void ValidateCardTextRecovery(MenuCatalog catalog)
    {
        ManagementComputerCatalogCardUI prefab =
            AssetDatabase.LoadAssetAtPath<ManagementComputerCatalogCardUI>(CardPath);
        Assert(prefab != null, "Menu/Restock card prefab is missing.");
        ManagementComputerCatalogCardUI card = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Recipe locked = FindProduct(catalog, "Roasted Chicken");
            TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].enabled = false;
                Color color = texts[i].color;
                color.a = 0f;
                texts[i].color = color;
                texts[i].canvasRenderer.SetAlpha(0f);
            }

            card.BindMenu(locked, false, null);
            AssertVisibleText(card, "titleText");
            AssertVisibleText(card, "metaText");
            AssertVisibleText(card, "statusText");
            AssertVisibleText(card, "priceText");
            TMP_Text status = GetPrivateField<TMP_Text>(card, "statusText");
            Assert(status.text.Contains("LOCKED") && status.text.Contains("20"),
                "Locked cards do not clearly show their unlock day.");

            card.gameObject.SetActive(false);
            card.gameObject.SetActive(true);
            AssertVisibleText(card, "titleText");
            AssertVisibleText(card, "statusText");

            ItemData ingredient = catalog.Ingredients[0];
            TMP_Text stockLabel = GetPrivateField<TMP_Text>(card, "inStockLabelText");
            CanvasGroup parentGroup = stockLabel.transform.parent.gameObject.AddComponent<CanvasGroup>();
            parentGroup.alpha = 0f;
            card.BindRestock(
                ingredient,
                RestockStockProjection.Calculate(ingredient, 40),
                0,
                true,
                true,
                null);
            Assert(parentGroup.alpha > 0.99f,
                "A reused card retained a transparent parent CanvasGroup.");
            AssertVisibleText(card, "inStockLabelText");
            AssertVisibleText(card, "neededTodayLabelText");
            AssertVisibleText(card, "quantityText");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(card.gameObject);
        }
    }

    private static void ValidateCategoryPanels(MenuCatalog catalog)
    {
        ManagementComputerCatalogUIConfig config =
            AssetDatabase.LoadAssetAtPath<ManagementComputerCatalogUIConfig>(ConfigPath);
        Assert(config != null && config.CatalogPanelPrefab != null,
            "Management catalog panel configuration is missing.");

        ManagementComputerCatalogPanelUI menuPanel =
            UnityEngine.Object.Instantiate(config.CatalogPanelPrefab);
        ManagementComputerCatalogPanelUI restockPanel =
            UnityEngine.Object.Instantiate(config.CatalogPanelPrefab);
        GameObject host = new GameObject("Catalog Layout Test Host", typeof(RectTransform));
        RectTransform hostRect = host.transform as RectTransform;
        hostRect.sizeDelta = new Vector2(1600f, 760f);
        menuPanel.transform.SetParent(host.transform, false);
        restockPanel.transform.SetParent(host.transform, false);
        CasualDiningPolishManager existingPolish = CasualDiningPolishManager.Instance;
        try
        {
            menuPanel.BindMenu(catalog.Products, true, null, null);
            Assert(menuPanel.transform.Find("Catalog/CatalogCategoryTabs/FoodTab") != null &&
                   menuPanel.transform.Find("Catalog/CatalogCategoryTabs/DrinksTab") != null,
                "Menu Editor Food/Drinks tabs were not created.");
            AssertActiveMenuCategory(menuPanel, MenuProductCategory.Food, FoodOrder);
            AssertFullHeightLayout(menuPanel, menu: true);
            InvokeCategory(menuPanel, MenuProductCategory.Drink);
            AssertActiveMenuCategory(menuPanel, MenuProductCategory.Drink, DrinkOrder);

            restockPanel.BindRestock(
                catalog.Ingredients,
                config.StorageConfig,
                null,
                40,
                null);
            Assert(restockPanel.transform.Find("Catalog/CatalogCategoryTabs/FoodTab") != null &&
                   restockPanel.transform.Find("Catalog/CatalogCategoryTabs/DrinksTab") != null,
                "Restock Food/Drinks tabs were not created.");
            AssertActiveRestockCount(restockPanel, 13);
            AssertFullHeightLayout(restockPanel, menu: false);

            IDictionary cart = GetPrivateField<IDictionary>(restockPanel, "cart");
            ItemData retainedItem = catalog.Ingredients[0];
            cart[retainedItem] = 2;
            InvokeCategory(restockPanel, MenuProductCategory.Drink);
            Assert(cart.Contains(retainedItem) && (int)cart[retainedItem] == 2,
                "Switching Restock tabs changed the cart.");
            AssertActiveRestockCount(restockPanel, 6);
            AssertActiveRestockOrder(restockPanel, PowderOrder);
            AssertRestockText(restockPanel);
            StressCatalogRefresh(restockPanel);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(menuPanel.gameObject);
            UnityEngine.Object.DestroyImmediate(restockPanel.gameObject);
            UnityEngine.Object.DestroyImmediate(host);
            if (existingPolish == null && CasualDiningPolishManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(CasualDiningPolishManager.Instance.gameObject);
        }
    }

    private static void ValidatePitcherPipeline(MenuCatalog catalog)
    {
        Assert(CustomerGroup.GetCasualDiningPitcherQuantity(1) == 1,
            "A one-person Casual Dining group does not order one pitcher.");
        Assert(CustomerGroup.GetCasualDiningPitcherQuantity(2) == 1,
            "A two-person Casual Dining group does not order one pitcher.");
        Assert(CustomerGroup.GetCasualDiningPitcherQuantity(3) == 2,
            "A three-person Casual Dining group does not order two pitchers.");
        Assert(CustomerGroup.GetCasualDiningPitcherQuantity(4) == 2,
            "A four-person Casual Dining group does not order two pitchers.");

        Recipe drink = FindProduct(catalog, DrinkOrder[0]);
        CustomerGroup.OrderLine line = new CustomerGroup.OrderLine();
        line.SetProduct(drink, 2);
        CustomerGroup.SimpleOrder order = new CustomerGroup.SimpleOrder();
        order.SetLines(new[] { line }, catalog);
        List<Recipe> resolved = order.ResolveProducts(catalog);
        Assert(order.lines.Count == 1 && order.lines[0].quantity == 2,
            "Pitcher quantity was lost from the order line.");
        Assert(resolved.Count == 2 && resolved[0] == drink && resolved[1] == drink,
            "Pitcher quantity did not propagate to preparation/validation contents.");
        Assert(order.unitPrice == drink.EffectiveSellPrice * 2,
            "Pitcher quantity did not propagate to billing.");
        Assert(resolved[0].ingredients[0].item == resolved[1].ingredients[0].item,
            "Pitcher quantity did not preserve powder consumption data.");

        MenuCatalog fastFood = AssetDatabase.LoadAssetAtPath<MenuCatalog>(
            "Assets/_Project/Resources/MenuCatalog.asset");
        Assert(fastFood != null && fastFood.RestaurantType == RestaurantType.FastFood,
            "Fast Food catalog compatibility changed.");
    }

    private static void AssertActiveMenuCategory(
        ManagementComputerCatalogPanelUI panel,
        MenuProductCategory category,
        IReadOnlyList<string> expectedNames)
    {
        IDictionary cards = GetPrivateField<IDictionary>(panel, "menuCards");
        List<ManagementComputerCatalogCardUI> visible = GetVisibleCards(cards);
        Assert(visible.Count == expectedNames.Count,
            category + " tab has the wrong card count.");
        visible.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        for (int i = 0; i < visible.Count; i++)
            Assert(visible[i].BoundProduct != null &&
                   visible[i].BoundProduct.category == category &&
                   visible[i].BoundProduct.DisplayName == expectedNames[i],
                category + " tab is not sorted by unlock progression.");
    }

    private static void AssertActiveRestockCount(ManagementComputerCatalogPanelUI panel, int expected)
    {
        IDictionary cards = GetPrivateField<IDictionary>(panel, "restockCards");
        Assert(GetVisibleCards(cards).Count == expected,
            "Restock category has the wrong ingredient count.");
    }

    private static void AssertActiveRestockOrder(
        ManagementComputerCatalogPanelUI panel,
        IReadOnlyList<string> expectedNames)
    {
        IDictionary cards = GetPrivateField<IDictionary>(panel, "restockCards");
        List<ManagementComputerCatalogCardUI> visible = GetVisibleCards(cards);
        visible.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        for (int i = 0; i < expectedNames.Count; i++)
            Assert(visible[i].BoundItem != null &&
                   visible[i].BoundItem.displayName == expectedNames[i],
                "Restock drink powders are not sorted by pitcher progression.");
    }

    private static void AssertRestockText(ManagementComputerCatalogPanelUI panel)
    {
        IDictionary cards = GetPrivateField<IDictionary>(panel, "restockCards");
        foreach (DictionaryEntry entry in cards)
        {
            ManagementComputerCatalogCardUI card = entry.Value as ManagementComputerCatalogCardUI;
            if (card == null || !card.gameObject.activeSelf)
                continue;
            AssertVisibleText(card, "titleText");
            AssertVisibleText(card, "statusText");
            AssertVisibleText(card, "priceText");
            AssertVisibleText(card, "inStockLabelText");
            AssertVisibleText(card, "neededTodayLabelText");
            AssertVisibleText(card, "quantityText");
        }
    }

    private static void AssertFullHeightLayout(
        ManagementComputerCatalogPanelUI panel,
        bool menu)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel.transform as RectTransform);

        RectTransform root = panel.transform as RectTransform;
        RectTransform tabs = panel.transform.Find("Catalog/CatalogCategoryTabs") as RectTransform;
        ScrollRect catalogScroll = GetPrivateField<ScrollRect>(panel, "catalogScroll");
        Assert(root.rect.height > 700f,
            "Catalog panel did not stretch to the available management-window height.");
        Assert(tabs != null && tabs.rect.width <= 320f && tabs.rect.height <= 54f,
            "Food/Drinks tabs are not compact Finance-style controls.");
        Assert(catalogScroll != null && catalogScroll.viewport.rect.height > 500f,
            "Catalog grid is not using the available vertical space.");

        string path = menu
            ? "DetailsAndCheckout/MenuDetails/IngredientScroll"
            : "DetailsAndCheckout/RestockCart/CartScroll";
        RectTransform rightScroll = panel.transform.Find(path) as RectTransform;
        Assert(rightScroll != null && rightScroll.rect.height > (menu ? 220f : 440f),
            (menu ? "Menu ingredient details" : "Restock cart") +
            " did not expand into the available lower space.");
    }

    private static void StressCatalogRefresh(ManagementComputerCatalogPanelUI panel)
    {
        ScrollRect scroll = GetPrivateField<ScrollRect>(panel, "catalogScroll");
        MethodInfo refresh = typeof(ManagementComputerCatalogPanelUI).GetMethod(
            "RefreshRestockView",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(refresh != null, "Restock refresh handler is missing.");

        for (int cycle = 0; cycle < 12; cycle++)
        {
            panel.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            InvokeCategory(panel, cycle % 2 == 0
                ? MenuProductCategory.Food
                : MenuProductCategory.Drink);
            if (scroll != null)
                scroll.verticalNormalizedPosition = cycle % 3 == 0 ? 0f : 1f;
            refresh.Invoke(panel, null);
            AssertRestockText(panel);
        }
    }

    private static List<ManagementComputerCatalogCardUI> GetVisibleCards(IDictionary cards)
    {
        List<ManagementComputerCatalogCardUI> result = new List<ManagementComputerCatalogCardUI>();
        foreach (DictionaryEntry entry in cards)
        {
            ManagementComputerCatalogCardUI card = entry.Value as ManagementComputerCatalogCardUI;
            if (card != null && card.gameObject.activeSelf)
                result.Add(card);
        }
        return result;
    }

    private static void InvokeCategory(
        ManagementComputerCatalogPanelUI panel,
        MenuProductCategory category)
    {
        MethodInfo method = typeof(ManagementComputerCatalogPanelUI).GetMethod(
            "SetCategory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "Category switch handler is missing.");
        method.Invoke(panel, new object[] { category });
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Missing field " + fieldName + ".");
        return field.GetValue(target) as T;
    }

    private static void AssertVisibleText(
        ManagementComputerCatalogCardUI card,
        string fieldName)
    {
        TMP_Text text = GetPrivateField<TMP_Text>(card, fieldName);
        Assert(text != null && text.enabled && text.gameObject.activeSelf &&
               text.color.a > 0.99f && text.canvasRenderer.GetAlpha() > 0.99f &&
               !text.canvasRenderer.cull &&
               !string.IsNullOrWhiteSpace(text.text),
            fieldName + " was not fully restored during card refresh.");
    }

    private static List<Recipe> GetProducts(MenuCatalog catalog, MenuProductCategory category)
    {
        List<Recipe> result = new List<Recipe>();
        for (int i = 0; i < catalog.Products.Count; i++)
            if (catalog.Products[i] != null && catalog.Products[i].category == category)
                result.Add(catalog.Products[i]);
        result.Sort((a, b) => a.dayToUnlock.CompareTo(b.dayToUnlock));
        return result;
    }

    private static void ValidateProgression(
        IReadOnlyList<Recipe> products,
        IReadOnlyList<string> expectedNames,
        IReadOnlyList<int> expectedDays)
    {
        for (int i = 0; i < expectedNames.Count; i++)
            Assert(products[i].DisplayName == expectedNames[i] &&
                   products[i].dayToUnlock == expectedDays[i],
                expectedNames[i] + " has the wrong progression data.");
    }

    private static Recipe FindProduct(MenuCatalog catalog, string name)
    {
        for (int i = 0; i < catalog.Products.Count; i++)
            if (catalog.Products[i] != null && catalog.Products[i].DisplayName == name)
                return catalog.Products[i];
        throw new InvalidOperationException("Missing product " + name + ".");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
