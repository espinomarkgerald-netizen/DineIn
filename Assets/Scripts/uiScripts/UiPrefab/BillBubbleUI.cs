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

        // If holding another group's bill, do nothing
        if (hands != null && hands.HasBill) return;

        // This bubble's main job is to request the bill for this exact group
        if (oneRequestOnly && requested) return;

        group.RequestBillFromCashier();
        requested = true;
    }
}