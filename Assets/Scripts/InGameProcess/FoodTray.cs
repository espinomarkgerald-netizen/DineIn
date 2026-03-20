using UnityEngine;

public class FoodTray : MonoBehaviour
{
    [Header("Runtime")]
    public int orderNumber;
    private CustomerGroup targetGroup;

    [Header("Stored Order Data")]
    [SerializeField] private CustomerGroup.FoodType deliveredFood;
    [SerializeField] private CustomerGroup.DrinkType deliveredDrink;

    [Header("Anchors (required)")]
    [SerializeField] private Transform foodAnchor;
    [SerializeField] private Transform drinkAnchor;

    [Header("Prefab Models (drag PREFABS from Project)")]
    [SerializeField] private GameObject chickenPrefab;
    [SerializeField] private GameObject friesPrefab;
    [SerializeField] private GameObject burgerPrefab;

    [SerializeField] private GameObject cokePrefab;
    [SerializeField] private GameObject pineapplePrefab;
    [SerializeField] private GameObject iceTeaPrefab;

    [Header("Order Number UI (optional)")]
    [SerializeField] private TableNumberUI numberUi;

    private GameObject spawnedFood;
    private GameObject spawnedDrink;

    public CustomerGroup TargetGroup => targetGroup;
    public CustomerGroup.FoodType DeliveredFood => deliveredFood;
    public CustomerGroup.DrinkType DeliveredDrink => deliveredDrink;

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        if (group != null)
        {
            deliveredFood = group.confirmedFood;
            deliveredDrink = group.confirmedDrink;
        }

        if (numberUi == null)
            numberUi = GetComponentInChildren<TableNumberUI>(true);

        if (numberUi != null)
            numberUi.SetNumber(orderNumber);

        SpawnOrderModels();
    }

    private void SpawnOrderModels()
    {
        if (foodAnchor == null)
        {
            Debug.LogError("[FoodTray] FoodAnchor not assigned.");
            return;
        }

        if (drinkAnchor == null)
        {
            Debug.LogError("[FoodTray] DrinkAnchor not assigned.");
            return;
        }

        if (spawnedFood != null)
            Destroy(spawnedFood);

        if (spawnedDrink != null)
            Destroy(spawnedDrink);

        GameObject foodPrefab = null;
        switch (deliveredFood)
        {
            case CustomerGroup.FoodType.Chicken:
                foodPrefab = chickenPrefab;
                break;
            case CustomerGroup.FoodType.Fries:
                foodPrefab = friesPrefab;
                break;
            case CustomerGroup.FoodType.Burger:
                foodPrefab = burgerPrefab;
                break;
        }

        GameObject drinkPrefab = null;
        switch (deliveredDrink)
        {
            case CustomerGroup.DrinkType.Coke:
                drinkPrefab = cokePrefab;
                break;
            case CustomerGroup.DrinkType.Pineapple:
                drinkPrefab = pineapplePrefab;
                break;
            case CustomerGroup.DrinkType.IceTea:
                drinkPrefab = iceTeaPrefab;
                break;
        }

        if (foodPrefab != null)
        {
            spawnedFood = Instantiate(foodPrefab, foodAnchor);
            ResetLocal(spawnedFood.transform);
        }

        if (drinkPrefab != null)
        {
            spawnedDrink = Instantiate(drinkPrefab, drinkAnchor);
            ResetLocal(spawnedDrink.transform);
        }
    }

    public void OverrideOrder(CustomerGroup.FoodType food, CustomerGroup.DrinkType drink)
    {
        deliveredFood = food;
        deliveredDrink = drink;
        SpawnOrderModels();
    }

    private static void ResetLocal(Transform t)
    {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null && group.currentOrderNumber == orderNumber;
    }
}