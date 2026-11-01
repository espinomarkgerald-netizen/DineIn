using System;

// Optional reporting-only save state. Do not raise GameSaveData.saveSchemaVersion.
[Serializable]
public sealed class SoloRankingV1
{
    public string runId;
    public int dayOrdinal, lastCreditedDayOrdinal, completedDays;
    public const int Limit = 1000000;

    public static bool Valid(SoloRankingV1 value) => value != null &&
        Guid.TryParseExact(value.runId, "N", out _) && value.runId == value.runId.ToLowerInvariant() &&
        value.dayOrdinal >= 1 && value.dayOrdinal <= Limit && value.lastCreditedDayOrdinal >= 0 &&
        value.lastCreditedDayOrdinal <= Limit && value.completedDays >= 0 &&
        value.completedDays <= value.lastCreditedDayOrdinal;

    public static SoloRankingV1 NewRun() => new SoloRankingV1 { runId = Guid.NewGuid().ToString("N"), dayOrdinal = 1 };
    public static SoloRankingV1 Copy(SoloRankingV1 value) => !Valid(value) ? null : new SoloRankingV1 {
        runId = value.runId, dayOrdinal = value.dayOrdinal,
        lastCreditedDayOrdinal = value.lastCreditedDayOrdinal, completedDays = value.completedDays };

    public static bool Settle(SoloRankingV1 value)
    {
        if (!Valid(value) || value.dayOrdinal <= value.lastCreditedDayOrdinal || value.completedDays >= Limit) return false;
        value.completedDays++;
        value.lastCreditedDayOrdinal = value.dayOrdinal;
        return true;
    }
    public static bool Advance(SoloRankingV1 value)
    {
        if (!Valid(value) || value.dayOrdinal > value.lastCreditedDayOrdinal || value.dayOrdinal >= Limit) return false;
        value.dayOrdinal++;
        return true;
    }
    // Preserve settled credit, but restore which logical day is being replayed.
    // Call only for a checkpoint in the same account-owned career.
    public static SoloRankingV1 RestoreCheckpoint(SoloRankingV1 checkpoint, SoloRankingV1 current)
    {
        var restored = Copy(checkpoint);
        if (restored == null || !Valid(current) || restored.runId != current.runId) return restored;
        restored.completedDays = Math.Max(restored.completedDays, current.completedDays);
        restored.lastCreditedDayOrdinal = Math.Max(restored.lastCreditedDayOrdinal, current.lastCreditedDayOrdinal);
        return restored;
    }
}

[Serializable]
public sealed class SoloRunRecord
{
    public int schema = 1, completedDays;
    public string restaurant, sceneName, rulesVersion, runId, gameVersion;
    public const string SubmissionFunction = "DineInPublishSoloRunV1";
    public static bool Valid(SoloRunRecord value) => value != null && value.schema == 1 &&
        Guid.TryParseExact(value.runId, "N", out _) && value.runId == value.runId.ToLowerInvariant() &&
        value.completedDays >= 1 && value.completedDays <= SoloRankingV1.Limit &&
        !string.IsNullOrEmpty(value.gameVersion) && value.gameVersion.Length <= 64 &&
        Matches(value.restaurant, value.sceneName, value.rulesVersion);
    public static bool Matches(string restaurant, string scene, string rules) =>
        restaurant == "CasualDining" && scene == "Lobby1" && rules == "casual-solo-1" ||
        restaurant == "FastFood" && scene == "Lobby2" && rules == "fastfood-solo-1" ||
        restaurant == "FineDining" && scene == "Lobby3" && rules == "finedining-solo-1";
    public static SoloRunRecord FromProgress(SoloRankingV1 progress, string scene, string version)
    {
        if (!SoloRankingV1.Valid(progress) || progress.completedDays == 0) return null;
        var value = new SoloRunRecord { runId = progress.runId, completedDays = progress.completedDays,
            sceneName = scene, gameVersion = version };
        if (scene == "Lobby1") { value.restaurant = "CasualDining"; value.rulesVersion = "casual-solo-1"; }
        else if (scene == "Lobby2") { value.restaurant = "FastFood"; value.rulesVersion = "fastfood-solo-1"; }
        else if (scene == "Lobby3") { value.restaurant = "FineDining"; value.rulesVersion = "finedining-solo-1"; }
        return Valid(value) ? value : null;
    }
}
