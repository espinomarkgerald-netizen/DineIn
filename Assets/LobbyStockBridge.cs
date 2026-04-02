using UnityEngine;

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
        if (InventoryManager.Instance == null)
            return false;

        switch (foodType)
        {
            case CustomerGroup.FoodType.Chicken:
                return InventoryManager.Instance.GetStock(ItemType.Drumsticks) > 0;

            case CustomerGroup.FoodType.Fries:
                return InventoryManager.Instance.GetStock(ItemType.FrenchFryBag) > 0;

            case CustomerGroup.FoodType.Burger:
                return HasBurgerIngredients();
        }

        return false;
    }

    public bool HasDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        if (InventoryManager.Instance == null) return true;

        ItemType item = DrinkTypeToItemType(drinkType);
        if (!InventoryManager.Instance.IsTracked(item)) return true;
        return InventoryManager.Instance.GetStock(item) > 0;
    }

    public bool TryUseFoodStock(CustomerGroup.FoodType foodType)
    {
        if (InventoryManager.Instance == null)
            return false;

        switch (foodType)
        {
            case CustomerGroup.FoodType.Chicken:
                return InventoryManager.Instance.UseStock(ItemType.Drumsticks, 1);

            case CustomerGroup.FoodType.Fries:
                return InventoryManager.Instance.UseStock(ItemType.FrenchFryBag, 1);

            case CustomerGroup.FoodType.Burger:
                return TryUseBurgerIngredients();
        }

        return false;
    }

    public bool TryUseDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        if (InventoryManager.Instance == null) return true;

        ItemType item = DrinkTypeToItemType(drinkType);
        if (!InventoryManager.Instance.IsTracked(item)) return true;
        if (InventoryManager.Instance.GetStock(item) <= 0) return false;

        return InventoryManager.Instance.UseStock(item, 1);
    }

    public int GetFoodStock(CustomerGroup.FoodType foodType)
    {
        if (InventoryManager.Instance == null)
            return 0;

        switch (foodType)
        {
            case CustomerGroup.FoodType.Chicken:
                return InventoryManager.Instance.GetStock(ItemType.Drumsticks);

            case CustomerGroup.FoodType.Fries:
                return InventoryManager.Instance.GetStock(ItemType.FrenchFryBag);

            case CustomerGroup.FoodType.Burger:
                return GetBurgerAvailableCount();
        }

        return 0;
    }

    public int GetDrinkStock(CustomerGroup.DrinkType drinkType)
    {
        if (InventoryManager.Instance == null) return 999;

        ItemType item = DrinkTypeToItemType(drinkType);
        if (!InventoryManager.Instance.IsTracked(item)) return 999;
        return InventoryManager.Instance.GetStock(item);
    }

    private static ItemType DrinkTypeToItemType(CustomerGroup.DrinkType drinkType)
    {
        switch (drinkType)
        {
            case CustomerGroup.DrinkType.Coke:      return ItemType.Coke;
            case CustomerGroup.DrinkType.Pineapple: return ItemType.Pineapple;
            case CustomerGroup.DrinkType.IceTea:    return ItemType.IcedTea;
            default:                                return ItemType.Coke;
        }
    }

    private bool HasBurgerIngredients()
    {
        var inv = InventoryManager.Instance;

        if (inv == null)
            return false;

        return inv.GetStock(ItemType.Bun) > 0
            && inv.GetStock(ItemType.Patty) > 0
            && inv.GetStock(ItemType.Cheese) > 0;
    }

    private bool TryUseBurgerIngredients()
    {
        var inv = InventoryManager.Instance;

        if (inv == null)
            return false;

        if (inv.GetStock(ItemType.Bun) <= 0) return false;
        if (inv.GetStock(ItemType.Patty) <= 0) return false;
        if (inv.GetStock(ItemType.Cheese) <= 0) return false;

        inv.UseStock(ItemType.Bun, 1);
        inv.UseStock(ItemType.Patty, 1);
        inv.UseStock(ItemType.Cheese, 1);

        return true;
    }

    private int GetBurgerAvailableCount()
    {
        var inv = InventoryManager.Instance;

        if (inv == null)
            return 0;

        int bun = inv.GetStock(ItemType.Bun);
        int patty = inv.GetStock(ItemType.Patty);
        int cheese = inv.GetStock(ItemType.Cheese);

        return Mathf.Min(bun, patty, cheese);
    }
}