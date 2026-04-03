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

    [Header("Daily Expenses")]
    public List<Expense> dailyExpenses = new List<Expense>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Records a new expense or adds to an existing one by name.
    /// </summary>
    public void RecordExpense(string name, float amount)
    {
        var record = dailyExpenses.Find(e => e.name == name);
        if (record != null) record.amount += amount;
        else dailyExpenses.Add(new Expense { name = name, amount = amount });

        MoneyManager.Instance?.Spend(Mathf.RoundToInt(amount), name);
    }

    /// <summary>Clears all daily expenses. Call at the start of a new day.</summary>
    public void ResetDailyExpenses()
    {
        dailyExpenses.Clear();
    }

    /// <summary>Returns total expenses for the day.</summary>
    public float GetTotalExpenses()
    {
        float total = 0;
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
        Debug.Log($"Total Expenses Today: {GetTotalExpenses()}");
        Debug.Log($"Cash Remaining: {MoneyManager.Instance.Money}");
        Debug.Log("---------------------------------");
    }
}