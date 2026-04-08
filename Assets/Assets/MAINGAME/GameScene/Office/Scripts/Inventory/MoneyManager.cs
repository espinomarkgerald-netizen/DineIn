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
        }
    }

    private void Start()
    {
        NotifyMoneyChanged();
    }

    public void Earn(int amount, string description = "Income")
    {
        if (amount <= 0)
            return;

        Money += amount;
        LogTransaction($"+{amount}: {description}");
        NotifyMoneyChanged();
    }

    public bool Spend(int amount, string description = "Expense")
    {
        if (amount < 0) // only reject negatives
            return false;

        if (Money < amount)
            return false;

        Money -= amount;
        LogTransaction($"-{amount}: {description}");
        NotifyMoneyChanged();
        return true;
    }

    public void SetMoney(int amount, string description = "Set Money")
    {
        Money = Mathf.Max(0, amount);
        LogTransaction($"={Money}: {description}");
        NotifyMoneyChanged();
    }

    public bool HasEnough(int amount)
    {
        return Money >= amount;
    }

    /// <summary>
    /// Unconditionally deducts the amount, flooring Money at zero.
    /// Use this only for end-of-day expense settlement where the deduction
    /// must always happen regardless of available funds — allowing bankruptcy
    /// to be detected by EvaluateEndOfDay() afterwards.
    /// </summary>
    public void ForceSpend(int amount, string description = "Forced Expense")
    {
        if (amount <= 0)
            return;

        Money = Mathf.Max(0, Money - amount);
        LogTransaction($"-{amount} (forced): {description}");
        NotifyMoneyChanged();
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
}