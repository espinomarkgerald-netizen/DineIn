using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Scene-local Tutorial Day catalog. It clones unlock-gated data in memory so every
/// Management lesson is usable without advancing the campaign day or editing assets.
/// </summary>
[DefaultExecutionOrder(-8998)]
[DisallowMultipleComponent]
public sealed class TutorialDayContext : MonoBehaviour
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private readonly List<Object> runtimeClones = new List<Object>();
    private MenuCatalog authoredCatalog;
    private MenuCatalog tutorialCatalog;
    private List<ItemData> authoredInventoryItems;
    private List<Equipment> authoredEquipment;

    private void Awake()
    {
        authoredCatalog = MenuCatalog.Default;
        if (authoredCatalog == null)
        {
            Debug.LogError("[Tutorial Day] Casual Dining catalog is unavailable.", this);
            return;
        }

        tutorialCatalog = BuildRuntimeCatalog(authoredCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedDefault", tutorialCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedSceneName", gameObject.scene.name);
    }

    private void Start()
    {
        // Managers may be persistent Bootstrap objects or scene objects whose Awake ran
        // after this component. Swap only their data references; their progression state,
        // wallet, stock counts, purchases, and save data remain untouched.
        if (InventoryManager.Instance != null && tutorialCatalog != null)
        {
            authoredInventoryItems = authoredCatalog != null
                ? new List<ItemData>(authoredCatalog.Ingredients)
                : new List<ItemData>(InventoryManager.Instance.Items);
            SetInstanceField(InventoryManager.Instance, "items",
                new List<ItemData>(tutorialCatalog.Ingredients));
        }

        EquipmentManager equipment = EquipmentManager.Instance;
        if (equipment != null && equipment.AllEquipment != null)
        {
            authoredEquipment = new List<Equipment>(equipment.AllEquipment);
            List<Equipment> tutorialEquipment = new List<Equipment>();
            foreach (Equipment source in authoredEquipment)
            {
                if (source == null) continue;
                Equipment clone = Instantiate(source);
                clone.name = source.name + " (Tutorial Day)";
                clone.hideFlags = HideFlags.DontSave;
                clone.dayToUnlock = 1;
                runtimeClones.Add(clone);
                tutorialEquipment.Add(clone);
            }
            equipment.Configure(tutorialEquipment);
        }
    }

    private MenuCatalog BuildRuntimeCatalog(MenuCatalog source)
    {
        Dictionary<ItemData, ItemData> items = new Dictionary<ItemData, ItemData>();
        List<ItemData> clonedItems = new List<ItemData>();
        foreach (ItemData item in source.Ingredients)
        {
            if (item == null) continue;
            ItemData clone = Instantiate(item);
            clone.name = item.name + " (Tutorial Day)";
            clone.hideFlags = HideFlags.DontSave;
            clone.dayToUnlock = 1;
            runtimeClones.Add(clone);
            items[item] = clone;
            clonedItems.Add(clone);
        }

        Dictionary<Recipe, Recipe> recipes = new Dictionary<Recipe, Recipe>();
        List<Recipe> clonedRecipes = new List<Recipe>();
        foreach (Recipe recipe in source.Products)
        {
            if (recipe == null) continue;
            Recipe clone = Instantiate(recipe);
            clone.name = recipe.name + " (Tutorial Day)";
            clone.hideFlags = HideFlags.DontSave;
            clone.dayToUnlock = 1;
            clone.ingredients = new List<RecipeIngredient>();
            if (recipe.ingredients != null)
            {
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                {
                    if (ingredient == null) continue;
                    clone.ingredients.Add(new RecipeIngredient
                    {
                        item = ingredient.item != null && items.TryGetValue(ingredient.item, out ItemData mapped)
                            ? mapped : ingredient.item,
                        amount = ingredient.amount
                    });
                }
            }
            runtimeClones.Add(clone);
            recipes[recipe] = clone;
            clonedRecipes.Add(clone);
        }

        List<MenuBundle> bundles = new List<MenuBundle>();
        foreach (MenuBundle sourceBundle in source.FoodBundles)
        {
            if (sourceBundle == null) continue;
            MenuBundle bundle = new MenuBundle
            {
                bundleId = sourceBundle.bundleId,
                displayName = sourceBundle.displayName,
                availableOnMenu = sourceBundle.availableOnMenu,
                menuSortOrder = sourceBundle.menuSortOrder,
                useCustomPrice = sourceBundle.useCustomPrice,
                customPrice = sourceBundle.customPrice,
                products = new List<Recipe>()
            };
            if (sourceBundle.products != null)
                foreach (Recipe product in sourceBundle.products)
                    if (product != null)
                        bundle.products.Add(recipes.TryGetValue(product, out Recipe mapped) ? mapped : product);
            bundles.Add(bundle);
        }

        MenuCatalog cloneCatalog = Instantiate(source);
        cloneCatalog.name = source.name + " (Tutorial Day)";
        cloneCatalog.hideFlags = HideFlags.DontSave;
        SetInstanceField(cloneCatalog, "products", clonedRecipes);
        SetInstanceField(cloneCatalog, "ingredients", clonedItems);
        SetInstanceField(cloneCatalog, "foodBundles", bundles);
        SetInstanceField(cloneCatalog, "byId", null);
        SetInstanceField(cloneCatalog, "byDisplayName", null);
        runtimeClones.Add(cloneCatalog);
        return cloneCatalog;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null && authoredInventoryItems != null)
            SetInstanceField(InventoryManager.Instance, "items", authoredInventoryItems);
        if (EquipmentManager.Instance != null && authoredEquipment != null)
            EquipmentManager.Instance.Configure(authoredEquipment);
        if (authoredCatalog != null)
            SetStaticField(typeof(MenuCatalog), "cachedDefault", authoredCatalog);

        foreach (Object clone in runtimeClones)
            if (clone != null) Destroy(clone);
        runtimeClones.Clear();
    }

    private static void SetInstanceField(object owner, string name, object value)
    {
        FieldInfo field = owner?.GetType().GetField(name, InstancePrivate);
        if (field == null)
            Debug.LogError("[Tutorial Day] Missing runtime field " + name + ".");
        else
            field.SetValue(owner, value);
    }

    private static void SetStaticField(System.Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, StaticPrivate);
        if (field == null)
            Debug.LogError("[Tutorial Day] Missing runtime field " + name + ".");
        else
            field.SetValue(null, value);
    }
}
