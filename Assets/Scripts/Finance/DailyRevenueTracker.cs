using UnityEngine;

/// <summary>
/// Persistent singleton that tracks order statistics and ingredient spend across
/// lobby and kitchen scenes. Revenue and money flow are handled exclusively by
/// DailyFinanceBridge. Reset at the start of each new day via ResetForNewDay().
/// </summary>
public class DailyRevenueTracker : MonoBehaviour
{
    public static DailyRevenueTracker Instance { get; private set; }

    public int OrdersCompleted { get; private set; }
    public int OrdersFailed    { get; private set; }
    public int IngredientCost  { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Records a successfully completed kitchen ticket.</summary>
    public void RecordOrderCompleted() => OrdersCompleted++;

    /// <summary>Records a failed (timed-out) kitchen ticket.</summary>
    public void RecordOrderFailed() => OrdersFailed++;

    /// <summary>Records ingredient spend from the shop. The actual MoneyManager
    /// deduction is handled by ShopCheckoutManager — this is for reporting only.</summary>
    public void RecordIngredientCost(int amount)
    {
        if (amount <= 0) return;
        IngredientCost += amount;
    }

    /// <summary>Resets all daily stats. Call at the start of each new day.</summary>
    public void ResetForNewDay()
    {
        OrdersCompleted = 0;
        OrdersFailed    = 0;
        IngredientCost  = 0;
    }
}
