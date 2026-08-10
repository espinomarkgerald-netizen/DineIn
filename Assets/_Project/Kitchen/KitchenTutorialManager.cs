using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Events;

[System.Serializable]
public class TutorialStep {
    [HideInInspector]
    public string inspectorTitle = "New Step";

    [Header("Organization")]
    public string stepName = "New Step";
    public bool isNewChapter;

    [TextArea(3, 5)]
    public string promptText;

    [Header("Targeting")]
    public Transform worldFocusObject;
    public RectTransform uiFocusObject;

    [Header("3D Arrow Settings")]
    [Tooltip("X/Z move it side-to-side, Y moves it up and down.")]
    public Vector3 worldArrowOffset = new Vector3(0, 3f, 0);

    [Header("UI Arrow Settings")]
    public Vector2 uiArrowOffset = new Vector2(0, 0);
    public float uiArrowRotation = 0f;

    [Header("Camera Control")]
    public bool moveCamera;
    public Transform cameraTarget;
    [Range(1f, 10f)]
    public float cameraSpeed = 3f;

    [Header("Triggers & Automation")]
    public string requiredItemName;
    public bool advanceOnInteract;
    public bool advanceByTimer;

    [Range(0f, 10f)]
    public float delayDuration = 1.0f;

    public bool isAutoDemonstration;
    public KitchenPlayerMovement actorToControl;

    [Header("Visuals")]
    public bool useDarkOverlay;
    public bool hideDialogueBox;

    [Header("Custom UI For This Step")]
    public GameObject customStepUI;

    [Header("Custom Events")]
    public UnityEvent onStepStart;
    public UnityEvent onStepComplete;
}

public class KitchenTutorialManager : MonoBehaviour {
    public static KitchenTutorialManager Instance;

    public List<TutorialStep> steps = new List<TutorialStep>();
    private int currentStepIndex = 0;
    private bool isWaiting = false;

    [Header("UI References")]
    public GameObject dialogueBoxPanel;
    public TextMeshProUGUI tutorialText;
    public GameObject darkOverlayPanel;
    public GameObject nextButton;

    [Header("Completion Popup")]
    public TutorialCompletePopup completionPopup;

    [Header("Camera References")]
    public Transform cameraRig;
    private Coroutine cameraCoroutine;

    [Header("The Arrows")]
    public GameObject worldArrowPrefab;
    public RectTransform uiArrowAnchor;
    private GameObject activeWorldArrow;

    public TutorialStep GetCurrentStep() {
        if (steps.Count > 0 && currentStepIndex < steps.Count) return steps[currentStepIndex];
        return null;
    }

    void OnValidate() {
        if (steps != null) {
            foreach (var step in steps) {
                if (step != null) step.inspectorTitle = step.isNewChapter ? "[CHAPTER] " + step.stepName : step.stepName;
            }
        }
    }

    void Awake() { Instance = this; }

    void Start() {
        if (worldArrowPrefab != null) {
            activeWorldArrow = Instantiate(worldArrowPrefab);
            activeWorldArrow.SetActive(false);
        }
        if (uiArrowAnchor != null) uiArrowAnchor.gameObject.SetActive(false);

        foreach (var s in steps) {
            if (s.customStepUI != null) s.customStepUI.SetActive(false);
        }

        if (steps.Count > 0) ShowStep(0);
    }

    public bool IsInteractionAllowed(Transform clickedObject) {
        if (steps.Count == 0) return true;
        TutorialStep current = GetCurrentStep();
        if (current == null || current.worldFocusObject == null) return true;
        return clickedObject.IsChildOf(current.worldFocusObject);
    }

    public void ReportInteraction(Transform interactedObject) {
        if (isWaiting) return;
        TutorialStep current = GetCurrentStep();
        if (current != null && current.advanceOnInteract && current.worldFocusObject != null) {
            if (interactedObject.IsChildOf(current.worldFocusObject)) {
                StartCoroutine(WaitAndMove(current.delayDuration));
            }
        }
    }

