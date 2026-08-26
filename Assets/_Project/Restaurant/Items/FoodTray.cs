using System.Collections.Generic;
using UnityEngine;

public class FoodTray : MonoBehaviour
{
    [Header("Runtime")]
    public int orderNumber;
    private CustomerGroup targetGroup;

    [Header("Order Data")]
    public string orderName;

    [Header("Delivered Data")]
    [SerializeField] private List<string> deliveredProductIds = new List<string>();
    [Tooltip("Editable quality flag used by the Manager complaint system. Kitchen stations may set this when a burnt serving reaches the tray.")]
    [SerializeField] private bool containsBurntFood;
    [SerializeField] private CustomerGroup.FoodType deliveredFood1;
    [SerializeField] private CustomerGroup.FoodType deliveredFood2;
    [SerializeField] private CustomerGroup.DrinkType deliveredDrink;

    [Header("Anchors")]
    [SerializeField] private Transform foodAnchor1;
    [SerializeField] private Transform foodAnchor2;
    [SerializeField] private Transform drinkAnchor;

    [Header("Food Prefabs")]
    [SerializeField] private GameObject chickenPrefab;
    [SerializeField] private GameObject friesPrefab;
    [SerializeField] private GameObject burgerPrefab;

    [Header("Drink Prefabs")]
    [SerializeField] private GameObject cokePrefab;
    [SerializeField] private GameObject pineapplePrefab;
    [SerializeField] private GameObject iceTeaPrefab;

    [Header("Order Number UI")]
    [SerializeField] private TableNumberUI numberUi;

    private GameObject spawnedFood1;
    private GameObject spawnedFood2;
    private GameObject spawnedDrink;

    private bool hasFood2;
    private bool hasDrink;

    public CustomerGroup TargetGroup => targetGroup;
    public bool ContainsBurntFood => containsBurntFood;

    public void SetContainsBurntFood(bool burnt)
    {
        containsBurntFood = burnt;
    }

    public bool TryGetFoodBitePosition(int dinerIndex, int biteIndex, out Vector3 position)
    {
        GameObject first = spawnedFood1;
        GameObject second = spawnedFood2;
        int foodCount = (first != null ? 1 : 0) + (second != null ? 1 : 0);

        if (foodCount > 0)
        {
            int choice = ((dinerIndex + biteIndex) % foodCount + foodCount) % foodCount;
            GameObject food = first != null && (choice == 0 || second == null)
                ? first
                : second;

            if (food != null)
            {
                Renderer[] renderers = food.GetComponentsInChildren<Renderer>(true);
                bool hasBounds = false;
                Bounds combinedBounds = default;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer foodRenderer = renderers[i];
                    if (foodRenderer == null) continue;

                    if (!hasBounds)
                    {
                        combinedBounds = foodRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(foodRenderer.bounds);
                    }
                }

                if (hasBounds)
                {
                    position = combinedBounds.center +
                               Vector3.up * (combinedBounds.extents.y * 0.45f);
                    return true;
                }

                position = food.transform.position;
                return true;
            }
        }

        Transform fallbackAnchor = foodAnchor1 != null ? foodAnchor1 : foodAnchor2;
        if (fallbackAnchor != null)
        {
            position = fallbackAnchor.position;
            return true;
        }

