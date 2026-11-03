// Runs the actual saved-return validator and readiness model against in-memory
// Unity/Photon boundaries. Never connects to a room or touches real PlayerPrefs.
using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace UnityEngine
{
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> values = new();
        public static string GetString(string key, string fallback) => values.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => values[key] = value;
        public static void DeleteKey(string key) => values.Remove(key);
        public static void Save() { }
    }
}
namespace Photon.Realtime
{
    public static class ErrorCode
    {
        public const short OperationNotAllowedInCurrentState = -3, JoinFailedWithRejoinerNotFound = 32748,
            GameDoesNotExist = 32758, GameClosed = 32764, GameFull = 32765;
    }
    public sealed class Player { public int ActorNumber; public string UserId; public bool IsInactive; }
    public sealed class PropertyMap : Dictionary<string, object>
    { public new object this[string key] { get => TryGetValue(key, out var value) ? value : null; set => base[key] = value; } }
    public sealed class Room { public string Name; public int PlayerTtl; public PropertyMap CustomProperties = new(); }
}
namespace Photon.Pun
{
    public static class PhotonNetwork
    { public static Room CurrentRoom; public static Player LocalPlayer, MasterClient; public static string CloudRegion = "asia"; }
}
public static class PhotonBootstrap
{ public const string RoomRegion = "asia", ConnectionVersion = "reporting-test.shared-session-24"; }

