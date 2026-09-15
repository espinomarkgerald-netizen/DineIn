using System;

// The host resolves the actor from the authenticated Photon sender. This context
// fences retries and presentation replies; it never supplies prices or positions.
[Serializable]
public sealed class MultiplayerActionContext
{
    public string run, operation, actionId, expectedStage;
    public int day, actor, customerView, orderNumber;
    public long lease, expectedRevision;

    public static MultiplayerActionContext Capture(MultiplayerSessionManager session, CustomerGroup group,
        string operation, string taskId, string expectedStage)
    {
        var claims = session != null ? session.GetComponent<MultiplayerTaskClaims>() : null;
        return new MultiplayerActionContext
        {
            run = session?.RunId, day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0,
            actor = session != null ? session.LocalActorNumber : 0,
            customerView = group != null ? group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 : 0,
            orderNumber = group != null ? group.currentOrderNumber : -1,
            operation = operation, actionId = Guid.NewGuid().ToString("N"), expectedStage = expectedStage,
            lease = claims != null ? claims.GetLease(taskId) : 0,
            expectedRevision = claims != null ? claims.CurrentRevision : 0
        };
    }

    public bool MatchesSession(MultiplayerSessionManager session, int sender) => session != null
        && session.IsMultiplayerSession && !session.Ended && session.Run != null && run == session.RunId
        && GameFlowManager.Instance != null && day == GameFlowManager.Instance.CurrentDay
        && actor == sender && session.ValidActor(sender) && !string.IsNullOrWhiteSpace(operation)
        && !string.IsNullOrWhiteSpace(expectedStage) && Guid.TryParseExact(actionId, "N", out _);

    public bool MatchesOrder(CustomerGroup group) => group != null && group.currentOrderNumber == orderNumber
        && group.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID == customerView;

    public bool MatchesAction(MultiplayerActionContext other) => other != null && run == other.run
        && day == other.day && actor == other.actor && customerView == other.customerView
        && orderNumber == other.orderNumber && operation == other.operation && actionId == other.actionId
        && expectedStage == other.expectedStage && lease == other.lease && expectedRevision == other.expectedRevision;
}

public enum MultiplayerActionOutcome
{
    Success, StaleStage, WrongTarget, OccupiedHands, AnotherOwner, Unreachable,
    UnavailableStock, Unavailable, Cancelled
}

[Serializable]
public sealed class MultiplayerActionResult
{
    public MultiplayerActionContext context;
    public MultiplayerActionOutcome outcome;
    public long revision;
    public string message;
    public bool Accepted => outcome == MultiplayerActionOutcome.Success;
}

public static class MultiplayerActionFeedback
{
    public static string Message(MultiplayerActionOutcome outcome) => outcome switch
    {
        MultiplayerActionOutcome.StaleStage => "This customer's task has changed. Select its current action.",
        MultiplayerActionOutcome.WrongTarget => "Take the item to its matching customer.",
        MultiplayerActionOutcome.OccupiedHands => "Finish or return the item you are carrying first.",
        MultiplayerActionOutcome.AnotherOwner => "Another player or staff member is handling this task.",
        MultiplayerActionOutcome.Unreachable => "Move closer to the service point and try again.",
        MultiplayerActionOutcome.UnavailableStock => "One or more products in this order are no longer available.",
        MultiplayerActionOutcome.Unavailable => "This service station is unavailable. Please try again.",
        MultiplayerActionOutcome.Cancelled => "The action was cancelled. Select the task to retry.",
        _ => string.Empty
    };
}
