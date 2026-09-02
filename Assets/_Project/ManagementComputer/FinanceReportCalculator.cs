using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class FinanceDayReport
{
    public int day;
    public int foodAndDrinkSales;
    public int otherIncome;
    public int ingredientRestock;
    public int staffPayroll;
    public int refunds;
    public int otherCosts;
    public int cashBalance;

    public int TotalRevenue => Mathf.Max(0, foodAndDrinkSales + otherIncome);
    public int TotalExpenses => Mathf.Max(0,
        ingredientRestock + staffPayroll + refunds + otherCosts);
    public int NetProfit => TotalRevenue - TotalExpenses;
}

/// <summary>
/// Converts the existing wallet transaction ledger into the small set of
/// categories shown by the Finance app. It does not own or mutate money.
/// </summary>
public static class FinanceReportCalculator
{
    public static FinanceDayReport BuildDay(
        int day,
        IReadOnlyList<MoneyTransactionSaveEntry> transactions,
        int scheduledPayroll,
        int cashBalance,
        int fallbackSales = 0,
        int fallbackRestock = 0,
        bool includePaidPayroll = true)
    {
        FinanceDayReport report = new FinanceDayReport
        {
            day = Mathf.Max(1, day),
            cashBalance = Mathf.Max(0, cashBalance)
        };

        if (transactions != null)
        {
            for (int i = 0; i < transactions.Count; i++)
            {
                MoneyTransactionSaveEntry entry = transactions[i];
                if (entry == null || entry.day != day || entry.adjustment ||
                    entry.amountDelta == 0)
                    continue;

                string description = entry.description ?? string.Empty;
                string category = description.Trim().ToLowerInvariant();
                if (entry.amountDelta > 0)
                {
                    if (category.Contains("rollback"))
                    {
                        report.ingredientRestock = Mathf.Max(
                            0,
                            report.ingredientRestock - entry.amountDelta);
                    }
                    else if (IsCustomerSale(category))
                    {
                        report.foodAndDrinkSales += entry.amountDelta;
                    }
                    else
                    {
                        report.otherIncome += entry.amountDelta;
                    }
                    continue;
                }

                int amount = Mathf.Abs(entry.amountDelta);
                if (category.Contains("refund"))
                {
                    report.refunds += amount;
                }
                else if (category.Contains("payroll") || category.Contains("salary"))
                {
                    if (includePaidPayroll)
                        report.staffPayroll += amount;
                }
                else if (category.Contains("restock") || category.Contains("ingredient"))
                {
                    report.ingredientRestock += amount;
                }
                else
                {
                    report.otherCosts += amount;
                }
            }
        }

        if (report.TotalRevenue <= 0 && fallbackSales > 0)
            report.foodAndDrinkSales = fallbackSales;
        if (report.ingredientRestock <= 0 && fallbackRestock > 0)
            report.ingredientRestock = fallbackRestock;
        // scheduledPayroll is a forecast only. Payroll becomes a Finance
        // expense when FinanceManager records its end-of-day transaction.

        return report;
    }

    public static DailyFinanceSummarySaveEntry ToSummary(FinanceDayReport report)
    {
        if (report == null)
            return null;
        return new DailyFinanceSummarySaveEntry
        {
            day = report.day,
            sales = report.TotalRevenue,
            expenses = report.TotalExpenses,
            netProfit = report.NetProfit
        };
    }

    private static bool IsCustomerSale(string description)
    {
        return string.IsNullOrEmpty(description) ||
               description.Contains("daily earnings") ||
               description.Contains("cashier") ||
               description.Contains("kitchen order") ||
               description.Contains("customer payment") ||
               description == "income";
    }
}
