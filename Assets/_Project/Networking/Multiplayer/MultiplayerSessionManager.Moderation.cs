using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public partial class MultiplayerSessionManager : IOnEventCallback
{
    public const byte RemoveRequestEvent = 161, LeaveRequestEvent = 162, RemovalNoticeEvent = 163;
    public const string RemovedMessage = "You were removed by the host.";
    private readonly Dictionary<string, int> pendingRemovals = new(StringComparer.Ordinal);
    private bool intentionalLeavePending;
    private float leaveReplyUntil;
    internal bool IsLeavingSession => leaving || intentionalLeavePending;

    private void TickLeaveRequest()
    {
        if (!intentionalLeavePending || Time.realtimeSinceStartup < leaveReplyUntil) return;
        intentionalLeavePending = false;
        WarningSlideUI.Instance?.Show("Leaving was not confirmed. Try Leave Session again.");
    }

    public bool CanRemove(string accountId) => IsAuthority && !string.IsNullOrEmpty(accountId)
        && run.participants.Exists(p => p.accountId == accountId && p.actor != run.hostActor && !p.departed && !p.kicked);

    // The confirmation keeps the stable identity, never a row index that can change.
    public bool RequestRemove(string accountId)
    {
        if (!CanRemove(accountId)) return false;
        return PhotonNetwork.RaiseEvent(RemoveRequestEvent, new object[] { RunId, accountId },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (!IsConnected || Ended || ev.CustomData is not object[] data || data.Length != 2
            || data[0] is not string id || id != RunId || data[1] is not string account) return;
        if (ev.Code == RemovalNoticeEvent)
        {
            if (ev.Sender != run.hostActor || ev.Sender != PhotonNetwork.MasterClient?.ActorNumber
                || account != PhotonNetwork.LocalPlayer.UserId) return;
            ReturnAfterRecoveryFailure(RemovedMessage, true);
            return;
        }
        if (!IsAuthority || !ValidActor(ev.Sender)) return;
        if (ev.Code == RemoveRequestEvent)
        {
            // UI visibility is not authorization: authenticate the actual Photon sender again.
            if (ev.Sender != run.hostActor || ev.Sender != PhotonNetwork.MasterClient?.ActorNumber || !CanRemove(account)) return;
            MarkKicked(account);
        }
        else if (ev.Code == LeaveRequestEvent && PhotonNetwork.CurrentRoom.Players.TryGetValue(ev.Sender, out var player)
            && player.UserId == account && ev.Sender != run.hostActor)
        {
            // Use the same reserved roster state as a disconnected guest.
            var member = run.participants.Find(p => p.actor == ev.Sender);
            member.disconnectDeadline = Math.Max(1d, PhotonNetwork.Time);
            PublishRun();
        }
    }

    private void MarkKicked(string account)
    {
        var member = run.participants.Find(p => p.accountId == account);
        if (member == null || member.departed || member.actor == run.hostActor) return;
        member.kicked = true; member.departed = true; member.disconnectDeadline = 0;
        member.provisionalDay = member.provisionalHighestDay = 0;
        pendingRemovals[account] = member.actor;
        // Await the server property echo before destroying/disconnecting the participant.
        PublishRun();
    }

    private void FinishPendingRemovals(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 32768) return;
        MultiplayerRunRecord published;
        try { published = JsonUtility.FromJson<MultiplayerRunRecord>(json); }
        catch (ArgumentException) { return; }
        if (!MultiplayerRunRecords.Valid(published) || published.runId != RunId || published.revision != run.revision) return;
        // ReadRun has already checked the accepted roster; an unrelated property payload
        // must not manufacture a local departure or remove the original host.
        var own = run.participants.Find(p => p.accountId == PhotonNetwork.LocalPlayer?.UserId);
        if (!IsHostConnection && own?.kicked == true) { ReturnAfterRecoveryFailure(RemovedMessage, true); return; }
        // A late host acknowledgement still commits the leave, even if the UI timed out.
        if (!leaving && !RemovalPending && !Ended && leaveReplyUntil > 0f &&
            own != null && own.actor == localActor && own.disconnectDeadline > 0)
        {
            intentionalLeavePending = false;
            leaveReplyUntil = 0f;
            MultiplayerLastRun.MenuNotice = "You left the session. You can rejoin while it is active.";
            LeaveToMenu();
            return;
        }
        if (!IsAuthority) return;
        foreach (var removal in new List<KeyValuePair<string, int>>(pendingRemovals))
        {
            var member = published.participants.Find(p => p.accountId == removal.Key && p.departed);
            if (member == null) continue;
            pendingRemovals.Remove(removal.Key);
            gameplay?.ParticipantUnavailable(removal.Value);
            if (PhotonNetwork.CurrentRoom.Players.TryGetValue(removal.Value, out var player))
            {
                GetComponent<MultiplayerTaskClaims>()?.OnPlayerLeftRoom(player);
                PhotonNetwork.DestroyPlayerObjects(removal.Value); // Existing pre-destroy hooks preserve shared carried items.
                if (member.kicked && !player.IsInactive)
                {
                    PhotonNetwork.RaiseEvent(RemovalNoticeEvent, new object[] { RunId, removal.Key },
                        new RaiseEventOptions { TargetActors = new[] { removal.Value } }, SendOptions.SendReliable);
                    PhotonNetwork.CloseConnection(player);
                }
            }
            var reserved = new List<string>(PhotonNetwork.CurrentRoom.ExpectedUsers ?? Array.Empty<string>());
            reserved.Remove(removal.Key);
            if (reserved.Count == 0) PhotonNetwork.CurrentRoom.ClearExpectedUsers();
            else PhotonNetwork.CurrentRoom.SetExpectedUsers(reserved.ToArray());
        }
    }

    internal bool TryLeaveSession()
    {
        if (IsLeavingSession || RemovalPending) return false;
        if (IsHostConnection || Ended) { ForgetLastRun(); LeaveToMenu(); return true; }
        if (!IsConnected) return false;
        intentionalLeavePending = PhotonNetwork.RaiseEvent(LeaveRequestEvent,
            new object[] { RunId, PhotonNetwork.LocalPlayer.UserId },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        if (intentionalLeavePending)
        {
            leaveReplyUntil = Time.realtimeSinceStartup + 8f;
            Status = "Leaving session...";
        }
        return intentionalLeavePending;
    }
}
