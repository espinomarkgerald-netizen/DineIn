using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ReviewedOrderSelection
{
    [Serializable] public sealed class Line
    {
        public string itemId;
        public bool isBundle;
        public int quantity;
    }
    public List<Line> lines = new List<Line>();

    public static ReviewedOrderSelection FromLines(IReadOnlyList<CustomerGroup.OrderLine> source)
    {
        var selection = new ReviewedOrderSelection();
        if (source != null)
            foreach (var line in source)
                if (line != null) selection.lines.Add(new Line
                    { itemId = line.itemId, isBundle = line.isBundle, quantity = line.quantity });
        return selection;
    }
}

// One authoritative submission for single-player and multiplayer. The request
// contains menu identity/quantity only; prices and ingredients come from the catalog.
public static class ReviewedOrderSubmission
{
    public enum Failure { None, ChangedOrder, IncorrectSelection, UnavailableProduct, UnavailableStock, UnavailableKitchen }

    public static string Message(Failure failure) => failure switch
    {
        Failure.ChangedOrder => "The customer order changed before it could be confirmed.",
        Failure.IncorrectSelection => "Match every meal, drink, and quantity in the customer's order.",
        Failure.UnavailableProduct => "One or more selected products are no longer on the menu.",
        Failure.UnavailableStock => "One or more products in this order are no longer available.",
        Failure.UnavailableKitchen => "The kitchen could not start this order. Please try again.",
        _ => string.Empty
    };

    public static bool TrySubmit(CustomerGroup group, int expectedOrder, ReviewedOrderSelection selection,
        MenuCatalog catalog, KitchenManager kitchen, out Failure failure)
    {
        failure = Failure.ChangedOrder;
        if (group == null || group.IsNetworkObserver || group.currentOrderNumber != expectedOrder
            || group.state != CustomerGroup.GroupState.ReadyToOrder || !group.IsPlayerReviewingOrder
            || group.HasConfirmedOrder) return false;
        if (!TryBuild(group, selection, catalog, out var submitted, out var products,
            out var food, out var drink, out failure))
        {
            if (failure == Failure.UnavailableProduct) group.HandleGlobalStockout();
            return false;
        }
        failure = Failure.UnavailableKitchen;
        if (!group.IsTakeout && (kitchen == null || !kitchen.CanStartReviewedOrder(group))) return false;

        var stock = LobbyStockBridge.Instance;
        failure = Failure.UnavailableStock;
        if (stock != null && !stock.HasOrderStock(products))
        { group.HandleGlobalStockout(); return false; }
        var previousSubmission = group.submittedOrder;
        bool attempted = false;
        bool committed = false;
        try
        {
            Func<bool> commit = () =>
            {
                attempted = true;
                group.submittedOrder = submitted;
                return group.ConfirmPlayerReviewedOrder(food, drink,
                    acceptedGroup => acceptedGroup.IsTakeout || kitchen.ProcessOrder(acceptedGroup));
            };
            committed = stock == null ? commit() : stock.TryUseOrderStock(products, commit);
        }
        finally
        {
            if (!committed) group.submittedOrder = previousSubmission;
        }
        if (!committed)
        {
            failure = attempted ? Failure.UnavailableKitchen : Failure.UnavailableStock;
            if (!attempted) group.HandleGlobalStockout();
            return false;
        }
        RestaurantTaskClaim.Complete(group);
        group.FastFood?.RequestPaymentAfterReview(group);
        failure = Failure.None;
        return true;
    }

