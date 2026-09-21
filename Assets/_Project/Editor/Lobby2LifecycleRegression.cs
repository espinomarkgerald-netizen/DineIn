#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit, save-suppressed Play-mode regression. Never runs on import.
[InitializeOnLoad]
public static class Lobby2LifecycleRegression
{
    private const string Key = "DineIn.Lobby2LifecycleRegression";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int phase;
    private static double started;
    private static float gameStarted;
    private static LobbyHUDRoot originalHud;
    private static LobbyPauseMenu originalPause;
    private static int startingDay;
    public static string Result => SessionState.GetString(Key + ".result", "Not run");

    static Lobby2LifecycleRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { SetPhase(0); EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Key, false); }
        };
    }

    [MenuItem("Dine In/Fast Food/Run Lifecycle Regression")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode first.");
        Require(SceneManager.GetActiveScene().name == "Lobby2", "Open Lobby2 first.");
        Require(!SceneManager.GetActiveScene().isDirty, "Save your scene changes first.");
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".result", "RUNNING");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (GameSaveManager.Instance != null) GameSaveManager.Instance.SuppressWritesForTests = true;
            double elapsed = EditorApplication.timeSinceStartup - started;
            Require(elapsed < (phase == 4 ? 180 : 90), "Timed out in phase " + phase);
            if (elapsed < 3) return;
            var restaurant = UnityEngine.Object.FindFirstObjectByType<FastFoodRestaurant>();
            switch (phase)
            {
                case 0:
                    originalHud = LobbyHUDRoot.Instance;
                    originalPause = originalHud.GetComponent<LobbyPauseMenu>();
                    ValidateHud();
                    ValidateTrays();
                    if (SceneManagerUI.Instance == null) new GameObject("Regression SceneManagerUI").AddComponent<SceneManagerUI>();
                    RestockFlowCoordinator.Instance.EnterRestockRoom(RestockStorageType.Dry);
                    SetPhase(1);
                    break;
                case 1:
                    if (RestockFlowCoordinator.Instance.IsTransitioning || !RestockFlowCoordinator.Instance.IsRestockRoomOpen) return;
                    Require(SceneManager.GetActiveScene().name == "RestockScene", "Restock scene not active.");
                    RestockFlowCoordinator.Instance.ExitRestockRoom();
                    SetPhase(2);
                    break;
                case 2:
                    if (RestockFlowCoordinator.Instance.IsTransitioning || RestockFlowCoordinator.Instance.IsRestockRoomOpen) return;
                    ValidateHud();
                    startingDay = GameFlowManager.Instance.CurrentDay;
                    // Avoid an unrelated bankruptcy restart when testing the next-day action.
                    MoneyManager.Instance.SetMoney(50000, "Save-suppressed lifecycle regression");
                    Set(GameDayManager.Instance, "useManagementComputerForDayStart", false);
                    GameDayManager.Instance.StartShift();
                    Require(GameDayManager.Instance.ShiftRunning, "Shift did not start.");
                    // Keep a registered customer immobilized to exercise the real bounded closing path.
                    var spawner = GroupSpawner.Instance;
                    Set(spawner, "minGroupSize", 4);
                    Set(spawner, "maxGroupSize", 4);
                    spawner.SpawnGroup();
                    SetPhase(3);
                    break;
                case 3:
                    Require(restaurant.ActiveCustomerCount > 0, "No customer was available for the closing regression.");
                    ValidateFormationRoutes();
                    foreach (var group in UnityEngine.Object.FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None))
                        foreach (var member in group.members) if (member != null)
                        { member.Agent.enabled = false; member.enabled = false; }
                    Require(Mathf.Approximately(restaurant.ClosingServiceSeconds, 30), "Service grace must be 30 seconds.");
                    GameDayManager.Instance.EndShift();
                    Require(!restaurant.CanAdmitCustomer, "Admissions remained open at closing.");
                    SetPhase(4);
                    break;
                case 4:
                    Require(Time.time - gameStarted < 56, "Closing exceeded its 30-second service + 20-second exit budget.");
                    var action = (Button)Get(GameDayManager.Instance, "resultsActionButton");
                    if (action == null || !action.gameObject.activeInHierarchy) return;
                    Require(restaurant.ActiveCustomerCount == 0, "Closing left registered customers.");
                    Require(UnityEngine.Object.FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Length == 0, "Closing left scene customers.");
                    Require(action != null && action.interactable, "Next-day action is unavailable.");
                    SetPhase(5);
                    action.onClick.Invoke();
                    break;
                case 5:
                    Require(GameFlowManager.Instance.CurrentDay == startingDay + 1, "Day did not advance.");
                    ValidateHud();
                    Require(LobbyHUDRoot.Instance == originalHud && originalHud.GetComponent<LobbyPauseMenu>() == originalPause,
                        "Next day replaced the persistent HUD or settings controller.");
                    Finish("PASS: tray bounds/separation, carry anchors, formations of 1-4 at entry/exit, restock return, bounded closing cleanup, next-day HUD and pause settings.", false);
                    break;
            }
        }
        catch (Exception error) { Finish("FAIL phase " + phase + ": " + error, true); }
    }

    private static void ValidateHud()
    {
        Require(SceneManager.GetActiveScene().name == "Lobby2", "Lobby2 is not active.");
        var root = LobbyHUDRoot.Instance;
        Require(root != null, "Combined HUD is missing.");
        root.RefreshScenePresentation();
        Require(UnityEngine.Object.FindObjectsByType<LobbyPauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Expected one persistent pause controller.");
        var controls = root.GetComponentInChildren<LobbyHUDRedesign>(true);
        Require(controls != null && controls.GetComponent<Canvas>().enabled, "Utility HUD canvas is disabled.");
        foreach (string name in new[] { "CameraButton", "ComputerButton", "NewspaperButton", "TaskButton" })
        {
            var button = controls.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == name);
            Require(button != null && button.gameObject.activeInHierarchy, name + " is missing/hidden.");
        }
        var view = root.PauseMenuView;
        var content = view.Overlay.transform.Find("PauseWindow/SettingsContent");
        Require(content != null && content.GetComponentsInChildren<Slider>(true).Length == 2, "Audio settings disappeared.");
        Require(content.GetComponentsInChildren<Button>(true).Length == 3, "Accessibility settings disappeared.");
        root.GetComponent<LobbyPauseMenu>().OpenFromReadyPrompt();
        Require(view.Overlay.activeInHierarchy, "Pause overlay did not open.");
        view.ResumeButton.onClick.Invoke();
        Require(!root.GetComponent<LobbyPauseMenu>().IsOpen, "Resume did not close pause.");
    }

    private static void ValidateTrays()
    {
        var kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenManager>();
        var bounds = new List<Bounds>();
        foreach (var slot in kitchen.traySpawnPoints)
        {
            var tray = (FoodTray)typeof(KitchenManager).GetMethod("SpawnOutputTray", Private).Invoke(kitchen, new object[] { slot });
            try
            {
                var b = tray.GetComponentInChildren<Renderer>().bounds;
                Require(b.min.x > -1.53f && b.max.x < 4.27f && b.min.z > 28.38f && b.max.z < 31.18f,
                    "Tray overhangs its counter: " + b);
                Require(Mathf.Abs(b.min.y - slot.position.y) < .01f, "Tray bottom is not on the counter.");
                Require(bounds.All(other => !other.Intersects(b)), "Output trays overlap.");
                bounds.Add(b);
            }
            finally { UnityEngine.Object.Destroy(tray.gameObject); }
        }
        foreach (string color in new[] { "Green", "Pink", "Blue" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Customer/Old Alien Models/" + color + "Customer.prefab");
            var expected = color == "Green" ? new Vector3(0f, .85f, 1.65f)
                : color == "Pink" ? new Vector3(0f, .75f, 1.3f) : new Vector3(0f, .9f, 1.45f);
            Require(Vector3.Distance(asset.GetComponent<CustomerAgent>().TrayCarryAnchor.localPosition, expected) < .01f,
                color + " does not use its body-clear, reachable carry anchor.");
        }
        var pose = kitchen.foodTrayPrefab.GetComponent<FoodTrayCarryPose>();
        Require(pose.IsValid && Mathf.Abs(pose.LeftGrip.localPosition.x + .0097f) < .0001f &&
            Mathf.Abs(pose.RightGrip.localPosition.x + .0097f) < .0001f, "Carry grips must sit on the tray's rear corners.");
    }

    private static void ValidateFormationRoutes()
    {
        var group = UnityEngine.Object.FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.FastFood != null && x.members.Count == 4);
        Require(group != null, "Missing four-member formation fixture.");
        var originalMembers = group.members;
        var points = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("Left Entrance") || t.name.StartsWith("Outside Group ") || t.name == "Customer Exit").ToArray();
        Require(points.Length >= 3, "Entry/exit route fixtures are missing.");
        try
        {
            for (int count = 1; count <= 4; count++)
            {
                group.members = originalMembers.Take(count).ToList();
                foreach (var point in points)
                {
                    group.MoveToTakeoutPoint(point.position, point.forward, 1.1f, 1f);
                    var destinations = (Dictionary<CustomerAgent, Vector3>)Get(group, "takeoutMemberDestinations");
                    Require(destinations.Count == count, count + " members could not route to " + point.name);
                    var targets = destinations.Values.ToArray();
                    for (int i = 0; i < targets.Length; i++) for (int j = i + 1; j < targets.Length; j++)
                        Require((targets[i] - targets[j]).sqrMagnitude >= .63f, "Formation targets overlap.");
                }
            }
        }
        finally { group.members = originalMembers; }
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void SetPhase(int value) { phase = value; started = EditorApplication.timeSinceStartup; gameStarted = Time.time; }
    private static void Finish(string result, bool failed)
    {
        EditorApplication.update -= Tick;
        SessionState.SetString(Key + ".result", result);
        SessionState.SetBool(Key, false);
        if (failed) Debug.LogError(result); else Debug.Log(result);
        EditorApplication.ExitPlaymode();
    }
}
#endif
