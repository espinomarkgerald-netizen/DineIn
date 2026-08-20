using UnityEngine;
using System.Collections.Generic;

public class TutorialCashierOrderRandomizer : MonoBehaviour
{
    public struct GeneratedOrder
    {
        public int orderNumber;

        public string firstFoodName;
        public Sprite firstFoodSprite;

        public string secondFoodName;
        public Sprite secondFoodSprite;

        public string drinkName;
        public Sprite drinkSprite;

        public int foodTotal;
        public int drinkTotal;
        public int total;
        public int received;
    }

    [Header("Order Numbers")]
    [SerializeField] private int minOrderNumber = 3001;
    [SerializeField] private int maxOrderNumber = 3999;

    [Header("Possible Received Amounts")]
    [SerializeField] private int[] receivedCandidates = { 100, 200, 500, 1000 };

    public GeneratedOrder Generate()
    {
        GeneratedOrder order = new GeneratedOrder();

        order.orderNumber = Random.Range(minOrderNumber, maxOrderNumber + 1);
        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog == null)
        {
            Debug.LogError("[TutorialCashierOrderRandomizer] MenuCatalog is missing.");
            return order;
        }

        List<List<Recipe>> mealOptions = new List<List<Recipe>>();
        List<Recipe> foods = catalog.GetProducts(MenuProductCategory.Food, false);
        for (int i = 0; i < foods.Count; i++)
            mealOptions.Add(new List<Recipe> { foods[i] });

        List<MenuBundle> bundles = catalog.GetFoodBundles(false);
        for (int i = 0; i < bundles.Count; i++)
            mealOptions.Add(new List<Recipe>(bundles[i].products));

        List<Recipe> drinks = catalog.GetProducts(MenuProductCategory.Drink, false);
        if (mealOptions.Count == 0 || drinks.Count == 0)
        {
            Debug.LogError("[TutorialCashierOrderRandomizer] MenuCatalog has no food or drink options.");
            return order;
        }

        List<Recipe> selectedMeal = mealOptions[Random.Range(0, mealOptions.Count)];
        Recipe firstFood = selectedMeal[0];
        order.firstFoodName = firstFood.DisplayName;
        order.firstFoodSprite = firstFood.sprite;

        if (selectedMeal.Count > 1)
        {
            Recipe secondFood = selectedMeal[1];
            order.secondFoodName = secondFood.DisplayName;
            order.secondFoodSprite = secondFood.sprite;
        }

        MenuBundle selectedBundle = catalog.FindBundle(selectedMeal);
        order.foodTotal = selectedBundle != null
            ? selectedBundle.GetPrice()
            : firstFood.EffectiveSellPrice;

        Recipe selectedDrink = drinks[Random.Range(0, drinks.Count)];
        order.drinkName = selectedDrink.DisplayName;
        order.drinkSprite = selectedDrink.sprite;
        order.drinkTotal = selectedDrink.EffectiveSellPrice;
        order.total = order.foodTotal + order.drinkTotal;
        order.received = GetRandomReceivedAmountAbove(order.total);

        return order;
    }

    private int GetRandomReceivedAmountAbove(int total)
    {
        if (receivedCandidates != null && receivedCandidates.Length > 0)
        {
            int[] valid = new int[receivedCandidates.Length];
            int count = 0;

            for (int i = 0; i < receivedCandidates.Length; i++)
            {
                if (receivedCandidates[i] > total)
                {
                    valid[count] = receivedCandidates[i];
                    count++;
                }
            }

            if (count > 0)
            {
                int index = Random.Range(0, count);
                return valid[index];
            }
        }

        int rounded = ((total / 100) + 1) * 100;
        return Mathf.Max(rounded, total + 1);
    }
}
