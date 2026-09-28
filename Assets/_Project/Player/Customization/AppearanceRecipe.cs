using System;
using UnityEngine;

namespace DineIn.Appearance
{
    [Serializable]
    public sealed class AppearanceRecipe
    {
        public int version = 2;
        public string bodyId, skinId, faceId, hairId, hairColorId, outfitId, hatId;
        public AppearanceRecipe Copy() => (AppearanceRecipe)MemberwiseClone();
        public static AppearanceRecipe Decode(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 2048) return null;
            try
            {
                var r = JsonUtility.FromJson<AppearanceRecipe>(json);
                return r != null && r.version == 2 && !string.IsNullOrWhiteSpace(r.bodyId) ? r : null;
            }
            catch { return null; }
        }
        public bool SameAs(AppearanceRecipe other) => other != null && version == other.version &&
            bodyId == other.bodyId && skinId == other.skinId && faceId == other.faceId &&
            hairId == other.hairId && hairColorId == other.hairColorId && outfitId == other.outfitId && hatId == other.hatId;
    }
}
