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
            playerTtlSeconds = Mathf.Clamp(room.PlayerTtl / 1000, 1, 300),
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
        { Forget(accountId); return null; }
        return pointer;
    }

    // The timestamp only bounds plausibility; Photon alone confirms an inactive slot still exists.
    public static bool IsCompatible(LastMultiplayerRun pointer, string accountId, DateTime now, out string reason)
    {
        reason = "The previous run is no longer available.";
        if (pointer == null || pointer.schema != 1 || pointer.accountId != accountId
            || pointer.actor <= 0 || string.IsNullOrEmpty(pointer.room) || pointer.room.Length > 64
            || !Guid.TryParseExact(pointer.runId, "N", out _) || pointer.restaurant != "CasualDining"
            || pointer.playerTtlSeconds < 1 || pointer.playerTtlSeconds > 300) return false;
        if (pointer.networkVersion != PhotonBootstrap.ConnectionVersion
            || !string.Equals(pointer.region, PhotonBootstrap.RoomRegion, StringComparison.OrdinalIgnoreCase))
        { reason = "The previous run uses a different game version or region."; return false; }
        if (!DateTime.TryParse(pointer.lastSeenUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var seen)
            || seen.ToUniversalTime() > now.AddMinutes(1)
            || now - seen.ToUniversalTime() > TimeSpan.FromSeconds(pointer.playerTtlSeconds + 60))
        { reason = "The return window for the previous run has expired."; return false; }
        reason = null;
        return true;
    }

    public static bool TryValidateJoinedRun(LastMultiplayerRun pointer, out MultiplayerRunRecord run, out string reason)
    {
        run = null;
        reason = "The previous run is no longer available.";
        var room = PhotonNetwork.CurrentRoom;
        var local = PhotonNetwork.LocalPlayer;
        if (pointer == null || room == null || local == null || !local.HasRejoined
            || room.Name != pointer.room || local.ActorNumber != pointer.actor || local.UserId != pointer.accountId
            || !Equals(room.CustomProperties[MultiplayerSessionManager.ProtocolKey], MultiplayerSessionManager.Protocol)
            || room.CustomProperties[MultiplayerSessionManager.RunKey] is not string json || json.Length > 32768) return false;
        try { run = JsonUtility.FromJson<MultiplayerRunRecord>(json); }
        catch (ArgumentException) { return false; }
        if (!MultiplayerRunRecords.Valid(run) || run.runId != pointer.runId || run.gameVersion != Application.version
            || run.restaurant != pointer.restaurant || !string.IsNullOrEmpty(run.endReason)) return false;
        if (PhotonNetwork.MasterClient == null || PhotonNetwork.MasterClient.ActorNumber != run.hostActor
            || PhotonNetwork.MasterClient.IsInactive || PhotonNetwork.MasterClient.UserId != run.hostAccountId)
        { reason = "The host left, so this run has ended."; return false; }
        var participant = run.participants.Find(p => p.actor == local.ActorNumber && p.accountId == local.UserId);
        if (participant == null || participant.departed)
        { reason = "Your place in that run has expired."; return false; }
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
