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

        MenuCatalog catalog = MenuCatalog.Default;
        if (catalog != null)
            return catalog.GetOrderTotal(group.GetCurrentOrderProductIds());

        List<string> contents = group.GetCurrentOrderContents();
        if (contents == null)
            return 0;

        if (OrderChecklistUI.Instance != null)
        {
            int food = OrderChecklistUI.Instance.GetFoodTotalFromContents(contents);
            int drink = OrderChecklistUI.Instance.GetDrinkTotalFromContents(contents);
            return food + drink;
        }

        return group.currentOrder != null ? group.currentOrder.unitPrice : 0;
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

}
