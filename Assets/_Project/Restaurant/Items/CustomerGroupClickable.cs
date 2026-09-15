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
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && (events.IsPointerOverGameObject()
                || (Input.touchCount > 0 && events.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))) return;
            var localHands = WaiterHands.ActivePlayerHands;
            if (localHands != null && localHands.HasBill)
                MultiplayerCustomerInteractionBridge.TryHandleBill(group);
            else MultiplayerCustomerInteractionBridge.TryServe(group);
            return;
        }
        if (!TutorialCustomerFlowBridge.AllowsWorldInteraction(transform)) return;
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
            if (tray.NetworkCarryLocked) return;

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
