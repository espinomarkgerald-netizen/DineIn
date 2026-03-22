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
    public CustomerGroup.FoodType DeliveredFood => deliveredFood1;
    public CustomerGroup.FoodType DeliveredFood1 => deliveredFood1;
    public CustomerGroup.FoodType DeliveredFood2 => deliveredFood2;
    public CustomerGroup.DrinkType DeliveredDrink => deliveredDrink;

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        hasFood2 = false;
        hasDrink = false;
        deliveredFood1 = CustomerGroup.FoodType.Chicken;
        deliveredFood2 = CustomerGroup.FoodType.Chicken;
        deliveredDrink = CustomerGroup.DrinkType.Coke;

        if (group != null)
        {
            if (group.submittedOrder != null && group.submittedOrder.contents != null && group.submittedOrder.contents.Count > 0)
            {
                orderName = group.submittedOrder.name;
                ExtractFromContents(group.submittedOrder.contents);
            }
            else if (group.currentOrder != null)
            {
                orderName = group.currentOrder.name;
                ExtractFromContents(group.currentOrder.contents);
            }
            else
            {
                deliveredFood1 = group.confirmedFood;
                deliveredDrink = group.confirmedDrink;
                hasDrink = true;
            }
        }

        if (numberUi == null)
            numberUi = GetComponentInChildren<TableNumberUI>(true);

        if (numberUi != null)
            numberUi.SetNumber(orderNumber);

        Debug.Log($"[FoodTray] Init tray: order={orderName}, number={orderNumber}");
        Debug.Log($"[FoodTray] Anchors: food1={(foodAnchor1 ? foodAnchor1.name : "NULL")}, food2={(foodAnchor2 ? foodAnchor2.name : "NULL")}, drink={(drinkAnchor ? drinkAnchor.name : "NULL")}");
        Debug.Log($"[FoodTray] Prefabs: chicken={(chickenPrefab ? chickenPrefab.name : "NULL")}, fries={(friesPrefab ? friesPrefab.name : "NULL")}, burger={(burgerPrefab ? burgerPrefab.name : "NULL")}, coke={(cokePrefab ? cokePrefab.name : "NULL")}, pineapple={(pineapplePrefab ? pineapplePrefab.name : "NULL")}, icedtea={(iceTeaPrefab ? iceTeaPrefab.name : "NULL")}");
        Debug.Log($"[FoodTray] Delivered: food1={deliveredFood1}, food2={deliveredFood2}, drink={deliveredDrink}");

        SpawnVisuals();
    }

    private void ExtractFromContents(List<string> contents)
    {
        if (contents == null || contents.Count == 0)
            return;

        List<CustomerGroup.FoodType> foods = new List<CustomerGroup.FoodType>();

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];

            if (string.IsNullOrWhiteSpace(item))
                continue;

            switch (item)
            {
                case "Chicken":
                    foods.Add(CustomerGroup.FoodType.Chicken);
                    break;
                case "Fries":
                    foods.Add(CustomerGroup.FoodType.Fries);
                    break;
                case "Burger":
                    foods.Add(CustomerGroup.FoodType.Burger);
                    break;
                case "Coke":
                    deliveredDrink = CustomerGroup.DrinkType.Coke;
                    hasDrink = true;
                    break;
                case "Pineapple":
                    deliveredDrink = CustomerGroup.DrinkType.Pineapple;
                    hasDrink = true;
                    break;
                case "Ice Tea":
                    deliveredDrink = CustomerGroup.DrinkType.IceTea;
                    hasDrink = true;
                    break;
            }
        }

        if (foods.Count > 0)
            deliveredFood1 = foods[0];

        if (foods.Count > 1)
        {
            deliveredFood2 = foods[1];
            hasFood2 = true;
        }
    }

    private void SpawnVisuals()
    {
        Debug.Log("[FoodTray] SpawnVisuals called");

        if (spawnedFood1 != null) Destroy(spawnedFood1);
        if (spawnedFood2 != null) Destroy(spawnedFood2);
        if (spawnedDrink != null) Destroy(spawnedDrink);

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