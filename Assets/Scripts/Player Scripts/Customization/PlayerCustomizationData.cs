using UnityEngine;
using System;
using System.Collections.Generic;

public static class PlayerCustomizationData
{
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
    }

    public static string ToJson()
    {
        var model = new SaveModel
        {
            HeadColorIndex = HeadColorIndex,
            BodyColorIndex = BodyColorIndex,
            ArmsColorIndex = ArmsColorIndex,
            LegsColorIndex = LegsColorIndex,
            EquippedHatId = EquippedHatId,
            OwnedHats = new List<int>(OwnedHats),
            Version = 1
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
