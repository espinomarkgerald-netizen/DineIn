using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class LocalSaveManager
{
    private const string FileName = "singleplayer_save.json";

    [Serializable]
    public class SaveData
    {
        public string username = "Guest";
        public int coins = 0;
        public List<string> ownedItemIds = new List<string>();

        public List<RoleEntry> roles = new List<RoleEntry>();
    }

    [Serializable]
    public class RoleEntry
    {
        public string roleId;              // waiter/cook/cashier/manager
        public int highestUnlockedLevel = 1;
        public List<int> completedLevels = new List<int>();
    }

    private static SaveData _cache;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SaveData Data => _cache ??= Load();

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
        try
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Save failed: {e}");
        }
    }

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var d = new SaveData();
                _cache = d;
                EnsureDefaultRoles();
                Save();
                return d;
            }

            var json = File.ReadAllText(FilePath);
            var d2 = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            _cache = d2;
            EnsureDefaultRoles();
            return d2;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Load failed: {e}");
            var d = new SaveData();
            _cache = d;
            EnsureDefaultRoles();
            return d;
        }
    }
}
