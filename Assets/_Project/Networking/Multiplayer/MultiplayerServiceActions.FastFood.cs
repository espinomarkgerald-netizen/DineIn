using UnityEngine;

public sealed partial class MultiplayerServiceActions
{
    private CustomerGroup continueFastFoodPayment;
    private int continueFastFoodOrder;
    public void ContinueFastFoodPayment(CustomerGroup group)
    { continueFastFoodPayment = group; continueFastFoodOrder = group != null ? group.currentOrderNumber : -1; }
    private void TickFastFoodPaymentContinuation()
    {
        var group = continueFastFoodPayment;
        if (group == null) return;
        if (!session.CanAct || group.currentOrderNumber != continueFastFoodOrder || group.FastFoodPaid || group.IsFastFoodLeaving)
        { continueFastFoodPayment = null; return; }
        if (group.FastFood != null && group.FastFood.CanSettle(group) && RequestFastFoodPayment(group))
            continueFastFoodPayment = null;
    }
    public static bool RequestFastFoodPayment(CustomerGroup group)
    {
        var active = Active;
        if (active == null || !active.session.CanAct || group == null || group.FastFood == null
            || !group.FastFood.CanSettle(group) || active.pending != null) return false;
        var station = group.FastFood.StationFor(group);
        var mover = active.session.LocalManager?.GetComponent<PlayerMovement>();
        if (station == null || station.IsKiosk || station.StaffApproach == null || mover == null) return false;
        active.ApproachClaimed($"Customer:{ViewId(group)}:Payment", mover, station.StaffApproach, 2f,
            () => Send("fastfood_payment", group));
        return true;
    }

    private bool ExecuteFastFoodPayment(Command request, int actor, GameObject player, CustomerGroup group, Reply reply)
    {
        bool owned = reservations.TryGetValue(request.view, out var reservation) && reservation.actor == actor;
        if (request.operation == "payment_cancel")
        {
            if (!owned) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
            ReleaseReservation(request.view, true);
            return true;
        }
        var restaurant = group.FastFood;
        var station = restaurant.StationFor(group);
        if (!restaurant.CanSettle(group) || station == null || station.IsKiosk)
            return Reject(reply, MultiplayerActionOutcome.StaleStage);
        if (!Near(player, station.StaffApproach)) return Reject(reply, MultiplayerActionOutcome.Unreachable);
        var hands = player.GetComponent<WaiterHands>();
        if (hands == null || hands.HasMoney || hands.HasTray || hands.HasBill || hands.HasTicket
            || player.GetComponent<BusserHands>()?.HasTray == true || LocalBagFor(actor) != null)
            return Reject(reply, MultiplayerActionOutcome.OccupiedHands);
        bool card = restaurant.UsesCard(group);
        if (request.operation == "fastfood_payment" || request.operation == "card_open" && card)
        {
            if (card && Money(group) == null && restaurant.CreateNetworkCard(group) == null)
                return Reject(reply, MultiplayerActionOutcome.Unavailable);
            if (!Reserve(request.view, actor, group, group)) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
            reply.card = card; reply.total = group.GetCurrentOrderTotal();
            reply.amount = TakeoutFlowManager.GetPaymentDenomination(reply.total);
            reservations[request.view].registerOpen = !card;
            return true;
        }
        if (!owned) return Reject(reply, MultiplayerActionOutcome.AnotherOwner);
        if (request.operation != "cash_confirm" && request.operation != "card_confirm")
            return Reject(reply, MultiplayerActionOutcome.WrongTarget);
        if (card != (request.operation == "card_confirm")) return Reject(reply, MultiplayerActionOutcome.WrongTarget);
        if (!card && (!reservation.registerOpen || request.value !=
            TakeoutFlowManager.GetPaymentDenomination(group.GetCurrentOrderTotal()) - group.GetCurrentOrderTotal()))
            return Reject(reply, MultiplayerActionOutcome.WrongTarget, "Enter the exact change shown by the payment amount and bill total.");
        var payment = Money(group);
        if (card && (payment == null || !payment.IsCardPayment)) return Reject(reply, MultiplayerActionOutcome.StaleStage);
        var register = CashierRegisterUI.ResolveInstance();
        if (register == null || !register.CompleteAutomatedPayment(group)) return false;
        reservations.Remove(request.view);
        session.GetComponent<MultiplayerTaskClaims>().CompleteOnAuthority($"Customer:{request.view}:Payment", actor);
        if (payment != null) RemoveObservedMoney(payment);
        group.GetComponentInParent<MultiplayerCustomerSpawn>()?.PublishAssignment();
        return true;
    }
}
