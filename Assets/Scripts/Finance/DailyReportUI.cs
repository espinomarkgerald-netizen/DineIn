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

        DailyRevenueTracker tracker = DailyRevenueTracker.Instance;
        if (tracker == null) return;

        int revenue          = tracker.TotalRevenue;
        int ingredientCost   = tracker.IngredientCost;
        int payroll          = tracker.GetPayrollTotal();
        int optionalExpenses = tracker.GetOptionalExpensesTotal();
        int totalExpenses    = tracker.GetTotalExpenses();
        int netProfit        = tracker.GetNetProfit();
        int ordersCompleted  = tracker.OrdersCompleted;
        int ordersFailed     = tracker.OrdersFailed;
        int day              = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"DAY {day} REPORT");
        sb.AppendLine("──────────────────────");
        sb.AppendLine();
        sb.AppendLine($"Orders Completed    {ordersCompleted}");
        sb.AppendLine($"Orders Failed       {ordersFailed}");
        sb.AppendLine();
        sb.AppendLine("──────── REVENUE ─────────");
        sb.AppendLine($"Total Revenue       ₱{revenue}");
        sb.AppendLine();
        sb.AppendLine("──────── EXPENSES ────────");
        sb.AppendLine($"Ingredients         ₱{ingredientCost}");
        sb.AppendLine($"Payroll             ₱{payroll}");
        sb.AppendLine($"Other Expenses      ₱{optionalExpenses}");
        sb.AppendLine($"Total Expenses      ₱{totalExpenses}");
        sb.AppendLine();
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
    /// Deducts payroll and optional expenses, resets daily tracker, then loads the management scene.
    /// </summary>
    public void ConfirmAndExit()
    {
        if (DailyRevenueTracker.Instance != null)
        {
            int payroll = DailyRevenueTracker.Instance.GetPayrollTotal();
            if (payroll > 0)
                MoneyManager.Instance?.Spend(payroll, "Daily Payroll");

            FinanceManager.Instance?.PayOptionalExpenses();

            DailyRevenueTracker.Instance.ResetForNewDay();
        }

        if (reportPanel != null)
            reportPanel.SetActive(false);

        GameFlowManager.Instance?.ReturnToManagementFromKitchen();
    }
}