public static class MultiplayerReturnRegression
{
    private static int assertions;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    public static void Run()
    {
        var now = DateTime.UtcNow;
        foreach (string restaurant in new[] { "CasualDining", "FastFood", "FineDining" })
        {
            MultiplayerDestinations.TryGet(restaurant, out string scene, out string rules);
            var pointer = new LastMultiplayerRun { actor = 2, playerTtlSeconds = 0, room = "DINE-TEST",
                accountId = "guest", runId = "0123456789abcdef0123456789abcdef", restaurant = restaurant,
                region = PhotonBootstrap.RoomRegion, networkVersion = PhotonBootstrap.ConnectionVersion,
                lastSeenUtc = now.AddDays(-2).ToString("o") };
            bool Compatible() => MultiplayerLastRun.IsCompatible(pointer, "guest", now, out _);
            Check(Compatible(), restaurant + " TTL-zero reserved return rejected");
            Check(!MultiplayerLastRun.UsesActorRejoin(pointer), "TTL-zero return must use a new actor");
            pointer.playerTtlSeconds = 90;
            Check(MultiplayerLastRun.UsesActorRejoin(pointer), "Retained actor must rejoin");
            pointer.playerTtlSeconds = 0;
            Check(!MultiplayerLastRun.IsCompatible(pointer, "other", now, out _), "Account switch accepted old identity");
            pointer.networkVersion += ".old"; Check(!Compatible(), "Old namespace accepted");
            pointer.networkVersion = PhotonBootstrap.ConnectionVersion;
            pointer.region = "eu"; Check(!Compatible(), "Wrong region accepted"); pointer.region = "asia";
            pointer.playerTtlSeconds = -1; Check(!Compatible(), "Negative TTL accepted"); pointer.playerTtlSeconds = 0;

            var run = new MultiplayerRunRecord { runId = pointer.runId, restaurant = restaurant, sceneName = scene,
                rulesVersion = rules, hostActor = 1, hostAccountId = "host", partySize = 2,
                gameVersion = Application.version, startedUtc = now.ToString("o"), participants = new() {
                    new() { accountId = "host", actor = 1, initialActor = 1 },
                    new() { accountId = "guest", actor = 2, initialActor = 2, disconnectDeadline = 1 } } };
            PhotonNetwork.CurrentRoom = new Room { Name = pointer.room, PlayerTtl = 0 };
            PhotonNetwork.LocalPlayer = new Player { ActorNumber = 3, UserId = "guest" };
            PhotonNetwork.MasterClient = new Player { ActorNumber = 1, UserId = "host" };
            var properties = PhotonNetwork.CurrentRoom.CustomProperties;
            properties[MultiplayerSessionManager.ProtocolKey] = MultiplayerSessionManager.Protocol;
            properties[MultiplayerDestinations.SceneKey] = scene;
            properties[MultiplayerDestinations.RestaurantKey] = restaurant;
            void Publish() => properties[MultiplayerSessionManager.RunKey] = JsonUtility.ToJson(run);
            bool Validate(out string reason) => MultiplayerLastRun.TryValidateJoinedRun(pointer, out _, out reason);
            Publish();
            Check(!Validate(out var reason) && reason == "Waiting for host admission.", "New actor bypassed host admission");
            run.participants[1].actor = 3; run.participants[1].loadEpoch = 1;
            Publish(); Check(!Validate(out reason) && reason == "Waiting for host admission.", "Presence marker bypassed admission");
            run.participants[1].disconnectDeadline = 0;
            Publish(); Check(Validate(out _), "Original host's remapped participant rejected");
            MultiplayerLastRun.Remember(run, 3);
            var saved = MultiplayerLastRun.Load("guest", out _);
            Check(saved?.actor == 3 && saved.playerTtlSeconds == 0 && !MultiplayerLastRun.UsesActorRejoin(saved), "New-actor return saved wrong transport");
            Check(MultiplayerLastRun.Load("other", out _) == null, "Saved return leaked to another account");
            run.participants[1].kicked = true; Publish(); Check(!Validate(out _), "Kicked guest admitted");
            run.participants[1].kicked = false; run.participants[1].departed = true;
            Publish(); Check(!Validate(out _), "Departed guest admitted"); run.participants[1].departed = false;
            run.endReason = "InsufficientPlayers"; Publish(); Check(!Validate(out _), "Ended solo run reopened");
            run.endReason = null; Publish();
            PhotonNetwork.MasterClient.IsInactive = true; Check(!Validate(out _), "Inactive host accepted");
            PhotonNetwork.MasterClient.IsInactive = false;
            PhotonNetwork.MasterClient.UserId = "other"; Check(!Validate(out _), "Replacement host accepted");
            PhotonNetwork.MasterClient.UserId = "host";
            properties[MultiplayerDestinations.SceneKey] = "wrong"; Check(!Validate(out _), "Wrong restaurant scene accepted");
            properties[MultiplayerDestinations.SceneKey] = scene;
            properties[MultiplayerSessionManager.ProtocolKey] = "old"; Check(!Validate(out _), "Wrong protocol accepted");
            MultiplayerLastRun.Forget("guest", pointer.runId);
            Check(MultiplayerLastRun.Load("guest", out _) == null, "Ended return pointer was retained");

            var solo = MultiplayerDayReadiness.Create(2, 1, new[] { 1 }, "1:0");
            Check(!solo.Valid && !solo.AllReady && !solo.BeginCountdown(100), restaurant + " one player started another day");
            for (int count = 2; count <= 4; count++)
            {
                int[] actors = new int[count]; for (int i = 0; i < count; i++) actors[i] = i + 1;
                var ready = MultiplayerDayReadiness.Create(2, 1, actors, "current");
                Check(ready.Valid && !ready.BeginCountdown(100), "Start skipped guest votes");
                for (int i = 1; i < count; i++) Check(ready.SetVote(ready.id, actors[i], 1, true), "Guest vote rejected");
                Check(ready.AllReady && ready.BeginCountdown(100) && ready.deadline == 103, "Valid multiplayer team could not start");
                Check(ready.SetVote(ready.id, 2, 2, false) && !ready.CountingDown, "Withdrawal did not stop start");
            }
        }
        foreach (short code in new short[] { -3, 32748 })
        {
            Check(MultiplayerLastRun.CanFallBackToJoin(true, code), "Actor-only failure lacks fallback");
            Check(!MultiplayerLastRun.CanFallBackToJoin(false, code), "Ordinary join can loop actor fallback");
        }
        foreach (short code in new short[] { 32758, 32764, 32765 })
            Check(!MultiplayerLastRun.CanFallBackToJoin(true, code), "Missing/closed/full room masked by fallback");
        PhotonNetwork.CurrentRoom = null;
        Console.WriteLine($"PASS: {assertions} source-linked return/admission/readiness assertions; Photon and Unity boundaries are test doubles.");
    }
}