    public void AdvanceFromUI() {
        if (isWaiting) return;
        TutorialStep current = GetCurrentStep();
        if (current != null && current.advanceOnInteract) {
            StartCoroutine(WaitAndMove(current.delayDuration));
        }
    }

    private IEnumerator WaitAndMove(float time) {
        isWaiting = true;
        yield return new WaitForSeconds(time);
        isWaiting = false;
        Button_NextStep();
    }

    public void Button_NextStep() {
        StopAllCoroutines();
        isWaiting = false;
        HaltAllChefs();

        if (steps.Count > 0 && currentStepIndex < steps.Count) {
            steps[currentStepIndex].onStepComplete?.Invoke();
        }

        if (currentStepIndex < steps.Count - 1) {
            currentStepIndex++;
            ShowStep(currentStepIndex);
        } else {
            EndTutorial();
        }
    }

    public void Button_SkipChapter() {
        StopAllCoroutines();
        isWaiting = false;
        HaltAllChefs();
        for (int i = currentStepIndex + 1; i < steps.Count; i++) {
            if (steps[i].isNewChapter) {
                currentStepIndex = i;
                ShowStep(currentStepIndex);
                return;
            }
        }
        EndTutorial();
    }

    private void HaltAllChefs() {
        var chefs = FindObjectsOfType<KitchenPlayerMovement>();
        foreach (var c in chefs) c.StopMovement();
    }

    private void ShowStep(int index) {
        TutorialStep step = steps[index];
        if (dialogueBoxPanel != null) dialogueBoxPanel.SetActive(!step.hideDialogueBox);
        if (tutorialText != null) tutorialText.text = step.promptText;
        if (darkOverlayPanel != null) darkOverlayPanel.SetActive(step.useDarkOverlay);

        if (nextButton != null) {
            nextButton.SetActive(!step.advanceOnInteract);
        }

        if (step.moveCamera && step.cameraTarget != null && cameraRig != null) {
            if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
            cameraCoroutine = StartCoroutine(SmoothCameraMove(step.cameraTarget.position, step.cameraSpeed));
        }

        foreach (var s in steps) {
            if (s.customStepUI != null) s.customStepUI.SetActive(false);
        }

        if (step.customStepUI != null) {
            step.customStepUI.SetActive(true);
        }

        if (activeWorldArrow != null) {
            activeWorldArrow.SetActive(step.worldFocusObject != null);
            if (step.worldFocusObject != null) {
                activeWorldArrow.transform.position = step.worldFocusObject.position + step.worldArrowOffset;
            }
        }

        if (uiArrowAnchor != null) {
            uiArrowAnchor.gameObject.SetActive(step.uiFocusObject != null);
            if (step.uiFocusObject != null) {
                uiArrowAnchor.position = step.uiFocusObject.position;
                uiArrowAnchor.anchoredPosition += step.uiArrowOffset;
                uiArrowAnchor.localEulerAngles = new Vector3(0, 0, step.uiArrowRotation);
            }
        }

        if (step.isAutoDemonstration && step.actorToControl != null && step.worldFocusObject != null) {
            step.actorToControl.ForceMoveToStation(step.worldFocusObject);
        }

        step.onStepStart?.Invoke();

        if (step.advanceByTimer) {
            StartCoroutine(WaitAndMove(step.delayDuration));
        }
    }

    private IEnumerator SmoothCameraMove(Vector3 targetPos, float speed) {
        while (Vector3.Distance(cameraRig.position, targetPos) > 0.01f) {
            cameraRig.position = Vector3.Lerp(cameraRig.position, targetPos, Time.deltaTime * speed);
            yield return null;
        }
        cameraRig.position = targetPos;
    }

    private void EndTutorial() {
        if (activeWorldArrow != null) activeWorldArrow.SetActive(false);
        if (uiArrowAnchor != null) uiArrowAnchor.gameObject.SetActive(false);
        if (dialogueBoxPanel != null) dialogueBoxPanel.SetActive(false);
        if (darkOverlayPanel != null) darkOverlayPanel.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);

        foreach (var s in steps) {
            if (s.customStepUI != null) s.customStepUI.SetActive(false);
        }

        if (completionPopup != null) completionPopup.Show();
    }
}
