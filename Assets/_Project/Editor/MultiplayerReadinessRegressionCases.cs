#if UNITY_EDITOR
using System;
using UnityEngine;

// Prepared for the explicit external repair runner. Never runs on import.
public static class MultiplayerReadinessRegressionCases
{
    public static void Run()
    {
        var state = MultiplayerDayReadiness.Create(1, 2, new[] { 2, 7 }, "2:0|7:0");
        Check(state.Valid && state.Count == 1 && !state.AllReady, "Only the requesting host starts ready.");
        Check(!state.BeginCountdown(100) && state.deadline < 0, "Waiting has no timeout or implicit guest vote.");
        Check(!state.SetVote("old-request", 7, 1, true), "Old request accepted a vote.");
        Check(!state.SetVote(state.id, 4, 1, true), "Nonparticipant accepted a vote.");
        Check(!state.SetVote(state.id, 2, 1, false), "Host readiness must change through cancel.");
        Check(state.SetVote(state.id, 7, 1, true) && state.AllReady, "Current guest vote missing.");
        Check(state.BeginCountdown(200) && state.deadline == 203, "Countdown must last three shared-clock seconds.");
        Check(!state.BeginCountdown(201) && state.deadline == 203, "Snapshot refresh restarted the countdown.");
        Check(!state.SetVote(state.id, 7, 1, false) && state.AllReady, "Duplicate vote changed readiness.");
        Check(state.SetVote(state.id, 7, 3, false) && !state.CountingDown, "Withdrawal failed to cancel countdown.");
        Check(!state.SetVote(state.id, 7, 2, true), "Out-of-order vote restored readiness.");
        Check(state.SetVote(state.id, 7, 4, true) && state.BeginCountdown(210) && state.deadline == 213,
            "Reready reused an expired deadline.");
        var restored = JsonUtility.FromJson<MultiplayerDayReadiness>(JsonUtility.ToJson(state));
        Check(restored.Valid && restored.id == state.id && restored.Sequence(7) == 4 && restored.deadline == 213,
            "Snapshot lost request, vote sequence or clock anchor.");
        var next = MultiplayerDayReadiness.Create(2, 2, new[] { 2, 7 }, "2:0|7:0");
        Check(next.id != state.id && next.Count == 1 && !next.CountingDown && !next.IsReady(7),
            "Next day reused readiness.");
        var reduced = MultiplayerDayReadiness.Create(1, 2, new[] { 2 }, "2:0");
        Check(reduced.id != state.id && reduced.AllReady && !reduced.SetVote(state.id, 7, 5, true),
            "Cancelled roster request can affect its replacement.");
        var style = Resources.Load<MultiplayerReadyStyle>("UI/MultiplayerReadyStyle");
        Check(style != null && style.button != null && style.pressed != null && style.font != null,
            "Ready UI lost existing Blue/Double sprites or font.");
        Check(MultiplayerSessionManager.Protocol == "casual-session-8"
            && MultiplayerSessionManager.ResultRulesVersion == "casual-session-2", "Protocol/result compatibility changed.");
    }
    private static void Check(bool okay, string message)
    { if (!okay) throw new InvalidOperationException(message); }
}
#endif
