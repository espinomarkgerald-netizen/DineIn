#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

// Explicit Edit Mode checks only: no PlayerPrefs writes, room operations, or scene objects.
public static class MultiplayerReconnectRegressionCases
{
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run reconnect metadata checks in Edit Mode.");
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        const string account = "reconnect-regression-account";
        LastMultiplayerRun Pointer() => new LastMultiplayerRun {
            actor = 2, playerTtlSeconds = 90, room = "DINE-TEST", accountId = account,
            region = PhotonBootstrap.RoomRegion, networkVersion = PhotonBootstrap.ConnectionVersion,
            runId = "0123456789abcdef0123456789abcdef", restaurant = "CasualDining",
            lastSeenUtc = now.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
        };
        bool Compatible(LastMultiplayerRun value) => MultiplayerLastRun.IsCompatible(value, account, now, out _);
        void Reject(Action<LastMultiplayerRun> mutate, string message)
        {
            var pointer = Pointer(); mutate(pointer);
            Check(!Compatible(pointer), message);
        }
        Check(Compatible(Pointer()), "A current account-bound room pointer was rejected.");
        Check(!Compatible(null), "Missing pointer was accepted.");
        Check(!MultiplayerLastRun.IsCompatible(Pointer(), "another-account", now, out _),
            "A different account could use the saved actor identity.");
        Reject(p => p.schema = 2, "Unknown pointer schema was accepted.");
        Reject(p => p.actor = 0, "A missing actor identity was accepted.");
        Reject(p => p.room = "", "A missing room was accepted.");
        Reject(p => p.room = new string('x', 65), "An oversized room identifier was accepted.");
        Reject(p => p.runId = "old-run", "A malformed run identity was accepted.");
        Reject(p => p.restaurant = "unsupported", "An unsupported restaurant was accepted.");
        Reject(p => p.playerTtlSeconds = -1, "A negative actor lifetime was accepted.");
        Reject(p => p.networkVersion += ".other", "A different network version was accepted.");
        Reject(p => p.region = "wrong-region", "A different region was accepted.");
        var rosterReturn = Pointer(); rosterReturn.playerTtlSeconds = 0; rosterReturn.lastSeenUtc = "old or unavailable";
        Check(Compatible(rosterReturn) && !MultiplayerLastRun.UsesActorRejoin(rosterReturn),
            "TTL-zero room return must join with a new actor and await host admission.");
        Check(MultiplayerLastRun.UsesActorRejoin(Pointer()), "A retained Photon actor did not use rejoin.");
        foreach (string restaurant in new[] { "CasualDining", "FastFood", "FineDining" })
        { var value = Pointer(); value.restaurant = restaurant; Check(Compatible(value), "Supported return destination was rejected."); }
        Check(MultiplayerLastRun.CanFallBackToJoin(true, Photon.Realtime.ErrorCode.OperationNotAllowedInCurrentState)
            && MultiplayerLastRun.CanFallBackToJoin(true, Photon.Realtime.ErrorCode.JoinFailedWithRejoinerNotFound)
            && !MultiplayerLastRun.CanFallBackToJoin(false, Photon.Realtime.ErrorCode.OperationNotAllowedInCurrentState)
            && !MultiplayerLastRun.CanFallBackToJoin(true, Photon.Realtime.ErrorCode.GameDoesNotExist),
            "Actor rejoin failure fallback is missing or can loop/create a replacement room.");
        var restored = JsonUtility.FromJson<LastMultiplayerRun>(JsonUtility.ToJson(Pointer()));
        Check(Compatible(restored) && restored.actor == 2 && restored.accountId == account,
            "Pointer serialization lost the actor/account pairing.");

        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        var compose = typeof(MultiplayerTaskClaims).GetMethod("ScopeGeneration", methods);
        var matches = typeof(MultiplayerTaskClaims).GetMethod("GenerationMatchesEpoch", methods);
        Check(compose != null && matches != null, "Task claim connection fencing is missing.");
        long Generation(int epoch, long sequence) => (long)compose.Invoke(null, new object[] { epoch, sequence });
        bool Matches(long generation, int epoch) => (bool)matches.Invoke(null, new object[] { generation, epoch });
        long previous = Generation(0, 1000);
        long returned = Generation(1, 1);
        Check(previous == 1000 && Matches(previous, 0), "Initial connection changed its existing claim ordering.");
        Check(returned > previous && Matches(returned, 1), "A relaunched actor's restarted sequence cannot claim tasks.");
        Check(!Matches(previous, 1), "A deferred pre-disconnect request could mutate the returning actor's tasks.");
        Check(!Matches(returned, 0), "A future connection request bypassed the host's current epoch.");
        Check(returned == Generation(1, 1), "Retrying the same claim changed its request identity.");
        Check(Generation(-1, 1) == 0 && Generation(1, 0) == 0
            && Generation(1, (long)uint.MaxValue + 1) == 0 && !Matches(0, 0),
            "An invalid or overflowed task sequence was accepted.");
        Check(Matches(Generation(int.MaxValue, uint.MaxValue), int.MaxValue),
            "A valid epoch/sequence overflowed signed generation storage.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
