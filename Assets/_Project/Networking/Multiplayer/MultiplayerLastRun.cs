using System;
using Photon.Pun;
using UnityEngine;

// A local account-bound room pointer, never a restaurant save or authentication token.
[Serializable]
public sealed class LastMultiplayerRun
{
    public int schema = 1, actor, playerTtlSeconds;
    public string room, region, networkVersion, runId, restaurant, accountId, lastSeenUtc;
}

public static class MultiplayerLastRun
{
    private const string KeyPrefix = "DineIn.LastMultiplayerRun.";
    public static string MenuNotice { get; set; }

    public static void Remember(MultiplayerRunRecord run, int actor)
    {
        var room = PhotonNetwork.CurrentRoom;
        var participant = run?.participants?.Find(p => p.actor == actor);
        if (room == null || participant == null || participant.departed || !string.IsNullOrEmpty(run.endReason)) return;
        var pointer = new LastMultiplayerRun {
            room = room.Name, region = PhotonNetwork.CloudRegion?.Split('/')[0],
            networkVersion = PhotonBootstrap.ConnectionVersion, runId = run.runId,
            restaurant = run.restaurant, accountId = participant.accountId, actor = actor,
            playerTtlSeconds = Mathf.Max(0, room.PlayerTtl / 1000),
            lastSeenUtc = DateTime.UtcNow.ToString("o")
        };
        PlayerPrefs.SetString(KeyPrefix + participant.accountId, JsonUtility.ToJson(pointer));
        PlayerPrefs.Save();
    }

    public static LastMultiplayerRun Load(string accountId, out string reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(accountId)) return null;
        string json = PlayerPrefs.GetString(KeyPrefix + accountId, "");
        if (string.IsNullOrEmpty(json)) return null;
        LastMultiplayerRun pointer;
        try { pointer = json.Length <= 2048 ? JsonUtility.FromJson<LastMultiplayerRun>(json) : null; }
        catch (ArgumentException) { pointer = null; }
        if (!IsCompatible(pointer, accountId, DateTime.UtcNow, out reason))
        { return null; }
        return pointer;
    }

    // Eligibility belongs to the live session roster, not a local retention timer.
    public static bool IsCompatible(LastMultiplayerRun pointer, string accountId, DateTime now, out string reason)
    {
        reason = "The previous run is no longer available.";
        if (pointer == null || pointer.schema != 1 || pointer.accountId != accountId
            || pointer.actor <= 0 || string.IsNullOrEmpty(pointer.room) || pointer.room.Length > 64
            || !Guid.TryParseExact(pointer.runId, "N", out _) || !MultiplayerDestinations.TryGet(pointer.restaurant, out _, out _)
            || pointer.playerTtlSeconds < 0) return false;
        if (pointer.networkVersion != PhotonBootstrap.ConnectionVersion
            || !string.Equals(pointer.region, PhotonBootstrap.RoomRegion, StringComparison.OrdinalIgnoreCase))
        { reason = "The previous run uses a different game version or region."; return false; }
        reason = null;
        return true;
    }

    public static bool TryValidateJoinedRun(LastMultiplayerRun pointer, out MultiplayerRunRecord run, out string reason)
    {
        run = null;
        reason = "The previous run is no longer available.";
        var room = PhotonNetwork.CurrentRoom;
        var local = PhotonNetwork.LocalPlayer;
        if (pointer == null || room == null || local == null
            || room.Name != pointer.room || local.UserId != pointer.accountId
            || !Equals(room.CustomProperties[MultiplayerSessionManager.ProtocolKey], MultiplayerSessionManager.Protocol)
            || room.CustomProperties[MultiplayerSessionManager.RunKey] is not string json || json.Length > 32768) return false;
        try { run = JsonUtility.FromJson<MultiplayerRunRecord>(json); }
        catch (ArgumentException) { return false; }
        if (!MultiplayerRunRecords.Valid(run) || run.runId != pointer.runId || run.gameVersion != Application.version
            || run.restaurant != pointer.restaurant || !string.IsNullOrEmpty(run.endReason)
            || !Equals(room.CustomProperties[MultiplayerDestinations.RestaurantKey], run.restaurant)
            || !Equals(room.CustomProperties[MultiplayerDestinations.SceneKey], run.sceneName)) return false;
        if (PhotonNetwork.MasterClient == null || PhotonNetwork.MasterClient.ActorNumber != run.hostActor
            || PhotonNetwork.MasterClient.IsInactive || PhotonNetwork.MasterClient.UserId != run.hostAccountId)
        { reason = "The host left, so this run has ended."; return false; }
        var participant = run.participants.Find(p => p.accountId == local.UserId);
        if (participant == null || participant.departed || participant.kicked)
        { reason = participant?.kicked == true ? "You were removed by the host." : "You have left that session."; return false; }
        if (participant.actor != local.ActorNumber || participant.disconnectDeadline > 0)
        { reason = "Waiting for host admission."; return false; }
        reason = null;
        return true;
    }

    public static void Forget(string accountId, string runId = null)
    {
        if (string.IsNullOrEmpty(accountId)) return;
        if (!string.IsNullOrEmpty(runId))
        {
            var json = PlayerPrefs.GetString(KeyPrefix + accountId, "");
            if (string.IsNullOrEmpty(json)) return;
            try { if (JsonUtility.FromJson<LastMultiplayerRun>(json)?.runId != runId) return; }
            catch (ArgumentException) { return; }
        }
        PlayerPrefs.DeleteKey(KeyPrefix + accountId);
        PlayerPrefs.Save();
    }
}
