using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RestaurantType
{
    FastFood,
    CasualDining,
    FineDining
}

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
                total += products[i].EffectiveSellPrice;
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

    [Header("Restaurant")]
    [SerializeField] private RestaurantType restaurantType = RestaurantType.FastFood;
    [Tooltip("Scene names that use this catalog. An explicit restaurant selection can override this mapping.")]
    [SerializeField] private List<string> sceneNames = new List<string>();

    [Header("Catalog")]
    [SerializeField] private List<Recipe> products = new List<Recipe>();
    [SerializeField] private List<MenuBundle> foodBundles = new List<MenuBundle>();
    [SerializeField] private List<ItemData> ingredients = new List<ItemData>();

    private static MenuCatalog cachedDefault;
    private static string cachedSceneName;
    private static bool hasRestaurantOverride;
    private static RestaurantType restaurantOverride;
    private Dictionary<string, Recipe> byId;
    private Dictionary<string, Recipe> byDisplayName;

    public RestaurantType RestaurantType => restaurantType;
    public IReadOnlyList<Recipe> Products => products;
    public IReadOnlyList<MenuBundle> FoodBundles => foodBundles;
    public IReadOnlyList<ItemData> Ingredients => ingredients;

    public static MenuCatalog Default
    {
        get
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (cachedDefault == null ||
                (!hasRestaurantOverride && !string.Equals(cachedSceneName, sceneName, StringComparison.Ordinal)))
            {
                cachedDefault = ResolveActiveCatalog(sceneName);
                cachedSceneName = sceneName;
            }

            return cachedDefault;
        }
    }

    // Additive storage scenes belong to their loaded restaurant, not the fallback catalog.
    // Preserve the isolated tutorial catalog while its explicit override is active.
    public static MenuCatalog ForScene(string sceneName) =>
        hasRestaurantOverride ? Default : ResolveActiveCatalog(sceneName);

    public static void SetActiveRestaurantType(RestaurantType type)
    {
        restaurantOverride = type;
        hasRestaurantOverride = true;
        ClearCachedDefault();
    }

    public static void ClearActiveRestaurantOverride()
    {
        hasRestaurantOverride = false;
        ClearCachedDefault();
    }

    public static void ClearCachedDefault()
    {
        cachedDefault = null;
        cachedSceneName = null;
    }

    private static MenuCatalog ResolveActiveCatalog(string sceneName)
    {
        MenuCatalog[] catalogs = Resources.LoadAll<MenuCatalog>(string.Empty);
        if (hasRestaurantOverride)
        {
            for (int i = 0; i < catalogs.Length; i++)
                if (catalogs[i] != null && catalogs[i].restaurantType == restaurantOverride)
                    return catalogs[i];
        }
        else if (!string.IsNullOrWhiteSpace(sceneName))
        {
            for (int i = 0; i < catalogs.Length; i++)
                if (catalogs[i] != null && catalogs[i].MatchesScene(sceneName))
                    return catalogs[i];
        }

        return Resources.Load<MenuCatalog>(ResourcesPath);
    }

    private bool MatchesScene(string sceneName)
    {
        if (sceneNames == null || string.IsNullOrWhiteSpace(sceneName))
            return false;

        for (int i = 0; i < sceneNames.Count; i++)
            if (string.Equals(sceneNames[i]?.Trim(), sceneName, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
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
            if (!MenuAvailabilityManager.IsProductAvailable(product) || product.category != category)
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
            if (!MenuAvailabilityManager.IsBundleAvailable(bundle))
                continue;

            bool valid = true;
            for (int p = 0; p < bundle.products.Count; p++)
            {
                Recipe product = bundle.products[p];
                if (!MenuAvailabilityManager.IsProductAvailable(product) ||
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
            if (MenuAvailabilityManager.IsBundleAvailable(bundle) && bundle.ContainsSameProducts(foodProducts))
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
                total += product.EffectiveSellPrice;
        }

        MenuBundle bundle = FindBundle(foods);
        if (bundle != null)
            total += bundle.GetPrice();
        else
        {
            for (int i = 0; i < foods.Count; i++)
                total += foods[i].EffectiveSellPrice;
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

            if (product.restaurantType != restaurantType)
                Debug.LogError($"[MenuCatalog] Product '{product.name}' belongs to {product.restaurantType}, not {restaurantType}.", this);
        }

        HashSet<string> ingredientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ingredients.Count; i++)
        {
            ItemData ingredient = ingredients[i];
            if (ingredient == null)
                continue;

            if (!ingredientIds.Add(ingredient.StableItemId))
                Debug.LogError($"[MenuCatalog] Duplicate ingredient ID '{ingredient.StableItemId}'.", this);
            if (ingredient.restaurantType != restaurantType)
                Debug.LogError($"[MenuCatalog] Ingredient '{ingredient.name}' belongs to {ingredient.restaurantType}, not {restaurantType}.", this);
        }
    }
}
