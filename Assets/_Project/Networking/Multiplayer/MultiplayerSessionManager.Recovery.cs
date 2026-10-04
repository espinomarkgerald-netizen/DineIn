using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public partial class MultiplayerSessionManager
{
    private bool recoveryFailed, fallbackJoin, joinInFlight, admissionPending;
    private LastMultiplayerRun recoveryPointer;
    public bool RecoveryNeedsRetry => recoveryFailed;

    private void StartRecovery()
    {
        recoveryPointer = MultiplayerLastRun.Load(PlayFabAuthManager.Instance?.PlayFabId, out _);
        recoveryFailed = false; reconnecting = true; restoringConnection = false;
        admissionPending = joinInFlight = false; fallbackJoin = false;
        reconnectUntil = Time.realtimeSinceStartup + automaticReconnectSeconds;
        nextReconnect = Time.realtimeSinceStartup + 1f;
        Status = "Connection lost. Reconnecting…";
    }
    public void RetryRecovery()
    {
        if (leaving || Ended || !recoveryFailed) return;
        if (IsConnected && ValidActor(localActor))
        { recoveryFailed = false; BeginRestoration(); return; }
        StartRecovery();
    }
    private void FailRecovery(string message)
    {
        reconnecting = restoringConnection = false;
        recoveryFailed = true;
        Status = message;
    }
    private void TickRecovery()
    {
        if (Time.realtimeSinceStartup >= reconnectUntil)
        { FailRecovery("Could not reconnect. Retry or return to menu; your place is reserved."); return; }
        if (IsConnected) { TryFinishAdmission(); return; }
        if (joinInFlight || Time.realtimeSinceStartup < nextReconnect) return;
        nextReconnect = Time.realtimeSinceStartup + 3f;
        if (recoveryPointer == null)
        { FailRecovery("Session details are unavailable. Return to the multiplayer menu."); return; }
        var state = PhotonNetwork.NetworkClientState;
        if (state == ClientState.Disconnected || state == ClientState.PeerCreated)
        {
            Status = "Reconnecting…";
            if (PhotonBootstrap.Instance == null) new GameObject("PhotonBootstrap").AddComponent<PhotonBootstrap>();
            PhotonBootstrap.Instance.SafeConnect();
            return;
        }
        if (state != ClientState.ConnectedToMasterServer && state != ClientState.JoinedLobby) return;
        if (!PhotonBootstrap.ConnectionMatchesAccount(recoveryPointer.accountId)) return;
        Status = "Rejoining the original session…";
        // No JoinOrCreate: a missing original room must never become a new session.
        joinInFlight = fallbackJoin ? PhotonNetwork.JoinRoom(recoveryPointer.room) : PhotonNetwork.RejoinRoom(recoveryPointer.room);
    }
    private void TryFinishAdmission()
    {
        if (!admissionPending || !IsConnected || recoveryPointer == null) return;
        if (!MultiplayerLastRun.TryValidateJoinedRun(recoveryPointer, out var returned, out var reason))
        {
            if (reason == "Waiting for host admission.") return;
            bool definitive = MultiplayerRunRecords.Valid(returned) &&
                (returned.runId != recoveryPointer.runId || !string.IsNullOrEmpty(returned.endReason)
                    || reason == "The host left, so this run has ended."
                    || returned.participants.Exists(p => p.accountId == recoveryPointer.accountId && (p.departed || p.kicked)));
            ReturnAfterRecoveryFailure(reason, definitive); return;
        }
        if (returned.runId != RunId || returned.hostAccountId != run.hostAccountId)
        { ReturnAfterRecoveryFailure("The original session has ended.", true); return; }
        if (returned.revision < run.revision) return; // Await the current host snapshot.
        ReadRun();
        admissionPending = reconnecting = false;
        MultiplayerLastRun.Remember(run, localActor);
        BeginRestoration();
    }
    private void AdmitParticipant(Player player)
    {
        if (!IsAuthority || player == null || player.IsInactive) return;
        var member = run.participants.Find(p => p.accountId == player.UserId);
        if (member == null || member.departed || member.kicked || member.actor == run.hostActor)
        { RejectParticipant(player); return; }
        if (member.actor != player.ActorNumber && PhotonNetwork.CurrentRoom.Players.TryGetValue(member.actor, out var old) && !old.IsInactive)
        { RejectParticipant(player); return; }
        int oldActor = member.actor;
        if (member.initialActor == 0) member.initialActor = oldActor;
        if (oldActor != player.ActorNumber)
        {
            PhotonNetwork.DestroyPlayerObjects(oldActor);
            managers.Remove(oldActor);
            member.actor = player.ActorNumber;
        }
        member.loadEpoch++;
        member.completedDay = Math.Max(member.completedDay, member.provisionalDay);
        member.highestDay = Math.Max(member.highestDay, member.provisionalHighestDay);
        member.provisionalDay = member.provisionalHighestDay = 0;
        member.disconnectDeadline = 0;
        PublishRun();
        RefreshReservations();
        gameplay?.ParticipantReturned();
    }
    private void RejectParticipant(Player player)
    {
        PhotonNetwork.RaiseEvent(RemovalNoticeEvent, new object[] { RunId, player.UserId },
            new RaiseEventOptions { TargetActors = new[] { player.ActorNumber } }, ExitGames.Client.Photon.SendOptions.SendReliable);
        PhotonNetwork.CloseConnection(player);
    }
    private void RefreshReservations()
    {
        if (!IsAuthority) return;
        var expected = new List<string>();
        foreach (var member in run.participants)
            if (!member.departed && !member.kicked && !PhotonNetwork.CurrentRoom.Players.ContainsKey(member.actor))
                expected.Add(member.accountId);
        expected.Sort(StringComparer.Ordinal);
        var old = new List<string>(PhotonNetwork.CurrentRoom.ExpectedUsers ?? Array.Empty<string>());
        old.Sort(StringComparer.Ordinal);
        if (string.Join("|", old) == string.Join("|", expected)) return;
        if (expected.Count == 0) PhotonNetwork.CurrentRoom.ClearExpectedUsers();
        else PhotonNetwork.CurrentRoom.SetExpectedUsers(expected.ToArray());
    }
}
