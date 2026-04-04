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

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>(true);

        Debug.Log($"[MoneyBubbleUI.Awake] name={name}", this);
    }

    private void Update()
    {
        if (isRemoving) return;

        if (money == null)
        {
            Debug.Log("[MoneyBubbleUI.Update] money is null, removing bubble.", this);
            RemoveBubble();
            return;
        }

        if (!money.gameObject.activeInHierarchy)
        {
            Debug.Log("[MoneyBubbleUI.Update] money object inactive, removing bubble.", this);
            RemoveBubble();
            return;
        }

        CustomerGroup group = money.TargetGroup;
        if (group == null)
        {
            Debug.Log("[MoneyBubbleUI.Update] TargetGroup is null, removing bubble.", this);
            RemoveBubble();
            return;
        }

        if (group.state == CustomerGroup.GroupState.Leaving ||
            group.state == CustomerGroup.GroupState.AngryLeft ||
            group.state == CustomerGroup.GroupState.UnhappyLeft)
        {
            Debug.Log($"[MoneyBubbleUI.Update] Group {group.name} is leaving, removing bubble.", this);
            RemoveBubble();
            return;
        }

        SetTableNumber(group.currentOrderNumber);

        var hands = WaiterHands.Instance;
        if (hands != null && hands.HasMoney && hands.HeldMoney == money)
        {
            Debug.Log("[MoneyBubbleUI.Update] Money picked up by waiter, removing bubble.", this);
            RemoveBubble();
        }
    }

    public void Init(int amount, MoneyPickup m)
    {
        money = m;

        string groupName = m != null && m.TargetGroup != null ? m.TargetGroup.name : "null";
        string orderNo = m != null && m.TargetGroup != null ? m.TargetGroup.currentOrderNumber.ToString() : "null";

        Debug.Log($"[MoneyBubbleUI.Init] amount={amount}, group={groupName}, currentOrderNumber={orderNo}", this);

        if (amountText != null)
            amountText.text = amount.ToString();

        SetTableNumber(m != null && m.TargetGroup != null ? m.TargetGroup.currentOrderNumber : -1);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickCollect);
        }
    }

    public void SetTableNumber(int number)
    {
        if (tableNumberText == null)
        {
            Debug.LogWarning("[MoneyBubbleUI.SetTableNumber] tableNumberText is null.", this);
            return;
        }

        tableNumberText.text = number >= 0 ? $"#{number}" : string.Empty;
        Debug.Log($"[MoneyBubbleUI.SetTableNumber] visible text set to {tableNumberText.text}", this);
    }

    public void RemoveBubble()
    {
        if (isRemoving) return;
        isRemoving = true;
        Destroy(gameObject);
    }

    private void OnClickCollect()
    {
        if (money == null) return;
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null) return;

        player.UI_MoveTo(money);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClickCollect);
    }
}