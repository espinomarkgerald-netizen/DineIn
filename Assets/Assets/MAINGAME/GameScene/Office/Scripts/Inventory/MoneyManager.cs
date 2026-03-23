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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Money = startingMoney;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        OnMoneyChanged?.Invoke(Money);
    }

    public void Earn(int amount, string description = "Income")
    {
        Money += amount;
        OnMoneyChanged?.Invoke(Money);
        transactionLog.Add($"+{amount}: {description}");
    }

    public bool Spend(int amount, string description = "Expense")
    {
        if (Money < amount) return false;

        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        transactionLog.Add($"-{amount}: {description}");
        return true;
    }

    public List<string> TransactionLog => transactionLog;
}