using UnityEngine;

/// <summary>
/// Completes Camera.Zoom only after the real MainCameraController target zoom changes.
/// It reads the existing public camera state and does not replace or simulate input.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialCameraZoomObserver : MonoBehaviour
{
    [SerializeField, Min(0.001f)] private float requiredZoomChange = 0.02f;

    private TutorialSystem tutorial;
    private MainCameraController cameraController;
    private TutorialSystem.TutorialStep observedStep;
    private float startingZoom;

    private void Awake()
    {
        tutorial = GetComponent<TutorialSystem>();
        cameraController = FindFirstObjectByType<MainCameraController>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (tutorial == null || cameraController == null)
            return;

        TutorialSystem.TutorialStep step = tutorial.CurrentStep;
        bool isZoomStep = tutorial.IsWaitingForGameplayAction && step != null &&
            step.ActionKey == "Camera.Zoom";
        if (!isZoomStep)
        {
            observedStep = null;
            return;
        }

        if (observedStep != step)
        {
            observedStep = step;
            startingZoom = cameraController.GetTargetOrthographicSize();
            return;
        }

        if (Mathf.Abs(cameraController.GetTargetOrthographicSize() - startingZoom) < requiredZoomChange)
            return;

        observedStep = null;
        tutorial.NotifyAction("Camera.Zoom", cameraController);
    }
}
