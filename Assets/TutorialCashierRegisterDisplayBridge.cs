using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialCashierRegisterDisplayBridge : MonoBehaviour
{
    [Header("Current Order")]
    [SerializeField] private TMP_Text tableNumberText;

    [Header("Food")]
    [SerializeField] private Image foodImage;
    [SerializeField] private Image foodImage2;
    [SerializeField] private TMP_Text foodPriceText;

    [Header("Drink")]
    [SerializeField] private Image drinkImage;
    [SerializeField] private TMP_Text drinkPriceText;

    [Header("Top Totals")]
    [SerializeField] private TMP_Text receivedText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text changeText;

    public void Apply(TutorialCashierOrderRandomizer.GeneratedOrder order)
    {
        if (tableNumberText != null)
            tableNumberText.text = order.orderNumber.ToString();

        if (foodImage != null)
        {
            foodImage.sprite = order.firstFoodSprite;
            foodImage.enabled = order.firstFoodSprite != null;
        }

        if (foodImage2 != null)
        {
            foodImage2.sprite = order.secondFoodSprite;
            foodImage2.enabled = order.secondFoodSprite != null;
        }

        if (foodPriceText != null)
            foodPriceText.text = order.firstFoodSprite != null ? order.foodTotal.ToString("0.00") : "";

        if (drinkImage != null)
        {
            drinkImage.sprite = order.drinkSprite;
            drinkImage.enabled = order.drinkSprite != null;
        }

        if (drinkPriceText != null)
            drinkPriceText.text = order.drinkSprite != null ? order.drinkTotal.ToString("0.00") : "";

        int change = Mathf.Max(0, order.received - order.total);

        if (receivedText != null)
            receivedText.text = order.received.ToString("0.00");

        if (totalText != null)
            totalText.text = order.total.ToString("0.00");

        if (changeText != null)
            changeText.text = change.ToString("0.00");
    }
}