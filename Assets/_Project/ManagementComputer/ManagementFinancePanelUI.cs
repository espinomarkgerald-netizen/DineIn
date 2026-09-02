using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive TODAY/HISTORY presentation for the existing finance ledger.</summary>
public sealed class ManagementFinancePanelUI : MonoBehaviour
{
    private static readonly Color Navy = new Color(0.08f, 0.20f, 0.32f, 1f);
    private static readonly Color Blue = new Color(0.12f, 0.55f, 0.83f, 1f);
    private static readonly Color PaleBlue = new Color(0.86f, 0.94f, 0.98f, 1f);
    private static readonly Color Paper = new Color(1f, 0.99f, 0.95f, 1f);
    private static readonly Color Ink = new Color(0.055f, 0.12f, 0.18f, 1f);
    private static readonly Color Muted = new Color(0.22f, 0.34f, 0.42f, 1f);
    private static readonly Color Green = new Color(0.06f, 0.46f, 0.25f, 1f);
    private static readonly Color PaleGreen = new Color(0.85f, 0.95f, 0.87f, 1f);
    private static readonly Color Red = new Color(0.87f, 0.14f, 0.22f, 1f);
    private static readonly Color Divider = new Color(0.70f, 0.77f, 0.80f, 0.7f);

    private static readonly string[] Tips =
    {
        "Adjust your menu prices based on the cost of the ingredients used to make each item.",
        "Pricing food too low can increase sales but may leave you with little or no profit.",
        "Pricing food too high may cause customers to refuse the order and leave.",
        "Compare your ingredient costs with your menu prices before starting the shift.",
        "High sales do not always mean high profit. Watch payroll, restocking and refunds.",
        "Avoid buying much more stock than you need today. Overstocking ties up money and storage.",
        "Refunds reduce today's profit. Good service protects approval and earnings.",
        "Higher-rated staff cost more, so make sure their performance justifies their salary.",
        "Keep some cash available after restocking for unexpected expenses and upgrades.",
        "Check Net Profit, not just Sales, when judging how successful the day was."
    };

    private static int lastTipIndex = -1;

    private Sprite panelSprite;
    private TMP_FontAsset displayFont;
    private TMP_FontAsset readableFont;
    private TMP_FontAsset readableBoldFont;
    private RectTransform todayRoot;
    private RectTransform historyRoot;
    private CanvasGroup todayGroup;
    private CanvasGroup historyGroup;
    private Image todayButtonImage;
    private Image historyButtonImage;
    private TMP_Text dayLabel;
    private TMP_Text salesValue;
    private TMP_Text expensesValue;
    private TMP_Text profitValue;
    private TMP_Text cashValue;
    private TMP_Text netProfitValue;
    private CanvasGroup tipGroup;
    private GameObject historyEmptyState;
    private bool built;
    private Coroutine tabRoutine;

    public void Initialize(
        Sprite sharedPanelSprite,
        TMP_FontAsset sharedDisplayFont,
        TMP_FontAsset sharedReadableFont,
        TMP_FontAsset sharedReadableBoldFont,
        FinanceDayReport today,
        IReadOnlyList<DailyFinanceSummarySaveEntry> history)
    {
        panelSprite = sharedPanelSprite;
        readableFont = sharedReadableFont != null
            ? sharedReadableFont
            : TMP_Settings.defaultFontAsset;
        readableBoldFont = sharedReadableBoldFont != null
            ? sharedReadableBoldFont
            : readableFont;
        displayFont = sharedDisplayFont != null
            ? sharedDisplayFont
            : readableBoldFont;
        if (!built)
            Build();
        Bind(today, history);
    }

    private void Build()
    {
        built = true;
        RectTransform root = transform as RectTransform;
        Stretch(root);
        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        BuildTabs();
        BuildToday();
        BuildHistory();
    }

