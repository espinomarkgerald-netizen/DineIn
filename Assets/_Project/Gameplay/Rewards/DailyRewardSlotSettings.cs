using System;
using TMPro;
using UnityEngine;

public sealed class DailyRewardSlotSettings : ScriptableObject
{
    [Serializable] public sealed class Prize
    {
        public Sprite food;
        [Range(.7f, 1.4f)] public float symbolScale = 1.12f;
        public Vector2 symbolOffset;
        public string foodName;
        public int amount;
        public bool gold;
        [Min(0)] public int weight;
    }
    public string rulesVersion = "daily-slot-1";
    [Min(0)] public int noPrizeWeight = 60;
    public TMP_FontAsset font;
    public Sprite buttonSprite, panelSprite, particleSprite, cashIcon, goldCoinIcon;
    [Header("Currency and outcome feedback")]
    [Range(40, 80)] public float rewardIconSize = 62;
    [Range(4, 16)] public float iconTextGap = 8;
    [Range(.2f, 1.5f)] public float lossDuration = .55f;
    [Range(0, 8)] public float lossDipPixels = 4;
    public AudioClip currencyWinSound;
    [Header("Compact popup")]
    [Min(400)] public float popupWidth = 800, popupHeight = 630;
    [Range(.3f, .9f)] public float maximumSafeWidth = .68f, maximumSafeHeight = .78f;
    [Range(0, .7f)] public float backdropOpacity = .36f;
    [Header("Presentation")]
    [Min(.05f)] public float entranceSeconds = .24f, closeSeconds = .18f, settleSeconds = .2f, celebrationSeconds = 1.05f;
    [Range(0, 24)] public int particleCount = 16;
    [Range(.2f, 2f)] public float particleLifetime = .75f;
    [Range(4, 20)] public int reelCycles = 10;
    public Prize[] prizes;
    [Min(.3f)] public float spinSeconds = 2.2f;
    [Min(.02f)] public float reelStagger = .3f;
    [Range(0, 1)] public float audioLevel = .22f;
    public AudioClip reelTick, spinSound, spinLoop;
}

// Shared outcome model used by the game reward provider and the presentation preview.
public static class DailyRewardSlotOdds
{
    public static int Total(int[] weights, int noPrize)
    {
        int total = Math.Max(0, Math.Min(10000, noPrize));
        foreach (int weight in weights) total += Math.Max(0, Math.Min(10000, weight));
        return Math.Max(1, total);
    }
    public static int Select(int sample, int[] weights)
    {
        if (sample < 0) return -1;
        int end = 0;
        for (int i = 0; i < weights.Length; i++) { end += Math.Max(0, Math.Min(10000, weights[i])); if (sample < end) return i; }
        return -1;
    }
}
