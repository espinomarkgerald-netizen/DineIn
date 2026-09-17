#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Opt-in external runner only; never executes on asset import.
public static class StockoutRegressionCases
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Run()
    {
        var roots = new List<GameObject>();
        T Fixture<T>() where T : Component
        { var root = new GameObject(typeof(T).Name); root.SetActive(false); roots.Add(root); return root.AddComponent<T>(); }
        var dayField = typeof(GameDayManager).GetField("<Instance>k__BackingField", Flags);
        var revenueField = typeof(DailyRevenueTracker).GetField("<Instance>k__BackingField", Flags);
        var approvalField = typeof(AlienApprovalManager).GetField("<Instance>k__BackingField", Flags);
        object oldDay = dayField.GetValue(null), oldRevenue = revenueField.GetValue(null), oldApproval = approvalField.GetValue(null);
        try
        {
            var day = Fixture<GameDayManager>(); dayField.SetValue(null, day);
            typeof(GameDayManager).GetField("shiftRunning", Flags).SetValue(day, true);
            var revenue = Fixture<DailyRevenueTracker>(); revenueField.SetValue(null, revenue);
            approvalField.SetValue(null, null);
            var group = Fixture<CustomerGroup>();
            var report = typeof(CustomerGroup).GetMethod("ReportFinalResult", Flags);
            report.Invoke(group, new object[] { CustomerGroup.FinalResult.Neutral, false });
            report.Invoke(group, new object[] { CustomerGroup.FinalResult.Neutral, false });
            Check(day.NeutralCustomers == 1 && revenue.OrdersFailed == 1 && revenue.OrdersCompleted == 0,
                "Stockout must count one unsatisfied group and one failure, without completed-order credit.");
            var served = Fixture<CustomerGroup>();
            report.Invoke(served, new object[] { CustomerGroup.FinalResult.Neutral, true });
            Check(day.NeutralCustomers == 2 && revenue.OrdersCompleted == 1, "Served neutral outcomes changed.");
            var observer = Fixture<CustomerGroup>(); observer.IsNetworkObserver = true;
            report.Invoke(observer, new object[] { CustomerGroup.FinalResult.Neutral, false });
            Check(day.NeutralCustomers == 2 && revenue.OrdersFailed == 1, "Guest presentation changed authoritative counters.");
            observer.PresentStockWait(true, 19f);
            var tick = typeof(CustomerGroup).GetMethod("TickStockWait", Flags);
            tick.Invoke(observer, null);
            Check(observer.WaitingForStock && observer.StockWaitRemaining == 19f, "Guest independently advanced stockout patience.");
            observer.PresentStockWait(false, 19f);
            Check(!observer.WaitingForStock && observer.StockWaitRemaining == 19f, "Recovery replenished the waiting allowance.");
        }
        finally
        {
            foreach (var root in roots) Object.DestroyImmediate(root);
            dayField.SetValue(null, oldDay); revenueField.SetValue(null, oldRevenue); approvalField.SetValue(null, oldApproval);
        }
    }
    private static void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }
}
#endif
