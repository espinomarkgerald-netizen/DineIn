using UnityEngine;
using System;
using System.Collections.Generic;
using DineIn.Appearance;

public static class PlayerCustomizationData
{
    private static string accountId = "guest";
    public static string AccountId => accountId;
    public static event Action AppearanceChanged;
    public static event Action ProfileChanged;
    public static LocalSaveManager.AppearanceRecord AppearanceRecord
    {
        get
        {
            var records = LocalSaveManager.Data.appearances ??= new List<LocalSaveManager.AppearanceRecord>();
            var record = records.Find(r => r != null && r.accountId == accountId);
            if (record == null) { record = new LocalSaveManager.AppearanceRecord { accountId = accountId }; records.Add(record); }
            return record;
        }
    }
    public static AppearanceRecipe CommittedAppearance
    {
        get { TryReadAppearance(AppearanceRecord.json, out var recipe); return recipe?.Copy(); }
    }
    public static void SelectProfile(string playFabId)
    {
        string next = string.IsNullOrEmpty(playFabId) ? "guest" : "playfab:" + playFabId;
        if (next == accountId) return;
        accountId = next;
        ResetToDefault();
        if (TryReadAppearance(AppearanceRecord.json, out _)) LoadFromJson(AppearanceRecord.json);
        ProfileChanged?.Invoke(); AppearanceChanged?.Invoke();
    }
    public static bool CommitAppearance(AppearanceRecipe recipe, AppearanceCatalog catalog)
    {
        var record = AppearanceRecord;
        string previous = record.json;
        long revision = record.revision;
        bool pending = record.pendingUpload;
        record.json = Serialize(catalog.Validate(recipe));
        record.revision++; record.pendingUpload = accountId != "guest";
        if (!LocalSaveManager.TrySave())
        { record.json = previous; record.revision = revision; record.pendingUpload = pending; return false; }
        AppearanceChanged?.Invoke();
        PhotonCustomizationSync.PushToPhoton();
        return true;
    }
    public static bool TryAcceptCloud(string expectedAccount, long expectedRevision, string json)
    {
        if (accountId != expectedAccount || AppearanceRecord.revision != expectedRevision || AppearanceRecord.pendingUpload ||
            !TryReadAppearance(json, out _)) return false;
        // A legacy payload must not remove a modern local selection.
        if (CommittedAppearance != null && (!TryReadAppearance(json, out var remote) || remote == null)) return false;
        AppearanceRecord.json = json;
        LoadFromJson(json);
        LocalSaveManager.Save(); AppearanceChanged?.Invoke(); PhotonCustomizationSync.PushToPhoton();
        return true;
    }
    public static bool TryReadAppearance(string json, out AppearanceRecipe recipe)
    {
        recipe = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16384) return false;
        try
        {
            // Unity may construct a default nested class even when an old JSON payload omitted it.
            // Inspect presence explicitly so V1 data is not mistaken for a malformed V2 recipe.
            var value = Newtonsoft.Json.Linq.JObject.Parse(json);
            var versionToken = value["Version"];
            if (versionToken != null && versionToken.Type != Newtonsoft.Json.Linq.JTokenType.Integer) return false;
            int version = (int?)versionToken ?? 1;
            if (version < 1 || version > 2) return false;
            var appearance = value["Appearance"];
            if (appearance == null || appearance.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                return value["HeadColorIndex"]?.Type == Newtonsoft.Json.Linq.JTokenType.Integer;
            if (version != 2 || appearance.Type != Newtonsoft.Json.Linq.JTokenType.Object) return false;
            recipe = JsonUtility.FromJson<AppearanceRecipe>(appearance.ToString(Newtonsoft.Json.Formatting.None));
            if (recipe == null || recipe.version != 2 || string.IsNullOrWhiteSpace(recipe.bodyId)) { recipe = null; return false; }
            return true;
        }
        catch { return false; }
    }
    public static int HeadColorIndex = 0;
    public static int BodyColorIndex = 0;
    public static int ArmsColorIndex = 0;
    public static int LegsColorIndex = 0;

    public static int EquippedHatId = 0;

    // hat 0 always owned
    public static HashSet<int> OwnedHats = new HashSet<int>() { 0 };

    // ✅ IMPORTANT:
    // While you don't have a store yet (all hats are "free"), keep this FALSE.
    // When your store is ready and you want to enforce ownership, set it TRUE.
    public static bool EnforceOwnership = false;

    public static bool IsHatOwned(int hatId)
    {
        // If store is not enforced yet, treat all hats as usable
        if (!EnforceOwnership) return true;

        return OwnedHats.Contains(hatId);
    }

    public static void UnlockHat(int hatId)
    {
        OwnedHats.Add(hatId);
    }

    // ✅ Use this when player selects a hat (recommended).
    // It will keep EquippedHatId + optionally mark it owned (useful while hats are free).
    public static void SetEquippedHat(int hatId, bool autoUnlock = true)
    {
        EquippedHatId = Mathf.Max(0, hatId);

        if (autoUnlock)
            OwnedHats.Add(EquippedHatId);

        OwnedHats.Add(0);
    }

    public static void ResetToDefault()
    {
        HeadColorIndex = 0;
        BodyColorIndex = 0;
        ArmsColorIndex = 0;
        LegsColorIndex = 0;

        EquippedHatId = 0;

        OwnedHats.Clear();
        OwnedHats.Add(0);

        EnforceOwnership = false;
    }

    [Serializable]
    private class SaveModel
    {
        public int HeadColorIndex;
        public int BodyColorIndex;
        public int ArmsColorIndex;
        public int LegsColorIndex;

        public int EquippedHatId;

        public List<int> OwnedHats;

        // optional for future
        public int Version = 1;
        public AppearanceRecipe Appearance;
    }

    public static string ToJson()
    {
        return Serialize(CommittedAppearance);
    }

    private static string Serialize(AppearanceRecipe appearance)
    {
        var model = new SaveModel
        {
            HeadColorIndex = HeadColorIndex,
            BodyColorIndex = BodyColorIndex,
            ArmsColorIndex = ArmsColorIndex,
            LegsColorIndex = LegsColorIndex,
            EquippedHatId = EquippedHatId,
            OwnedHats = new List<int>(OwnedHats),
            Version = 2,
            Appearance = appearance?.Copy()
        };

        return JsonUtility.ToJson(model);
    }

    public static void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            ResetToDefault();
            return;
        }

        SaveModel model;
        try
        {
            model = JsonUtility.FromJson<SaveModel>(json);
        }
        catch
        {
            ResetToDefault();
            return;
        }

        if (model == null) { ResetToDefault(); return; }
        HeadColorIndex = Mathf.Max(0, model.HeadColorIndex);
        BodyColorIndex = Mathf.Max(0, model.BodyColorIndex);
        ArmsColorIndex = Mathf.Max(0, model.ArmsColorIndex);
        LegsColorIndex = Mathf.Max(0, model.LegsColorIndex);

        EquippedHatId = Mathf.Max(0, model.EquippedHatId);

        OwnedHats.Clear();
        if (model.OwnedHats != null)
        {
            foreach (var id in model.OwnedHats)
                OwnedHats.Add(Mathf.Max(0, id));
        }

        // always ensure hat 0 exists
        OwnedHats.Add(0);

        // ✅ KEY FIX:
        // Only force equipped hat to 0 if you're enforcing ownership (store mode)
        if (EnforceOwnership && !OwnedHats.Contains(EquippedHatId))
            EquippedHatId = 0;
    }
}
