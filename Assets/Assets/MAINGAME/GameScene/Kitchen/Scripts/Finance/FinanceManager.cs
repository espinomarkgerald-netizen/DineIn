using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Expense
{
    public string name;
    public float amount;
}

public class FinanceManager : MonoBehaviour
{
    public static FinanceManager Instance { get; private set; }

    [Header("Expenses")]
    public List<Expense> optionalExpenses = new List<Expense>();

    [Header("Daily Financials (Read-Only)")]
    [SerializeField] private float totalExpensesToday = 0;
    [SerializeField] private float payrollPaidToday = 0;
    [SerializeField] private float optionalExpensesPaidToday = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Pay optional expenses dynamically
    public void PayOptionalExpenses()
    {
        float optionalTotal = 0;

        foreach (var expense in optionalExpenses)
        {
            optionalTotal += expense.amount;
            MoneyManager.Instance.Spend(Mathf.RoundToInt(expense.amount), expense.name);
        }

        optionalExpensesPaidToday += optionalTotal;
        totalExpensesToday += optionalTotal;

        Debug.Log($"Paid {optionalTotal} in optional expenses. Remaining cash: {MoneyManager.Instance.Money}");
    }

    public void ResetDailyExpenses()
    {
        payrollPaidToday = 0;
        optionalExpensesPaidToday = 0;
        totalExpensesToday = 0;
    }

    public void PrintDailyReport()
    {
        Debug.Log($"----- DAILY FINANCIAL REPORT -----");
        Debug.Log($"Payroll Paid: {payrollPaidToday}");
        Debug.Log($"Optional Expenses Paid: {optionalExpensesPaidToday}");
        Debug.Log($"Total Expenses Today: {totalExpensesToday}");
        Debug.Log($"Cash Remaining: {MoneyManager.Instance.Money}");
        Debug.Log($"---------------------------------");
    }
}