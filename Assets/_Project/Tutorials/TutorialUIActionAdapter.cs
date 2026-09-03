using UnityEngine;
using UnityEngine.UI;

// Listens to the real highlighted button. Opening a UI additionally waits for
// its public state, so a click that cannot open it never completes the lesson.
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
        if (owner == null || target == null || string.IsNullOrEmpty(owner.CurrentStep?.ActionKey)) return;
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
        if (tutorial == null || tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction) return;
        clicked = true;
        clickedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        // Wait a frame for all of the button's original gameplay listeners.
        if (!clicked || Time.frameCount <= clickedFrame || tutorial == null ||
            tutorial.CurrentStep != step || !tutorial.IsWaitingForGameplayAction) return;
        bool completed;
        switch (step.ActionKey)
        {
            case "Newspaper.Open":
                completed = FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == true;
                break;
            case "Newspaper.Close":
                completed = FindFirstObjectByType<DailyNewspaperPresenter>()?.IsOpen == false;
                break;
            case "Computer.Open":
                completed = FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include)?.IsOpen == true;
                break;
            case "Management.Dashboard":
                var computer = FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);
                completed = computer != null && computer.IsOpen && computer.AppWindow != null && computer.AppWindow.gameObject.activeInHierarchy;
                break;
            case "Camera.Focus": completed = ManagerPlayer.Active != null; break;
            case "Task.Open": completed = PlayerTaskGuidance.Current.Source == "Lobby1Tutorial"; break;
            default: completed = false; break;
        }
        if (!completed) return;
        TutorialSystem owner = tutorial;
        string key = step.ActionKey;
        StopWaiting();
        owner.NotifyAction(key);
    }

    public void StopWaiting()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
        button = null;
        clicked = false;
        tutorial = null;
        step = null;
    }

    private void OnDisable() => StopWaiting();
}
