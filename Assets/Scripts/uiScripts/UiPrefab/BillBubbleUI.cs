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

        var hands = WaiterHands.Instance;

        // If already holding THIS group's bill -> deliver it
        if (hands != null && hands.HasBill && hands.holdingBillFor == group)
        {
            hands.ClearBill();
            group.ReceiveBillFromWaiter();
            Destroy(gameObject);
            return;
        }

        // If holding some other bill, don't do anything
        if (hands != null && hands.HasBill) return;

        // If bill already printed, go pick it up (same method as tray UI)
        var bm = BillManager.Instance;
        if (bm != null)
        {
            var existing = bm.FindBillForGroup(group);
            if (existing != null)
            {
                existing.UI_Pickup();
                return;
            }
        }

        // Otherwise request print
        if (oneRequestOnly && requested) return;

        group.RequestBillFromCashier();
        requested = true;
    }
}