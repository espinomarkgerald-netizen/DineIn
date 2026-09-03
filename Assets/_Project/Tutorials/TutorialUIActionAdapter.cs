using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial-only listener for the REAL highlighted UI button.
/// It never replaces gameplay listeners. A step completes only after the real button
/// was clicked and, where possible, the real UI state confirms the action succeeded.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialUIActionAdapter : MonoBehaviour
{
    private TutorialSystem tutorial;
    private TutorialSystem.TutorialStep step;
    private Button button;
    private bool clicked;
    private int clickedFrame;

    public void Begin(TutorialSystem owner, RectTransform target)
    {
        StopWaiting();

        if (owner == null || target == null || string.IsNullOrEmpty(owner.CurrentStep?.ActionKey))
            return;

        tutorial = owner;
        step = owner.CurrentStep;
        button = target.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("[Tutorial] Action target has no Button: " + step.Id, target);
            return;
        }

        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        if (tutorial == null || tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction)
            return;

        clicked = true;
        clickedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        // Wait at least one frame so every original gameplay listener runs first.
        // For delayed actions (e.g. walking to the computer) clicked remains true and
        // this keeps checking until the real state confirms success.
        if (!clicked || Time.frameCount <= clickedFrame || tutorial == null ||
            tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction)
            return;

        bool completed = IsRealActionComplete(step.ActionKey);
        if (!completed)
            return;

        TutorialSystem owner = tutorial;
        string key = step.ActionKey;
        StopWaiting();
        owner.NotifyAction(key);
    }

    private bool IsRealActionComplete(string actionKey)
    {
        switch (actionKey)
        {
            case "Newspaper.Open":
                return FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == true;

            case "Newspaper.Close":
                return FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == false;

            case "Computer.Open":
                return FindManagementComputer()?.IsOpen == true;

            case "Management.Dashboard":
            case "Management.Staff":
            case "Management.Menu":
            case "Management.Equipment":
            case "Management.Finance":
            case "Management.Objectives":
            case "Management.Restock":
            {
                // The tutorial is attached to the real navigation Button, so the click
                // itself is genuine. Keep the additional requirement that the real
                // Management Computer is still open after its listeners ran.
                //
                // IMPORTANT: clicking Management.Staff does NOT enable/spawn staff.
                // Staff permission is released only by the final Staff LESSON step via
                // TutorialStep.EnableStaffSpawningOnComplete.
                ManagementComputerController computer = FindManagementComputer();
                return computer != null && computer.IsOpen && button != null &&
                       button.gameObject.activeInHierarchy;
            }

            case "Camera.Focus":
                // Existing skeleton check. This can be strengthened later with a
                // tutorial-only camera observer without editing gameplay scripts.
                return ManagerPlayer.Active != null;

            case "Task.Open":
                // Existing skeleton check. Keep compatibility with the current HUD flow.
                return PlayerTaskGuidance.Current.Source == "Lobby1Tutorial";

            default:
                return false;
        }
    }

    private static ManagementComputerController FindManagementComputer() =>
        FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);

    public void StopWaiting()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);

        button = null;
        clicked = false;
        tutorial = null;
        step = null;
    }

    private void OnDisable() => StopWaiting();
}
