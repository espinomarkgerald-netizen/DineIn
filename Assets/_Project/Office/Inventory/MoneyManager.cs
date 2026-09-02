using UnityEngine;
using System;
using System.Collections.Generic;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance;

    [Header("Finance Setup")]
    [SerializeField] private int startingMoney = 5000;
    public int Money { get; private set; }

    [Header("Optional Inspector Debug")]
    [SerializeField] private List<string> transactionLog = new List<string>();
    [SerializeField] private List<MoneyTransactionSaveEntry> dailyTransactions =
        new List<MoneyTransactionSaveEntry>();
    [SerializeField] private List<DailyFinanceSummarySaveEntry> completedFinanceHistory =
        new List<DailyFinanceSummarySaveEntry>();

    public event Action<int> OnMoneyChanged;

    private bool initialized;
    private PlayFabWalletManager boundWallet;
    private int pendingWalletDelta;
    private float nextWalletBindAttempt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!initialized)
        {
            Money = Mathf.Max(0, startingMoney);
            initialized = true;
            Debug.Log("[MoneyManager] Awake default money = " + Money);
        }
    }

    private void Start()
    {
        TryBindWallet();
        NotifyMoneyChanged();
        Debug.Log("[MoneyManager] Start current money = " + Money);
    }

    private void Update()
    {
        if (boundWallet == null && Time.unscaledTime >= nextWalletBindAttempt)
        {
            nextWalletBindAttempt = Time.unscaledTime + 1f;
            TryBindWallet();
        }
    }

    private void OnDestroy()
    {
        if (boundWallet != null)
            boundWallet.OnWalletUpdated -= ApplyWalletBalance;

        if (Instance == this)
            Instance = null;
    }

    public void Earn(int amount, string description = "Income")
    {
        if (amount <= 0)
            return;

        Money += amount;
        SyncWalletDelta(amount);
        LogTransaction($"+{amount}: {description}", amount, description, false);
        Debug.Log("[MoneyManager] Earn -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
    }

    public bool Spend(int amount, string description = "Expense")
    {
        if (amount < 0)
            return false;

        if (Money < amount)
            return false;

        Money -= amount;
        SyncWalletDelta(-amount);
        LogTransaction($"-{amount}: {description}", -amount, description, false);
        Debug.Log("[MoneyManager] Spend -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public void SetMoney(int amount, string description = "Set Money")
    {
        int previousMoney = Money;
        Money = Mathf.Max(0, amount);
        SyncWalletDelta(Money - previousMoney);
        LogTransaction($"={Money}: {description}", Money - previousMoney, description, true);
        Debug.Log("[MoneyManager] SetMoney -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
    }

    public bool HasEnough(int amount)
    {
        return Money >= amount;
    }

    public void ForceSpend(int amount, string description = "Forced Expense")
    {
        if (amount <= 0)
            return;

        int previousMoney = Money;
        Money = Mathf.Max(0, Money - amount);
        SyncWalletDelta(Money - previousMoney);
        LogTransaction(
            $"-{amount} (forced): {description}",
            Money - previousMoney,
            description,
            false);
        Debug.Log("[MoneyManager] ForceSpend -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
    }

    public IReadOnlyList<string> TransactionLog => transactionLog;
    public IReadOnlyList<MoneyTransactionSaveEntry> DailyTransactions => dailyTransactions;
    public IReadOnlyList<DailyFinanceSummarySaveEntry> CompletedFinanceHistory =>
        completedFinanceHistory;

    public void RecordCompletedFinanceDay(int day)
    {
        if (day <= 0)
            return;

        DailyFinanceBridge bridge = DailyFinanceBridge.Instance;
        FinanceDayReport report = FinanceReportCalculator.BuildDay(
            day,
            dailyTransactions,
            0,
            Money,
            bridge != null ? bridge.EarnedToday : 0,
            bridge != null ? bridge.IngredientCostToday : 0);
        DailyFinanceSummarySaveEntry summary = FinanceReportCalculator.ToSummary(report);
        UpsertFinanceSummary(summary);
    }

    private void UpsertFinanceSummary(DailyFinanceSummarySaveEntry summary)
    {
        if (summary == null || summary.day <= 0)
            return;
        completedFinanceHistory ??= new List<DailyFinanceSummarySaveEntry>();
        int index = completedFinanceHistory.FindIndex(entry =>
            entry != null && entry.day == summary.day);
        DailyFinanceSummarySaveEntry copy = new DailyFinanceSummarySaveEntry
        {
            day = summary.day,
            sales = Mathf.Max(0, summary.sales),
            expenses = Mathf.Max(0, summary.expenses),
            netProfit = summary.netProfit
        };
        if (index >= 0)
            completedFinanceHistory[index] = copy;
        else
            completedFinanceHistory.Add(copy);

        completedFinanceHistory.Sort((left, right) => left.day.CompareTo(right.day));
        const int retainedDays = 90;
        if (completedFinanceHistory.Count > retainedDays)
            completedFinanceHistory.RemoveRange(
                0,
                completedFinanceHistory.Count - retainedDays);
    }

    private void NotifyMoneyChanged()
    {
        OnMoneyChanged?.Invoke(Money);
    }

    private void LogTransaction(
        string entry,
        int amountDelta,
        string description,
        bool adjustment)
    {
        transactionLog.Add(entry);
        dailyTransactions.Add(new MoneyTransactionSaveEntry
        {
            day = GameFlowManager.Instance != null
                ? Mathf.Max(1, GameFlowManager.Instance.CurrentDay)
                : 0,
            amountDelta = amountDelta,
            description = string.IsNullOrWhiteSpace(description) ? "Transaction" : description,
            adjustment = adjustment
        });

        // Keep saves small during long endless-mode runs while retaining enough
        // history for the current-day Finance app.
        const int retainedEntries = 160;
        if (dailyTransactions.Count > retainedEntries)
            dailyTransactions.RemoveRange(0, dailyTransactions.Count - retainedEntries);
    }

    private void TryBindWallet()
    {
        PlayFabWalletManager wallet = PlayFabWalletManager.Instance;
        if (wallet == null || wallet == boundWallet)
            return;

        if (boundWallet != null)
            boundWallet.OnWalletUpdated -= ApplyWalletBalance;

        boundWallet = wallet;
        boundWallet.OnWalletUpdated += ApplyWalletBalance;

        int queuedDelta = pendingWalletDelta;
        pendingWalletDelta = 0;

        if (boundWallet.HasLoadedWallet)
        {
            Money = Mathf.Max(0, boundWallet.NormalMoney + queuedDelta);
            NotifyMoneyChanged();
        }

        if (queuedDelta != 0)
            boundWallet.ChangeNormalMoney(queuedDelta);
    }

    private void SyncWalletDelta(int delta)
    {
        if (delta == 0)
            return;

        TryBindWallet();
        if (boundWallet != null)
            boundWallet.ChangeNormalMoney(delta);
        else
            pendingWalletDelta += delta;
    }

    private void ApplyWalletBalance(int _, int normalMoney)
    {
        Money = Mathf.Max(0, normalMoney);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
    }

    public void ResetToStartingMoney()
    {
        SetMoney(startingMoney, "Bankruptcy Reset");
    }

    public void ResetFinanceHistory()
    {
        transactionLog?.Clear();
        dailyTransactions?.Clear();
        completedFinanceHistory?.Clear();
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.money = Money;
        if (data.moneyTransactions == null)
            data.moneyTransactions = new List<MoneyTransactionSaveEntry>();
        data.moneyTransactions.Clear();
        foreach (MoneyTransactionSaveEntry entry in dailyTransactions)
        {
            if (entry == null)
                continue;
            data.moneyTransactions.Add(new MoneyTransactionSaveEntry
            {
                day = entry.day,
                amountDelta = entry.amountDelta,
                description = entry.description,
                adjustment = entry.adjustment
            });
        }
        data.financeHistory ??= new List<DailyFinanceSummarySaveEntry>();
        data.financeHistory.Clear();
        if (completedFinanceHistory != null)
        {
            foreach (DailyFinanceSummarySaveEntry entry in completedFinanceHistory)
            {
                if (entry == null)
                    continue;
                data.financeHistory.Add(new DailyFinanceSummarySaveEntry
                {
                    day = entry.day,
                    sales = entry.sales,
                    expenses = entry.expenses,
                    netProfit = entry.netProfit
                });
            }
        }
        Debug.Log("[MoneyManager] FillSaveData saved money = " + Money);
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        Money = Mathf.Max(0, data.money);
        dailyTransactions.Clear();
        if (data.moneyTransactions != null)
        {
            foreach (MoneyTransactionSaveEntry entry in data.moneyTransactions)
            {
                if (entry == null)
                    continue;
                dailyTransactions.Add(new MoneyTransactionSaveEntry
                {
                    day = entry.day,
                    amountDelta = entry.amountDelta,
                    description = entry.description,
                    adjustment = entry.adjustment
                });
            }
        }
        completedFinanceHistory ??= new List<DailyFinanceSummarySaveEntry>();
        completedFinanceHistory.Clear();
        if (data.financeHistory != null)
        {
            foreach (DailyFinanceSummarySaveEntry entry in data.financeHistory)
                UpsertFinanceSummary(entry);
        }

        // Older saves already contain the source transaction ledger. Rebuild
        // recent completed days once rather than discarding that history.
        if (completedFinanceHistory.Count == 0 && dailyTransactions.Count > 0)
        {
            HashSet<int> days = new HashSet<int>();
            foreach (MoneyTransactionSaveEntry entry in dailyTransactions)
            {
                if (entry != null && entry.day > 0 && entry.day < data.currentDay)
                    days.Add(entry.day);
            }
            foreach (int day in days)
            {
                FinanceDayReport report = FinanceReportCalculator.BuildDay(
                    day,
                    dailyTransactions,
                    0,
                    Money);
                UpsertFinanceSummary(FinanceReportCalculator.ToSummary(report));
            }
        }
        if (completedFinanceHistory.Count == 0 && data.lastDailyRestaurantSnapshot != null)
        {
            DailyRestaurantSnapshotSaveData snapshot = data.lastDailyRestaurantSnapshot;
            UpsertFinanceSummary(new DailyFinanceSummarySaveEntry
            {
                day = snapshot.day,
                sales = Mathf.Max(0, snapshot.revenue),
                expenses = Mathf.Max(0,
                    snapshot.ingredientCost + snapshot.employeeCost + snapshot.otherCosts),
                netProfit = snapshot.profit
            });
        }
        Debug.Log("[MoneyManager] ApplySaveData loaded money = " + Money);
        NotifyMoneyChanged();
    }

    [ContextMenu("Debug Set Money To 10000 And Save")]
    private void DebugSetMoneyTo10000AndSave()
    {
        SetMoney(10000, "Debug Test");
        GameSaveManager.Instance?.SaveGame();
        Debug.Log("[MoneyManager] Debug set to 10000 and requested save.");
    }

    [ContextMenu("Log Current Money")]
    private void LogCurrentMoney()
    {
        Debug.Log("[MoneyManager] Current Money = " + Money);
    }
}
