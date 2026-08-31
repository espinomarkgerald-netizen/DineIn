using UnityEngine;

public class DailyFinanceBridge : MonoBehaviour
{
    public static DailyFinanceBridge Instance { get; private set; }

    [Header("Today Costs")]
    [SerializeField] private int employeeCostToday;
    [SerializeField] private int marketingCostToday;
    [SerializeField] private int billsCostToday;
    [SerializeField] private int ingredientCostToday;

    [Header("Runtime")]
    [SerializeField] private int totalRequiredEarningsToday;
    [SerializeField] private int earnedToday;

    [Header("Daily Sales Quota")]
    [Tooltip("Day 1 target. Bots should be able to make progress, while the manager secures the win.")]
    [SerializeField, Min(0)] private int dayOneSalesQuota = 4500;
    [Tooltip("Day 2 target. This is deliberately above the observed bots-only revenue.")]
    [SerializeField, Min(0)] private int dayTwoSalesQuota = 7500;
    [Tooltip("Flat daily increase from Day 3 through the difficulty ceiling.")]
    [SerializeField, Min(0)] private int quotaIncreasePerDayThroughCeiling = 500;
    [Tooltip("The last day of the fixed progression. Later days grow more slowly.")]
    [SerializeField, Min(2)] private int quotaDifficultyCeilingDay = 20;
    [Tooltip("Growth applied after the difficulty ceiling. Keep this low because customer volume stops scaling.")]
    [SerializeField, Range(0f, 0.1f)] private float postCeilingQuotaGrowth = 0.04f;

    public int EmployeeCostToday => employeeCostToday;
    public int MarketingCostToday => marketingCostToday;
    public int BillsCostToday => billsCostToday;
    public int IngredientCostToday => ingredientCostToday;

    public int TotalRequiredEarningsToday => totalRequiredEarningsToday;
    public int EarnedToday => earnedToday;

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

    public void ResetDay()
    {
        employeeCostToday = 0;
        marketingCostToday = 0;
        billsCostToday = 0;
        ingredientCostToday = 0;

        totalRequiredEarningsToday = CalculateSalesQuota();
        earnedToday = 0;
    }

    public void SetDailyCosts(int employeeCost, int marketingCost, int billsCost, int ingredientCost)
    {
        employeeCostToday = Mathf.Max(0, employeeCost);
        marketingCostToday = Mathf.Max(0, marketingCost);
        billsCostToday = Mathf.Max(0, billsCost);
        ingredientCostToday = Mathf.Max(0, ingredientCost);

        RefreshRequiredEarnings();
    }

    public void AddEarnings(int amount, string description = "Daily Earnings")
    {
        if (amount <= 0)
            return;

        earnedToday += amount;

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.Earn(amount, description);
    }

    /// <summary>
    /// Records a customer refund against both today's sales and the restaurant's
    /// money. Sales cannot become negative, while the money manager keeps its
    /// existing floor-at-zero behavior and transaction history.
    /// </summary>
    public void ApplyRefund(int amount, string description = "Customer Refund")
    {
        if (amount <= 0)
            return;

        earnedToday = Mathf.Max(0, earnedToday - amount);
        MoneyManager.Instance?.ForceSpend(amount, description);
    }

    public bool SpendMoney(int amount, string description = "Daily Expense")
    {
        if (amount <= 0)
            return false;

        bool success = false;

        if (MoneyManager.Instance != null)
            success = MoneyManager.Instance.Spend(amount, description);

        if (!success)
            return false;

        ingredientCostToday += amount;
        RefreshRequiredEarnings();

        return true;
    }

    public float GetProgress01()
    {
        if (totalRequiredEarningsToday <= 0)
            return 0f;

        return Mathf.Clamp01((float)earnedToday / totalRequiredEarningsToday);
    }

    private void RefreshRequiredEarnings()
    {
        int operatingCosts = employeeCostToday + marketingCostToday +
                             billsCostToday + ingredientCostToday;
        totalRequiredEarningsToday = Mathf.Max(operatingCosts, CalculateSalesQuota());
    }

    private int CalculateSalesQuota()
    {
        int day = GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
            : 1;

        if (day == 1)
            return dayOneSalesQuota;

        int ceilingDay = Mathf.Max(2, quotaDifficultyCeilingDay);
        int fixedProgressionDay = Mathf.Min(day, ceilingDay);
        int quotaAtCeiling = dayTwoSalesQuota +
                             (fixedProgressionDay - 2) * quotaIncreasePerDayThroughCeiling;

        if (day <= ceilingDay)
            return quotaAtCeiling;

        float lateDayMultiplier = Mathf.Pow(postCeilingQuotaGrowth + 1f, day - ceilingDay);
        return Mathf.RoundToInt(quotaAtCeiling * lateDayMultiplier);
    }
}
