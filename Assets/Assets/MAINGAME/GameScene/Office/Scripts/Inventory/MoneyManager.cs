using UnityEngine;
using System;
using System.Collections.Generic;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance;

    [Header("Finance Setup")]
    [SerializeField] private int startingMoney = 500;
    public int Money { get; private set; }

    [Header("Optional Inspector Debug")]
    [SerializeField] private List<string> transactionLog = new List<string>();

    public event Action<int> OnMoneyChanged;

    private bool initialized;

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
        NotifyMoneyChanged();
        Debug.Log("[MoneyManager] Start current money = " + Money);
    }

    public void Earn(int amount, string description = "Income")
    {
        if (amount <= 0)
            return;

        Money += amount;
        LogTransaction($"+{amount}: {description}");
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
        LogTransaction($"-{amount}: {description}");
        Debug.Log("[MoneyManager] Spend -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public void SetMoney(int amount, string description = "Set Money")
    {
        Money = Mathf.Max(0, amount);
        LogTransaction($"={Money}: {description}");
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

        Money = Mathf.Max(0, Money - amount);
        LogTransaction($"-{amount} (forced): {description}");
        Debug.Log("[MoneyManager] ForceSpend -> " + Money);
        NotifyMoneyChanged();
        GameSaveManager.Instance?.RequestSave();
    }

    public IReadOnlyList<string> TransactionLog => transactionLog;

    private void NotifyMoneyChanged()
    {
        OnMoneyChanged?.Invoke(Money);
    }

    private void LogTransaction(string entry)
    {
        transactionLog.Add(entry);
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
        Debug.Log("[MoneyManager] FillSaveData saved money = " + Money);
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        Money = Mathf.Max(0, data.money);
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