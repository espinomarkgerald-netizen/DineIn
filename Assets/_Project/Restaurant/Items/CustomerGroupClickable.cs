using System.Collections.Generic;
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
        WaiterHands hands = WaiterHands.ActivePlayerHands;
        if (group == null || hands == null) return;

        if (TakeoutBagInteractable.PlayerHasHeldBag)
        {
            TakeoutBagInteractable.HeldBag.TryDeliverTo(group);
            return;
        }

        if (hands.HasTray)
        {
            if (group.state != CustomerGroup.GroupState.OrderTaken) return;

            var tray = hands.holdingTray;
            if (tray == null) return;

            List<string> deliveredContents = new List<string>();

            if (tray.DeliveredContents != null && tray.DeliveredContents.Count > 0)
                deliveredContents.AddRange(tray.DeliveredContents);

            hands.ClearTray();
            Destroy(tray.gameObject);

            group.ReceiveFoodFromWaiter(deliveredContents);
            return;
        }

        if (hands.HasBill && hands.holdingBillFor == group)
        {
            if (group.state != CustomerGroup.GroupState.NeedsBill) return;

            hands.ClearBill();
            group.ReceiveBillFromWaiter();

            Debug.Log($"[Waiter] Delivered bill for #{group.currentOrderNumber}");
            return;
        }
    }
}
