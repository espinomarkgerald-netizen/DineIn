using TMPro;
using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    public TMP_Text moneyText;

    void Start()
    {
        UpdateMoney(MoneyManager.Instance.Money);
        MoneyManager.Instance.OnMoneyChanged += UpdateMoney;
    }

    void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoney;
    }

    void UpdateMoney(int amount)
    {
        moneyText.text = $"₱{amount:N0}";
    }
}