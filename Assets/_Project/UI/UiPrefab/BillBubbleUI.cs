using TMPro;
using UnityEngine;

public class BillBubbleUI : MonoBehaviour
{
    [SerializeField] private bool oneRequestOnly = true;
    [SerializeField] private TMP_Text tableNumberText;

    private CustomerGroup group;
    private bool requested;

    /// <summary>Initializes the bill bubble and sets the table number label.</summary>
    public void Init(CustomerGroup g)
    {
        group = g;
        requested = false;
        SetTableNumber(g != null ? g.currentOrderNumber : -1);
    }

    /// <summary>Sets the table number displayed on the bubble. Pass -1 to hide it.</summary>
    public void SetTableNumber(int number)
    {
        if (tableNumberText == null) return;
        tableNumberText.text = number >= 0 ? $"#{number}" : string.Empty;
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

        if (!RestaurantTaskClaim.TryClaimPlayer(group))
        {
            ShowWarning(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already handling this bill.");
            return;
        }

        var hands = WaiterHands.ActivePlayerHands;
        PlayerMovement movement = RoleManager.Instance.GetActivePlayerMovement();
        Booth booth = group.assignedBooth;
        Transform approach = booth != null && booth.approachPoint != null
            ? booth.approachPoint
            : booth != null ? booth.transform : null;

        if (hands != null && hands.HasBill && hands.holdingBillFor == group)
        {
            if (movement == null || approach == null)
            {
                ShowWarning("This table has no reachable service point.");
                return;
            }

            group.SetBillTaskClaimedByStaff(true);
            movement.UI_MoveToAction(
                approach,
                2.75f,
                () =>
                {
                    if (group == null || hands == null || !hands.HasBill ||
                        hands.holdingBillFor != group)
                        return;

                    hands.ClearBill();
                    group.ReceiveBillFromWaiter();
                },
                () =>
                {
                    // Keep ownership while the Manager is physically carrying
                    // this bill, but restore the Give Bill button for retry.
                    if (group != null)
                        group.SetBillTaskClaimedByStaff(false);
                });
            return;
        }

        if (hands != null && hands.HasBill)
        {
            int tableNo = hands.holdingBillFor != null ? hands.holdingBillFor.currentOrderNumber : -1;

            ShowWarning(tableNo >= 0
                ? $"This bill is for table {tableNo}."
                : "You are already holding a bill.");

            RestaurantTaskClaim.ReleasePlayer(group);
            return;
        }

        if (oneRequestOnly && requested)
        {
            ShowWarning("The bill was already requested.");
            RestaurantTaskClaim.ReleasePlayer(group);
            return;
        }

        if (movement == null || approach == null)
        {
            ShowWarning("This table has no reachable service point.");
            RestaurantTaskClaim.ReleasePlayer(group);
            return;
        }

        group.SetBillTaskClaimedByStaff(true);
        movement.UI_MoveToAction(
            approach,
            2.75f,
            () =>
            {
                if (group == null || group.state != CustomerGroup.GroupState.NeedsBill)
                {
                    RestaurantTaskClaim.ReleasePlayer(group);
                    return;
                }

                group.RequestBillFromCashier();
                requested = true;
            },
            () =>
            {
                if (group == null) return;
                group.SetBillTaskClaimedByStaff(false);
                RestaurantTaskClaim.ReleasePlayer(group);
            });
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }
}
