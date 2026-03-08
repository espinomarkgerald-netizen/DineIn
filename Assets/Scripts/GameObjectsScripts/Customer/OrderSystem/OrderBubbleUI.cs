using UnityEngine;
using UnityEngine.UI;

public class OrderBubbleUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Image foodImage;
    public Image drinkImage;

    [Header("Patience")]
    [SerializeField] private Slider patienceSlider;

    [Header("Colors")]
    [SerializeField] private Color greenColor = Color.green;
    [SerializeField] private Color yellowColor = Color.yellow;
    [SerializeField] private Color redColor = Color.red;

    private CustomerGroup group;

    public void Init(CustomerGroup g)
    {
        group = g;
        SetPatience(1f);
    }

    public void SetOrder(Sprite foodSprite, Sprite drinkSprite)
    {
        if (foodImage != null) foodImage.sprite = foodSprite;
        if (drinkImage != null) drinkImage.sprite = drinkSprite;
    }

    public void SetPatience(float normalized)
    {
        if (patienceSlider == null) return;

        patienceSlider.value = normalized;

        var fill = patienceSlider.fillRect.GetComponent<Image>();
        if (fill == null) return;

        if (normalized > 0.6f)
            fill.color = greenColor;
        else if (normalized > 0.3f)
            fill.color = yellowColor;
        else
            fill.color = redColor;
    }

    public void OnClickBubble()
    {
        if (group == null) return;

        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            Debug.Log("[OrderBubbleUI] Only waiter can open the notepad.");
            return;
        }

        if (OrderChecklistUI.Instance == null)
        {
            Debug.LogError("[OrderBubbleUI] OrderChecklistUI.Instance NULL");
            return;
        }

        OrderChecklistUI.Instance.Open(group);
    }
}