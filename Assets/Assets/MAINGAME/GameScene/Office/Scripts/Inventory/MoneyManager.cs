using System;
using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance;

    [SerializeField] private int startingMoney = 500;
    public int Money { get; private set; }

    public event Action<int> OnMoneyChanged; // 👈 add this

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

    public bool Spend(int amount)
    {
        if (Money < amount) return false;

        Money -= amount;
        OnMoneyChanged?.Invoke(Money); // 👈 notify UI
        return true;
    }

    public void Earn(int amount)
    {
        Money += amount;
        OnMoneyChanged?.Invoke(Money); // 👈 notify UI
    }
}