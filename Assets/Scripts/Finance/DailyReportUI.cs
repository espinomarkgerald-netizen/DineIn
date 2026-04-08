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

    [Header("Objective Results (populated after ConfirmAndExit)")]
    [Tooltip("Assign a TMP_Text that overlays the report panel. " +
             "Populated by the OnObjectivesEvaluated event when ConfirmAndExit is pressed.")]
    [SerializeField] private TMP_Text objectiveResultText;

    [Header("Colors")]
    [SerializeField] private Color profitColor  = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color lossColor    = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color passColor    = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color failColor    = new Color(0.9f, 0.2f, 0.2f);

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

        if (objectiveResultText != null)
            objectiveResultText.text = string.Empty;
    }

    private void OnEnable()
    {
        if (DailyObjectiveManager.Instance != null)
            DailyObjectiveManager.Instance.OnObjectivesEvaluated += HandleObjectivesEvaluated;
    }

    private void OnDisable()
    {
        if (DailyObjectiveManager.Instance != null)
            DailyObjectiveManager.Instance.OnObjectivesEvaluated -= HandleObjectivesEvaluated;
    }

    /// <summary>
    /// Receives the grade and pass/fail results from DailyObjectiveManager.OnObjectivesEvaluated.
    /// Fires after ConfirmAndExit() triggers EvaluateEndOfDay().
    /// </summary>
    private void HandleObjectivesEvaluated(ObjectiveGrade grade, bool mandatoryPassed, bool secondaryPassed, bool bonusPassed)
    {
        if (objectiveResultText == null) return;

        var mgr = DailyObjectiveManager.Instance;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("── ALIEN DEMANDS ──────────");
        sb.AppendLine($"Grade: {grade}");
        sb.AppendLine();

        AppendObjectiveLine(sb, "MANDATORY", mgr?.ActiveMandatory, mandatoryPassed);
        AppendObjectiveLine(sb, "SERVICE",   mgr?.ActiveSecondary, secondaryPassed);
        AppendObjectiveLine(sb, "BONUS",     mgr?.ActiveBonus,     bonusPassed);

        sb.AppendLine("───────────────────────────");

        objectiveResultText.text = sb.ToString();

        // Colour the whole block by overall grade success
        objectiveResultText.color = grade == ObjectiveGrade.F ? failColor : passColor;
    }

    private static void AppendObjectiveLine(StringBuilder sb, string label, ObjectiveDefinition obj, bool passed)
    {
        string status = passed ? "[PASS]" : "[FAIL]";
        string desc   = obj != null ? obj.descriptionTemplate : "—";
        sb.AppendLine($"{status} {label}: {desc}");
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

        // DailyFinanceBridge.EarnedToday is the single authoritative revenue source —
        // AddEarnings() is the only earn path for both lobby (CashierRegisterUI) and
        // kitchen (OrderManagerKitchen) orders.
        int revenue = bridge != null ? bridge.EarnedToday : 0;
        int ingredientCost = tracker != null ? tracker.IngredientCost : 0;
        int financeExpenses = finance != null ? finance.GetTotalExpenses() : 0;
        int totalExpenses = financeExpenses + ingredientCost;
        int netProfit = revenue - totalExpenses;

        int day = flow != null ? flow.CurrentDay : 0;
        int ordersCompleted = tracker != null ? tracker.OrdersCompleted : 0;
        int ordersFailed = tracker != null ? tracker.OrdersFailed : 0;
        int targetRevenue = bridge != null ? bridge.TotalRequiredEarningsToday : 0;

        // Mood and cash data from GameDayManager (lobby-side shift tracker)
        int happy      = GameDayManager.Instance != null ? GameDayManager.Instance.HappyCustomers    : 0;
        int neutral    = GameDayManager.Instance != null ? GameDayManager.Instance.NeutralCustomers  : 0;
        int angry      = GameDayManager.Instance != null ? GameDayManager.Instance.AngryCustomers    : 0;
        int cashErrors = GameDayManager.Instance != null ? GameDayManager.Instance.CashErrors        : 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"DAY {day} REPORT");
        sb.AppendLine("──────────────────────");
        sb.AppendLine();
        sb.AppendLine($"Orders Completed    {ordersCompleted}");
        sb.AppendLine($"Orders Failed       {ordersFailed}");
        sb.AppendLine();
        sb.AppendLine("──── CUSTOMER MOOD ───────");
        sb.AppendLine($"😊 Happy     {happy}");
        sb.AppendLine($"😐 Neutral   {neutral}");
        sb.AppendLine($"😡 Angry     {angry}");
        sb.AppendLine();
        sb.AppendLine("── CASH HANDLING ─────────");
        sb.AppendLine(cashErrors == 0
            ? "✓ No cash errors"
            : $"⚠ {cashErrors} abandoned transaction{(cashErrors == 1 ? "" : "s")}");
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
    /// Deducts payroll and expenses first, then evaluates win/loss and advances the day.
    /// Order matters: EndOfDayFinance() must run before EvaluateEndOfDay() so the
    /// bankruptcy check sees the post-expense money balance.
    /// </summary>
    public void ConfirmAndExit()
    {
        Time.timeScale = 1f;

        if (reportPanel != null)
            reportPanel.SetActive(false);

        GameFlowManager.Instance.EndOfDayFinance();
        GameFlowManager.Instance.EvaluateEndOfDay();
    }
}
