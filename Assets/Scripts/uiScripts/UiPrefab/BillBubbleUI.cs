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

        // Deliver bill if holding it
        if (hands != null && hands.HasBill && hands.holdingBillFor == group)
        {
            hands.ClearBill();
            group.ReceiveBillFromWaiter();

            // Remove bubble after successful delivery
            Destroy(gameObject);
            return;
        }

        // Request bill from cashier
        if (oneRequestOnly && requested) return;

        group.RequestBillFromCashier();
        requested = true;
    }
}