using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoneyBubbleUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text amountText;

    private MoneyPickup money;
    private bool isRemoving;

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

        var hands = WaiterHands.Instance;
        if (hands != null && hands.HasMoney && hands.HeldMoney == money)
        {
            RemoveBubble();
        }
    }

    public void Init(int amount, MoneyPickup m)
    {
        money = m;

        if (amountText != null)
            amountText.text = amount.ToString();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickCollect);
        }
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