using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class LocalSaveManager
{
    public const int RestaurantProgressionVersion = 1;
    public const string CasualDiningId = "casual", FastFoodId = "fastfood", FineDiningId = "finedining";
    private const string FileName = "singleplayer_save.json";

    [Serializable]
    public class SaveData
    {
        public string username = "Guest";
        public string progressAccount;
        public int coins = 0;
        public ApprovalProgress sharedApproval;
        public int restaurantProgressionVersion;
        public List<string> unlockedRestaurantIds;
        public List<string> ownedItemIds = new List<string>();

        public List<RoleEntry> roles = new List<RoleEntry>();
        // Profile-wide cosmetics intentionally live outside restaurant/day-start career saves.
        public List<AppearanceRecord> appearances = new List<AppearanceRecord>();
    }

    [Serializable]
    public sealed class ApprovalProgress
    {
        public int version = 1;
        public int value;
        public bool lockedAt100, congratulationsDelivered;
    }

    public static bool ValidApproval(ApprovalProgress value) => value == null ||
        (value.version == 1 && value.value >= 0 && value.value <= 100 &&
         (!value.lockedAt100 || value.value == 100) && (!value.congratulationsDelivered || value.lockedAt100));

    [Serializable]
    public sealed class AppearanceRecord
    {
        public string accountId;
        public string json;
        public long revision;
        public bool pendingUpload;
    }

    [Serializable]
    public class RoleEntry
    {
        public string roleId;              // waiter/cook/cashier/manager
        public int highestUnlockedLevel = 1;
        public List<int> completedLevels = new List<int>();
    }

    private static SaveData _cache;
    public static bool NeedsRecovery { get; private set; }
    public static void MarkRestored() { NeedsRecovery = false; }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SaveData Data => _cache ??= Load();

    internal static void InitializeRestaurantProgression(SaveData data, bool legacy)
    {
        if (data.restaurantProgressionVersion != 0) return;
        if (data.unlockedRestaurantIds == null || data.unlockedRestaurantIds.Count == 0) data.unlockedRestaurantIds = legacy
            ? new List<string> { CasualDiningId, FastFoodId, FineDiningId }
            : new List<string> { CasualDiningId };
        data.restaurantProgressionVersion = RestaurantProgressionVersion;
    }

    // Repair only the broken v1-empty migration, with a valid legacy profile for this exact account.
    // New profiles always have Casual Dining and cannot enter this branch.
    private static bool RepairLegacyRestaurantOwnership(SaveData data)
    {
        if (data.restaurantProgressionVersion != 1 || data.unlockedRestaurantIds == null ||
            data.unlockedRestaurantIds.Count != 0 || string.IsNullOrEmpty(data.progressAccount)) return false;
        try
        {
            string path = FilePath + ".bak";
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            if (json.Contains("\"restaurantProgressionVersion\"") || !json.Contains("\"roles\"")) return false;
            var previous = JsonUtility.FromJson<SaveData>(json);
            if (previous == null || previous.progressAccount != data.progressAccount || previous.roles == null || previous.ownedItemIds == null ||
                previous.coins < 0 || !ValidApproval(previous.sharedApproval)) return false;
            data.unlockedRestaurantIds = new List<string> { CasualDiningId, FastFoodId, FineDiningId };
            return true;
        }
        catch (Exception error)
        {
            // An unreadable optional backup must not invalidate the otherwise readable current profile.
            Debug.LogWarning("[LocalSaveManager] Legacy ownership repair deferred: " + error.Message);
            return false;
        }
    }

    public static bool IsRestaurantUnlocked(string id)
    {
        var data = Data;
        return !NeedsRecovery && data.unlockedRestaurantIds != null && data.unlockedRestaurantIds.Contains(id);
    }

    public static bool CreditRestaurantDays(string account, string restaurant, string runId, List<int> days)
    {
        if (NeedsRecovery || Data.progressAccount != account || !CampaignSaveStore.AccountMatches(account)) return false;
        var added = new List<string>();
        foreach (int day in days ?? new List<int>())
        {
            if (day < 1) continue;
            string id = "restaurant-day:" + restaurant + ":" + runId + ":" + day;
            if (!Data.ownedItemIds.Contains(id)) { Data.ownedItemIds.Add(id); added.Add(id); }
        }
        if (added.Count == 0 || TrySave()) return true;
        foreach (string id in added) Data.ownedItemIds.Remove(id);
        return false;
    }

    public static void EnsureDefaultRoles()
    {
        EnsureRole("waiter");
        EnsureRole("cook");
        EnsureRole("cashier");
        EnsureRole("manager");
    }

    public static RoleEntry EnsureRole(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId)) roleId = "waiter";

        var list = Data.roles;
        var found = list.Find(r => r.roleId == roleId);
        if (found == null)
        {
            found = new RoleEntry { roleId = roleId, highestUnlockedLevel = 1 };
            list.Add(found);
            Save();
        }
        return found;
    }

    public static bool IsLevelUnlocked(string roleId, int level)
    {
        EnsureDefaultRoles();
        var r = EnsureRole(roleId);
        return level <= Mathf.Clamp(r.highestUnlockedLevel, 1, 3);
    }

    public static void CompleteLevel(string roleId, int level)
    {
        EnsureDefaultRoles();
        var r = EnsureRole(roleId);

        if (!r.completedLevels.Contains(level))
            r.completedLevels.Add(level);

        if (level < 3)
            r.highestUnlockedLevel = Mathf.Max(r.highestUnlockedLevel, level + 1);

        Save();
    }

    public static void Save()
    {
        TrySave();
    }

    public static bool TrySave()
    {
        try
        {
            var data = Data;
            if (NeedsRecovery) return false;
            var json = JsonUtility.ToJson(data, true);
            CampaignSaveStore.AtomicWrite(FilePath, json);
            CampaignSaveStore.NotifyLocalWrite();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Save failed: {e}");
            return false;
        }
    }

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var d = new SaveData();
                InitializeRestaurantProgression(d, false);
                _cache = d;
                EnsureDefaultRoles();
                Save();
                return d;
            }

            var json = File.ReadAllText(FilePath);
            var d2 = JsonUtility.FromJson<SaveData>(json);
            if (d2 == null || !ValidApproval(d2.sharedApproval) || d2.roles == null || d2.ownedItemIds == null || !json.Contains("\"roles\"")) throw new InvalidDataException("Invalid local profile.");
            bool migrate = d2.restaurantProgressionVersion == 0;
            InitializeRestaurantProgression(d2, true);
            migrate |= RepairLegacyRestaurantOwnership(d2);
            if (d2.unlockedRestaurantIds == null) throw new InvalidDataException("Missing restaurant ownership.");
            _cache = d2;
            if (migrate && !TrySave()) throw new IOException("Could not persist restaurant migration.");
            EnsureDefaultRoles();
            return d2;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Load failed: {e}");
            NeedsRecovery = true;
            // Quarantine the unreadable bytes; do not save defaults over the only copy.
            try { if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".corrupt", true); } catch { }
            var d = new SaveData();
            _cache = d;
            EnsureDefaultRoles();
            return d;
        }
    }
}
