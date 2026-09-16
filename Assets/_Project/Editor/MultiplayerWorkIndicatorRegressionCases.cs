#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

// Opt-in cases for an external Unity runner. Importing this file runs nothing.
public static class MultiplayerWorkIndicatorRegressionCases
{
    public static void Run()
    {
        var work = new MultiplayerTaskClaims.ActorWork
        {
            actor = "player:2", generation = 9, phase = MultiplayerTaskClaims.WorkPhase.Working,
            startedAt = 100d, duration = 4f
        };
        Assert(Progress(work, 102d, out float half) && Mathf.Approximately(half, 0.5f),
            "Known work did not use its authoritative start and actual duration.");
        Assert(Progress(work, 110d, out float done) && done == 1f, "Completed work exceeded its ring.");
        work.phase = MultiplayerTaskClaims.WorkPhase.Approach;
        Assert(!Progress(work, 102d, out _), "Approach fabricated timed progress.");
        work.phase = MultiplayerTaskClaims.WorkPhase.Carrying;
        Assert(!Progress(work, 102d, out _), "Carrying fabricated timed progress.");
        work.phase = MultiplayerTaskClaims.WorkPhase.Working;
        work.duration = 0f;
        Assert(!Progress(work, 102d, out _), "Unknown work duration did not use the spinner.");
        work.duration = float.NaN;
        Assert(!Progress(work, 102d, out _), "Invalid timing became progress.");

        work.duration = 4f;
        var received = JsonUtility.FromJson<MultiplayerTaskClaims.ActorWork>(JsonUtility.ToJson(work));
        Assert(received.actor == work.actor && received.generation == work.generation
            && Progress(received, 102d, out half) && Mathf.Approximately(half, 0.5f),
            "Activity projection lost actor, generation or timing during serialization.");

        var timing = new MultiplayerWorkTiming();
        timing.Begin(1f);
        timing.Clear();
        Assert(!timing.TryRead(out _, out _), "Cancelled timed work retained a progress clock.");
    }
    private static bool Progress(MultiplayerTaskClaims.ActorWork work, double now, out float amount)
    {
        var args = new object[] { work, now, 0f };
        bool timed = (bool)typeof(MultiplayerWorkIndicator).GetMethod("TryProgress",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        amount = (float)args[2];
        return timed;
    }
    private static void Assert(bool passed, string message)
    { if (!passed) throw new InvalidOperationException(message); }
}
#endif