    public static bool TryBuild(CustomerGroup group, ReviewedOrderSelection selection, MenuCatalog catalog,
        out CustomerGroup.SimpleOrder submitted, out List<Recipe> products,
        out CustomerGroup.FoodType food, out CustomerGroup.DrinkType drink, out Failure failure)
    {
        submitted = null;
        products = new List<Recipe>();
        food = CustomerGroup.FoodType.Chicken;
        drink = CustomerGroup.DrinkType.Coke;
        failure = Failure.IncorrectSelection;
        if (group == null || selection?.lines == null || selection.lines.Count == 0 || catalog == null) return false;

        var expected = new Dictionary<string, int>(StringComparer.Ordinal);
        var selected = new Dictionary<string, int>(StringComparer.Ordinal);
        bool hasLines = group.currentOrder?.lines != null && group.currentOrder.lines.Count > 0;
        if (hasLines)
            foreach (var line in group.currentOrder.lines)
                if (line == null || !AddCount(expected, Key(line.isBundle, line.itemId), line.quantity)) return false;
        foreach (var line in selection.lines)
            if (line == null || string.IsNullOrWhiteSpace(line.itemId)
                || !AddCount(selected, Key(line.isBundle, line.itemId), line.quantity)) return false;
        if (hasLines && !EqualCounts(expected, selected)) return false;

        var lines = new List<CustomerGroup.OrderLine>();
        int meals = 0, drinks = 0;
        bool foundFood = false, foundDrink = false;
        foreach (var requested in selection.lines)
        {
            var line = new CustomerGroup.OrderLine();
            var lineProducts = new List<Recipe>();
            if (requested.isBundle)
            {
                var bundle = catalog.FindBundle(requested.itemId);
                if (bundle == null || bundle.bundleId != requested.itemId || !MenuAvailabilityManager.IsBundleAvailable(bundle)
                    || bundle.products == null || bundle.products.Count == 0)
                { failure = Failure.UnavailableProduct; return false; }
                line.SetBundle(bundle, requested.quantity);
                lineProducts.AddRange(bundle.products);
                meals += requested.quantity;
            }
            else
            {
                var product = catalog.FindProduct(requested.itemId);
                if (product == null || product.ProductId != requested.itemId)
                { failure = Failure.UnavailableProduct; return false; }
                line.SetProduct(product, requested.quantity);
                lineProducts.Add(product);
                if (product.category == MenuProductCategory.Drink) drinks += requested.quantity;
                else meals += requested.quantity;
            }
            foreach (var product in lineProducts)
            {
                if (!MenuAvailabilityManager.IsProductAvailable(product))
                { failure = Failure.UnavailableProduct; return false; }
                if (product.category == MenuProductCategory.Drink && !foundDrink)
                {
                    drink = product.kitchenItemType == ItemTypeKitchen.Pineapple ? CustomerGroup.DrinkType.Pineapple
                        : product.kitchenItemType == ItemTypeKitchen.IcedTea ? CustomerGroup.DrinkType.IceTea : CustomerGroup.DrinkType.Coke;
                    foundDrink = true;
                }
                else if (product.category == MenuProductCategory.Food && !foundFood)
                {
                    food = product.kitchenItemType == ItemTypeKitchen.Fries ? CustomerGroup.FoodType.Fries
                        : product.kitchenItemType == ItemTypeKitchen.Burger ? CustomerGroup.FoodType.Burger : CustomerGroup.FoodType.Chicken;
                    foundFood = true;
                }
            }
            lines.Add(line);
            // A generated order's quantities were checked before expanding recipes.
            // Legacy free selections have the same per-group quantity limit as the notepad.
            if (!hasLines && requested.quantity > Mathf.Max(1, group.Size)) return false;
            for (int copy = 0; copy < requested.quantity; copy++) products.AddRange(lineProducts);
        }

        if (!hasLines)
        {
            var legacyProducts = group.currentOrder != null ? group.currentOrder.ResolveProducts(catalog) : new List<Recipe>();
            if (legacyProducts.Count > 0)
            {
                expected.Clear(); selected.Clear();
                foreach (var product in legacyProducts) if (!AddCount(expected, product.ProductId, 1)) return false;
                foreach (var product in products) if (!AddCount(selected, product.ProductId, 1)) return false;
                if (!EqualCounts(expected, selected)) return false;
            }
            else if (meals != Mathf.Max(1, group.Size)
                || (catalog.GetProducts(MenuProductCategory.Drink, false).Count > 0 && drinks != Mathf.Max(1, group.Size))) return false;
        }
        submitted = new CustomerGroup.SimpleOrder();
        submitted.SetLines(lines, catalog);
        failure = Failure.None;
        return products.Count > 0;
    }

    private static string Key(bool bundle, string id) => (bundle ? "bundle:" : "product:") + id?.Trim();
    private static bool AddCount(Dictionary<string, int> values, string key, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(key)) return false;
        values.TryGetValue(key, out int prior);
        if (prior > int.MaxValue - count) return false;
        values[key] = prior + count;
        return true;
    }
    private static bool EqualCounts(Dictionary<string, int> expected, Dictionary<string, int> actual)
    {
        if (expected.Count != actual.Count) return false;
        foreach (var pair in expected)
            if (!actual.TryGetValue(pair.Key, out int quantity) || quantity != pair.Value) return false;
        return true;
    }
}
