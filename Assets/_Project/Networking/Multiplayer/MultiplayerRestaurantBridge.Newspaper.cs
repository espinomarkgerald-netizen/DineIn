using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed partial class MultiplayerRestaurantBridge
{
    // Reading is independent of the exclusive management-edit request. One
    // retained receipt per current issue survives a lost reply or brief rejoin.
    private Command newspaperPending;
    private float newspaperRetryAt;

    public static bool RequestNewspaperViewed(int day, string issueId)
    {
        var bridge = Active;
        if (bridge == null || bridge.session == null || !bridge.session.IsConnected
            || GameFlowManager.Instance == null || day != GameFlowManager.Instance.CurrentDay
            || string.IsNullOrEmpty(issueId) || string.IsNullOrEmpty(bridge.session.RunId)) return false;
        if (bridge.newspaperPending != null && bridge.newspaperPending.run == bridge.session.RunId
            && bridge.newspaperPending.day == day && bridge.newspaperPending.target == issueId) return true;
        bridge.newspaperPending = new Command { run = bridge.session.RunId, day = day, value = day,
            target = issueId, operation = "newspaper", id = System.Guid.NewGuid().ToString("N") };
        bridge.newspaperRetryAt = 0;
        bridge.TickNewspaperAcknowledgement();
        return true;
    }

    private void TickNewspaperAcknowledgement()
    {
        var command = newspaperPending;
        if (command == null) return;
        if (session == null || session.Ended || command.run != session.RunId || GameFlowManager.Instance == null
            || command.day != GameFlowManager.Instance.CurrentDay)
        { newspaperPending = null; return; }
        var issue = CasualDiningPolishManager.Instance?.GetIssueForDay(command.day);
        if (issue != null && issue.issueID == command.target && issue.viewed)
        { newspaperPending = null; return; } // The authoritative snapshot itself acknowledges the reading.
        if (!session.IsConnected || !MultiplayerProgressionContext.Ready || Time.unscaledTime < newspaperRetryAt) return;
        newspaperRetryAt = Time.unscaledTime + 1f;
        if (session.IsAuthority) Handle(session.LocalActorNumber, command);
        else PhotonNetwork.RaiseEvent(CommandEvent, JsonUtility.ToJson(command),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private bool ReceiveNewspaperAcknowledgement(Reply reply)
    {
        if (reply == null || newspaperPending == null || reply.id != newspaperPending.id) return false;
        newspaperPending = null;
        StateChanged?.Invoke();
        return true;
    }
}
