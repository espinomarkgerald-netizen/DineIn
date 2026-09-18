using System;
using UnityEngine;

public enum HygieneArea { Kitchen, Lobby }
public enum HygieneThreshold { Clean, Dirty, Unsanitary }
public enum HygieneDecision { CleanNow, CleanWhileCooking, ContinueUntilBlocked, HelpClean, KeepServing, CleanLobby }
public enum BoothCleanliness { SuperClean, Clean, SlightlyDirty, Dirty, SuperDirty }
public enum KitchenCleaningMode { None, Immediate, WhileCooking, Forced }
public enum HygieneSurfaceKind { Equipment, Counter, Dining, Floor }

[Serializable]
public sealed class HygieneFloorMark
{
    public Vector3 position;
    public float dirt;
    public float yaw;
    public bool spill;
    public byte color;
    public int id;
}

[Serializable]
public sealed class HygieneSurfaceDirt
{
    public int id;
    public HygieneArea area;
    public float dirt;
    public float shineRemaining;
}

// Temporary service state. Deliberately excluded from GameSaveData and run receipts.
[Serializable]
public sealed class HygieneState
{
    public string run;
    public int day, revision, decisionId;
    public float kitchenDirt, lobbyDirt;
    public bool decisionOpen, kitchenWarned, lobbyWarned, deferredKitchen, forcedPending;
    public HygieneArea decisionArea;
    public KitchenCleaningMode cleaningMode;
    public float cleaningRemaining, cleaningDuration;
    public double sentAt;
    public float clockScale = 1f;
    public bool lobbyAssistance;
    public bool lobbyCleaningRequested;
    public int lobbyCleaningBudget;
    public int nextMarkId, floorRouteTotal, floorRouteDone, feedbackHour = -1;
    public string queuedBooth;
    public float floorWorkProgress;
    public bool floorRouteActive;
    public float floorWorkSeconds;
    public float lobbyGraceRemainingHours;
    public float lobbyClockHour = -1f;
    public bool LobbyDirtSuppressed => floorRouteActive || lobbyGraceRemainingHours > 0f;
    public bool AdvanceLobbyClock(float hour)
    {
        float elapsed = lobbyClockHour < 0f ? 0f : Mathf.Max(0f, hour - lobbyClockHour);
        lobbyClockHour = hour;
        if (decisionOpen || lobbyGraceRemainingHours <= 0f || elapsed <= 0f) return false;
        lobbyGraceRemainingHours = Mathf.Max(0f, lobbyGraceRemainingHours - elapsed);
        return true;
    }
    public int floorRouteSkipped;
    public int feedbackCustomer;
    public bool feedbackPositive;
    public double feedbackExpires;
    public System.Collections.Generic.List<int> floorRoute = new();
    [NonSerialized] private System.Collections.Generic.HashSet<int> capturedMarks;
    public void ResetRouteLookup() => capturedMarks = null;
    private bool IsCapturedMark(int id)
    {
        if (floorRoute.Count == 0) { capturedMarks = null; return false; }
        capturedMarks ??= new System.Collections.Generic.HashSet<int>(floorRoute);
        return capturedMarks.Contains(id);
    }
    // Bounded, host-owned marks: guests and rejoining players see the same dirt.
    public const int MaxFloorMarks = 512;
    public const int MaxSurfaces = 256;
    [NonSerialized] public System.Collections.Generic.List<HygieneFloorMark> floorMarks = new();
    public System.Collections.Generic.List<HygieneSurfaceDirt> surfaces = new();
    public float SurfaceDirt(int id)
    {
        foreach (var surface in surfaces) if (surface.id == id) return surface.dirt;
        return 0f;
    }
    public float SurfaceShine(int id)
    {
        foreach (var surface in surfaces) if (surface.id == id) return surface.shineRemaining;
        return 0f;
    }
    public static BoothCleanliness BoothTier(float dirt, float shine = 0f) => dirt >= .8f ? BoothCleanliness.SuperDirty
        : dirt >= .55f ? BoothCleanliness.Dirty : dirt >= .25f ? BoothCleanliness.SlightlyDirty
        : shine > 0f ? BoothCleanliness.SuperClean : BoothCleanliness.Clean;
    public bool AddSurfaceDirt(int id, HygieneArea area, float amount)
    {
        if (id == 0 || amount <= 0f || area == HygieneArea.Lobby && LobbyDirtSuppressed) return false;
        foreach (var surface in surfaces)
            if (surface.id == id) { surface.shineRemaining = 0f; surface.dirt = Mathf.Clamp01(surface.dirt + amount); Recalculate(); return true; }
        if (surfaces.Count >= MaxSurfaces) return false;
        surfaces.Add(new HygieneSurfaceDirt { id = id, area = area, dirt = Mathf.Clamp01(amount) });
        Recalculate();
        return true;
    }
    public void CleanSurface(int id, bool sparkle = false)
    {
        foreach (var surface in surfaces)
            if (surface.id == id && sparkle) { if (surface.dirt > 0f) { surface.dirt = 0f; surface.shineRemaining = HygieneSettings.Current.boothShineSeconds; } Recalculate(); return; }
        surfaces.RemoveAll(surface => surface.id == id);
        Recalculate();
    }
    public void Recalculate()
    {
        // A neglected busy station must still trigger cleaning even if other stations are unused.
        float kitchen = 0f, lobby = 0f;
        foreach (var surface in surfaces)
            if (surface.area == HygieneArea.Kitchen) kitchen = Mathf.Max(kitchen, surface.dirt);
            else lobby = Mathf.Max(lobby, surface.dirt);
        foreach (var mark in floorMarks) lobby = Mathf.Max(lobby, mark.dirt);
        kitchenDirt = kitchen; lobbyDirt = lobby;
    }
    public bool AddFloorDirt(Vector3 position, float amount, float yaw = 0f, bool spill = false, byte color = 0)
    {
        if (amount <= 0f || LobbyDirtSuppressed) return false;
        HygieneFloorMark closest = null;
        float distance = spill ? .25f : .09f;
        foreach (var mark in floorMarks)
        {
            // A route owns only the marks captured when requested. New traffic is a new mark.
            if (mark.spill != spill || mark.color != color || IsCapturedMark(mark.id)) continue;
            float d = (mark.position - position).sqrMagnitude;
            if (d < distance) { closest = mark; distance = d; }
        }
        if (closest != null) { closest.dirt = Mathf.Clamp01(closest.dirt + amount); Recalculate(); return true; }
        if (floorMarks.Count >= MaxFloorMarks)
        {
            // Preserve the busiest routes; recycle the faintest mark so a new
            // walking route can still appear after the visual budget fills.
            int faintest = -1;
            for (int i = 0; i < floorMarks.Count; i++)
                if (!IsCapturedMark(floorMarks[i].id) && (faintest < 0 || floorMarks[i].dirt < floorMarks[faintest].dirt)) faintest = i;
            // Do not evict an active cleanup target to make room for new traffic.
            if (faintest < 0) return false;
            floorMarks.RemoveAt(faintest);
        }
        floorMarks.Add(new HygieneFloorMark { id = ++nextMarkId, position = position, dirt = Mathf.Clamp01(amount), yaw = yaw, spill = spill, color = color });
        Recalculate();
        return true;
    }
    public bool CleanFloor(Vector3 position, float radius = 1.6f)
    {
        bool changed = floorMarks.RemoveAll(mark => (mark.position - position).sqrMagnitude <= radius * radius) > 0;
        if (changed) Recalculate();
        return changed;
    }
    public const float WarningLevel = .6f;
    public static HygieneThreshold Threshold(float dirt) => dirt >= 1f ? HygieneThreshold.Unsanitary
        : dirt >= WarningLevel ? HygieneThreshold.Dirty : HygieneThreshold.Clean;
    public bool Cleaning => cleaningMode != KitchenCleaningMode.None;
    public bool KitchenPaused => cleaningMode == KitchenCleaningMode.Immediate || cleaningMode == KitchenCleaningMode.Forced;
    public float CookMultiplier => cleaningMode == KitchenCleaningMode.WhileCooking ? HygieneSettings.Current.cleaningCookMultiplier : 1f;
    public float Progress => cleaningDuration > 0f ? Mathf.Clamp01(1f - cleaningRemaining / cleaningDuration) : 0f;

