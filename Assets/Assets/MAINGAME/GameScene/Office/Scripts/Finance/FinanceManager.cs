using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Expense
{
    public string name;
    public int amount;
}

public class FinanceManager : MonoBehaviour
{
    public static FinanceManager Instance { get; private set; }

    [Header("Daily Expenses")]
    public List<Expense> dailyExpenses = new List<Expense>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Records a named expense for end-of-day reporting.
    /// Does NOT deduct from MoneyManager — call DeductAllExpenses() once at end of day.
    /// </summary>
    public void RecordExpense(string name, int amount)
    {
        var record = dailyExpenses.Find(e => e.name == name);
        if (record != null) record.amount += amount;
        else dailyExpenses.Add(new Expense { name = name, amount = amount });
    }

    /// <summary>
    /// Deducts all recorded expenses from MoneyManager in a single settlement.
    /// Call once at end of day after all expenses have been recorded.
    /// </summary>
    public void DeductAllExpenses()
    {
        int total = GetTotalExpenses();
        if (total <= 0) return;

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.Spend(total, "Daily Expenses");
        else
            Debug.LogWarning("[FinanceManager] MoneyManager not found — expenses not deducted.");
    }

    /// <summary>Clears all daily expenses. Call at the start of a new day.</summary>
    public void ResetDailyExpenses()
    {
        dailyExpenses.Clear();
    }

    /// <summary>Returns total expenses recorded for the day.</summary>
    public int GetTotalExpenses()
    {
        int total = 0;
        foreach (var e in dailyExpenses)
            total += e.amount;
        return total;
    }

    /// <summary>Returns a copy of the expense list for reporting.</summary>
    public List<Expense> GetExpenses() => new List<Expense>(dailyExpenses);

    /// <summary>Prints a console report of all expenses.</summary>
    public void PrintDailyReport()
    {
        Debug.Log("----- DAILY FINANCIAL REPORT -----");
        foreach (var e in dailyExpenses)
            Debug.Log($"{e.name.PadRight(20)} ₱{e.amount}");
        Debug.Log($"Total Expenses Today: ₱{GetTotalExpenses()}");
        Debug.Log($"Cash Remaining: ₱{MoneyManager.Instance?.Money}");
        Debug.Log("---------------------------------");
    }
}