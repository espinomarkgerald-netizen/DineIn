#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialControlsRegressionTest
{
    private static readonly bool[] Checks = new bool[19];
    private static readonly List<string> Failures = new List<string>();

    [MenuItem("Tools/Dine In/Tutorial/Run Controls Tutorial Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TutorialControlsRegressionTest] Exit Play mode before running.");
            return;
        }

        Array.Clear(Checks, 0, Checks.Length);
        Failures.Clear();

        try
        {
            ValidateControlsTutorial();
            ValidateNormalLobby();
            for (int i = 1; i <= 18; i++)
            {
                if (!Checks[i])
                    Failures.Add($"Check {i} did not pass.");
            }
        }
        catch (Exception exception)
        {
            Failures.Add(exception.ToString());
        }
        finally
        {
            EditorSceneManager.OpenScene(TutorialFoundationInstaller.TutorialScenePath, OpenSceneMode.Single);
        }

        if (Failures.Count == 0)
            Debug.Log("[TutorialControlsRegressionTest] PASS — all 18 control-tutorial dialogue, real-input event, hand-hint, target, isolation, and compile checks passed.");
        else
            Debug.LogError("[TutorialControlsRegressionTest] FAIL\n" + string.Join("\n", Failures));
    }

    private static void ValidateControlsTutorial()
    {
        Scene scene = EditorSceneManager.OpenScene(
            TutorialFoundationInstaller.TutorialScenePath, OpenSceneMode.Single);
        TutorialSystem tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
        TutorialDialogueUI dialogue = UnityEngine.Object.FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);
        TutorialTargetIndicator targetIndicator = UnityEngine.Object.FindFirstObjectByType<TutorialTargetIndicator>(FindObjectsInactive.Include);
        TutorialHandIndicator hand = UnityEngine.Object.FindFirstObjectByType<TutorialHandIndicator>(FindObjectsInactive.Include);
        MainCameraController cameraController = UnityEngine.Object.FindFirstObjectByType<MainCameraController>(FindObjectsInactive.Include);
        TapOutlineSelector tapSelector = UnityEngine.Object.FindFirstObjectByType<TapOutlineSelector>(FindObjectsInactive.Include);
        GroupSpawner spawner = UnityEngine.Object.FindFirstObjectByType<GroupSpawner>(FindObjectsInactive.Include);

        if (!scene.IsValid() || tutorial == null || dialogue == null || targetIndicator == null ||
            hand == null || cameraController == null || tapSelector == null || spawner == null)
            throw new MissingReferenceException("The controls tutorial scene is missing a required existing or tutorial component.");

        Invoke(dialogue, "Awake");
        Invoke(targetIndicator, "Awake");
        Invoke(hand, "Awake");
        Invoke(cameraController, "Awake");
        Invoke(tapSelector, "Awake");
        Invoke(tutorial, "Awake");
        tutorial.StartTutorial();

        Sprite welcome = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Welcome  Greeting Pose.png");
        Sprite explaining = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
        Sprite success = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Success  Thumbs Up Pose.png");
        Sprite handSwipe = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Hand.png");
        Sprite handClick = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Hand Click.png");

        Check(1, tutorial.StepCount == 10 && tutorial.CurrentStep.Id == "welcome" &&
                 dialogue.Message == "Welcome to Dine In!" && dialogue.Portrait == welcome,
            "Welcome did not start with the existing greeting portrait.");

        tutorial.AdvanceManualStep();
        Check(2, tutorial.CurrentStep.Id == "controls_introduction" &&
                 dialogue.Message == "Before we open the restaurant, let me show you the basic controls." &&
                 dialogue.Portrait == explaining,
            "The basic-controls introduction is missing.");

        tutorial.AdvanceManualStep();
        Check(3, tutorial.CurrentStep.Id == "pan_instruction" &&
                 dialogue.Message == "Swipe across the screen to move the camera around the restaurant.",
            "The Swipe instruction is missing.");

        tutorial.AdvanceManualStep();
        Check(4, tutorial.CurrentStep.Id == "pan_practice" && !dialogue.IsVisible,
            "Dialogue did not hide for the camera-pan action.");
        Check(5, hand.IsVisible && hand.Mode == TutorialHandIndicator.HintMode.Swipe &&
                 hand.CurrentSprite == handSwipe,
            "The reusable Hand swipe loop did not appear.");

        Invoke(cameraController, "BeginPanGesture");
        Invoke(cameraController, "RecordPanGesture", new Vector2(10f, 0f));
        Check(6, tutorial.CurrentStep.Id == "pan_practice",
            "An accidental movement below the real camera threshold completed the pan lesson.");
        Invoke(cameraController, "RecordPanGesture", new Vector2(28f, 0f));
        Check(7, tutorial.CurrentStep.Id == "pan_success",
            "A valid real camera-controller pan event did not complete the lesson.");
        Check(8, !hand.IsVisible,
            "The swipe hand did not disappear immediately after the valid pan.");
        Check(9, dialogue.IsVisible && dialogue.Portrait == success &&
                 dialogue.Message == "Great job! You can use that anytime to look around your restaurant.",
            "Big Boss did not return with the pan success response.");

        tutorial.AdvanceManualStep();
        Check(10, tutorial.CurrentStep.Id == "interaction_introduction" &&
                  dialogue.Message == "Now let's try interacting with something. Just tap an object you want to use.",
            "The interaction explanation is missing.");

        tutorial.AdvanceManualStep();
        Transform requiredTarget = tutorial.CurrentStep.HighlightTarget;
        Check(11, tutorial.CurrentStep.Id == "tap_practice" && requiredTarget != null &&
                  requiredTarget.GetComponent<Booth>() != null && targetIndicator.IsVisible &&
                  targetIndicator.CurrentTarget == requiredTarget,
            "A real table/booth was not highlighted for the tap lesson.");
        Check(12, hand.IsVisible && hand.Mode == TutorialHandIndicator.HintMode.Tap &&
                  hand.CurrentSprite == handClick && hand.CurrentTarget == requiredTarget,
            "Hand Click is not following the highlighted table/booth.");

        Invoke(tapSelector, "PublishSelection", tutorial.transform);
        Check(13, tutorial.CurrentStep.Id == "tap_practice",
            "Selecting an unrelated object incorrectly completed the tap lesson.");

        Outline targetOutline = requiredTarget.GetComponentInChildren<Outline>(true);
        if (targetOutline == null)
            throw new MissingReferenceException("The configured tutorial booth has no existing selectable Outline.");
        Invoke(tapSelector, "PublishSelection", targetOutline.transform);
        Check(14, tutorial.CurrentStep.Id == "tap_success",
            "The existing selection path did not accept the highlighted table/booth.");
        Check(15, !hand.IsVisible && !targetIndicator.IsVisible,
            "The hand or target highlight remained after successful interaction.");
        Check(16, dialogue.IsVisible && dialogue.Portrait == success &&
                  dialogue.Message == "Nice! You're picking it up quickly.",
            "Big Boss did not return with the interaction success response.");

        tutorial.AdvanceManualStep();
        bool summaryReached = tutorial.CurrentStep.Id == "interaction_summary" &&
                              dialogue.Message.StartsWith("You'll use tapping");
        tutorial.AdvanceManualStep();
        Check(17, summaryReached && tutorial.CurrentStep.Id == "first_customer" &&
                  dialogue.Message == "Now let's help your first customer.",
            "The controls tutorial did not reach the first-customer transition.");

        Check(18, !spawner.AutoSpawnEnabled &&
                  UnityEngine.Object.FindObjectsByType<MainCameraController>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1 &&
                  UnityEngine.Object.FindObjectsByType<TapOutlineSelector>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1,
            "Opening spawn control or reuse of the real input systems is incorrect.");

        Invoke(tutorial, "OnDestroy");
    }

    private static void ValidateNormalLobby()
    {
        Scene scene = EditorSceneManager.OpenScene(
            TutorialFoundationInstaller.LobbyScenePath, OpenSceneMode.Single);
        bool normalSceneUnaffected = scene.IsValid() && scene.isLoaded &&
            UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include) == null &&
            UnityEngine.Object.FindFirstObjectByType<TutorialHandIndicator>(FindObjectsInactive.Include) == null &&
            UnityEngine.Object.FindObjectsByType<MainCameraController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length == 1 &&
            UnityEngine.Object.FindObjectsByType<TapOutlineSelector>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length == 1 &&
            !TutorialSystem.IsTutorialMode;
        Checks[18] &= normalSceneUnaffected;
        if (!normalSceneUnaffected)
            throw new InvalidOperationException("Check 18: Normal Lobby1 no longer has its untouched real input setup.");
    }

    private static void Check(int number, bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException($"Check {number}: {failure}");
        Checks[number] = true;
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        if (target == null)
            throw new MissingReferenceException(methodName + " target is missing.");

        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, arguments);
    }
}
#endif
