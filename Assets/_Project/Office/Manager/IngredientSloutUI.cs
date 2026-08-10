using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class IngredientSlotUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text amountText;

    public void Setup(ItemData item, int amount)
    {
        icon.sprite = item.sprite;
        amountText.text = "x" + amount;
    }
}
