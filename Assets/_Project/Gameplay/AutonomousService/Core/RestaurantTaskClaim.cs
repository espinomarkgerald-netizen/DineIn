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
        public long jobGeneration;
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static readonly List<int> expiredTargets = new();
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
        entry.jobGeneration = botOwner.JobGeneration + (botOwner.IsBusy ? 0 : 1);
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

    public static void ReleaseBot(Object target, AutonomousStaffBot botOwner, long expectedGeneration = -1)
    {
        if (target == null || botOwner == null)
            return;

        if (Entries.TryGetValue(target.GetInstanceID(), out Entry entry) && entry.botOwner == botOwner
            && (expectedGeneration < 0 || entry.jobGeneration == expectedGeneration))
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

    public static string GetMultiplayerTaskId(Object target)
    {
        if (target is Booth booth) return booth.CleanupTaskId;
        if (target is MoneyPickup money && money.TargetGroup != null)
        {
            var owner = money.TargetGroup.GetComponentInParent<MultiplayerCustomerSpawn>();
            return owner != null ? $"Customer:{owner.photonView.ViewID}:Payment" : null;
        }
        if (target is FoodTray dirty && dirty.GetComponent<FoodTrayInteractable>()?.IsCleanupPickable == true)
            return $"Order:{dirty.orderNumber}:Cleanup";
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
        if (MultiplayerServiceActions.IsReserved(target)) return false;
        if (target is FoodTray tray && tray.NetworkCarryLocked) return false;
        string id = GetMultiplayerTaskId(target);
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        return id == null || (claims != null && !claims.IsClaimed(id));
    }

    private static void RemoveInactiveMultiplayerOwner(Entry entry)
    {
        if (!MultiplayerActive || (IsEligibleServiceBot(entry.botOwner)
            && entry.target != null && (entry.claimedFrame == Time.frameCount ||
                (entry.botOwner.IsBusy && entry.botOwner.JobGeneration == entry.jobGeneration)))) return;
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

    public static void ReleaseJob(AutonomousStaffBot bot, long generation)
    {
        if (!MultiplayerActive || bot == null) return;
        foreach (Entry entry in Entries.Values)
            if (entry.botOwner == bot && entry.jobGeneration == generation)
            {
                entry.botOwner = null;
                entry.multiplayerTaskId = null;
                if (entry.target is CustomerGroup group) group.ReleaseBotReceptionTask();
                if (entry.target is FoodTray tray) tray.GetComponent<FoodTrayInteractable>()?.SetClaimedByStaff(false);
            }
    }

    public static bool TryTakeOver(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return false;
        if (!MultiplayerActive || !MultiplayerSessionManager.Instance.IsAuthority) return false;
        foreach (Entry entry in Entries.Values)
        {
            RemoveInactiveMultiplayerOwner(entry);
            if (entry.botOwner == null || entry.multiplayerTaskId != taskId) continue;
            var bot = entry.botOwner;
            // An uncollected batch item can be yielded without disturbing the
            // tray currently being approached or the trolley's loaded contents.
            if (entry.target is FoodTray tray && !tray.NetworkCarryLocked
                && tray.GetComponentInParent<BotTrolleyCarrier>() == null
                && bot.ApproachingTarget != null)
            {
                ReleaseBot(tray, bot);
                bot.CancelApproachReservation(tray);
                tray.GetComponent<FoodTrayInteractable>()?.SetClaimedByStaff(false);
                var booth = tray.GetComponentInParent<Booth>();
                bool anotherTray = false;
                if (booth != null)
                {
                    foreach (var other in Entries.Values)
                        if (other.botOwner == bot && other.target is FoodTray held && held.GetComponentInParent<Booth>() == booth)
                            anotherTray = true;
                    if (!anotherTray) ReleaseBot(booth, bot, entry.jobGeneration);
                }
                return true;
            }
            return bot.TryYieldJob(entry.jobGeneration);
        }
        return true;
    }

    public static void CaptureBotClaims(List<MultiplayerTaskClaims.BotClaim> result)
    {
        result.Clear();
        expiredTargets.Clear();
        foreach (var entry in Entries) if (entry.Value.target == null) expiredTargets.Add(entry.Key);
        foreach (int key in expiredTargets) Entries.Remove(key);
        foreach (Entry entry in Entries.Values)
        {
            RemoveInactiveMultiplayerOwner(entry);
            if (entry.botOwner == null || string.IsNullOrEmpty(entry.multiplayerTaskId)) continue;
            var bot = entry.botOwner;
            bool batchAvailable = entry.target is FoodTray tray && !tray.NetworkCarryLocked
                && tray.GetComponentInParent<BotTrolleyCarrier>() == null
                && bot.ApproachingTarget != null;
            result.Add(new MultiplayerTaskClaims.BotClaim { task = entry.multiplayerTaskId,
                owner = bot.name, generation = entry.jobGeneration, committed = (bot.JobCommitted || bot.HasCommittedItem) && !batchAvailable });
        }
        // A failed path can end a job while a bot still holds its bill for retry.
        // The item remains committed during the gap before that retry is scheduled.
        foreach (var bot in MultiplayerWorldRegistry.All<AutonomousStaffBot>())
        {
            if (!IsEligibleServiceBot(bot)) continue;
            var hands = bot.GetComponent<WaiterHands>();
            if (hands == null || !hands.HasBill || hands.holdingBillFor == null || hands.holdingBillFor.HasReceivedBill) continue;
            string task = GetMultiplayerTaskId(hands.holdingBillFor);
            if (!string.IsNullOrEmpty(task) && !result.Exists(c => c.task == task))
                result.Add(new MultiplayerTaskClaims.BotClaim { task = task, owner = bot.name, generation = bot.JobGeneration, committed = true });
        }
        result.Sort((a, b) => string.CompareOrdinal(a.task, b.task));
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
