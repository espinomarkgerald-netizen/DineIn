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
    [SerializeField, Min(0)] private int baseSalesQuota = 3500;
    [Tooltip("0.25 makes each day add 25% of the Day 1 quota.")]
    [SerializeField, Min(0f)] private float quotaGrowthPerDay = 0.25f;

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
        return Mathf.RoundToInt(baseSalesQuota * (1f + quotaGrowthPerDay * (day - 1)));
    }
}
