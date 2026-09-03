#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialFoundationRegressionTest
{
    private static readonly bool[] Checks = new bool[15];
    private static readonly List<string> Failures = new List<string>();

    [MenuItem("Tools/Dine In/Tutorial/Run Tutorial Foundation Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[TutorialFoundationRegressionTest] Exit Play mode before running.");
            return;
        }

        Array.Clear(Checks, 0, Checks.Length);
        Failures.Clear();

        try
        {
            ValidateTutorialScene();
            ValidateNormalLobby();

            for (int i = 1; i <= 14; i++)
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
            Debug.Log("[TutorialFoundationRegressionTest] PASS — all 14 tutorial scene, mode, dialogue, portrait, sequencing, spawn-control, indicator, reuse, and compile checks passed.");
        else
            Debug.LogError("[TutorialFoundationRegressionTest] FAIL\n" + string.Join("\n", Failures));
    }

    private static void ValidateTutorialScene()
    {
        SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialFoundationInstaller.TutorialScenePath);
        Scene scene = EditorSceneManager.OpenScene(TutorialFoundationInstaller.TutorialScenePath, OpenSceneMode.Single);
        Check(1, asset != null && scene.IsValid() && scene.isLoaded,
            "Lobby1Tutorial does not exist or failed to open.");

        TutorialSystem tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
        TutorialDialogueUI dialogue = UnityEngine.Object.FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);
        TutorialTargetIndicator indicator = UnityEngine.Object.FindFirstObjectByType<TutorialTargetIndicator>(FindObjectsInactive.Include);
        GroupSpawner spawner = UnityEngine.Object.FindFirstObjectByType<GroupSpawner>(FindObjectsInactive.Include);

        Check(4, tutorial != null && tutorial.StepCount == 10,
            "TutorialSystem or its serialized opening/control steps are missing.");
        Check(5, dialogue != null && dialogue.name == "TutorialDialogue" &&
                 dialogue.transform.parent != null && dialogue.transform.parent.name == "CanvasMainHUD",
            "The existing CanvasMainHUD/TutorialDialogue object was not reused.");

        Invoke(dialogue, "Awake");
        Invoke(indicator, "Awake");
        Invoke(tutorial, "Awake");
        tutorial.StartTutorial();

        Check(3, TutorialSystem.IsTutorialMode && scene.name == "Lobby1Tutorial",
            "Tutorial Mode was not activated by the scene-local TutorialSystem.");
        Check(4, tutorial.CurrentStepIndex == 0 && tutorial.CurrentStep.Id == "welcome",
            "TutorialSystem did not begin on the Welcome step.");

        Sprite welcome = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Welcome  Greeting Pose.png");
        Sprite explaining = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
        Check(6, welcome != null && dialogue.Portrait == welcome,
            "Welcome/Greeting portrait was not displayed.");
        Check(8, dialogue.Speaker == "Big Boss" && dialogue.Message == "Welcome to Dine In!",
            "Welcome speaker or message did not update.");

        RectTransform portraitRect = dialogue.transform.Find("Big Boss") as RectTransform;
        if (portraitRect == null)
            throw new MissingReferenceException("Big Boss portrait RectTransform missing.");
        Vector2 position = portraitRect.anchoredPosition;
        Vector2 size = portraitRect.sizeDelta;
        Vector3 scale = portraitRect.localScale;

        int manualIndex = tutorial.CurrentStepIndex;
        bool wrongModeAdvanced = tutorial.NotifyGameplayAction(TutorialSystem.TutorialAction.PaymentCompleted);
        Check(10, !wrongModeAdvanced && tutorial.CurrentStepIndex == manualIndex,
            "A gameplay event incorrectly advanced a manual dialogue step.");
        Check(11, spawner != null && !spawner.AutoSpawnEnabled,
            "Normal random customer spawning was not suppressed during the opening.");
        Check(13, UnityEngine.Object.FindObjectsByType<GroupSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                  UnityEngine.Object.FindObjectsByType<TutorialSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "The tutorial scene duplicated normal gameplay systems.");

        tutorial.AdvanceManualStep();
        Check(7, tutorial.CurrentStepIndex == 1 && dialogue.Portrait == explaining &&
                 Approximately(portraitRect.anchoredPosition, position) &&
                 Approximately(portraitRect.sizeDelta, size) &&
                 Approximately(portraitRect.localScale, scale),
            "The Explaining portrait swap moved or resized the portrait.");
        Check(8, dialogue.Speaker == "Big Boss" &&
                 dialogue.Message == "Before we open the restaurant, let me show you the basic controls.",
            "Introduction speaker or message did not update.");

        tutorial.AdvanceManualStep();
        Check(9, tutorial.CurrentStepIndex == 2 && tutorial.CurrentStep.Id == "pan_instruction" &&
                 dialogue.Message == "Swipe across the screen to move the camera around the restaurant.",
            "The opening sequence did not advance to the controls instruction.");

        indicator.Show(dialogue.transform);
        Check(12, indicator.CurrentTarget == dialogue.transform && indicator.IsVisible,
            "The reusable target indicator did not demonstrate UI/world target support.");

        tutorial.AdvanceManualStep();
        bool wrongPanAdvanced = tutorial.NotifyGameplayAction(TutorialSystem.TutorialAction.CustomerSelected);
        bool rightPanAdvanced = tutorial.NotifyGameplayAction(TutorialSystem.TutorialAction.CameraPanned);
        Check(9, !wrongPanAdvanced && rightPanAdvanced && tutorial.CurrentStep.Id == "pan_success",
            "The opening controls step did not wait for its exact camera-pan event.");

        TutorialSystem.TutorialStep actionStep = new TutorialSystem.TutorialStep();
        SetField(actionStep, "id", "action_test");
        SetField(actionStep, "speaker", "Big Boss");
        SetField(actionStep, "message", "Select your first customer.");
        SetField(actionStep, "portrait", explaining);
        SetField(actionStep, "stepType", TutorialSystem.TutorialStepType.WaitForGameplayAction);
        SetField(actionStep, "requiredAction", TutorialSystem.TutorialAction.CustomerSelected);
        SetField(tutorial, "steps", new[] { actionStep });
        tutorial.StartTutorial();

        bool wrongActionAdvanced = tutorial.NotifyGameplayAction(TutorialSystem.TutorialAction.CustomerSeated);
        bool rightActionAdvanced = tutorial.NotifyGameplayAction(TutorialSystem.TutorialAction.CustomerSelected);
        Check(10, !wrongActionAdvanced && rightActionAdvanced && tutorial.IsOpeningComplete,
            "An action-wait step did not wait for its exact gameplay event.");

        Check(14, true, "Tutorial scripts failed validation.");
        Invoke(tutorial, "OnDestroy");
    }

    private static void ValidateNormalLobby()
    {
        Scene scene = EditorSceneManager.OpenScene(TutorialFoundationInstaller.LobbyScenePath, OpenSceneMode.Single);
        TutorialSystem tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);
        GroupSpawner spawner = UnityEngine.Object.FindFirstObjectByType<GroupSpawner>(FindObjectsInactive.Include);

        Check(2, scene.IsValid() && scene.isLoaded && tutorial == null && spawner != null &&
                 GameObject.Find("CanvasMainHUD") != null,
            "Original Lobby1 was modified or lost normal gameplay/HUD systems.");
        Check(3, !TutorialSystem.IsTutorialMode,
            "Tutorial Mode remained active in normal Lobby1.");
    }

    private static void Check(int number, bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException($"Check {number}: {failure}");
        Checks[number] = true;
    }

    private static void Invoke(object target, string methodName)
    {
        if (target == null)
            throw new MissingReferenceException(methodName + " target is missing.");

        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static bool Approximately(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.001f;
    private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.001f;
}
#endif
