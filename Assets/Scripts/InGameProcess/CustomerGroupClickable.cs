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

        if (WaiterHands.Instance.HasTray)
        {
            if (group.state != CustomerGroup.GroupState.OrderTaken) return;

            var tray = WaiterHands.Instance.holdingTray;
            if (tray == null) return;

            WaiterHands.Instance.ClearTray();
            Destroy(tray.gameObject);

            group.ReceiveFoodFromWaiter(
                tray.DeliveredFood,
                tray.DeliveredDrink
            );

            Debug.Log($"[Waiter] Delivered food for #{group.currentOrderNumber}");
            return;
        }

        if (WaiterHands.Instance.HasBill && WaiterHands.Instance.holdingBillFor == group)
        {
            if (group.state != CustomerGroup.GroupState.NeedsBill) return;

            WaiterHands.Instance.ClearBill();
            group.ReceiveBillFromWaiter();

            Debug.Log($"[Waiter] Delivered bill for #{group.currentOrderNumber}");
            return;
        }
    }
}