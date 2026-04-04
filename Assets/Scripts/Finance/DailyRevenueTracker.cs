using UnityEngine;

/// <summary>
/// Persistent singleton that accumulates revenue and order stats across lobby and kitchen scenes.
/// Reset at the start of each new day via ResetForNewDay().
/// </summary>
public class DailyRevenueTracker : MonoBehaviour
{
    public static DailyRevenueTracker Instance { get; private set; }

    public int TotalRevenue       { get; private set; }
    public int OrdersCompleted    { get; private set; }
    public int OrdersFailed       { get; private set; }
    public int IngredientCost     { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Records money earned from a completed order.</summary>
    public void RecordRevenue(int amount)
    {
        if (amount <= 0) return;
        TotalRevenue += amount;
        MoneyManager.Instance?.Earn(amount, "Order Revenue");
    }

    /// <summary>Records a successfully completed kitchen ticket.</summary>
    public void RecordOrderCompleted() => OrdersCompleted++;

    /// <summary>Records a failed (timed-out) kitchen ticket.</summary>
    public void RecordOrderFailed() => OrdersFailed++;

    /// <summary>Records ingredient spend (called by ShopCheckoutManager on purchase).</summary>
    public void RecordIngredientCost(int amount)
    {
        if (amount <= 0) return;
        IngredientCost += amount;
    }

    /// <summary>Calculates payroll total from all assigned employees.</summary>
    public int GetPayrollTotal()
    {
        if (EmployeeManager.Instance == null) return 0;
        return EmployeeManager.Instance.CalculateTotalPayroll();
    }

    /// <summary>Returns total tracked expenses: ingredients + payroll.</summary>
    public int GetTotalExpenses() => IngredientCost + GetPayrollTotal();

    public int GetNetProfit() => TotalRevenue - GetTotalExpenses();

    /// <summary>Resets all daily stats. Call at the start of each new day.</summary>
    public void ResetForNewDay()
    {
        TotalRevenue    = 0;
        OrdersCompleted = 0;
        OrdersFailed    = 0;
        IngredientCost  = 0;
    }
}
