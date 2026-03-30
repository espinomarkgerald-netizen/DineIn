using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentItemUI : MonoBehaviour
{
    public Image equipmentImage;
    public TMP_Text nameText;
    public TMP_Text costText;
    public Button buyButton;
    private Equipment equip;

    public void Setup(Equipment e)
    {
        equip = e;
        nameText.text = e.displayName;
        RefreshUI();

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() =>
        {
            if (EquipmentManager.Instance.Purchase(equip.itemID, MoneyManager.Instance.Money))
                RefreshUI();
        });
    }

    public void RefreshUI()
    {
        int level = EquipmentManager.Instance.GetLevel(equip.itemID);
        costText.text = level < equip.upgradeLevels.Length ? $"₱{equip.cost}" : "MAX";
        buyButton.interactable = level < equip.upgradeLevels.Length;
    }
}