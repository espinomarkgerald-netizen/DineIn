using UnityEngine;

// Read-through presentation/eligibility, not another task store. Claims and item
// lifecycles remain authoritative; local pending commands cover projection gaps.
public static class MultiplayerActorActivity
{
    public enum Phase { Idle, Approaching, Reviewing, Carrying, Working, AwaitingAuthority }
    public struct View
    {
        public Phase phase;
        public string action, detail, blocker;
        public Object target;
    }
    public static View Read(GameObject actor)
    {
        var value = new View { action = "Choose a task", detail = "Interact with a customer or the restaurant computer." };
        if (actor == null) return value;
        WaiterHands.ReconcileMultiplayerHands(actor);
        var hands = actor.GetComponent<WaiterHands>();
        var busser = actor.GetComponent<BusserHands>();
        var mover = actor.GetComponent<PlayerMovement>();
        var session = MultiplayerSessionManager.Instance;
        bool local = session != null && session.LocalManager == actor;
        value.blocker = hands?.MultiplayerHeldItemBlocker();
        if (busser != null && busser.HasTray)
        { value.action = "Wash the dirty tray"; value.blocker = "Take the dirty tray you are carrying to the sink first."; }
        else if (local && MultiplayerServiceActions.Active?.LocalBag != null)
        { value.action = "Deliver takeout"; value.blocker = "Deliver the takeout bag to its customer first."; }
        else if (hands != null && hands.HasMoney) value.action = "Collect payment";
        else if (hands != null && hands.HasBill) value.action = "Deliver the bill";
        else if (hands != null && hands.HasTicket) value.action = "Submit order #" + hands.holdingTicketFor.currentOrderNumber;
        else if (hands != null && hands.HasTray) value.action = "Deliver order #" + hands.holdingTray.orderNumber;
        if (value.blocker != null)
        { value.phase = Phase.Carrying; value.detail = value.blocker; return value; }
        var interaction = local ? session.GetComponent<MultiplayerCustomerInteractionBridge>() : null;
        if (local && (MultiplayerServiceActions.Active?.AwaitingLocalAction == true || interaction?.AwaitingLocalAction == true))
        {
            value.phase = Phase.AwaitingAuthority; value.action = "Finishing current interaction";
            value.detail = "Waiting for the restaurant to confirm the action.";
            value.blocker = "Your current interaction is still being confirmed. Please wait.";
            return value;
        }
        if (interaction?.ReviewingLocalOrder == true)
        { value.phase = Phase.Reviewing; value.action = "Review the customer order"; value.detail = "Confirm the order or close the review to switch tasks."; return value; }
        var target = mover != null ? mover.LockedTarget ?? mover.CurrentTarget : null;
        value.target = target as Object;
        if (target != null)
        {
            value.phase = mover.CurrentState == PlayerMovement.State.DoingJob ? Phase.Working : Phase.Approaching;
            value.action = value.phase == Phase.Working ? "Completing interaction" : "Approaching task";
            value.detail = "Continue to the selected task, or cancel the approach to choose another.";
        }
        else if (session != null)
        {
            var claims = session.GetComponent<MultiplayerTaskClaims>();
            var view = actor.GetComponent<Photon.Pun.PhotonView>();
            string task = claims != null && view != null ? claims.GetTaskForActor(view.OwnerActorNr) : null;
            if (local && claims != null && claims.HasPendingAcquisition)
            { value.phase = Phase.AwaitingAuthority; value.action = "Selecting task"; value.detail = "Waiting for task ownership confirmation."; }
            else if (task != null)
            {
                value.phase = Phase.Working;
                value.action = task.EndsWith(":Bill") ? "Continue bill service"
                    : task.EndsWith(":Payment") ? "Complete customer payment"
                    : task.EndsWith(":GreetSeat") ? "Choose a table"
                    : task.EndsWith(":Order") ? "Review the customer order"
                    : task.EndsWith(":Cleanup") ? "Complete cleanup" : "Pick up the prepared order";
                value.detail = "Use the selected task's action to continue.";
            }
        }
        return value;
    }
}
