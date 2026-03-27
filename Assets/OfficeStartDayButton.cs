using UnityEngine;

public class OfficeStartDayButton : MonoBehaviour
{
    public void OnClickStartDay()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("No GameFlowManager found.");
            return;
        }

        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("No MoneyManager found.");
            return;
        }

        int required = GameFlowManager.Instance.TotalRequiredToday;
        int currentMoney = MoneyManager.Instance.Money;

        Debug.Log($"[StartDay] Money: {currentMoney} | Required: {required}");


        if (currentMoney < required)
        {
            Debug.Log("BANKRUPT - Resetting run");

            WarningSlideUI.Instance?.Show("Bankrupt! Restarting from Day 1");

            GameFlowManager.Instance.ResetRun();
            GameFlowManager.Instance.LoadManagementScene();
            return;
        }

  
        bool paid = MoneyManager.Instance.Spend(required, "Daily Costs");

        if (!paid)
        {
            Debug.LogWarning("Spend failed unexpectedly.");
            return;
        }

        GameFlowManager.Instance.StartDay();
    }
}