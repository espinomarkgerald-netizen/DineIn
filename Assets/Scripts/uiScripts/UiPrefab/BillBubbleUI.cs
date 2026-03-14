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

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            ShowWarning("Only the waiter can handle bills.");
            return;
        }

        var hands = WaiterHands.Instance;

        if (hands != null && hands.HasBill && hands.holdingBillFor == group)
        {
            hands.ClearBill();
            group.ReceiveBillFromWaiter();
            Destroy(gameObject);
            return;
        }

        if (hands != null && hands.HasBill)
        {
            int tableNo = hands.holdingBillFor != null ? hands.holdingBillFor.currentOrderNumber : -1;

            ShowWarning(tableNo >= 0
                ? $"This bill is for table {tableNo}."
                : "You are already holding a bill.");

            return;
        }

        if (oneRequestOnly && requested)
        {
            ShowWarning("The bill was already requested.");
            return;
        }

        group.RequestBillFromCashier();
        requested = true;
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }
}