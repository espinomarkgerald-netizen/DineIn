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
    private Image fillImage;

    private void Awake()
    {
        AutoResolveReferences();
        ForceVisible();
    }

    private void OnEnable()
    {
        ForceVisible();
    }

    private void AutoResolveReferences()
    {
        if (foodImage == null || drinkImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                string n = images[i].name.ToLower();

                if (foodImage == null && n.Contains("food"))
                    foodImage = images[i];

                if (drinkImage == null && n.Contains("drink"))
                    drinkImage = images[i];
            }
        }

        if (patienceSlider == null)
            patienceSlider = GetComponentInChildren<Slider>(true);

        if (patienceSlider != null && patienceSlider.fillRect != null)
            fillImage = patienceSlider.fillRect.GetComponent<Image>();
    }

    private void ForceVisible()
    {
        gameObject.SetActive(true);

        CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].alpha = 1f;
            groups[i].interactable = true;
            groups[i].blocksRaycasts = true;
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = true;

        if (foodImage != null) foodImage.enabled = true;
        if (drinkImage != null) drinkImage.enabled = true;

        if (patienceSlider != null)
            patienceSlider.gameObject.SetActive(true);
    }

    public void Init(CustomerGroup g)
    {
        group = g;
        AutoResolveReferences();
        ForceVisible();
        SetPatience(1f);
    }

    public void SetOrder(Sprite foodSprite, Sprite drinkSprite)
    {
        AutoResolveReferences();
        ForceVisible();

        if (foodImage != null)
        {
            foodImage.sprite = foodSprite;
            foodImage.enabled = foodSprite != null;
        }
        else
        {
            Debug.LogWarning("[OrderBubbleUI] foodImage is missing.");
        }

        if (drinkImage != null)
        {
            drinkImage.sprite = drinkSprite;
            drinkImage.enabled = drinkSprite != null;
        }
        else
        {
            Debug.LogWarning("[OrderBubbleUI] drinkImage is missing.");
        }

        Debug.Log($"[OrderBubbleUI] SetOrder food={(foodSprite != null ? foodSprite.name : "NULL")} drink={(drinkSprite != null ? drinkSprite.name : "NULL")}");
    }

    public void SetPatience(float normalized)
    {
        if (patienceSlider == null) return;

        normalized = Mathf.Clamp01(normalized);
        patienceSlider.value = normalized;

        if (fillImage == null && patienceSlider.fillRect != null)
            fillImage = patienceSlider.fillRect.GetComponent<Image>();

        if (fillImage == null) return;

        if (normalized > 0.6f)
            fillImage.color = greenColor;
        else if (normalized > 0.3f)
            fillImage.color = yellowColor;
        else
            fillImage.color = redColor;
    }

    public void OnClickBubble()
    {
        if (group == null) return;
        if (RoleManager.Instance == null) return;

        if (GameplayUIBlocker.IsBlockedExcept(gameObject))
        {
            Debug.Log("[OrderBubbleUI] Blocked by other gameplay UI.");
            return;
        }

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