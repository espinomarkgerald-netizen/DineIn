using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialCashierRegisterOpener : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CashierRegisterUI registerUI;

    [Header("Timing")]
    [SerializeField] private float openDelay = 1.5f;

    [Header("Tutorial Payment")]
    [SerializeField] private bool autoChooseReceivedAmount = true;
    [SerializeField] private int forcedReceivedAmount = 0;

    private Coroutine openRoutine;

    public void OpenForTutorial(CustomerGroup group)
    {
        if (group == null || registerUI == null)
            return;

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenRoutine(group));
    }

    public void OpenForTutorialNow(CustomerGroup group)
    {
        if (group == null || registerUI == null)
            return;

        int total = CalculateOrderTotal(group);
        int received = autoChooseReceivedAmount
            ? GetSuggestedReceivedAmount(total)
            : Mathf.Max(total, forcedReceivedAmount);

        registerUI.OpenForPayment(group, received, total);
    }

    private IEnumerator OpenRoutine(CustomerGroup group)
    {
        yield return new WaitForSeconds(openDelay);

        if (group == null || registerUI == null)
        {
            openRoutine = null;
            yield break;
        }

        OpenForTutorialNow(group);
        openRoutine = null;
    }

    private int CalculateOrderTotal(CustomerGroup group)
    {
        if (group == null)
            return 0;

        List<string> contents = group.GetCurrentOrderContents();
        if (contents == null)
            return 0;

        if (OrderChecklistUI.Instance != null)
        {
            int food = OrderChecklistUI.Instance.GetFoodTotalFromContents(contents);
            int drink = OrderChecklistUI.Instance.GetDrinkTotalFromContents(contents);
            return food + drink;
        }

        return GetFallbackFoodTotal(contents) + GetFallbackDrinkTotal(contents);
    }

    private int GetSuggestedReceivedAmount(int total)
    {
        if (total <= 0)
            return 0;

        int[] bills = { 50, 100, 200, 500, 1000 };

        for (int i = 0; i < bills.Length; i++)
        {
            if (bills[i] >= total)
                return bills[i];
        }

        return total;
    }

    private int GetFallbackFoodTotal(List<string> contents)
    {
        if (contents == null)
            return 0;

        List<string> foods = new List<string>();

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];
            if (item == "Chicken" || item == "Fries" || item == "Burger")
                foods.Add(item);
        }

        if (foods.Count == 2)
        {
            bool hasChicken = foods.Contains("Chicken");
            bool hasFries = foods.Contains("Fries");
            bool hasBurger = foods.Contains("Burger");

            if (hasChicken && hasFries)
                return 349;

            if (hasChicken && hasBurger)
                return 399;

            if (hasBurger && hasFries)
                return 179;
        }

        int total = 0;

        for (int i = 0; i < foods.Count; i++)
        {
            switch (foods[i])
            {
                case "Chicken":
                    total += 299;
                    break;
                case "Fries":
                    total += 79;
                    break;
                case "Burger":
                    total += 119;
                    break;
            }
        }

        return total;
    }

    private int GetFallbackDrinkTotal(List<string> contents)
    {
        if (contents == null)
            return 0;

        int total = 0;

        for (int i = 0; i < contents.Count; i++)
        {
            switch (contents[i])
            {
                case "Coke":
                case "Pineapple":
                case "Ice Tea":
                    total += 50;
                    break;
            }
        }

        return total;
    }
}