    private void BuildTabs()
    {
        GameObject tabs = CreateObject("Finance Tabs", transform);
        HorizontalLayoutGroup row = tabs.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;
        SetLayout(tabs, 58f, 58f, 0f, 0f);

        Button todayButton = CreateButton("Today Tab", tabs.transform, "TODAY", Blue, 142f);
        Button historyButton = CreateButton("History Tab", tabs.transform, "HISTORY", Navy, 154f);
        todayButtonImage = todayButton.image;
        historyButtonImage = historyButton.image;
        todayButton.onClick.AddListener(() => ShowTab(true, true));
        historyButton.onClick.AddListener(() => ShowTab(false, true));

        GameObject spacer = CreateObject("Spacer", tabs.transform);
        SetLayout(spacer, 1f, 1f, 1f, 0f);
        dayLabel = CreateText("Day Label", tabs.transform, "DAY 1 • RESTAURANT FINANCIALS",
            Muted, 16f, 21f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        SetLayout(dayLabel.gameObject, 40f, 420f, 0f, 0f);
    }

    private void BuildToday()
    {
        todayRoot = CreateObject("Today", transform).GetComponent<RectTransform>();
        todayGroup = todayRoot.gameObject.AddComponent<CanvasGroup>();
        VerticalLayoutGroup column = todayRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 10f;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        SetLayout(todayRoot.gameObject, 516f, 560f, 1f, 1f);

        GameObject summary = CreateObject("Summary Cards", todayRoot);
        HorizontalLayoutGroup summaryRow = summary.AddComponent<HorizontalLayoutGroup>();
        summaryRow.spacing = 12f;
        summaryRow.childControlWidth = true;
        summaryRow.childControlHeight = true;
        summaryRow.childForceExpandWidth = true;
        summaryRow.childForceExpandHeight = true;
        SetLayout(summary, 94f, 100f, 0f, 0f);
        salesValue = CreateSummaryCard(summary.transform, "SALES SO FAR", Green);
        expensesValue = CreateSummaryCard(summary.transform, "EXPENSES SO FAR", Red);
        profitValue = CreateSummaryCard(summary.transform, "CURRENT NET", Green);
        cashValue = CreateSummaryCard(summary.transform, "CASH BALANCE", Navy);

        GameObject main = CreateObject("Statement Area", todayRoot);
        HorizontalLayoutGroup mainRow = main.AddComponent<HorizontalLayoutGroup>();
        mainRow.spacing = 12f;
        mainRow.childControlWidth = true;
        mainRow.childControlHeight = true;
        mainRow.childForceExpandWidth = true;
        mainRow.childForceExpandHeight = true;
        SetLayout(main, 400f, 420f, 1f, 1f);
        BuildReceipt(main.transform);
        BuildNetSidebar(main.transform);
    }

    private TMP_Text CreateSummaryCard(Transform parent, string label, Color valueColor)
    {
        GameObject card = CreatePanel(label + " Card", parent, PaleBlue);
        VerticalLayoutGroup column = card.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(14, 14, 10, 10);
        column.spacing = 2f;
        column.childAlignment = TextAnchor.MiddleLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        SetLayout(card, 92f, 96f, 1f, 1f);
        TMP_Text heading = CreateText("Label", card.transform, label, Muted,
            13f, 18f, FontStyles.Normal, TextAlignmentOptions.BottomLeft,
            display: true);
        SetLayout(heading.gameObject, 26f, 28f, 0f, 0f);
        TMP_Text value = CreateText("Value", card.transform, "₱0", valueColor,
            23f, 34f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetLayout(value.gameObject, 42f, 48f, 0f, 0f);
        return value;
    }

    private void BuildReceipt(Transform parent)
    {
        GameObject receipt = CreatePanel("Daily Statement", parent, Paper);
        SetLayout(receipt, 394f, 430f, 1.72f, 1f);
        VerticalLayoutGroup column = receipt.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(22, 22, 15, 15);
        column.spacing = 6f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        GameObject titleRow = CreateObject("Statement Title Row", receipt.transform);
        HorizontalLayoutGroup titleLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
        titleLayout.childAlignment = TextAnchor.MiddleLeft;
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = false;
        titleLayout.childForceExpandHeight = true;
        SetLayout(titleRow, 44f, 46f, 0f, 0f);
        TMP_Text title = CreateText("Restaurant", titleRow.transform, "DINE IN", Navy,
            26f, 34f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            display: true);
        SetLayout(title.gameObject, 40f, 220f, 0f, 0f);
        GameObject titleSpacer = CreateObject("Spacer", titleRow.transform);
        SetLayout(titleSpacer, 1f, 1f, 1f, 0f);
        GameObject badge = CreatePanel("Today Badge", titleRow.transform, PaleGreen);
        SetLayout(badge, 28f, 86f, 0f, 0f);
        TMP_Text badgeText = CreateText("Label", badge.transform, "TODAY", Green,
            10f, 13f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(badgeText.rectTransform);
        TMP_Text subtitle = CreateText("Statement Heading", receipt.transform,
            "DAILY RESTAURANT STATEMENT", Muted, 14f, 19f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetLayout(subtitle.gameObject, 27f, 30f, 0f, 0f);
        CreateDivider(receipt.transform, true);

        GameObject columns = CreateObject("Receipt Columns", receipt.transform);
        HorizontalLayoutGroup row = columns.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 14f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;
        SetLayout(columns, 270f, 300f, 1f, 1f);

        Transform revenue = CreateStatementColumn(columns.transform, "Revenue", "REVENUE");
        CreateStatementLine(revenue, "Food & Drink Sales", out TMP_Text foodSales, Green);
        foodSales.name = "Food Sales Amount";
        CreateStatementLine(revenue, "Other Income", out TMP_Text otherIncome, Green);
        otherIncome.name = "Other Income Amount";
        CreateStatementSpacer(revenue);
        CreateDivider(revenue, false);
        CreateStatementLine(revenue, "Total Revenue", out TMP_Text totalRevenue, Green, true);
        totalRevenue.name = "Total Revenue Amount";

        CreateStatementVerticalDivider(columns.transform);

        Transform expenses = CreateStatementColumn(columns.transform, "Expenses", "EXPENSES");
        CreateStatementLine(expenses, "Ingredient Restock", out TMP_Text restock, Red);
        restock.name = "Restock Amount";
        CreateStatementLine(expenses, "Staff Payroll", out TMP_Text payroll, Red);
        payroll.name = "Payroll Amount";
        CreateStatementLine(expenses, "Refunds", out TMP_Text refunds, Red);
        refunds.name = "Refund Amount";
        CreateStatementLine(expenses, "Other Costs", out TMP_Text otherCosts, Red);
        otherCosts.name = "Other Costs Amount";
        CreateStatementSpacer(expenses);
        CreateDivider(expenses, false);
        CreateStatementLine(expenses, "Total Expenses", out TMP_Text totalExpenses, Red, true);
        totalExpenses.name = "Total Expenses Amount";
    }

    private Transform CreateStatementColumn(Transform parent, string name, string heading)
    {
        GameObject columnObject = CreateObject(name, parent);
        VerticalLayoutGroup column = columnObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        SetLayout(columnObject, 256f, 220f, 1f, 1f);
        TMP_Text label = CreateText("Section", columnObject.transform, heading, Muted,
            15f, 21f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
            display: true);
        SetLayout(label.gameObject, 32f, 34f, 0f, 0f);
        return columnObject.transform;
    }

    private void CreateStatementLine(
        Transform parent,
        string label,
        out TMP_Text amount,
        Color amountColor,
        bool bold = false)
    {
        GameObject line = CreateObject(label + " Line", parent);
        HorizontalLayoutGroup row = line.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;
        SetLayout(line, bold ? 44f : 38f, 42f, 0f, 0f);
        TMP_Text name = CreateText("Label", line.transform, label, Ink,
            bold ? 16.5f : 15f, bold ? 23f : 20f,
            bold ? FontStyles.Bold : FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetLayout(name.gameObject, bold ? 38f : 32f, 1f, 1f, 0f);
        amount = CreateText("Amount", line.transform, "₱0", amountColor,
            bold ? 17f : 15f, bold ? 24f : 20f,
            bold ? FontStyles.Bold : FontStyles.Normal,
            TextAlignmentOptions.MidlineRight);
        SetLayout(amount.gameObject, bold ? 38f : 32f, 138f, 0f, 0f);
    }

    private void CreateStatementSpacer(Transform parent)
    {
        GameObject spacer = CreateObject("Total Spacer", parent);
        SetLayout(spacer, 10f, 1f, 0f, 1f);
    }

    private void CreateStatementVerticalDivider(Transform parent)
    {
        GameObject divider = CreateObject("Column Divider", parent);
        Image image = divider.AddComponent<Image>();
        image.color = new Color(Divider.r, Divider.g, Divider.b, 0.55f);
        image.raycastTarget = false;
        SetLayout(divider, 1f, 2f, 0f, 1f);
        LayoutElement element = divider.GetComponent<LayoutElement>();
        element.minWidth = 2f;
    }

    private void BuildNetSidebar(Transform parent)
    {
        GameObject side = CreateObject("Profit Sidebar", parent);
        VerticalLayoutGroup column = side.AddComponent<VerticalLayoutGroup>();
        column.spacing = 10f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        SetLayout(side, 394f, 300f, 0.95f, 1f);

        GameObject net = CreatePanel("Net Profit", side.transform, PaleGreen);
        VerticalLayoutGroup netColumn = net.AddComponent<VerticalLayoutGroup>();
        netColumn.padding = new RectOffset(14, 14, 10, 10);
        netColumn.spacing = 1f;
        netColumn.childControlWidth = true;
        netColumn.childControlHeight = true;
        netColumn.childForceExpandWidth = true;
        netColumn.childForceExpandHeight = false;
        SetLayout(net, 126f, 120f, 0f, 0f);
        TMP_Text netLabel = CreateText("Label", net.transform, "CURRENT NET", Green,
            13f, 17f, FontStyles.Normal, TextAlignmentOptions.BottomLeft,
            display: true);
        SetLayout(netLabel.gameObject, 24f, 26f, 0f, 0f);
        netProfitValue = CreateText("Value", net.transform, "₱0", Green,
            27f, 39f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetLayout(netProfitValue.gameObject, 48f, 54f, 0f, 0f);
        TMP_Text explanation = CreateText("Explanation", net.transform,
            "Revenue so far after operating expenses.  NET = REVENUE - EXPENSES", Muted, 12f, 16f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        SetLayout(explanation.gameObject, 34f, 36f, 0f, 0f);

        GameObject detail = CreatePanel("Finance Totals", side.transform, PaleBlue);
        VerticalLayoutGroup details = detail.AddComponent<VerticalLayoutGroup>();
        details.padding = new RectOffset(12, 12, 8, 8);
        details.spacing = 1f;
        details.childControlWidth = true;
        details.childControlHeight = true;
        details.childForceExpandWidth = true;
        details.childForceExpandHeight = false;
        SetLayout(detail, 160f, 190f, 0f, 0f);
        CreateSidebarLine(detail.transform, "Payroll", "Payroll Sidebar Amount");
        CreateSidebarLine(detail.transform, "Refunds Given", "Refund Sidebar Amount");
        CreateSidebarLine(detail.transform, "Restock Spending", "Restock Sidebar Amount");
        CreateSidebarLine(detail.transform, "Cash Available", "Cash Sidebar Amount");
        BuildTip(side.transform);
    }

    private void CreateSidebarLine(Transform parent, string label, string amountName)
    {
        CreateStatementLine(parent, label, out TMP_Text amount, Navy);
        amount.name = amountName;
        CreateDivider(parent, false);
    }

    private void BuildTip(Transform parent)
    {
        GameObject tip = CreatePanel("Finance Tip", parent, PaleBlue);
        tipGroup = tip.AddComponent<CanvasGroup>();
        HorizontalLayoutGroup row = tip.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(12, 12, 8, 8);
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        SetLayout(tip, 94f, 110f, 1f, 1f);

        GameObject icon = CreatePanel("Peso Icon", tip.transform, Blue);
        SetLayout(icon, 46f, 46f, 0f, 0f);
        TMP_Text peso = CreateText("Symbol", icon.transform, "₱", Color.white,
            22f, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(peso.rectTransform);

        GameObject copy = CreateObject("Tip Copy", tip.transform);
        VerticalLayoutGroup column = copy.AddComponent<VerticalLayoutGroup>();
        column.spacing = 0f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        SetLayout(copy, 74f, 1f, 1f, 0f);
        TMP_Text heading = CreateText("Heading", copy.transform, "FINANCE TIP", Blue,
            12f, 16f, FontStyles.Normal, TextAlignmentOptions.BottomLeft,
            display: true);
        SetLayout(heading.gameObject, 20f, 22f, 0f, 0f);
        TMP_Text body = CreateText("Tip Text", copy.transform, string.Empty, Muted,
            13f, 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        SetLayout(body.gameObject, 54f, 58f, 0f, 0f);
    }

    private void BuildHistory()
    {
        historyRoot = CreateObject("History", transform).GetComponent<RectTransform>();
        historyGroup = historyRoot.gameObject.AddComponent<CanvasGroup>();
        VerticalLayoutGroup historyColumn = historyRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        historyColumn.spacing = 10f;
        historyColumn.childControlWidth = true;
        historyColumn.childControlHeight = true;
        historyColumn.childForceExpandWidth = true;
        historyColumn.childForceExpandHeight = false;
        SetLayout(historyRoot.gameObject, 500f, 560f, 1f, 1f);

        GameObject banner = CreatePanel("History Introduction", historyRoot, PaleBlue);
        HorizontalLayoutGroup bannerRow = banner.AddComponent<HorizontalLayoutGroup>();
        bannerRow.padding = new RectOffset(16, 16, 8, 8);
        bannerRow.spacing = 12f;
        bannerRow.childAlignment = TextAnchor.MiddleLeft;
        bannerRow.childControlWidth = true;
        bannerRow.childControlHeight = true;
        bannerRow.childForceExpandWidth = false;
        bannerRow.childForceExpandHeight = true;
        SetLayout(banner, 60f, 62f, 0f, 0f);
        TMP_Text bannerTitle = CreateText("History Heading", banner.transform,
            "Recent Restaurant Performance", Navy, 15f, 20f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetLayout(bannerTitle.gameObject, 32f, 360f, 0f, 0f);
        GameObject bannerSpacer = CreateObject("Spacer", banner.transform);
        SetLayout(bannerSpacer, 1f, 1f, 1f, 0f);
        TMP_Text bannerDescription = CreateText("Description", banner.transform,
            "Compare sales, expenses and profit from completed days.", Muted,
            13f, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        SetLayout(bannerDescription.gameObject, 32f, 560f, 0f, 0f);

        GameObject table = CreatePanel("History Table", historyRoot, Color.white);
        VerticalLayoutGroup tableColumn = table.AddComponent<VerticalLayoutGroup>();
        tableColumn.padding = new RectOffset(0, 0, 0, 0);
        tableColumn.spacing = 0f;
        tableColumn.childControlWidth = true;
        tableColumn.childControlHeight = true;
        tableColumn.childForceExpandWidth = true;
        tableColumn.childForceExpandHeight = false;
        SetLayout(table, 420f, 450f, 1f, 1f);

        CreateHistoryHeader(table.transform);
        GameObject viewport = CreateObject("Viewport", table.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();
        SetLayout(viewport, 352f, 390f, 1f, 1f);
        GameObject content = CreateObject("Rows", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup rows = content.AddComponent<VerticalLayoutGroup>();
        rows.spacing = 0f;
        rows.childControlWidth = true;
        rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;

        TMP_Text emptyText = CreateText("History Empty State", viewport.transform,
            "Completed days will appear here after a shift ends.", Muted, 16f, 22f,
            FontStyles.Normal, TextAlignmentOptions.Center, true);
        Stretch(emptyText.rectTransform);
        historyEmptyState = emptyText.gameObject;

    }

    private void CreateHistoryHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, PaleBlue);
        HorizontalLayoutGroup row = ConfigureHistoryRow(header);
        SetLayout(header, 56f, 58f, 0f, 0f);
        AddHistoryCell(row.transform, "DAY", Muted, true);
        AddHistoryCell(row.transform, "SALES", Muted, true);
        AddHistoryCell(row.transform, "EXPENSES", Muted, true);
        AddHistoryCell(row.transform, "NET PROFIT", Muted, true);
        CreateDivider(parent, false);
    }

    private void Bind(FinanceDayReport report, IReadOnlyList<DailyFinanceSummarySaveEntry> history)
    {
        report ??= new FinanceDayReport { day = 1 };
        dayLabel.text = "DAY " + report.day + " • RESTAURANT FINANCIALS";
        TMP_Text statementHeading = FindText("Statement Heading");
        if (statementHeading != null)
            statementHeading.text = "DAILY RESTAURANT STATEMENT • DAY " + report.day;
        SetNamedAmount("Food Sales Amount", SignedMoney(report.foodAndDrinkSales, true));
        SetNamedAmount("Other Income Amount", SignedMoney(report.otherIncome, true));
        SetNamedAmount("Total Revenue Amount", Money(report.TotalRevenue));
        SetNamedAmount("Restock Amount", SignedMoney(report.ingredientRestock, false));
        SetNamedAmount("Payroll Amount", SignedMoney(report.staffPayroll, false));
        SetNamedAmount("Refund Amount", SignedMoney(report.refunds, false));
        SetNamedAmount("Other Costs Amount", SignedMoney(report.otherCosts, false));
        SetNamedAmount("Total Expenses Amount", Money(report.TotalExpenses));
        SetNamedAmount("Payroll Sidebar Amount", Money(report.staffPayroll));
        SetNamedAmount("Refund Sidebar Amount", Money(report.refunds));
        SetNamedAmount("Restock Sidebar Amount", Money(report.ingredientRestock));
        SetNamedAmount("Cash Sidebar Amount", Money(report.cashBalance));
        netProfitValue.color = report.NetProfit >= 0 ? Green : Red;

        RebuildHistory(history, report.day);
        SelectTip();
        ShowTab(true, false);
        StartCoroutine(AnimateMoney(salesValue, report.TotalRevenue, Green, 0.05f));
        StartCoroutine(AnimateMoney(expensesValue, report.TotalExpenses, Red, 0.08f));
        StartCoroutine(AnimateMoney(profitValue, report.NetProfit,
            report.NetProfit >= 0 ? Green : Red, 0.11f));
        StartCoroutine(AnimateMoney(cashValue, report.cashBalance, Navy, 0.14f));
        StartCoroutine(AnimateMoney(netProfitValue, report.NetProfit,
            report.NetProfit >= 0 ? Green : Red, 0.10f));
        StartCoroutine(AnimateTip());
    }

    private void RebuildHistory(
        IReadOnlyList<DailyFinanceSummarySaveEntry> history,
        int currentDay)
    {
        Transform rows = Find("Rows");
        if (rows == null)
            return;
        for (int i = rows.childCount - 1; i >= 0; i--)
            Destroy(rows.GetChild(i).gameObject);

        List<DailyFinanceSummarySaveEntry> ordered = new List<DailyFinanceSummarySaveEntry>();
        if (history != null)
        {
            for (int i = 0; i < history.Count; i++)
                if (history[i] != null && history[i].day < currentDay)
                    ordered.Add(history[i]);
        }
        ordered.Sort((left, right) => right.day.CompareTo(left.day));
        if (historyEmptyState != null)
            historyEmptyState.SetActive(ordered.Count == 0);
        if (ordered.Count == 0)
        {
            ForceHistoryLayout(rows as RectTransform);
            return;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            DailyFinanceSummarySaveEntry entry = ordered[i];
            GameObject line = CreateObject("Day " + entry.day, rows);
            HorizontalLayoutGroup row = ConfigureHistoryRow(line);
            SetLayout(line, 58f, 60f, 0f, 0f);
            AddHistoryCell(row.transform, "Day " + entry.day, Navy, true);
            AddHistoryCell(row.transform, Money(entry.sales), Green, false);
            AddHistoryCell(row.transform, Money(entry.expenses), Red, false);
            AddHistoryCell(row.transform, Money(entry.netProfit),
                entry.netProfit >= 0 ? Navy : Red, true);
            UIRevealAnimation reveal = line.AddComponent<UIRevealAnimation>();
            reveal.Play(Mathf.Min(0.18f, i * 0.025f));
            CreateDivider(rows, false);
        }
        ForceHistoryLayout(rows as RectTransform);
    }

    private HorizontalLayoutGroup ConfigureHistoryRow(GameObject line)
    {
        HorizontalLayoutGroup row = line.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        return row;
    }

    private void AddHistoryCell(Transform parent, string value, Color color, bool bold)
    {
        TMP_Text text = CreateText("Cell", parent, value, color, 15f, 20f,
            bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetLayout(text.gameObject, 36f, 80f, 1f, 0f);
    }

    private void ShowTab(bool today, bool animate)
    {
        todayButtonImage.color = today ? Blue : Navy;
        historyButtonImage.color = today ? Navy : Blue;
        if (tabRoutine != null)
            StopCoroutine(tabRoutine);
        RectTransform target = today ? todayRoot : historyRoot;
        CanvasGroup group = today ? todayGroup : historyGroup;
        RectTransform hidden = today ? historyRoot : todayRoot;
        hidden.gameObject.SetActive(false);
        target.gameObject.SetActive(true);
        if (!today)
            ForceHistoryLayout(Find("Rows") as RectTransform);
        if (!animate || LevelOneUIAccessibility.ReducedMotion)
        {
            group.alpha = 1f;
            target.localScale = Vector3.one;
            return;
        }
        tabRoutine = StartCoroutine(AnimateTab(target, group));
    }

    private static void ForceHistoryLayout(RectTransform rows)
    {
        if (rows == null)
            return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
        if (rows.parent is RectTransform viewport)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }
    }

    private IEnumerator AnimateTab(RectTransform target, CanvasGroup group)
    {
        group.alpha = 0f;
        target.localScale = Vector3.one * 0.985f;
        float elapsed = 0f;
        const float duration = 0.14f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = t;
            target.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, t);
            yield return null;
        }
        group.alpha = 1f;
        target.localScale = Vector3.one;
        tabRoutine = null;
    }

    private IEnumerator AnimateMoney(TMP_Text target, int value, Color color, float delay)
    {
        if (target == null)
            yield break;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            target.text = Money(value);
            target.color = color;
            yield break;
        }
        float waited = 0f;
        while (waited < delay)
        {
            waited += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            yield return null;
        }
        float elapsed = 0f;
        const float duration = 0.24f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.text = Money(Mathf.RoundToInt(value * t));
            yield return null;
        }
        target.text = Money(value);
        target.color = color;
    }

    private void SelectTip()
    {
        int index = UnityEngine.Random.Range(0, Tips.Length);
        if (Tips.Length > 1 && index == lastTipIndex)
            index = (index + UnityEngine.Random.Range(1, Tips.Length)) % Tips.Length;
        lastTipIndex = index;
        TMP_Text text = FindText("Tip Text");
        if (text != null)
            text.text = Tips[index];
    }

    private IEnumerator AnimateTip()
    {
        if (tipGroup == null)
            yield break;
        RectTransform rect = tipGroup.transform as RectTransform;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            tipGroup.alpha = 1f;
            if (rect != null) rect.localScale = Vector3.one;
            yield break;
        }
        tipGroup.alpha = 0f;
        if (rect != null) rect.localScale = Vector3.one * 0.98f;
        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            tipGroup.alpha = t;
            if (rect != null)
                rect.localScale = Vector3.one * Mathf.Lerp(0.98f, 1f, t);
            yield return null;
        }
        tipGroup.alpha = 1f;
        if (rect != null) rect.localScale = Vector3.one;
    }

    private void SetNamedAmount(string objectName, string value)
    {
        TMP_Text text = FindText(objectName);
        if (text != null)
            text.text = value;
    }

    private Transform Find(string objectName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == objectName)
                return all[i];
        return null;
    }

    private TMP_Text FindText(string objectName)
    {
        Transform found = Find(objectName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = parent != null ? parent.gameObject.layer : gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = CreateObject(objectName, parent);
        Image image = panel.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        float width)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, color);
        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
        colors.pressedColor = new Color(0.82f, 0.87f, 0.92f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        SetLayout(buttonObject, 52f, width, 0f, 0f);
        TMP_Text text = CreateText("Label", buttonObject.transform, label, Color.white,
            16f, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        buttonObject.AddComponent<UISubtlePressFeedback>();
        return button;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        Color color,
        float minimum,
        float maximum,
        FontStyles style,
        TextAlignmentOptions alignment,
        bool wrap = false,
        bool display = false)
    {
        GameObject textObject = CreateObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        bool bold = (style & FontStyles.Bold) != 0;
        text.font = display
            ? displayFont
            : bold && readableBoldFont != null
                ? readableBoldFont
                : readableFont;
        text.text = value ?? string.Empty;
        text.color = color;
        text.fontStyle = bold && readableBoldFont != null && !display
            ? style & ~FontStyles.Bold
            : style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void CreateDivider(Transform parent, bool dashed)
    {
        GameObject divider = CreateObject(dashed ? "Statement Divider" : "Divider", parent);
        Image image = divider.AddComponent<Image>();
        image.color = Divider;
        image.raycastTarget = false;
        SetLayout(divider, dashed ? 2f : 1f, 1f, 0f, 0f);
    }

    private static void SetLayout(
        GameObject target,
        float height,
        float preferredWidth,
        float flexibleWidth,
        float flexibleHeight)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>() ??
                               target.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static string Money(int value)
    {
        return (value < 0 ? "-₱" : "₱") + Mathf.Abs(value).ToString("N0");
    }

    private static string SignedMoney(int value, bool positive)
    {
        if (value <= 0)
            return "₱0";
        return (positive ? "+₱" : "-₱") + value.ToString("N0");
    }
}
