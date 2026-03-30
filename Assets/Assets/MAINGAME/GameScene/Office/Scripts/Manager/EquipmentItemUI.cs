using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentItemUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text costText;
    public Button buyButton;
    private Equipment equip;
    private bool listenerAdded = false;

    public void Setup(Equipment e)
    {
        equip = e;
        nameText.text = e.displayName;
        costText.text = $"₱{e.cost}";

        if (!listenerAdded)
        {
            buyButton.onClick.AddListener(OnBuy);
            listenerAdded = true;
        }

        RefreshUI();
    }

    public void OnBuy()
    {
        if (equip == null) return;

        Debug.Log("Purchase called for: " + equip.itemID);
        EquipmentManager.Instance.Purchase(equip.itemID);
    }

    public void RefreshUI()
    {
        if (equip == null || buyButton == null || MoneyManager.Instance == null)
            return;

        bool alreadyBought = EquipmentManager.Instance.Purchased(equip.itemID);
        buyButton.interactable = MoneyManager.Instance.Money >= equip.cost && !alreadyBought;
    }
}