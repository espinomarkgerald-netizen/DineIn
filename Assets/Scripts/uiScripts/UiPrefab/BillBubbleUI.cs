using UnityEngine;

public class BillBubbleUI : MonoBehaviour
{
    [SerializeField] private bool oneRequestOnly = true;

    private CustomerGroup group;
    private bool requested;

    public void Init(CustomerGroup g)
    {
        group = g;
        requested = false;
    }

    public void OnClickBillBubble()
    {
        if (group == null) return;
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var hands = WaiterHands.Instance;

        if (hands != null && hands.HasBill && hands.holdingBillFor == group)
        {
            hands.ClearBill();
            group.ReceiveBillFromWaiter();
            Destroy(gameObject);
            return;
        }

        if (hands != null && hands.HasBill) return;
        if (oneRequestOnly && requested) return;

        group.RequestBillFromCashier();
        requested = true;
    }
}