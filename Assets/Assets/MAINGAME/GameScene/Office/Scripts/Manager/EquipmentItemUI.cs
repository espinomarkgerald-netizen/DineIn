using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentItemUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text costText;
    public Button buyButton;
    public Image equipmentImage;

    private Equipment equip;
    private bool listenerAdded = false;

    public void Setup(Equipment e, bool unlocked)
    {
        equip = e;

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuy);

        if (unlocked)
        {
            nameText.text = e.displayName;
            costText.text = $"₱{e.cost}";
            equipmentImage.sprite = e.sprite;

            bool alreadyBought = EquipmentManager.Instance.Purchased(e.itemID);

            buyButton.interactable =
                !alreadyBought &&
                MoneyManager.Instance.Money >= e.cost;
        }
        else
        {
            nameText.text = "???";
            costText.text = $"Unlock at Day {e.dayToUnlock}";
            buyButton.interactable = false;
        }
    }

    public void OnBuy()
    {
        if (equip == null) return;
        EquipmentManager.Instance.Purchase(equip.itemID);
    }
}