#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Opt-in external runner. Importing this file never starts Play Mode, opens a
// room, authenticates an account, or runs a test. Use a disposable external
// Unity 6000.0.40f1 checkout/profile, then execute RunBatch explicitly.
[InitializeOnLoad]
public static class MultiplayerInputRegressionTest
{
    private const string Running = "DineIn.MultiplayerInput.Running";
    private const string Batch = "DineIn.MultiplayerInput.Batch";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<GameObject> roots = new();
    private static readonly List<string> results = new();

    static MultiplayerInputRegressionTest()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(Running, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += Check;
    }

    [MenuItem("Tools/Dine In/Run Multiplayer Input Regressions (External)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start the runner outside Play Mode.");
        if (!SessionState.GetBool(Batch, false) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunBatch() { SessionState.SetBool(Batch, true); Run(); }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (SessionState.GetBool(Running, false) && change == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += Check;
    }

    private static void Check()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Running, false);
        results.Clear();
        int exitCode = 0;
        try
        {
            RunCase("hand notifications never issue commands or cancel an existing target", HandsArePresentationOnly);
            RunCase("uncontrolled movement failures cannot display local action feedback", FeedbackOwnership);
            RunCase("authority arrival data stays ahead of presentation and rejects stale/reset poses", ReceivedMovement);
            RunCase("food replies match actor and action; completed receipts cannot be changed", FoodReplyIsolation);
        }
        catch (Exception error)
        {
            exitCode = 1; results.Add("FAIL: " + error); Debug.LogException(error);
        }
        finally
        {
            foreach (var root in roots) if (root != null) Object.Destroy(root);
            roots.Clear();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "MultiplayerInputRegression.txt"), results);
            bool batch = SessionState.GetBool(Batch, false);
            SessionState.SetBool(Batch, false);
            if (batch) EditorApplication.Exit(exitCode); else EditorApplication.ExitPlaymode();
        }
    }

    private sealed class Target : IInteractable, ICancelableTaskTarget
    {
        public int interactions, cancellations;
        public Transform StandPoint { get; set; }
        public bool AutoReturnHome => false;
        public bool CanInteract() => true;
        public float GetInteractRadius() => 1;
        public void Interact(PlayerMovement mover) => interactions++;
        public void OnTaskCancelled() => cancellations++;
    }

    private static void HandsArePresentationOnly()
    {
        var actor = Root("human with task");
        var movement = actor.AddComponent<PlayerMovement>();
        var hands = actor.AddComponent<WaiterHands>();
        var otherHands = Root("another human").AddComponent<WaiterHands>();
        var camera = Root("old pointer camera").AddComponent<Camera>();
        movement.SetCamera(camera);
        movement.SetPlayerControlled(true);
        var target = new Target { StandPoint = Root("selected service point").transform };
        foreach (PlayerMovement.State state in Enum.GetValues(typeof(PlayerMovement.State)))
        {
            foreach (bool locked in new[] { false, true })
            {
                Set(movement, "state", state);
                Set(movement, "currentTarget", target);
                Set(movement, "currentStandPoint", target.StandPoint);
                Set(movement, "lastCommandTime", -123f);
                Set(movement, "idleTimer", 5f);
                if (locked) movement.LockTask(target); else movement.UnlockTask();
                for (int i = 0; i < 10; i++)
                {
                    Call(movement, "HandleHandsStateChanged", hands);
                    Call(movement, "HandleHandsStateChanged", otherHands);
                }
                Assert(Get<float>(movement, "lastCommandTime") == -123f && Get<float>(movement, "idleTimer") == 5f,
                    "A hand update registered a new player command.");
                Assert(movement.CurrentState == state && ReferenceEquals(movement.CurrentTarget, target)
                    && movement.IsTaskLocked == locked, "A hand update replaced, completed or cancelled the active task.");
                Assert(target.interactions == 0 && target.cancellations == 0,
                    "A presentation update invoked a gameplay callback.");
            }
        }
    }

    private static void FeedbackOwnership()
    {
        var movement = Root("uncontrolled role").AddComponent<PlayerMovement>();
        movement.SetPlayerControlled(false);
        Assert(!(bool)Call(movement, "CanShowMovementFeedback"), "An autonomous or remote mover owns local feedback.");
        movement.SetPlayerControlled(true);
        Assert((bool)Call(movement, "CanShowMovementFeedback"), "Single-player controlled movement lost its feedback.");
    }

    private static void ReceivedMovement()
    {
        var root = Root("remote avatar presentation");
        root.transform.position = Vector3.left * 8;
        var sync = root.AddComponent<NetworkPlayerMovementSync>();
        var view = root.GetComponent<PhotonView>();
        view.OwnerActorNr = 2;
        Call(sync, "RefreshPoseScope");
        Set(sync, "minimumPoseTime", 0d);
        Assert((bool)Call(sync, "ReceivePose", 10d, Vector3.zero, Quaternion.identity), "First pose rejected.");
        Assert((bool)Call(sync, "ReceivePose", 10.1d, Vector3.right, Quaternion.identity), "Latest pose rejected.");
        Get<MultiplayerPoseBuffer>(sync, "poses").Read(10.05d, out var rendered, out _);
        Assert(Mathf.Abs(rendered.x - 0.5f) < 0.001f && Get<Vector3>(sync, "receivedPosition") == Vector3.right,
            "Distance validation would use the delayed render position.");
        Assert(!(bool)Call(sync, "ReceivePose", 10d, Vector3.left * 99, Quaternion.identity), "Old pose replaced the latest arrival.");
        Assert(!(bool)Call(sync, "ReceivePose", 11d, new Vector3(float.NaN, 0, 0), Quaternion.identity), "Nonfinite pose accepted.");
        Assert(Get<Vector3>(sync, "receivedPosition") == Vector3.right, "Rejected data changed arrival position.");
        Assert(NetworkPlayerMovementSync.AuthorityPosition(root) == root.transform.position,
            "Non-authority/local fallback used a remote sample.");
        view.OwnerActorNr = 3;
        Call(sync, "RefreshPoseScope");
        Assert(!Get<bool>(sync, "hasReceivedPosition") && Get<MultiplayerPoseBuffer>(sync, "poses").Count == 0,
            "Ownership change retained the previous actor's position.");
        Set(sync, "minimumPoseTime", 20d);
        Assert(!(bool)Call(sync, "ReceivePose", 19d, Vector3.one, Quaternion.identity), "Pre-reset queued pose was accepted.");
        Assert((bool)Call(sync, "ReceivePose", 21d, Vector3.one, Quaternion.identity), "New owner pose failed after reset.");
        Call(sync, "OnDisable");
        Assert(!Get<bool>(sync, "hasReceivedPosition"), "Disabled/rejoining avatar retained an arrival sample.");
    }

    private static void FoodReplyIsolation()
    {
        var root = Root("food command context");
        var bridge = root.AddComponent<MultiplayerCustomerInteractionBridge>();
        var session = root.AddComponent<MultiplayerSessionManager>();
        var claims = root.AddComponent<MultiplayerTaskClaims>();
        Set(session, "localActor", 2);
        Set(bridge, "session", session); Set(bridge, "claims", claims);
        var context = new MultiplayerActionContext { run = "test-run", day = 1, actor = 2,
            customerView = 8, orderNumber = 41, operation = "food_deliver", expectedStage = "Carried",
            actionId = Guid.NewGuid().ToString("N"), lease = 7, expectedRevision = 8 };
        Type requestType = typeof(MultiplayerCustomerInteractionBridge).GetNestedType("FoodRequest", BindingFlags.NonPublic);
        Type actionType = typeof(MultiplayerCustomerInteractionBridge).GetNestedType("FoodAction", BindingFlags.NonPublic);
        var request = Activator.CreateInstance(requestType, true); Set(request, "context", context); Set(request, "sequence", 1L);
        var action = Activator.CreateInstance(actionType, true); Set(action, "request", request);
        Set(bridge, "pendingFoodAction", action); Set(bridge, "pickupTaskId", "Order:41:Pickup");
        var foreign = JsonUtility.FromJson<MultiplayerActionContext>(JsonUtility.ToJson(context)); foreign.actor = 3;
        Call(bridge, "ApplyFoodResult", new MultiplayerActionResult { context = foreign, outcome = MultiplayerActionOutcome.Success });
        Assert(ReferenceEquals(Get<object>(bridge, "pendingFoodAction"), action), "Another actor's reply consumed the local action.");
        var old = JsonUtility.FromJson<MultiplayerActionContext>(JsonUtility.ToJson(context)); old.actionId = Guid.NewGuid().ToString("N");
        Call(bridge, "ApplyFoodResult", new MultiplayerActionResult { context = old, outcome = MultiplayerActionOutcome.Success });
        Assert(ReferenceEquals(Get<object>(bridge, "pendingFoodAction"), action), "A delayed reply consumed a replacement action.");
        Call(bridge, "CompleteFoodAction", action, MultiplayerActionOutcome.Success);
        Assert(Get<object>(bridge, "pendingFoodAction") == null && Get<string>(bridge, "pickupTaskId") == null,
            "The matching delivery receipt did not clear the completed local intent.");
        Set(bridge, "pickupTaskId", "Order:42:Pickup");
        Call(bridge, "CompleteFoodAction", action, MultiplayerActionOutcome.Cancelled);
        var result = Get<MultiplayerActionResult>(action, "result");
        Assert(result.Accepted && Get<string>(bridge, "pickupTaskId") == "Order:42:Pickup",
            "Repeating a completed receipt changed its outcome or cleared the next task.");
    }

    private static GameObject Root(string name)
    {
        var root = new GameObject(name); root.SetActive(false); roots.Add(root); return root;
    }
    private static void RunCase(string name, Action body) { body(); results.Add("PASS: " + name); Debug.Log("[MultiplayerInput] PASS: " + name); }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Members).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
}
#endif
