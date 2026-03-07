using UnityEngine;

public class CustomerGroupClickable : MonoBehaviour
{
    private CustomerGroup group;

    private void Awake()
    {
        group = GetComponent<CustomerGroup>();
    }

    private void OnMouseDown()
    {
        if (group == null || WaiterHands.Instance == null) return;

        // Deliver food tray
        if (WaiterHands.Instance.HasTray)
        {
            var tray = WaiterHands.Instance.holdingTray;
            if (tray != null && tray.Matches(group))
            {
                WaiterHands.Instance.ClearTray();
                Destroy(tray.gameObject);

                group.ReceiveFoodFromWaiter();
                Debug.Log($"[Waiter] Delivered food for #{group.currentOrderNumber}");
                return;
            }
            else
            {
                Debug.Log("[Waiter] Wrong tray for this table.");
                return;
            }
        }

        // Deliver bill
        if (WaiterHands.Instance.HasBill && WaiterHands.Instance.holdingBillFor == group)
        {
            WaiterHands.Instance.ClearBill();
            group.ReceiveBillFromWaiter();
            Debug.Log($"[Waiter] Delivered bill for #{group.currentOrderNumber}");
            return;
        }
    }

    
}