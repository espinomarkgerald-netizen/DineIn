using TMPro;
using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;

    private void OnEnable()
    {
        TryBind();
    }

    private void Start()
    {
        TryBind();
    }

    private void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoney;
    }

    private void TryBind()
    {
        if (moneyText == null)
        {
            Debug.LogWarning("[MoneyUI] moneyText is not assigned.");
            return;
        }

        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("[MoneyUI] MoneyManager not found in scene.");
            return;
        }

        MoneyManager.Instance.OnMoneyChanged -= UpdateMoney;
        MoneyManager.Instance.OnMoneyChanged += UpdateMoney;
        UpdateMoney(MoneyManager.Instance.Money);
    }

    private void UpdateMoney(int amount)
    {
        if (moneyText == null)
            return;

        moneyText.text = $"₱{amount:N0}";
    }
}