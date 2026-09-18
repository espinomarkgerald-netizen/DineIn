#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

// Explicit external runner only. No import hook, automatic scene launch or asset mutation.
public static class HygieneRegressionCases
{
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("[Hygiene regression] " + reason);
    }
    public static void Run()
    {
        Require(!Application.isPlaying, "Run these model/asset checks outside Play Mode.");
        CheckCleaning(KitchenCleaningMode.Immediate, 5f, true, 1f);
        CheckCleaning(KitchenCleaningMode.WhileCooking, 20f, false, 1.1f);
        CheckCleaning(KitchenCleaningMode.Forced, 30f, true, 1f);
        Require(HygieneState.Threshold(0f) == HygieneThreshold.Clean, "Fresh day must start clean.");
        Require(HygieneState.Threshold(.59f) == HygieneThreshold.Clean, "Warning must not start early.");
        Require(HygieneState.Threshold(.6f) == HygieneThreshold.Dirty, "Warning threshold.");
        Require(HygieneState.Threshold(1f) == HygieneThreshold.Unsanitary, "Unsanitary threshold.");
        var original = new HygieneState { run = "test-run", day = 31, revision = 42, decisionId = 3,
            kitchenDirt = .9f, lobbyDirt = .5f, decisionArea = HygieneArea.Kitchen };
        original.BeginCleaning(KitchenCleaningMode.WhileCooking);
        original.Tick(7f);
        var restored = HygieneSnapshot.Decode(HygieneSnapshot.Encode(original));
        Require(restored != null, "Checkpoint must decode.");
        Require(restored.run == "test-run" && restored.day == 31 && restored.revision == 42, "Snapshot identity must survive rejoin.");
        Require(Mathf.Approximately(restored.cleaningRemaining, 13f), "Rejoin must preserve remaining work.");
        Require(Mathf.Approximately(restored.CookMultiplier, 1.1f), "Rejoin must preserve the cleaning option.");
        CheckFloorDirt();
        CheckSurfaceIsolation();
        CheckBoothShineAndColors();
        CheckLobbyRoute();
        CheckLobbyGrace();
        CheckAuthoredCoverage();
        Require(Resources.Load<Shader>("Hygiene/DirtOverlay") != null, "Dirt shader must be included as a Resources asset.");
        Require(Resources.Load<Shader>("Hygiene/Footprints") != null, "Footprint shader must be included.");
        Require(!UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>("Hygiene/DirtOverlay")), "Surface shader must compile.");
        Require(!UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>("Hygiene/Footprints")), "Footprint shader must compile.");
        var canvas = new GameObject("Hygiene UI regression", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var view = ManagerComplaintSystem.CreateHygieneDialogue(canvas.transform, canvas.GetComponent<CanvasScaler>());
            Require(view != null, "Staff dialogue must reuse valid complaint bindings.");
            Require(canvas.GetComponentsInChildren<ManagerComplaintSystem>(true).Length == 0, "Cloning presentation must not clone the complaint controller.");
            Require(view.buttons.Length == 3 && view.labels.Length == 3, "Kitchen needs three valid choices.");
            foreach (var button in view.buttons)
                Require((button.targetGraphic as Image)?.sprite == HygieneCleaningPresentation.Load().choiceButton
                    && button.transition == Selectable.Transition.ColorTint,
                    "Every staff hygiene choice must use the green asset without inherited red sprite states.");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
        Debug.Log("[Hygiene regression] PASS: model timing, pause, thresholds, rejoin and authored UI bindings. Human-control acceptance still required.");
    }
    private static void CheckLobbyGrace()
    {
        var state = new HygieneState { floorRouteActive = true };
        Require(!state.AddFloorDirt(Vector3.zero, .5f) && !state.AddSurfaceDirt(1, HygieneArea.Lobby, .5f),
            "Active mopping suppresses every lobby dirt path.");
        Require(state.AddSurfaceDirt(2, HygieneArea.Kitchen, .1f), "Kitchen use remains independent.");
        state.floorRouteActive = false;
        Require(state.AddFloorDirt(Vector3.zero, .0125f), "Cancelling a pass must release suppression.");
        state.lobbyClockHour = 10f; state.lobbyGraceRemainingHours = 2f;
        state.AdvanceLobbyClock(11f);
        Require(Mathf.Approximately(state.lobbyGraceRemainingHours, 1f)
            && !state.AddFloorDirt(Vector3.zero, .5f, spill: true), "Grace blocks lobby incidents for two game hours.");
        var restored = HygieneSnapshot.Decode(HygieneSnapshot.Encode(state));
        Require(restored != null && restored.LobbyDirtSuppressed, "Rejoin preserves cleanliness protection.");
        restored.AdvanceLobbyClock(11f);
        Require(Mathf.Approximately(restored.lobbyGraceRemainingHours, 1f), "Stopped game clocks do not consume grace.");
        restored.AdvanceLobbyClock(12f);
        Require(!restored.LobbyDirtSuppressed && restored.AddSurfaceDirt(1, HygieneArea.Lobby, .035f),
            "Normal dirt production resumes after grace.");
        Require(HygieneFootprintRenderer.VisibleOpacity(.0125f * 4f) == 0f
            && HygieneFootprintRenderer.VisibleOpacity(.2f) > 0f,
            "Early traffic is invisible; sustained traffic gradually appears.");
        Require(HygieneSnapshot.Decode(HygieneSnapshot.Encode(state).Replace("\"version\":5", "\"version\":4")) == null,
            "Older hygiene snapshots must be rejected.");
        Require(!new HygieneState().LobbyDirtSuppressed, "Fresh shifts do not inherit yesterday's grace.");
    }
    private static void CheckFloorDirt()
    {
        var state = new HygieneState();
        state.AddFloorDirt(Vector3.zero, .1f);
        state.AddFloorDirt(new Vector3(.2f, 0f, .2f), .1f);
        Require(state.floorMarks.Count == 1 && Mathf.Approximately(state.floorMarks[0].dirt, .2f),
            "Repeated use must darken the same local stain.");
        state.AddFloorDirt(new Vector3(8f, 0f, 0f), .4f);
        Require(state.CleanFloor(Vector3.zero) && state.floorMarks.Count == 1,
            "Cleaning one area must preserve distant dirt.");
        Require(!state.CleanFloor(Vector3.zero), "Duplicate cleanup must have no effect.");
        for (int i = 0; i < HygieneState.MaxFloorMarks + 32; i++) state.AddFloorDirt(new Vector3(i * 4f, 0f, 0f), .2f, 90f);
        Require(state.floorMarks.Count == HygieneState.MaxFloorMarks, "Floor state must stay bounded.");
        string payload = HygieneSnapshot.Encode(state);
        Require(payload.Length < 20000, "Full colored footprint checkpoint must remain compact.");
        var snapshot = HygieneSnapshot.Decode(payload);
        Require(snapshot != null && snapshot.floorMarks.Count == state.floorMarks.Count &&
            snapshot.floorMarks[0].position == state.floorMarks[0].position &&
            Mathf.Abs(snapshot.floorMarks[0].dirt - state.floorMarks[0].dirt) <= 1f/255f &&
            Mathf.Abs(Mathf.DeltaAngle(snapshot.floorMarks[0].yaw, state.floorMarks[0].yaw)) < .01f,
            "Guest rejoin must preserve position, darkness and direction.");
        Require(HygieneSnapshot.Decode("{}") == null, "Missing state must not replace the guest view.");
        state.kitchenDirt = .9f;
        state.BeginCleaning(KitchenCleaningMode.Immediate);
        state.Tick(5f);
        Require(state.floorMarks.Count == HygieneState.MaxFloorMarks, "Kitchen cleaning must not erase lobby dirt.");
        Require(new HygieneState().floorMarks.Count == 0, "A fresh shift must start without floor stains.");
    }
    private static void CheckSurfaceIsolation()
    {
        var state = new HygieneState();
        state.AddSurfaceDirt(1, HygieneArea.Kitchen, .8f);
        state.AddSurfaceDirt(2, HygieneArea.Kitchen, .1f);
        state.AddSurfaceDirt(3, HygieneArea.Lobby, .7f);
        state.AddSurfaceDirt(4, HygieneArea.Lobby, .2f);
        state.AddFloorDirt(Vector3.zero, .4f, 120f);
        Require(state.SurfaceDirt(5) == 0f, "Unused equipment must stay clean.");
        state.CleanSurface(3);
        Require(state.SurfaceDirt(4) == .2f && state.SurfaceDirt(1) == .8f && state.floorMarks.Count == 1,
            "Wiping a booth must preserve another table, equipment and footprints.");
        state.BeginCleaning(KitchenCleaningMode.Immediate); state.Tick(5f);
        Require(state.SurfaceDirt(1) == 0f && state.SurfaceDirt(2) == 0f && state.SurfaceDirt(4) == .2f,
            "Kitchen cleaning must clear all equipment, preserving lobby state.");
        var restored = HygieneSnapshot.Decode(HygieneSnapshot.Encode(state));
        Require(restored != null && restored.SurfaceDirt(4) == .2f, "Surface identities must survive rejoin.");
        var root = new GameObject("Hygiene ID regression");
        try
        {
            var station = new GameObject("Station"); station.transform.SetParent(root.transform);
            int before = HygieneSurfaceRegistry.StableId(station.transform);
            var actor = new GameObject("Unrelated player"); actor.transform.SetParent(root.transform); actor.transform.SetAsFirstSibling();
            Require(before == HygieneSurfaceRegistry.StableId(station.transform), "Spawned actors must not change equipment identities.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void CheckBoothShineAndColors()
    {
        var state = new HygieneState();
        Require(HygieneState.BoothTier(0f) == BoothCleanliness.Clean, "Fresh tables have no sparkle.");
        Require(HygieneState.BoothTier(.25f) == BoothCleanliness.SlightlyDirty
            && HygieneState.BoothTier(.55f) == BoothCleanliness.Dirty
            && HygieneState.BoothTier(.8f) == BoothCleanliness.SuperDirty, "Booth tier boundaries.");
        state.AddSurfaceDirt(8, HygieneArea.Lobby, .6f); state.CleanSurface(8, true);
        Require(state.SurfaceShine(8) == 90f && state.SurfaceDirt(8) == 0f, "Successful cleaning grants shine.");
        state.Tick(30f); state.CleanSurface(8, true);
        Require(state.SurfaceShine(8) == 60f, "Repeated completion must not extend shine.");
        state.decisionOpen = true; state.Tick(100f); state.decisionOpen = false;
        Require(state.SurfaceShine(8) == 60f, "Decisions freeze shine expiry.");
        var copy = HygieneSnapshot.Decode(HygieneSnapshot.Encode(state));
        Require(copy != null && copy.SurfaceShine(8) == 60f, "Rejoin preserves shine time.");
        state.AddSurfaceDirt(8, HygieneArea.Lobby, .01f);
        Require(state.SurfaceShine(8) == 0f, "New use ends shine immediately.");
        state.CleanSurface(8, true); state.Tick(91f);
        Require(state.SurfaceShine(8) == 0f, "Unused shine ends after 90 seconds.");
        for (byte color = 0; color < 4; color++) state.AddFloorDirt(Vector3.zero, .2f, color: color);
        Require(state.floorMarks.Count == 4, "Different customer colors cannot merge into one mark.");
        foreach (var mark in state.floorMarks) state.floorRoute.Add(mark.id);
        state.floorRouteTotal = state.floorRoute.Count;
        state.AddFloorDirt(Vector3.zero, .2f, color: 1);
        Require(state.floorMarks.Count == 5 && !state.floorRoute.Contains(state.floorMarks[4].id), "New traffic survives a captured cleaning route.");
        copy = HygieneSnapshot.Decode(HygieneSnapshot.Encode(state));
        Require(copy != null && copy.floorRoute.Count == 4 && copy.floorMarks[3].color == 3
            && copy.floorMarks[3].id == state.floorMarks[3].id, "Route and color identities survive rejoin.");
        Require(HygieneSnapshot.Decode("null") == null, "Null checkpoint is rejected.");
        Require(HygieneSnapshot.Decode(HygieneSnapshot.Encode(state).Replace("\"version\":4", "\"version\":3")) == null,
            "Old wire format is rejected.");
    }
    private static void CheckLobbyRoute()
    {
        var state = new HygieneState();
        state.AddFloorDirt(new Vector3(8f, 0f, 0f), .4f, color: 3);
        state.AddFloorDirt(Vector3.zero, .3f, color: 1);
        state.AddFloorDirt(new Vector3(3f, 0f, 0f), .5f, spill: true);
        HygieneCleaningRoute.Capture(state, Vector3.zero);
        Require(state.floorRouteTotal == 3 && state.floorRoute[0] == state.floorMarks[1].id
            && state.floorRoute[1] == state.floorMarks[2].id, "Route covers every color and spill in nearest-first order.");
        int first = state.floorRoute[0];
        state.AddFloorDirt(Vector3.zero, .2f, color: 1);
        Require(state.floorMarks.Count == 4 && !state.floorRoute.Contains(state.floorMarks[3].id),
            "Traffic during a cleaning pass stays outside its captured targets.");
        Require(HygieneCleaningRoute.WithinSweep(new Vector3(1f, 0f, .2f), Vector3.zero, new Vector3(2f, 0f, 0f))
            && !HygieneCleaningRoute.WithinSweep(new Vector3(1f, 0f, 2f), Vector3.zero, new Vector3(2f, 0f, 0f)),
            "A moving mop clears its traveled segment, not distant dirt.");
        state.floorRouteActive = true; state.floorWorkSeconds = 4f;
        var restored = HygieneSnapshot.Decode(HygieneSnapshot.Encode(state));
        Require(restored != null && restored.floorRouteActive && restored.floorWorkSeconds == 4f
            && restored.floorRoute[0] == first, "Rejoin preserves the active route and shared work budget.");
        state.floorWorkSeconds = 11f;
        Require(HygieneSnapshot.Decode(HygieneSnapshot.Encode(state)) == null, "Out-of-range work time must be rejected.");
        state = new HygieneState();
        for (int i = 0; i < HygieneState.MaxFloorMarks; i++) state.AddFloorDirt(new Vector3(i, 0f, 0f), .2f);
        HygieneCleaningRoute.Capture(state, Vector3.zero);
        Require(!state.AddFloorDirt(new Vector3(-10f, 0f, 0f), .5f) && state.floorMarks.Count == state.floorRouteTotal,
            "A full visual budget cannot evict active cleaning targets.");
        var art = HygieneCleaningPresentation.Load();
        Require(art != null && art.choiceButton != null && art.mop != null && art.bucket != null,
            "Green/Double button, mop and bucket references must be available in builds.");
        Require(art.choiceButton.border.sqrMagnitude > 0f, "The existing green button needs nine-slice borders.");
    }
    private static void CheckAuthoredCoverage()
    {
        string[] scenes = { "RoleBased/Lobby1", "RoleBased/Lobby1 Multiplayer", "RoleBased/Lobby2",
            "TutorialScenes/Lobby1Tutorial" };
        foreach (string scene in scenes)
        {
            string text = System.IO.File.ReadAllText("Assets/_Project/Scenes/" + scene + ".unity");
            int count = System.Text.RegularExpressions.Regex.Matches(text, "guid: ef6c276dd67f41f5b2236414496f11bc").Count;
            Require(count >= 23, "Missing explicit equipment bindings in " + scene);
        }
    }
    private static void CheckCleaning(KitchenCleaningMode mode, float duration, bool blocks, float multiplier)
    {
        var state = new HygieneState { kitchenDirt = 1f, kitchenWarned = true, forcedPending = true, deferredKitchen = true };
        state.BeginCleaning(mode);
        Require(state.KitchenPaused == blocks && Mathf.Approximately(state.CookMultiplier, multiplier), "Wrong operational effect for " + mode);
        Require(!state.forcedPending && !state.deferredKitchen, "Starting cleaning must clear the previous deferral.");
        state.decisionOpen = true;
        Require(!state.Tick(60f) && state.cleaningRemaining == duration, "Shared popup must freeze cleaning time.");
        state.decisionOpen = false;
        Require(!state.Tick(duration - .25f) && state.Cleaning, "Cleaning must not finish early.");
        Require(state.Tick(.5f) && !state.Cleaning && state.cleaningRemaining == 0f, "Large final frame must complete once.");
        Require(state.kitchenDirt == 0f && !state.kitchenWarned, "Completion must clean and rearm the warning.");
        Require(!state.Tick(100f), "A repeated completion must have no effect.");
    }
}
#endif
