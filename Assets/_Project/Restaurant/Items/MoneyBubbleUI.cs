using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoneyBubbleUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text tableNumberText;
    [Header("Editable Payment Presentation")]
    [SerializeField] private Image paymentFrame;
    [SerializeField] private Sprite cashFrameSprite;
    [SerializeField] private Sprite cardFrameSprite;
    [SerializeField] private Image paymentIcon;
    [SerializeField] private Sprite cashIcon;
    [SerializeField] private Sprite cardIcon;
    [Header("Fine Dining Card Icon Layout")]
    [SerializeField, Range(.2f, .9f)] private float cardIconBodyProportion = .65f;
    [SerializeField] private Vector2 cardIconBodyCenter = new Vector2(.5f, .585f);

    private MoneyPickup money;
    private bool isRemoving;
    private bool claimedByStaff;

    public void ConfigurePaymentPresentation(
        Image configuredFrame,
        Sprite configuredCashFrame,
        Sprite configuredCardFrame,
        Image configuredIcon,
        Sprite configuredCashIcon,
        Sprite configuredCardIcon)
    {
        paymentFrame = configuredFrame;
        cashFrameSprite = configuredCashFrame;
        cardFrameSprite = configuredCardFrame;
        paymentIcon = configuredIcon;
        cashIcon = configuredCashIcon;
        cardIcon = configuredCardIcon;
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>(true);
        if (paymentFrame == null && button != null)
            paymentFrame = button.image;
    }

    private void Update()
    {
        if (isRemoving) return;

        if (money == null)
        {
            RemoveBubble();
            return;
        }

        if (!money.gameObject.activeInHierarchy)
        {
            RemoveBubble();
            return;
        }

        CustomerGroup group = money.TargetGroup;
        if (group == null)
        {
            RemoveBubble();
            return;
        }

        if (group.state == CustomerGroup.GroupState.Leaving ||
            group.state == CustomerGroup.GroupState.AngryLeft ||
            group.state == CustomerGroup.GroupState.UnhappyLeft)
        {
            RemoveBubble();
            return;
        }

        SetTableNumber(group.currentOrderNumber);

        if (money.IsPickedUp)
            RemoveBubble();
    }

    public void Init(int amount, MoneyPickup m)
    {
        money = m;
        money?.SetBubbleUI(this);

        bool cardPayment = m != null && m.IsCardPayment;
        if (paymentFrame != null)
        {
            Sprite frameSprite = cardPayment ? cardFrameSprite : cashFrameSprite;
            if (frameSprite != null)
                paymentFrame.sprite = frameSprite;
            paymentFrame.preserveAspect = true;
        }
        if (amountText != null)
            amountText.text = cardPayment ? string.Empty : amount.ToString();
        if (paymentIcon != null)
        {
            paymentIcon.sprite = cardPayment ? cardIcon : cashIcon;
            paymentIcon.enabled = paymentIcon.sprite != null;
            paymentIcon.preserveAspect = true;
            if (cardPayment && FineDiningReservations.For(m != null ? m.TargetGroup : null) != null)
                CenterFineDiningCardIcon();
        }

        SetTableNumber(m != null && m.TargetGroup != null ? m.TargetGroup.currentOrderNumber : -1);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickCollect);
            PlayerTaskBubbleFocus.Bind(button.gameObject, money);
        }

        SetClaimedByStaff(
            money != null && RestaurantTaskClaim.IsClaimedByBot(money));
    }

    public void SetClaimedByStaff(bool claimed)
    {
        if (MultiplayerDayBridge.IsActive)
        {
            claimedByStaff = false;
            MultiplayerTaskPresentation.Bind(gameObject, RestaurantTaskClaim.GetMultiplayerTaskId(money));
            return;
        }
        claimedByStaff = claimed;

        // Ownership is a hard visible/hidden state. Do not use the Button's
        // disabled transition here: its ColorBlock fades the whole bubble and
        // makes an owned task look like it is flickering.
        if (gameObject.activeSelf == claimed)
            gameObject.SetActive(!claimed);
    }

    private void CenterFineDiningCardIcon()
    {
        if (paymentFrame == null || paymentIcon == null) return;
        RectTransform frame = paymentFrame.rectTransform;
        RectTransform icon = paymentIcon.rectTransform;
        icon.SetParent(frame, false);
        icon.anchorMin = icon.anchorMax = cardIconBodyCenter;
        icon.pivot = new Vector2(.5f, .5f);
        icon.anchoredPosition = Vector2.zero;
        icon.localScale = Vector3.one;
        icon.localRotation = Quaternion.identity;
        float width = frame.rect.width;
        if (paymentFrame.sprite != null && paymentFrame.sprite.rect.height > 0f)
            width = Mathf.Min(width, frame.rect.height * paymentFrame.sprite.rect.width / paymentFrame.sprite.rect.height);
        icon.sizeDelta = Vector2.one * Mathf.Max(1f, width) * cardIconBodyProportion;
        paymentIcon.raycastTarget = false;
        icon.SetAsLastSibling();
    }

    public void SetTableNumber(int number)
    {
        if (tableNumberText == null)
        {
            Debug.LogWarning("[MoneyBubbleUI.SetTableNumber] tableNumberText is null.", this);
            return;
        }

        string nextText = number >= 0 ? $"#{number}" : string.Empty;
        if (tableNumberText.text != nextText)
            tableNumberText.text = nextText;
    }

    public void RemoveBubble()
    {
        if (isRemoving) return;
        isRemoving = true;
        MultiplayerTaskPresentation.DestroyBubble(gameObject);
    }

    private void OnClickCollect()
    {
        if (MultiplayerDayBridge.IsActive && money != null)
        {
            if (money.IsCardPayment) money.UI_RequestCardPayment(); else money.UI_RequestPickup();
            return;
        }
        if (claimedByStaff) return;
        if (money == null) return;
        if (money.IsCardPayment)
        {
            money.UI_RequestCardPayment();
            return;
        }
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        money.UI_RequestPickup();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClickCollect);
    }
}