    public void BeginCleaning(KitchenCleaningMode mode)
    {
        if (mode == KitchenCleaningMode.None) throw new ArgumentException("A cleaning mode is required.", nameof(mode));
        decisionOpen = false;
        forcedPending = deferredKitchen = false;
        cleaningMode = mode;
        var settings = HygieneSettings.Current;
        cleaningDuration = mode == KitchenCleaningMode.Immediate ? settings.immediateCleanSeconds
            : mode == KitchenCleaningMode.WhileCooking ? settings.whileCookingCleanSeconds : settings.forcedCleanSeconds;
        cleaningRemaining = cleaningDuration;
    }

    public bool Tick(float delta)
    {
        if (decisionOpen || delta <= 0f) return false;
        bool shineChanged = false;
        foreach (var surface in surfaces)
            if (surface.shineRemaining > 0f) { surface.shineRemaining = Mathf.Max(0f, surface.shineRemaining - delta); shineChanged = true; }
        if (!Cleaning) return shineChanged;
        cleaningRemaining = Mathf.Max(0f, cleaningRemaining - delta);
        if (cleaningRemaining > 0f) return shineChanged;
        cleaningMode = KitchenCleaningMode.None;
        surfaces.RemoveAll(surface => surface.area == HygieneArea.Kitchen);
        kitchenDirt = 0f;
        kitchenWarned = false;
        return true;
    }
}