        position = default;
        return false;
    }

    // ✅ NEW: unified delivered contents
    public List<string> DeliveredContents
    {
        get
        {
            MenuCatalog catalog = MenuCatalog.Default;
            if (catalog != null && deliveredProductIds.Count > 0)
            {
                List<Recipe> products = catalog.ResolveProducts(deliveredProductIds);
                return catalog.GetDisplayNames(products);
            }

            List<string> list = new List<string> { deliveredFood1.ToString() };
            if (hasFood2) list.Add(deliveredFood2.ToString());
            if (hasDrink) list.Add(deliveredDrink.ToString());
            return list;
        }
    }

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        hasFood2 = false;
        hasDrink = false;
        containsBurntFood = false;
        deliveredProductIds.Clear();
        deliveredFood1 = CustomerGroup.FoodType.Chicken;
        deliveredFood2 = CustomerGroup.FoodType.Chicken;
        deliveredDrink = CustomerGroup.DrinkType.Coke;

        if (group != null)
        {
            if (group.submittedOrder != null && group.submittedOrder.contents != null && group.submittedOrder.contents.Count > 0)
            {
                orderName = group.submittedOrder.name;
                SetDeliveredProducts(group.submittedOrder.ResolveProducts());
            }
            else if (group.currentOrder != null)
            {
                orderName = group.currentOrder.name;
                SetDeliveredProducts(group.currentOrder.ResolveProducts());
            }
            else
            {
                deliveredFood1 = group.confirmedFood;
                deliveredDrink = group.confirmedDrink;
                hasDrink = true;
                AddLegacyConfirmedProducts(group.confirmedFood, group.confirmedDrink);
            }
        }

        if (numberUi == null)
            numberUi = GetComponentInChildren<TableNumberUI>(true);

        if (numberUi != null)
            numberUi.SetNumber(orderNumber);

        SpawnVisuals();
    }

    private void SetDeliveredProducts(IReadOnlyList<Recipe> products)
    {
        if (products == null || products.Count == 0)
            return;

        int foodCount = 0;
        for (int i = 0; i < products.Count; i++)
        {
            Recipe product = products[i];
            if (product == null) continue;
            deliveredProductIds.Add(product.ProductId);

            if (product.category == MenuProductCategory.Drink)
            {
                deliveredDrink = ToLegacyDrinkType(product);
                hasDrink = true;
                continue;
            }

            if (foodCount == 0)
                deliveredFood1 = ToLegacyFoodType(product);
            else if (foodCount == 1)
            {
                deliveredFood2 = ToLegacyFoodType(product);
                hasFood2 = true;
            }

            foodCount++;
        }
    }

    private void AddLegacyConfirmedProducts(
        CustomerGroup.FoodType food,
        CustomerGroup.DrinkType drink)
    {
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null) return;

        Recipe foodProduct = catalog.FindByKitchenItem(ToKitchenItem(food));
        Recipe drinkProduct = catalog.FindByKitchenItem(ToKitchenItem(drink));
        if (foodProduct != null) deliveredProductIds.Add(foodProduct.ProductId);
        if (drinkProduct != null) deliveredProductIds.Add(drinkProduct.ProductId);
    }

    private void SpawnVisuals()
    {
        if (spawnedFood1 != null) Destroy(spawnedFood1);
        if (spawnedFood2 != null) Destroy(spawnedFood2);
        if (spawnedDrink != null) Destroy(spawnedDrink);

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null && deliveredProductIds.Count > 0)
        {
            List<Recipe> products = catalog.ResolveProducts(deliveredProductIds);
            int foodIndex = 0;

            for (int i = 0; i < products.Count; i++)
            {
                Recipe product = products[i];
                if (product == null || product.servingPrefab == null) continue;

                if (product.category == MenuProductCategory.Drink)
                {
                    if (drinkAnchor != null)
                    {
                        spawnedDrink = Instantiate(product.servingPrefab, drinkAnchor);
                        ResetLocal(spawnedDrink.transform);
                    }
                }
                else if (foodIndex == 0 && foodAnchor1 != null)
                {
                    spawnedFood1 = Instantiate(product.servingPrefab, foodAnchor1);
                    ResetLocal(spawnedFood1.transform);
                    foodIndex++;
                }
                else if (foodIndex == 1 && foodAnchor2 != null)
                {
                    spawnedFood2 = Instantiate(product.servingPrefab, foodAnchor2);
                    ResetLocal(spawnedFood2.transform);
                    foodIndex++;
                }
            }

            return;
        }

        if (foodAnchor1 != null)
        {
            GameObject prefab = GetFoodPrefab(deliveredFood1);
            if (prefab != null)
            {
                spawnedFood1 = Instantiate(prefab, foodAnchor1);
                ResetLocal(spawnedFood1.transform);
            }
        }
        if (hasFood2 && foodAnchor2 != null)
        {
            GameObject prefab = GetFoodPrefab(deliveredFood2);
            if (prefab != null)
            {
                spawnedFood2 = Instantiate(prefab, foodAnchor2);
                ResetLocal(spawnedFood2.transform);
            }
        }

        if (hasDrink && drinkAnchor != null)
        {
            GameObject prefab = GetDrinkPrefab(deliveredDrink);
            if (prefab != null)
            {
                spawnedDrink = Instantiate(prefab, drinkAnchor);
                ResetLocal(spawnedDrink.transform);
            }
        }
    }

    private GameObject GetFoodPrefab(CustomerGroup.FoodType food)
    {
        switch (food)
        {
            case CustomerGroup.FoodType.Chicken: return chickenPrefab;
            case CustomerGroup.FoodType.Fries: return friesPrefab;
            case CustomerGroup.FoodType.Burger: return burgerPrefab;
            default: return null;
        }
    }

    private GameObject GetDrinkPrefab(CustomerGroup.DrinkType drinkType)
    {
        switch (drinkType)
        {
            case CustomerGroup.DrinkType.Coke: return cokePrefab;
            case CustomerGroup.DrinkType.Pineapple: return pineapplePrefab;
            case CustomerGroup.DrinkType.IceTea: return iceTeaPrefab;
            default: return null;
        }
    }

    private static CustomerGroup.FoodType ToLegacyFoodType(Recipe product)
    {
        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Fries:  return CustomerGroup.FoodType.Fries;
            case ItemTypeKitchen.Burger: return CustomerGroup.FoodType.Burger;
            default:                     return CustomerGroup.FoodType.Chicken;
        }
    }

    private static CustomerGroup.DrinkType ToLegacyDrinkType(Recipe product)
    {
        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Pineapple: return CustomerGroup.DrinkType.Pineapple;
            case ItemTypeKitchen.IcedTea:   return CustomerGroup.DrinkType.IceTea;
            default:                        return CustomerGroup.DrinkType.Coke;
        }
    }

    private static ItemTypeKitchen ToKitchenItem(CustomerGroup.FoodType food)
    {
        switch (food)
        {
            case CustomerGroup.FoodType.Fries:  return ItemTypeKitchen.Fries;
            case CustomerGroup.FoodType.Burger: return ItemTypeKitchen.Burger;
            default:                            return ItemTypeKitchen.Chicken;
        }
    }

    private static ItemTypeKitchen ToKitchenItem(CustomerGroup.DrinkType drink)
    {
        switch (drink)
        {
            case CustomerGroup.DrinkType.Pineapple: return ItemTypeKitchen.Pineapple;
            case CustomerGroup.DrinkType.IceTea:    return ItemTypeKitchen.IcedTea;
            default:                                return ItemTypeKitchen.Coke;
        }
    }

    private static void ResetLocal(Transform t)
    {
        Vector3 originalScale = t.localScale;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = originalScale;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null && group.currentOrderNumber == orderNumber;
    }
}
