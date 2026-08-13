using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoneyBubbleUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text tableNumberText;

    private MoneyPickup money;
    private bool isRemoving;
    private bool claimedByStaff;

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>(true);
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

        if (amountText != null)
            amountText.text = amount.ToString();

        SetTableNumber(m != null && m.TargetGroup != null ? m.TargetGroup.currentOrderNumber : -1);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickCollect);
        }

        SetClaimedByStaff(
            money != null && RestaurantTaskClaim.IsClaimedByBot(money));
    }

    public void SetClaimedByStaff(bool claimed)
    {
        claimedByStaff = claimed;

        // Ownership is a hard visible/hidden state. Do not use the Button's
        // disabled transition here: its ColorBlock fades the whole bubble and
        // makes an owned task look like it is flickering.
        if (gameObject.activeSelf == claimed)
            gameObject.SetActive(!claimed);
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
        Destroy(gameObject);
    }

    private void OnClickCollect()
    {
        if (claimedByStaff) return;
        if (money == null) return;
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
