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
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================
    // DAY SETUP
    // =========================
    public void ResetDay()
    {
        employeeCostToday = 0;
        marketingCostToday = 0;
        billsCostToday = 0;
        ingredientCostToday = 0;

        totalRequiredEarningsToday = 0;
        earnedToday = 0;
    }

    public void SetDailyCosts(int employeeCost, int marketingCost, int billsCost, int ingredientCost)
    {
        employeeCostToday = Mathf.Max(0, employeeCost);
        marketingCostToday = Mathf.Max(0, marketingCost);
        billsCostToday = Mathf.Max(0, billsCost);
        ingredientCostToday = Mathf.Max(0, ingredientCost);

        totalRequiredEarningsToday =
            employeeCostToday +
            marketingCostToday +
            billsCostToday +
            ingredientCostToday;
    }

    // =========================
    // EARNINGS
    // =========================
    public void AddEarnings(int amount)
    {
        if (amount <= 0) return;

        earnedToday += amount;
    }

    // =========================
    // PROGRESS
    // =========================
    public float GetProgress01()
    {
        if (totalRequiredEarningsToday <= 0)
            return 0f;

        return Mathf.Clamp01((float)earnedToday / totalRequiredEarningsToday);
    }
}