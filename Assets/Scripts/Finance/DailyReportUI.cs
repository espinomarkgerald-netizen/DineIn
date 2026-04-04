using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays a daily financial report panel after the kitchen shift ends.
/// Call Show() from KitchenSceneController.EndShift() before transitioning scenes.
/// Assign all fields in the Inspector.
/// </summary>
public class DailyReportUI : MonoBehaviour
{
    public static DailyReportUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject reportPanel;

    [Header("Text Fields")]
    [SerializeField] private TMP_Text reportText;
    [SerializeField] private TMP_Text netProfitText;

    [Header("Colors")]
    [SerializeField] private Color profitColor  = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color lossColor    = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (reportPanel != null)
            reportPanel.SetActive(false);
    }

    /// <summary>
    /// Builds and displays the daily report. Does not trigger scene transition — 
    /// the Continue button calls ConfirmAndExit().
    /// </summary>
    public void Show()
    {
        if (reportPanel == null || reportText == null) return;

        var tracker = DailyRevenueTracker.Instance;
        var bridge = DailyFinanceBridge.Instance;
        var finance = FinanceManager.Instance;
        var flow = GameFlowManager.Instance;

        // Use DailyFinanceBridge.EarnedToday as the authoritative revenue source —
        // it captures both lobby and kitchen earnings, matching the in-game HUD.
        int revenue = bridge != null ? bridge.EarnedToday : (tracker != null ? tracker.TotalRevenue : 0);
        int ingredientCost = tracker != null ? tracker.IngredientCost : 0;
        int financeExpenses = finance != null ? Mathf.RoundToInt(finance.GetTotalExpenses()) : 0;
        int totalExpenses = financeExpenses + ingredientCost;
        int netProfit = revenue - totalExpenses;

        int day = flow != null ? flow.CurrentDay : 0;
        int ordersCompleted = tracker != null ? tracker.OrdersCompleted : 0;
        int ordersFailed = tracker != null ? tracker.OrdersFailed : 0;
        int targetRevenue = bridge != null ? bridge.TotalRequiredEarningsToday : 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"DAY {day} REPORT");
        sb.AppendLine("──────────────────────");
        sb.AppendLine();
        sb.AppendLine($"Orders Completed    {ordersCompleted}");
        sb.AppendLine($"Orders Failed       {ordersFailed}");
        sb.AppendLine();
        sb.AppendLine("──────── REVENUE ─────────");
        sb.AppendLine($"Total Revenue       ₱{revenue}");
        sb.AppendLine($"Target Revenue      ₱{targetRevenue}");
        sb.AppendLine();
        sb.AppendLine("──────── EXPENSES ────────");

        if (finance != null)
        {
            foreach (var e in finance.GetExpenses())
                sb.AppendLine($"{e.name.PadRight(20)} ₱{e.amount}");
        }

        sb.AppendLine($"Ingredients         ₱{ingredientCost}");
        sb.AppendLine($"Total Expenses      ₱{totalExpenses}");
        sb.AppendLine("──────────────────────────");

        reportText.text = sb.ToString();

        if (netProfitText != null)
        {
            string sign = netProfit >= 0 ? "+" : "";
            netProfitText.text = $"Net Profit: {sign}₱{netProfit}";
            netProfitText.color = netProfit > 0 ? profitColor : netProfit < 0 ? lossColor : neutralColor;
        }

        reportPanel.SetActive(true);
    }

    /// <summary>
    /// Called by the Continue button on the report panel.
    /// Runs end-of-day finance once, then advances the day and returns to management.
    /// </summary>
    public void ConfirmAndExit()
    {
        Time.timeScale = 1f;

        if (reportPanel != null)
            reportPanel.SetActive(false);

        GameFlowManager.Instance.StartNewDay();
    }
}
