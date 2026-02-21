using UnityEngine;
using UnityEngine.UI;

public class OrderBubbleUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Image foodImage;
    public Image drinkImage;

    private CustomerGroup group;

    public void Init(CustomerGroup g) => group = g;

    public void SetOrder(Sprite foodSprite, Sprite drinkSprite)
    {
        if (foodImage != null) foodImage.sprite = foodSprite;
        if (drinkImage != null) drinkImage.sprite = drinkSprite;
    }

    // Hook this to Button.OnClick on the bubble
    public void OnClickBubble()
    {
        if (group == null) return;

        if (OrderChecklistUI.Instance == null)
        {
            Debug.LogError("[OrderBubbleUI] OrderChecklistUI.Instance is NULL (Notepad not in scene or Awake not run).");
            return;
        }

        OrderChecklistUI.Instance.Open(group);
    }
}