#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit external runner, never invoked by import. Uses an empty Play Mode
// scene and inactive fixtures, without accounts, a Photon room or a career save.
// These regressions supplement, not replace, the two/four-player control checks.
[InitializeOnLoad]
public static class MultiplayerRepairRegressionTest
{
    private const string Running = "DineIn.MultiplayerRepair.Running", Batch = "DineIn.MultiplayerRepair.Batch";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<GameObject> roots = new();
    private static readonly List<string> results = new();

    static MultiplayerRepairRegressionTest()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(Running, false) && EditorApplication.isPlaying) EditorApplication.delayCall += Check;
    }

    [MenuItem("Tools/Dine In/Run Multiplayer Repair Regressions (External)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start the runner outside Play Mode.");
        if (!SessionState.GetBool(Batch, false) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    public static void RunBatch() { SessionState.SetBool(Batch, true); Run(); }
    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Check;
    }
    private static void Check()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Running, false);
        int exitCode = 0;
        results.Clear();
        var originalBillManager = BillManager.Instance;
        try
        {
            RunCase("claim snapshots reject old/duplicate revisions and malformed maps", ClaimRevisions);
            RunCase("old bot generation cannot release a replacement reservation", BotGeneration);
            RunCase("one bill per order; transfer, detach and slot reservation remain consistent", BillRegistry);
            RunCase("action replies match only the original actor, order, stage and lease", ActionContexts);
            RunCase("money projection transfers and returns the same object without completing claims", MultiplayerPaymentParityRegressionCases.Run);
            RunCase("an authored inactive cashier initializes and opens once without losing payment input", MultiplayerCashierActivationRegressionCases.Run);
            RunCase("payment snapshots cannot reopen a delivered or paid bill", PaymentOrdering);
            RunCase("focus reuses CanvasGroup and composes with visibility", BubbleFocus);
            RunCase("movement ignores stale samples, buffers interpolation and bounds memory", Movement);
            RunCase("room protocol changes leave result rules compatible", ResultRules);
        }
        catch (Exception error) { exitCode = 1; results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            SetStatic(typeof(BillManager), "<Instance>k__BackingField", originalBillManager);
            foreach (var root in roots) if (root != null) Object.Destroy(root);
            roots.Clear();
            CallStatic(typeof(RestaurantTaskClaim), "ResetRuntimeState");
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "MultiplayerRepairRegression.txt"), results);
            bool batch = SessionState.GetBool(Batch, false);
            SessionState.SetBool(Batch, false);
            if (batch) EditorApplication.Exit(exitCode); else EditorApplication.ExitPlaymode();
        }
    }
    private static void RunCase(string name, Action body) { body(); results.Add("PASS: " + name); Debug.Log("[MultiplayerRepair] PASS: " + name); }
    private static GameObject Root(string name, bool ui = false)
    {
        var root = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        root.SetActive(false); roots.Add(root); return root;
    }
    private static void ClaimRevisions()
    {
        var claims = Root("claims").AddComponent<MultiplayerTaskClaims>();
        Call(claims, "Reconcile", 4L, new[] { "Customer:7:Bill" }, new[] { 2 }, new[] { 4L });
        Assert(claims.GetLease("Customer:7:Bill") == 4, "Initial claim token missing.");
        Call(claims, "Reconcile", 3L, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<long>());
        Assert(claims.GetLease("Customer:7:Bill") == 4, "Old snapshot released a live claim.");
        Call(claims, "Reconcile", 5L, new[] { "Customer:7:Bill", "Customer:7:Bill" }, new[] { 2, 3 }, new[] { 5L, 5L });
        Assert(claims.GetLease("Customer:7:Bill") == 4, "Duplicate keys partially applied.");
        Call(claims, "Reconcile", 6L, new[] { "Customer:7:Bill" }, new[] { 3 }, new[] { 6L });
        Call(claims, "Reconcile", 6L, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<long>());
        Assert(claims.GetLease("Customer:7:Bill") == 6, "Equal revision changed ownership.");
        var owners = Get<Dictionary<string, int>>(claims, "claims");
        Assert(owners.Count == 1 && owners["Customer:7:Bill"] == 3, "Atomic replacement did not leave one owner.");
        Call(claims, "Reconcile", 7L, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<long>());
        Assert(claims.GetLease("Customer:7:Bill") == 0, "Release left a reusable old claim token.");
    }
    private static void BotGeneration()
    {
        CallStatic(typeof(RestaurantTaskClaim), "ResetRuntimeState");
        var bot = Root("bot").AddComponent<AutonomousStaffBot>();
        var target = Root("job target");
        Set(bot, "<JobGeneration>k__BackingField", 7L);
        Assert(RestaurantTaskClaim.TryClaimBot(target, bot, 0), "Bot could not reserve test target.");
        RestaurantTaskClaim.ReleaseBot(target, bot, 7);
        Assert(RestaurantTaskClaim.IsClaimedByBot(target, bot), "Previous generation released the next job.");
        RestaurantTaskClaim.ReleaseBot(target, bot, 8);
        Assert(!RestaurantTaskClaim.IsClaimedByBot(target), "Matching generation failed to release.");
        RestaurantTaskClaim.Complete(target);
    }
    private static void BillRegistry()
    {
        var manager = Root("printer").AddComponent<BillManager>();
        SetStatic(typeof(BillManager), "<Instance>k__BackingField", manager);
        var group = Root("bill customer").AddComponent<CustomerGroup>();
        group.currentOrderNumber = 41;
        var bill = Root("first bill").AddComponent<BillPaper>(); bill.Init(group); bill.Init(group);
        var duplicate = Root("duplicate bill").AddComponent<BillPaper>(); duplicate.Init(group);
        Assert(manager.FindBillForGroup(group) == bill, "Duplicate replaced canonical bill.");
        Assert(Get<Dictionary<CustomerGroup, BillPaper>>(manager, "papers").Count == 1, "Registry has duplicate bills.");
        var slot = Root("printer slot").transform;
        var slots = Get<Dictionary<BillPaper, Transform>>(manager, "slots"); slots[bill] = slot;
        var a = Root("human A").AddComponent<WaiterHands>();
        var b = Root("human B").AddComponent<WaiterHands>();
        var carried = MultiplayerCustomerSpawn.BillStage.Carried;
        var printed = MultiplayerCustomerSpawn.BillStage.Printed;
        Assert(!(bool)CallStatic(typeof(MultiplayerCustomerInteractionBridge), "BillProjectionReady", a, group, bill, printed, carried),
            "Pickup acknowledgement enabled delivery before held-item projection.");
        a.PresentBillPaper(bill); a.PresentBillPaper(bill); b.PresentBillPaper(bill);
        Assert(!a.HasBill && b.holdingBillFor == group && bill.transform.parent == b.BillHoldPoint, "Holder transfer left two owners.");
        Assert((bool)CallStatic(typeof(MultiplayerCustomerInteractionBridge), "BillProjectionReady", b, group, bill, carried, carried),
            "Actual bill holder cannot deliver the projected bill.");
        Assert(!(bool)CallStatic(typeof(MultiplayerCustomerInteractionBridge), "BillProjectionReady", a, group, bill, carried, carried),
            "Previous holder can still deliver another actor's bill.");
        Assert(slots[bill] == slot && manager.FindBillForGroup(group) == bill, "Carrying lost reserved return slot/identity.");
        manager.ReturnUndeliveredBill(group);
        Assert(!b.HasBill && bill.transform.position == slot.position && manager.FindBillForGroup(group) == bill, "Cancellation lost paper or holder state.");
        Assert((bool)CallStatic(typeof(MultiplayerCustomerInteractionBridge), "BillProjectionReady", b, group, bill, printed, printed),
            "Returned paper did not become a usable pickup again.");
        group.currentOrderNumber = 42;
        var replacement = Root("remake bill").AddComponent<BillPaper>(); replacement.Init(group);
        Assert(manager.FindBillForGroup(group) == replacement && !slots.ContainsKey(bill), "Remake retained old order's paper/slot.");
        manager.Unregister(replacement);
        Assert(Get<Dictionary<CustomerGroup, BillPaper>>(manager, "papers").Count == 0, "Unregister retained paper identity.");
    }
    private static void PaymentOrdering()
    {
        var group = Root("payment customer").AddComponent<CustomerGroup>();
        group.IsNetworkObserver = true; group.currentOrderNumber = 15;
        group.PresentMultiplayerBillDelivered(true);
        group.PresentServiceState(new MultiplayerServiceActions.GroupState { billDelivered = false, paid = false });
        Assert(group.HasReceivedBill, "Delayed state undelivered a bill.");
        group.PresentServiceState(new MultiplayerServiceActions.GroupState { paid = true });
        group.PresentServiceState(new MultiplayerServiceActions.GroupState { paid = false });
        Assert(group.MultiplayerPaymentComplete, "Delayed state reopened payment.");
        group.ResetObservedOrder(16);
        Assert(!group.HasReceivedBill && !group.MultiplayerPaymentComplete, "A fresh order inherited payment completion.");
    }
    private static void ActionContexts()
    {
        var context = new MultiplayerActionContext { run = "fixture-run", day = 3, actor = 2, customerView = 15,
            orderNumber = 41, operation = "bill_deliver", actionId = Guid.NewGuid().ToString("N"),
            expectedStage = "Carried", lease = 9, expectedRevision = 11 };
        string json = JsonUtility.ToJson(context);
        Assert(context.MatchesAction(JsonUtility.FromJson<MultiplayerActionContext>(json)), "Identical retry did not match.");
        Action<MultiplayerActionContext>[] changes =
        {
            c => c.actor++, c => c.orderNumber++, c => c.customerView++, c => c.day++, c => c.run = "next-run",
            c => c.operation = "bill_pickup", c => c.actionId = Guid.NewGuid().ToString("N"),
            c => c.expectedStage = "Printed", c => c.lease++, c => c.expectedRevision++
        };
        foreach (var change in changes)
        {
            var replyContext = JsonUtility.FromJson<MultiplayerActionContext>(json);
            change(replyContext);
            Assert(!context.MatchesAction(replyContext), "Unrelated or superseded reply matched the active action.");
        }
    }
    private static void BubbleFocus()
    {
        var root = Root("BillAlertUI", true);
        var canvas = root.AddComponent<CanvasGroup>();
        var follow = root.AddComponent<UIFollowWorldPoint>(); Call(follow, "Awake");
        var focus = root.AddComponent<PlayerTaskBubbleFocus>(); Call(focus, "Awake");
        var target = Root("focus target");
        for (int i = 0; i < 20; i++) PlayerTaskBubbleFocus.Bind(root, target);
        Assert(root.GetComponents<CanvasGroup>().Length == 1 && root.GetComponents<PlayerTaskBubbleFocus>().Length == 1, "Binding duplicated components.");
        follow.SetTaskFocus(0.28f, false); Call(follow, "SetVisible", true);
        Assert(Mathf.Approximately(canvas.alpha, 0.28f) && !canvas.interactable, "Focus did not compose with visibility.");
        Call(follow, "SetVisible", false); Assert(canvas.alpha == 0 && !canvas.blocksRaycasts, "Hidden bubble intercepts input.");
        follow.SetTaskFocus(1, true); Call(follow, "SetVisible", true);
        Assert(canvas.alpha == 1 && canvas.interactable, "Bubble did not recover after focus/visibility changes.");
    }
    private static void Movement()
    {
        var poses = new MultiplayerPoseBuffer();
        Assert(poses.Add(1, Vector3.zero, Quaternion.identity), "Initial sample rejected.");
        Assert(poses.Add(1.1, Vector3.right, Quaternion.identity), "Next sample rejected.");
        Assert(!poses.Add(1, Vector3.left * 100, Quaternion.identity), "Stale movement accepted.");
        poses.Read(1.05, out var midpoint, out _); Assert(Mathf.Abs(midpoint.x - 0.5f) < 0.001f, "Interpolation does not use network time.");
        poses.Read(2, out var latest, out _); Assert(latest == Vector3.right, "Packet loss extrapolated through the restaurant.");
        for (int i = 2; i < 100; i++) poses.Add(i, Vector3.right * i, Quaternion.identity);
        Assert(poses.Count <= 8, "Movement backlog grows without bound.");
        poses.Add(101, Vector3.zero, Quaternion.identity, true);
        Assert(poses.Count == 1, "Explicit recovery retained old movement.");
    }
    private static void ResultRules()
    {
        Assert(MultiplayerSessionManager.Protocol != MultiplayerSessionManager.ResultRulesVersion, "Room and receipt versions are coupled.");
        Assert(new MultiplayerRunRecord().rulesVersion == "casual-session-2", "Existing receipt contract changed.");
        Assert(MultiplayerSessionManager.RejoinSeconds == 90, "Guest grace changed.");
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Fields).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    private static void SetStatic(Type type, string name, object value) => type.GetField(name, Fields).SetValue(null, value);
    private static void Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Fields).Invoke(instance, args);
    private static object CallStatic(Type type, string name, params object[] args) => type.GetMethod(name, Fields).Invoke(null, args);
}
#endif
