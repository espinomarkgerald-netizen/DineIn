#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class RestockRoomTransitionSmokeTest
{
    private const string LobbyScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string RunningKey = "DineIn.RestockRoomTransitionSmokeTest.Running";
    private const string ResultPath = "Temp/RestockRoomTransitionSmokeTest.result";

    private enum Phase
    {
        None,
        WaitingForLobby,
        WaitingForRestock,
        WaitingForLobbyReturn
    }

    private static Phase phase;
    private static double phaseStarted;

    static RestockRoomTransitionSmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string requestPath = !string.IsNullOrWhiteSpace(projectRoot)
            ? Path.Combine(projectRoot, "Temp", "RunRestockRoomTransitionSmokeTest.request")
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(requestPath) && File.Exists(requestPath))
        {
            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Dine In/Run Restock Room Transition Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[RestockRoomTransitionSmokeTest] Stop Play mode before running.");
            return;
        }

        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        WriteResult("RUNNING");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SetPhase(Phase.WaitingForLobby);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(RunningKey);
        }
    }

    private static void Tick()
    {
        try
        {
            if (Elapsed > 12d)
                throw new InvalidOperationException("Transition phase timed out: " + phase + ".");

            switch (phase)
            {
                case Phase.WaitingForLobby when Elapsed >= 2.5d:
                    BeginRestockTransition();
                    break;
                case Phase.WaitingForRestock:
                    ValidateRestockAndExit();
                    break;
                case Phase.WaitingForLobbyReturn:
                    ValidateLobbyReturn();
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish("FAIL: " + exception, true);
        }
    }

    private static void BeginRestockTransition()
    {
        Assert(SceneManager.GetActiveScene().name == "Lobby1", "Lobby1 is not active.");
        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SuppressWritesForTests = true;

        RestockFlowCoordinator coordinator = RestockFlowCoordinator.Instance;
        Assert(coordinator != null, "Restock flow coordinator is missing.");
        coordinator.EnterRestockRoom(RestockStorageType.Dry);
        SetPhase(Phase.WaitingForRestock);
    }

    private static void ValidateRestockAndExit()
    {
        RestockFlowCoordinator coordinator = RestockFlowCoordinator.Instance;
        if (coordinator == null || coordinator.IsTransitioning || !coordinator.IsRestockRoomOpen)
            return;

        Assert(SceneManager.GetActiveScene().name == "RestockScene",
            "RestockScene did not become the active additive scene.");
        Assert(EventSystem.current != null && EventSystem.current.isActiveAndEnabled,
            "Restock UI has no active EventSystem.");

        Button exit = FindActiveRestockButton("ExitButton");
        Button roomSwitch = FindActiveRestockButton("SwitchRoomToFreezer");
        Assert(exit != null && exit.interactable, "Restock close button is not usable.");
        Assert(roomSwitch != null && roomSwitch.interactable, "Restock freezer/dry-room button is not usable.");

        roomSwitch.onClick.Invoke();
        exit.onClick.Invoke();
        SetPhase(Phase.WaitingForLobbyReturn);
    }

    private static void ValidateLobbyReturn()
    {
        RestockFlowCoordinator coordinator = RestockFlowCoordinator.Instance;
        if (coordinator == null || coordinator.IsTransitioning || coordinator.IsRestockRoomOpen)
            return;

        Assert(SceneManager.GetActiveScene().name == "Lobby1",
            "Lobby1 was not restored after leaving RestockScene.");
        Assert(EventSystem.current != null && EventSystem.current.isActiveAndEnabled,
            "Lobby UI input was not restored after leaving RestockScene.");

        RestockIrisGraphic iris = UnityEngine.Object.FindFirstObjectByType<RestockIrisGraphic>(
            FindObjectsInactive.Include);
        if (iris != null && iris.gameObject.activeSelf && iris.raycastTarget)
            return;

        Finish("PASS: restock open, room switch, close button, lobby return, and iris release passed.", false);
    }

    private static Button FindActiveRestockButton(string objectName)
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        return Array.Find(buttons, button =>
            button != null && button.name == objectName &&
            button.gameObject.scene.name == "RestockScene");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - phaseStarted;

    private static void SetPhase(Phase next)
    {
        phase = next;
        phaseStarted = EditorApplication.timeSinceStartup;
    }

    private static void Finish(string result, bool failed)
    {
        EditorApplication.update -= Tick;
        SetPhase(Phase.None);
        WriteResult(result);
        if (failed)
            Debug.LogError("[RestockRoomTransitionSmokeTest] " + result);
        else
            Debug.Log("[RestockRoomTransitionSmokeTest] " + result);
        EditorApplication.ExitPlaymode();
    }

    private static void WriteResult(string result)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrWhiteSpace(projectRoot))
            File.WriteAllText(Path.Combine(projectRoot, ResultPath), result);
    }
}
#endif
