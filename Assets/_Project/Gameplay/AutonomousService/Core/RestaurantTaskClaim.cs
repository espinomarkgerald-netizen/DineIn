using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight arbitration between the Manager and autonomous staff.
/// A newly available task gives the player a short reaction window. Clicking
/// the task claims it for the player; otherwise a bot may take it afterward.
/// </summary>
public static class RestaurantTaskClaim
{
    private sealed class Entry
    {
        public float firstSeenAt;
        public float playerClaimUntil;
        public AutonomousStaffBot botOwner;
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static int activePlayerTargetId;

    public static bool PlayerHasActiveTask => activePlayerTargetId != 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Entries.Clear();
        activePlayerTargetId = 0;
    }

    public static bool CanBotStart(Object target, float playerGraceSeconds)
    {
        if (target == null)
            return false;

        if (target is CustomerGroup customerGroup && customerGroup.IsReceptionClaimedByPlayer)
            return false;

        Entry entry = GetOrCreate(target);
        if (entry.botOwner != null)
            return false;
        if (Time.time < entry.playerClaimUntil)
            return false;

        if (PlayerHasActiveTask)
            return true;

        return Time.time >= entry.firstSeenAt + Mathf.Max(0f, playerGraceSeconds);
    }

    public static bool TryClaimBot(Object target, AutonomousStaffBot botOwner, float playerGraceSeconds)
    {
        if (target == null || botOwner == null || !CanBotStart(target, playerGraceSeconds))
            return false;

        GetOrCreate(target).botOwner = botOwner;
        return true;
    }

    public static bool TryClaimPlayer(Object target)
    {
        if (target == null)
            return false;

        int targetId = target.GetInstanceID();
        if (activePlayerTargetId != 0 && activePlayerTargetId != targetId)
            return false;

        Entry entry = GetOrCreate(target);
        if (target is CustomerGroup customerGroup && customerGroup.IsReceptionClaimedByBot)
            return false;
        if (entry.botOwner != null)
            return false;

        entry.playerClaimUntil = float.PositiveInfinity;
        activePlayerTargetId = targetId;
        return true;
    }

    public static bool IsClaimedByBot(Object target)
    {
        return target != null && Entries.TryGetValue(target.GetInstanceID(), out Entry entry) &&
               entry.botOwner != null;
    }

    public static bool IsClaimedByPlayer(Object target)
    {
        return target != null && activePlayerTargetId == target.GetInstanceID();
    }

    public static void ReleasePlayer(Object target)
    {
        if (target == null)
            return;

        if (Entries.TryGetValue(target.GetInstanceID(), out Entry entry))
            entry.playerClaimUntil = Time.time;

        if (activePlayerTargetId == target.GetInstanceID())
            activePlayerTargetId = 0;
    }

    public static void ReleaseBot(Object target, AutonomousStaffBot botOwner)
    {
        if (target == null || botOwner == null)
            return;

        if (Entries.TryGetValue(target.GetInstanceID(), out Entry entry) && entry.botOwner == botOwner)
            entry.botOwner = null;
    }

    public static void Complete(Object target)
    {
        if (target != null)
        {
            if (activePlayerTargetId == target.GetInstanceID())
                activePlayerTargetId = 0;
            Entries.Remove(target.GetInstanceID());
        }
    }

    private static Entry GetOrCreate(Object target)
    {
        int id = target.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry))
        {
            entry = new Entry { firstSeenAt = Time.time };
            Entries.Add(id, entry);
        }

        return entry;
    }
}
