using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TutorialSceneRuntimeMarker : MonoBehaviour
{
    public static TutorialSceneRuntimeMarker Instance { get; private set; }

    public static bool IsTutorialRuntimeActive =>
        Instance != null && Instance.isActiveAndEnabled && Instance.gameObject.activeInHierarchy;

    public static bool UsesMinimalHUD => IsTutorialRuntimeActive || TutorialSystem.IsTutorialMode;

    // Keep unrelated shortcuts out of guided lessons, while revealing the actual
    // control a lesson (or its existing recovery path) asks the player to use.
    public static bool AllowsHUDControl(string key, Transform control = null)
    {
        if (!UsesMinimalHUD) return true;
        if (key == "TaskButton" || key == "TaskMessage" || key == "Task" ||
            key == "PauseButton" || key == "Pause") return true;
        if (key == "InteractionLabel") return false;

        TutorialSystem tutorial = TutorialSystem.Instance;
        if (tutorial == null) return false;
        var step = tutorial.CurrentStep;
        if (step == null) return false;
        if (key == "NewspaperButton" || key == "NewspaperClose")
            return !tutorial.IsFastFood && !tutorial.IsFineDining &&
                step.Id != null && step.Id.StartsWith("hud_newspaper", StringComparison.Ordinal);
        if (control != null && tutorial.IsHUDControlFocused(control)) return true;

        string target = step.UITargetKey;
        string action = step.ActionKey;
        switch (key)
        {
            case "CameraButton":
                return target == "CameraButton" || action == "Camera.Focus";
            case "LivePanel":
            case "LiveCounts":
            case "LivePanelToggle":
                return target == "LivePanel" || target == "LiveCounts";
            case "ComputerButton":
                return target == "ComputerButton" || action == "Computer.Open" ||
                    FastFoodTutorialBridge.Active &&
                    FastFoodTutorialBridge.Instance.CurrentLesson?.action == FastFoodTutorialBridge.ActionKind.RestockOpen;
            case "KitchenButton":
                return tutorial.IsFastFood && (target == "FF.Enter" ||
                    FastFoodTutorialBridge.Active &&
                    (FastFoodTutorialBridge.Instance.CurrentLesson?.control == "Enter" ||
                     FastFoodTutorialBridge.Instance.CurrentLesson?.action == FastFoodTutorialBridge.ActionKind.Practice));
            case "ShiftPlayButton":
                return target == "ShiftPlayButton" || action == "Shift.Start" ||
                    !tutorial.IsFastFood && !tutorial.IsFineDining &&
                    tutorial.CurrentPhase == TutorialSystem.TutorialPhase.NormalGameplay;
            case "DayTime":
            case "DayAndTime":
                return target == "DayTime" || target == "DayAndTime";
            default:
                return !string.IsNullOrEmpty(key) && target == key;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
