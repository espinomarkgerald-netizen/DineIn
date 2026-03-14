using UnityEngine;
using UnityEngine.UI;

public class BillPaperPickupButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BillPaper bill;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Click);
        }
    }

    public void SetBill(BillPaper b)
    {
        bill = b;
    }

    private void Click()
    {
        if (bill == null) return;
        if (RoleManager.Instance == null) return;

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            ShowWarning("Only the waiter can pick up bills.");
            return;
        }

        var hands = WaiterHands.Instance;
        if (hands != null && hands.HasBill)
        {
            int tableNo = hands.holdingBillFor != null ? hands.holdingBillFor.currentOrderNumber : -1;

            ShowWarning(tableNo >= 0
                ? $"You are already holding the bill for table {tableNo}."
                : "You are already holding a bill.");

            return;
        }

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null) return;

        player.UI_MoveTo(bill);
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }
}