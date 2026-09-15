using UnityEngine;

public partial class CustomerGroup
{
    private string MultiplayerBubbleTaskId(string kind)
    {
        var customer = GetComponentInParent<MultiplayerCustomerSpawn>();
        return customer != null ? $"Customer:{customer.photonView.ViewID}:{kind}" : null;
    }
    public void ResetObservedOrder(int order)
    {
        if (!IsNetworkObserver || currentOrderNumber == order) return;
        hasConfirmedOrder = false; hasReceivedBill = false; MultiplayerPaymentComplete = false;
        MultiplayerEatingStarted = false; MultiplayerEatingComplete = false;
        submittedOrder = new SimpleOrder();
    }
    // Presentation only. The host continues to use the normal CustomerGroup operations.
    public void PresentServiceState(MultiplayerServiceActions.GroupState value)
    {
        if (!IsNetworkObserver || value == null) return;
        if (IsTakeout)
        {
            state = (GroupState)value.phase;
            GetComponentInParent<MultiplayerCustomerSpawn>()?.PresentTakeoutPhase(value.phase);
            SetTakeoutQueueState((TakeoutQueueState)value.queue);
            currentOrderNumber = value.orderNumber;
            currentOrder = value.order ?? new SimpleOrder();
            submittedOrder = value.submitted ?? new SimpleOrder();
            currentOrder.ResolveProducts();
            submittedOrder.ResolveProducts();
            hasConfirmedOrder = value.confirmed;
            if (GetComponent<TakeoutCustomerInteractable>() == null)
                gameObject.AddComponent<TakeoutCustomerInteractable>();
            GetComponent<TakeoutCustomerInteractable>().enabled = true;
        }
        // Dine-in lifecycle comes from the customer's revisioned snapshot.
        // A slower payment-object snapshot must never reopen a delivered bill.
        hasReceivedBill |= value.billDelivered;
        MultiplayerPaymentComplete |= value.paid;
        if (hasReceivedBill) ClearBillBubble();
        if (value.paid) ClearMoneyBubble();
    }

    public void PresentMultiplayerBillDelivered(bool delivered)
    { if (IsNetworkObserver && delivered) { hasReceivedBill = true; ClearBillBubble(); } }

    public void ReconcileMultiplayerBubbles(MultiplayerCustomerSpawn customer)
    {
        if (customer == null) return;
        bool order = !IsTakeout && state == GroupState.ReadyToOrder && !HasConfirmedOrder;
        bool bill = !IsTakeout && state == GroupState.NeedsBill && !HasReceivedBill && !MultiplayerPaymentComplete;
        if (order) { if (orderBubbleInstance == null) SpawnOrderBubble(); }
        else ClearOrderBubble();
        if (bill) { if (billBubbleInstance == null) SpawnBillBubble(); }
        else ClearBillBubble();
        MultiplayerTaskPresentation.Bind(orderBubbleInstance, $"Customer:{customer.photonView.ViewID}:Order");
        MultiplayerTaskPresentation.Bind(billBubbleInstance, $"Customer:{customer.photonView.ViewID}:Bill");
        var money = MultiplayerWorldRegistry.MoneyFor(this);
        if (money == null || money.IsPickedUp || MultiplayerPaymentComplete) ClearMoneyBubble();
        else if (state == GroupState.NeedsBill) PresentPaymentBubble(money);
        if (HasReceivedBill || MultiplayerPaymentComplete) ClearBillBubble();
        if (MultiplayerPaymentComplete || state == GroupState.Leaving || state == GroupState.AngryLeft || state == GroupState.UnhappyLeft)
        { ClearOrderBubble(); ClearBillBubble(); ClearMoneyBubble(); ClearTableNumber(); }
    }

    public void PresentPaymentBubble(MoneyPickup money)
    {
        if (!MultiplayerDayBridge.IsActive || money == null || money.IsPickedUp || MultiplayerPaymentComplete
            || moneyBubbleInstance != null || moneyBubblePrefab == null) return;
        moneyBubbleInstance = MultiplayerTaskPresentation.Acquire(moneyBubblePrefab, MultiplayerBubbleTaskId("Payment"));
        var follow = moneyBubbleInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null) ConfigureCustomerBubble(follow);
        moneyBubbleInstance.GetComponentInChildren<MoneyBubbleUI>(true)?.Init(money.Amount, money);
    }
}
