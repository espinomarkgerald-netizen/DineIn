using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MenuBundle
{
    [Tooltip("Stable unique ID for this bundle offer.")]
    public string bundleId;
    public string displayName;
    public bool availableOnMenu = true;
    public int menuSortOrder;
    public List<Recipe> products = new List<Recipe>();
    [Tooltip("When disabled, the bundle price is the sum of its products.")]
    public bool useCustomPrice = true;
    public int customPrice;

    public int GetPrice()
    {
        if (useCustomPrice)
            return Mathf.Max(0, customPrice);

        int total = 0;
        for (int i = 0; i < products.Count; i++)
        {
            if (products[i] != null)
                total += Mathf.Max(0, products[i].sellPrice);
        }

        return total;
    }

    public bool ContainsSameProducts(IReadOnlyList<Recipe> otherProducts)
    {
        if (otherProducts == null || products.Count != otherProducts.Count)
            return false;

        List<string> expected = new List<string>();
        List<string> actual = new List<string>();

        for (int i = 0; i < products.Count; i++)
        {
            if (products[i] == null || otherProducts[i] == null)
                return false;

            expected.Add(products[i].ProductId);
            actual.Add(otherProducts[i].ProductId);
        }

        expected.Sort(StringComparer.Ordinal);
        actual.Sort(StringComparer.Ordinal);

        for (int i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

[CreateAssetMenu(fileName = "MenuCatalog", menuName = "Game/Menu Catalog")]
public class MenuCatalog : ScriptableObject
{
    private const string ResourcesPath = "MenuCatalog";

    [SerializeField] private List<Recipe> products = new List<Recipe>();
    [SerializeField] private List<MenuBundle> foodBundles = new List<MenuBundle>();

    private static MenuCatalog cachedDefault;
    private Dictionary<string, Recipe> byId;
    private Dictionary<string, Recipe> byDisplayName;

    public IReadOnlyList<Recipe> Products => products;
    public IReadOnlyList<MenuBundle> FoodBundles => foodBundles;

    public static MenuCatalog Default
    {
        get
        {
            if (cachedDefault == null)
                cachedDefault = Resources.Load<MenuCatalog>(ResourcesPath);

            return cachedDefault;
        }
    }

    public static void ClearCachedDefault()
    {
        cachedDefault = null;
    }

    public Recipe FindProduct(string idOrDisplayName)
    {
        if (string.IsNullOrWhiteSpace(idOrDisplayName))
            return null;

        EnsureLookup();
        string key = idOrDisplayName.Trim();

        if (byId.TryGetValue(key, out Recipe byProductId))
            return byProductId;

        byDisplayName.TryGetValue(key, out Recipe byName);
        return byName;
    }

    public Recipe FindByKitchenItem(ItemTypeKitchen itemType)
    {
        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product != null && product.kitchenItemType == itemType)
                return product;
        }

        return null;
    }

    public List<Recipe> GetProducts(MenuProductCategory category, bool requireUnlocked = true)
    {
        List<Recipe> result = new List<Recipe>();

        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product == null || !product.availableOnMenu || product.category != category)
                continue;

            if (requireUnlocked && !product.IsUnlocked)
                continue;

            result.Add(product);
        }

        result.Sort((a, b) => a.menuSortOrder.CompareTo(b.menuSortOrder));
        return result;
    }

    public List<MenuBundle> GetFoodBundles(bool requireUnlocked = true)
    {
        List<MenuBundle> result = new List<MenuBundle>();

        for (int i = 0; i < foodBundles.Count; i++)
        {
            MenuBundle bundle = foodBundles[i];
            if (bundle == null || !bundle.availableOnMenu || bundle.products.Count == 0)
                continue;

            bool valid = true;
            for (int p = 0; p < bundle.products.Count; p++)
            {
                Recipe product = bundle.products[p];
                if (product == null || !product.availableOnMenu ||
                    product.category != MenuProductCategory.Food ||
                    (requireUnlocked && !product.IsUnlocked))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                result.Add(bundle);
        }

        result.Sort((a, b) => a.menuSortOrder.CompareTo(b.menuSortOrder));
        return result;
    }

    public MenuBundle FindBundle(IReadOnlyList<Recipe> foodProducts)
    {
        for (int i = 0; i < foodBundles.Count; i++)
        {
            MenuBundle bundle = foodBundles[i];
            if (bundle != null && bundle.availableOnMenu && bundle.ContainsSameProducts(foodProducts))
                return bundle;
        }

        return null;
    }

    public MenuBundle FindBundle(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
            return null;

        for (int i = 0; i < foodBundles.Count; i++)
        {
            MenuBundle bundle = foodBundles[i];
            if (bundle != null &&
                string.Equals(bundle.bundleId, bundleId, StringComparison.OrdinalIgnoreCase))
                return bundle;
        }

        return null;
    }

    public int GetOrderTotal(IReadOnlyList<string> productIdsOrNames)
    {
        List<Recipe> resolved = ResolveProducts(productIdsOrNames);
        List<Recipe> foods = new List<Recipe>();
        int total = 0;

        for (int i = 0; i < resolved.Count; i++)
        {
            Recipe product = resolved[i];
            if (product.category == MenuProductCategory.Food)
                foods.Add(product);
            else
                total += Mathf.Max(0, product.sellPrice);
        }

        MenuBundle bundle = FindBundle(foods);
        if (bundle != null)
            total += bundle.GetPrice();
        else
        {
            for (int i = 0; i < foods.Count; i++)
                total += Mathf.Max(0, foods[i].sellPrice);
        }

        return total;
    }

    public List<Recipe> ResolveProducts(IReadOnlyList<string> productIdsOrNames)
    {
        List<Recipe> result = new List<Recipe>();
        if (productIdsOrNames == null)
            return result;

        for (int i = 0; i < productIdsOrNames.Count; i++)
        {
            Recipe product = FindProduct(productIdsOrNames[i]);
            if (product != null)
                result.Add(product);
        }

        return result;
    }

    public List<string> GetProductIds(IReadOnlyList<Recipe> source)
    {
        List<string> result = new List<string>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i].ProductId);
        }

        return result;
    }

    public List<string> GetDisplayNames(IReadOnlyList<Recipe> source)
    {
        List<string> result = new List<string>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i].DisplayName);
        }

        return result;
    }

    private void EnsureLookup()
    {
        if (byId != null && byDisplayName != null)
            return;

        byId = new Dictionary<string, Recipe>(StringComparer.OrdinalIgnoreCase);
        byDisplayName = new Dictionary<string, Recipe>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product == null)
                continue;

            if (!string.IsNullOrWhiteSpace(product.ProductId))
                byId[product.ProductId] = product;

            if (!string.IsNullOrWhiteSpace(product.DisplayName))
                byDisplayName[product.DisplayName] = product;
        }
    }

    private void OnEnable()
    {
        byId = null;
        byDisplayName = null;
    }

    private void OnValidate()
    {
        byId = null;
        byDisplayName = null;

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product == null)
                continue;

            if (string.IsNullOrWhiteSpace(product.ProductId))
                Debug.LogError($"[MenuCatalog] Product '{product.name}' is missing a stable ID.", this);
            else if (!ids.Add(product.ProductId))
                Debug.LogError($"[MenuCatalog] Duplicate product ID '{product.ProductId}'.", this);
        }
    }
}
