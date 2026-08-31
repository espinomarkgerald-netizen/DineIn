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
