#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class TutorialPassRuntimeProbe
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string ReportPath = Path.GetFullPath("Temp/TutorialPassRuntimeProbe.log");
    private static readonly string CommandPath = Path.GetFullPath("Temp/TutorialPassRuntimeProbe.command");
    private static double nextSnapshotAt;
    private static string lastSnapshot;
    private static Action pendingAction;
    private static double pendingAt;

    static TutorialPassRuntimeProbe()
    {
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/Dine In/Tutorial/Verify/Prepare Menu Availability %#F9")]
    private static void PrepareMenuAvailability()
    {
        if (!EditorApplication.isPlaying) return;
        ManagementComputerController controller = UnityEngine.Object.FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);
        ManagerPlayer manager = UnityEngine.Object.FindFirstObjectByType<ManagerPlayer>(FindObjectsInactive.Include);
        ManagementComputerStation station = UnityEngine.Object.FindFirstObjectByType<ManagementComputerStation>(FindObjectsInactive.Include);
        controller?.OpenComputer(manager, station);
        controller?.OpenApp((int)ManagementComputerApp.Menu);
        Schedule(() => JumpTo("management_menu_availability"), 0.5d);
    }

    [MenuItem("Tools/Dine In/Tutorial/Verify/Prepare Restock Route %#F10")]
    private static void PrepareRestockRoute()
    {
        if (!EditorApplication.isPlaying) return;
        RestockOrderManager orders = RestockOrderManager.EnsureInstance();
        orders.ClearAll();
        ItemData dry = null;
        ItemData frozen = null;
        IReadOnlyList<ItemData> items = InventoryManager.Instance != null ? InventoryManager.Instance.Items : null;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                if (item == null) continue;
                if (dry == null && item.requiredStorage == RestockStorageType.Dry) dry = item;
                if (frozen == null && item.requiredStorage == RestockStorageType.Frozen) frozen = item;
            }
        }

        List<RestockCartLine> cart = new List<RestockCartLine>();
        if (dry != null) cart.Add(new RestockCartLine { item = dry, quantity = 1 });
        if (frozen != null) cart.Add(new RestockCartLine { item = frozen, quantity = 1 });
        string orderId = orders.CreateOrder("tutorial-runtime-verification", cart, 20);
        ManagementComputerController controller = UnityEngine.Object.FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);
        ManagerPlayer manager = UnityEngine.Object.FindFirstObjectByType<ManagerPlayer>(FindObjectsInactive.Include);
        ManagementComputerStation station = UnityEngine.Object.FindFirstObjectByType<ManagementComputerStation>(FindObjectsInactive.Include);
        controller?.OpenComputer(manager, station);
        Write("PREP RESTOCK order=" + orderId + " dry=" + (dry != null ? dry.displayName : "<missing>") +
              " frozen=" + (frozen != null ? frozen.displayName : "<missing>"));
        Schedule(() => JumpTo("physical_restocking_intro"), 0.5d);
    }

    [MenuItem("Tools/Dine In/Tutorial/Verify/Dump Runtime Snapshot %#F11")]
    private static void DumpSnapshot() => Write("DUMP " + Snapshot());

    private static void JumpTo(string id)
    {
        TutorialSystem tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
        if (tutorial == null) { Write("JUMP FAILED no TutorialSystem"); return; }
        FieldInfo stepsField = typeof(TutorialSystem).GetField("steps", PrivateInstance);
        FieldInfo indexField = typeof(TutorialSystem).GetField("currentStepIndex", PrivateInstance);
        MethodInfo show = typeof(TutorialSystem).GetMethod("ShowCurrentStep", PrivateInstance);
        TutorialSystem.TutorialStep[] steps = stepsField?.GetValue(tutorial) as TutorialSystem.TutorialStep[];
        int index = Array.FindIndex(steps ?? Array.Empty<TutorialSystem.TutorialStep>(), s => s != null && s.Id == id);
        if (index < 0 || indexField == null || show == null) { Write("JUMP FAILED id=" + id); return; }
        indexField.SetValue(tutorial, index);
        show.Invoke(tutorial, null);
        Write("JUMP id=" + id + " index=" + index);
    }

    private static void Schedule(Action action, double seconds)
    {
        pendingAction = action;
        pendingAt = EditorApplication.timeSinceStartup + seconds;
    }

    private static void Update()
    {
        if (File.Exists(CommandPath))
        {
            string command = File.ReadAllText(CommandPath).Trim().ToUpperInvariant();
            File.Delete(CommandPath);
            if (command == "RESTOCK") PrepareRestockRoute();
            else if (command == "MENU") PrepareMenuAvailability();
            else if (command == "SERVICE")
            {
                TutorialSystem serviceTutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
                serviceTutorial?.SetSpawnPermissions(true, true);
                GameDayManager.Instance?.StartShift();
                JumpTo("staff_roles_context");
            }
            else if (command == "NEXT")
                UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include)?.AdvanceManualStep();
            else if (command == "EXIT")
            {
                foreach (UnityEngine.UI.Button button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (button.name == "ExitButton") { button.onClick.Invoke(); break; }
            }
            Write("COMMAND " + command);
        }

        if (pendingAction != null && EditorApplication.timeSinceStartup >= pendingAt)
        {
            Action action = pendingAction;
            pendingAction = null;
            action();
        }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextSnapshotAt) return;
        nextSnapshotAt = EditorApplication.timeSinceStartup + 0.5d;
        string snapshot = Snapshot();
        if (snapshot != lastSnapshot)
        {
            lastSnapshot = snapshot;
            Write("STATE " + snapshot);
        }
    }

    private static string Snapshot()
    {
        TutorialSystem tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
        RestockOrderManager orders = RestockOrderManager.Instance;
        return "scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
               " step=" + (tutorial != null ? tutorial.CurrentStepIndex.ToString() : "-") +
               "/" + (tutorial?.CurrentStep != null ? tutorial.CurrentStep.Id : "-") +
               " phase=" + (tutorial != null ? tutorial.CurrentPhase.ToString() : "-") +
               " next=" + (tutorial != null && tutorial.IsWaitingForNext) +
               " action=" + (tutorial != null && tutorial.IsWaitingForGameplayAction) +
               " day=" + (GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay.ToString() : "-") +
               " money=" + (MoneyManager.Instance != null ? MoneyManager.Instance.Money.ToString() : "-") +
               " approval=" + (AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval.ToString() : "-") +
               " delivered=" + (orders != null ? orders.DeliveredContainerCount.ToString() : "-") +
               " hotbar=" + (orders != null ? orders.HotbarContainerCount.ToString() : "-") +
               " save=" + SaveHash();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            File.WriteAllText(ReportPath, "BEGIN " + DateTime.Now.ToString("O") + Environment.NewLine);
            Write("SAVE BEFORE " + SaveHash());
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            nextSnapshotAt = 0d;
            lastSnapshot = null;
            Write("ENTERED PLAY");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
            Write("SAVE EXITING " + SaveHash());
        else if (state == PlayModeStateChange.EnteredEditMode)
            Write("SAVE AFTER " + SaveHash());
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (!EditorApplication.isPlaying) return;
        if (condition.Contains("That task cannot be reached from here.", StringComparison.Ordinal) ||
            condition.StartsWith("[Tutorial", StringComparison.Ordinal) ||
            type == LogType.Exception)
            Write("LOG " + type + " " + condition.Replace('\n', ' '));
    }

    private static string SaveHash()
    {
        try
        {
            GameSaveManager save = GameSaveManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<GameSaveManager>(FindObjectsInactive.Include);
            string fileName = save != null
                ? typeof(GameSaveManager).GetField("saveFileName", PrivateInstance)?.GetValue(save) as string
                : "dinein_save.json";
            string path = Path.Combine(Application.persistentDataPath, string.IsNullOrWhiteSpace(fileName) ? "dinein_save.json" : fileName);
            if (!File.Exists(path)) return "<missing>@" + path;
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty) + "@" + path;
        }
        catch (Exception exception)
        {
            return "<hash-error:" + exception.GetType().Name + ">";
        }
    }

    private static void Write(string line)
    {
        try { File.AppendAllText(ReportPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine); }
        catch { }
    }
}
#endif
