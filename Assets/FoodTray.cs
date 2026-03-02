using UnityEngine;

public class FoodTray : MonoBehaviour
{
    [Header("Runtime")]
    public int orderNumber;
    private CustomerGroup targetGroup;

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

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = (group != null) ? group.currentOrderNumber : -1;

        if (numberUi == null) numberUi = GetComponentInChildren<TableNumberUI>(true);
        if (numberUi != null) numberUi.SetNumber(orderNumber);

        SpawnOrderModels();
    }

    private void SpawnOrderModels()
    {
        if (targetGroup == null) return;

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

        if (spawnedFood != null) Destroy(spawnedFood);
        if (spawnedDrink != null) Destroy(spawnedDrink);

        GameObject foodPrefab = null;
        switch (targetGroup.chosenFood)
        {
            case CustomerGroup.FoodType.Chicken: foodPrefab = chickenPrefab; break;
            case CustomerGroup.FoodType.Fries: foodPrefab = friesPrefab; break;
            case CustomerGroup.FoodType.Burger: foodPrefab = burgerPrefab; break;
        }

        GameObject drinkPrefab = null;
        switch (targetGroup.chosenDrink)
        {
            case CustomerGroup.DrinkType.Coke: drinkPrefab = cokePrefab; break;
            case CustomerGroup.DrinkType.Pineapple: drinkPrefab = pineapplePrefab; break;
            case CustomerGroup.DrinkType.IceTea: drinkPrefab = iceTeaPrefab; break;
        }

        if (foodPrefab != null)
        {
            spawnedFood = Instantiate(foodPrefab, foodAnchor);
            ResetLocal(spawnedFood.transform);
        }
        else Debug.LogWarning("[FoodTray] Missing food prefab reference.");

        if (drinkPrefab != null)
        {
            spawnedDrink = Instantiate(drinkPrefab, drinkAnchor);
            ResetLocal(spawnedDrink.transform);
        }
        else Debug.LogWarning("[FoodTray] Missing drink prefab reference.");
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