using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

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
        public Object target;
        public string multiplayerTaskId;
        public int claimedFrame;
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static int activePlayerTargetId;
    private static UnityEngine.Object activePlayerTarget;

    public static event Action PlayerTaskChanged;

    public static bool PlayerHasActiveTask
    {
        get
        {
            ValidateActivePlayerTarget();
            return activePlayerTargetId != 0;
        }
    }

    public static UnityEngine.Object ActivePlayerTarget
    {
        get
        {
            ValidateActivePlayerTarget();
            return activePlayerTarget;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Entries.Clear();
        activePlayerTargetId = 0;
        activePlayerTarget = null;
        PlayerTaskChanged = null;
    }

    public static bool CanBotStart(Object target, float playerGraceSeconds)
    {
        if (target == null)
            return false;

        if (!CanStartMultiplayerTask(target))
            return false;

        if (target is CustomerGroup customerGroup && customerGroup.IsReceptionClaimedByPlayer)
            return false;

        Entry entry = GetOrCreate(target);
        RemoveInactiveMultiplayerOwner(entry);
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

        if (MultiplayerActive && !IsEligibleServiceBot(botOwner))
            return false;

        Entry entry = GetOrCreate(target);
        entry.botOwner = botOwner;
        entry.multiplayerTaskId = MultiplayerActive ? GetMultiplayerTaskId(target) : null;
        entry.claimedFrame = Time.frameCount;
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

        bool changed = activePlayerTargetId != targetId || activePlayerTarget != target;
        entry.playerClaimUntil = float.PositiveInfinity;
        activePlayerTargetId = targetId;
        activePlayerTarget = target;
        if (changed)
            PlayerTaskChanged?.Invoke();
        return true;
    }

    public static bool IsClaimedByBot(Object target)
    {
        if (target == null || !Entries.TryGetValue(target.GetInstanceID(), out Entry entry)) return false;
        RemoveInactiveMultiplayerOwner(entry);
        return entry.botOwner != null;
    }

    public static bool IsClaimedByBot(Object target, AutonomousStaffBot botOwner)
    {
        return IsClaimedByBot(target) && botOwner != null &&
               Entries.TryGetValue(target.GetInstanceID(), out Entry entry) &&
               entry.botOwner == botOwner;
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
        {
            activePlayerTargetId = 0;
            activePlayerTarget = null;
            PlayerTaskChanged?.Invoke();
        }
    }

    public static void ReleaseBot(Object target, AutonomousStaffBot botOwner)
    {
        if (target == null || botOwner == null)
            return;

        if (Entries.TryGetValue(target.GetInstanceID(), out Entry entry) && entry.botOwner == botOwner)
        {
            entry.botOwner = null;
            entry.multiplayerTaskId = null;
            if (MultiplayerActive && target is CustomerGroup group) group.ReleaseBotReceptionTask();
        }
    }

    public static void Complete(Object target)
    {
        if (target != null)
        {
            if (activePlayerTargetId == target.GetInstanceID())
            {
                activePlayerTargetId = 0;
                activePlayerTarget = null;
                PlayerTaskChanged?.Invoke();
            }
            Entries.Remove(target.GetInstanceID());
        }
    }

    private static Entry GetOrCreate(Object target)
    {
        int id = target.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry entry))
        {
            entry = new Entry { firstSeenAt = Time.time, target = target };
            Entries.Add(id, entry);
        }

        return entry;
    }

    private static bool MultiplayerActive => MultiplayerSessionManager.Instance != null
        && MultiplayerSessionManager.Instance.IsMultiplayerSession;

    private static string GetMultiplayerTaskId(Object target)
    {
        if (target is Booth booth) return booth.CleanupTaskId;
        if (target is FoodTray tray && tray.TargetGroup != null && !tray.TargetGroup.IsTakeout
            && tray.TargetGroup.state == CustomerGroup.GroupState.OrderTaken)
            return $"Order:{tray.orderNumber}:Pickup";
        if (!(target is CustomerGroup group) || group.IsTakeout) return null;
        var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        var view = customer != null ? customer.photonView : null;
        if (view == null || view.ViewID <= 0) return null;
        string kind = group.state == CustomerGroup.GroupState.Waiting ? "GreetSeat"
            : group.state == CustomerGroup.GroupState.ReadyToOrder ? "Order"
            : group.state == CustomerGroup.GroupState.NeedsBill ? "Bill" : null;
        return kind == null ? null : $"Customer:{view.ViewID}:{kind}";
    }

    private static bool IsEligibleServiceBot(AutonomousStaffBot bot)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsAuthority || bot == null || !bot.isActiveAndEnabled) return false;
        var roster = session.GetComponent<MultiplayerStaffRosterController>();
        if (roster == null) return false;
        for (int i = 0; i < 4; i++)
        {
            var role = (MultiplayerStaffRosterController.ServiceRole)i;
            var root = roster.GetStaffRoot(role);
            if (root != null && (bot.gameObject == root || bot.transform.IsChildOf(root.transform)))
                return roster.IsRoleAvailableForAI(role);
        }
        return false;
    }

    private static bool CanStartMultiplayerTask(Object target)
    {
        if (!MultiplayerActive) return true;
        var session = MultiplayerSessionManager.Instance;
        if (!session.IsAuthority) return false;
        if (target is FoodTray tray && tray.NetworkCarryLocked) return false;
        string id = GetMultiplayerTaskId(target);
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        return id == null || (claims != null && !claims.IsClaimed(id));
    }

    private static void RemoveInactiveMultiplayerOwner(Entry entry)
    {
        if (!MultiplayerActive || (IsEligibleServiceBot(entry.botOwner)
            && (entry.claimedFrame == Time.frameCount || entry.botOwner.IsBusy))) return;
        entry.botOwner = null;
        entry.multiplayerTaskId = null;
        if (entry.target is CustomerGroup group) group.ReleaseBotReceptionTask();
    }

    // Called on the authority before the human switch commits. Store the ID at
    // acquisition so customer state changes cannot rename an in-flight AI task.
    public static bool IsMultiplayerTaskOwnedByBot(string taskId)
    {
        if (!MultiplayerActive || !MultiplayerSessionManager.Instance.IsAuthority) return false;
        foreach (Entry entry in Entries.Values)
        {
            RemoveInactiveMultiplayerOwner(entry);
            if (entry.target != null && entry.botOwner != null && entry.multiplayerTaskId == taskId)
                return true;
        }
        return false;
    }

    private static void ValidateActivePlayerTarget()
    {
        if (activePlayerTargetId == 0 || activePlayerTarget != null)
            return;

        activePlayerTargetId = 0;
        activePlayerTarget = null;
        PlayerTaskChanged?.Invoke();
    }
}
