using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialSystem : MonoBehaviour
{
    public const string TutorialCompletedSaveKey = "TutorialCompleted";

    public enum TutorialStepType
    {
        ManualContinue,
        WaitForGameplayAction
    }

    public enum TutorialAction
    {
        None,
        CustomerSpawned,
        CustomerSelected,
        CustomerSeated,
        NotepadOpened,
        OrderConfirmed,
        KitchenOrderCreated,
        FoodPrepared,
        FoodDelivered,
        TableCleaned,
        PaymentCompleted,
        CameraPanned,
        TableInteracted
    }

    public enum TutorialHintMode
    {
        None,
        Swipe,
        Tap
    }

    [Serializable]
    public sealed class TutorialStep
    {
        [SerializeField] private string id;
        [SerializeField] private string speaker = "Big Boss";
        [SerializeField, TextArea(2, 5)] private string message;
        [SerializeField] private Sprite portrait;
        [SerializeField] private TutorialStepType stepType = TutorialStepType.ManualContinue;
        [SerializeField] private TutorialAction requiredAction = TutorialAction.None;
        [SerializeField] private TutorialHintMode hintMode = TutorialHintMode.None;
        [SerializeField] private Transform highlightTarget;
        [SerializeField] private bool restrictUnrelatedInteractions;

        public string Id => id;
        public string Speaker => speaker;
        public string Message => message;
        public Sprite Portrait => portrait;
        public TutorialStepType StepType => stepType;
        public TutorialAction RequiredAction => requiredAction;
        public TutorialHintMode HintMode => hintMode;
        public Transform HighlightTarget => highlightTarget;
        public bool RestrictUnrelatedInteractions => restrictUnrelatedInteractions;
    }

    public static TutorialSystem Instance { get; private set; }
    public static bool IsTutorialMode =>
        Instance != null && Instance.isActiveAndEnabled && Instance.gameObject.activeInHierarchy;
    public static bool TutorialCompleted => PlayerPrefs.GetInt(TutorialCompletedSaveKey, 0) != 0;

    [Header("Tutorial UI")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private TutorialTargetIndicator targetIndicator;
    [SerializeField] private TutorialHandIndicator handIndicator;

    [Header("Gameplay Event Sources")]
    [SerializeField] private MainCameraController cameraController;
    [SerializeField] private TapOutlineSelector tapSelector;

    [Header("Opening Control")]
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private bool suppressAutomaticSpawningDuringOpening = true;
    [SerializeField] private bool restoreAutomaticSpawningAfterOpening = true;
    [SerializeField] private bool startAutomatically = true;

    [Header("Opening Steps")]
    [SerializeField] private TutorialStep[] steps = Array.Empty<TutorialStep>();

    private int currentStepIndex = -1;
    private bool openingComplete;
    private bool rememberedAutoSpawn;
    private bool spawnStateCaptured;

    public event Action<int, TutorialStep> StepChanged;
    public event Action<TutorialAction, UnityEngine.Object> GameplayActionReported;
    public event Action<bool> InteractionRestrictionChanged;
    public event Action OpeningSequenceCompleted;

    public int CurrentStepIndex => currentStepIndex;
    public TutorialStep CurrentStep =>
        steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Length ? steps[currentStepIndex] : null;
    public int StepCount => steps != null ? steps.Length : 0;
    public bool IsOpeningComplete => openingComplete;
    public bool IsWaitingForGameplayAction =>
        CurrentStep != null && CurrentStep.StepType == TutorialStepType.WaitForGameplayAction;
    public bool AreUnrelatedInteractionsRestricted =>
        CurrentStep != null && CurrentStep.RestrictUnrelatedInteractions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TutorialSystem] Duplicate controller removed. Tutorial scenes must have one controller.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<TutorialDialogueUI>(FindObjectsInactive.Include);

        if (targetIndicator == null)
            targetIndicator = FindFirstObjectByType<TutorialTargetIndicator>(FindObjectsInactive.Include);

        if (handIndicator == null)
            handIndicator = FindFirstObjectByType<TutorialHandIndicator>(FindObjectsInactive.Include);

        if (cameraController == null)
            cameraController = FindFirstObjectByType<MainCameraController>(FindObjectsInactive.Include);

        if (tapSelector == null)
            tapSelector = FindFirstObjectByType<TapOutlineSelector>(FindObjectsInactive.Include);

        if (groupSpawner == null)
            groupSpawner = FindFirstObjectByType<GroupSpawner>(FindObjectsInactive.Include);

        SubscribeToGameplayEvents();
        CaptureAndSuppressAutomaticSpawning();
    }

    private void Start()
    {
        if (startAutomatically)
            StartTutorial();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameplayEvents();

        if (Instance == this)
            Instance = null;

        RestoreAutomaticSpawning();
    }

    public void StartTutorial()
    {
        if (!IsTutorialMode)
            return;

        openingComplete = false;
        currentStepIndex = -1;
        CaptureAndSuppressAutomaticSpawning();
        AdvanceToNextStep();
    }

    public void AdvanceManualStep()
    {
        TutorialStep step = CurrentStep;
        if (step == null || step.StepType != TutorialStepType.ManualContinue)
            return;

        AdvanceToNextStep();
    }

    public bool NotifyGameplayAction(TutorialAction action, UnityEngine.Object context = null)
    {
        GameplayActionReported?.Invoke(action, context);

        TutorialStep step = CurrentStep;
        if (step == null || step.StepType != TutorialStepType.WaitForGameplayAction)
            return false;

        if (step.RequiredAction != action)
            return false;

        if (action == TutorialAction.TableInteracted &&
            !TargetsMatch(step.HighlightTarget, context as Transform))
            return false;

        AdvanceToNextStep();
        return true;
    }

    public static bool ReportGameplayAction(TutorialAction action, UnityEngine.Object context = null)
    {
        return IsTutorialMode && Instance.NotifyGameplayAction(action, context);
    }

    public static void MarkTutorialCompleted()
    {
        PlayerPrefs.SetInt(TutorialCompletedSaveKey, 1);
        PlayerPrefs.Save();
    }

    private void AdvanceToNextStep()
    {
        currentStepIndex++;
        if (currentStepIndex >= StepCount)
        {
            CompleteOpeningSequence();
            return;
        }

        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        TutorialStep step = CurrentStep;
        if (step == null)
        {
            CompleteOpeningSequence();
            return;
        }

        if (targetIndicator != null)
        {
            if (step.HintMode == TutorialHintMode.Tap && step.HighlightTarget != null)
                targetIndicator.Show(step.HighlightTarget);
            else
                targetIndicator.Hide();
        }

        if (handIndicator != null)
        {
            if (step.HintMode == TutorialHintMode.Swipe)
                handIndicator.ShowSwipeHint();
            else if (step.HintMode == TutorialHintMode.Tap)
                handIndicator.ShowTapHint(step.HighlightTarget);
            else
                handIndicator.HideHint();
        }

        if (dialogueUI != null)
        {
            if (step.StepType == TutorialStepType.ManualContinue)
                dialogueUI.ShowManual(step.Speaker, step.Message, step.Portrait, AdvanceManualStep);
            else if (step.HintMode != TutorialHintMode.None)
                dialogueUI.HideDialogue();
            else
                dialogueUI.ShowWaiting(step.Speaker, step.Message, step.Portrait);
        }

        InteractionRestrictionChanged?.Invoke(step.RestrictUnrelatedInteractions);
        StepChanged?.Invoke(currentStepIndex, step);
    }

    private void CompleteOpeningSequence()
    {
        openingComplete = true;
        currentStepIndex = StepCount;

        if (dialogueUI != null)
            dialogueUI.HideDialogue();

        if (targetIndicator != null)
            targetIndicator.Hide();

        if (handIndicator != null)
            handIndicator.HideHint();

        InteractionRestrictionChanged?.Invoke(false);
        RestoreAutomaticSpawning();
        OpeningSequenceCompleted?.Invoke();
    }

    private void SubscribeToGameplayEvents()
    {
        if (cameraController != null)
        {
            cameraController.CameraPanned -= OnCameraPanned;
            cameraController.CameraPanned += OnCameraPanned;
        }

        if (tapSelector != null)
        {
            tapSelector.SelectionSucceeded -= OnSelectionSucceeded;
            tapSelector.SelectionSucceeded += OnSelectionSucceeded;
        }
    }

    private void UnsubscribeFromGameplayEvents()
    {
        if (cameraController != null)
            cameraController.CameraPanned -= OnCameraPanned;
        if (tapSelector != null)
            tapSelector.SelectionSucceeded -= OnSelectionSucceeded;
    }

    private void OnCameraPanned(Vector2 screenMovement)
    {
        if (screenMovement.sqrMagnitude > 0f)
            NotifyGameplayAction(TutorialAction.CameraPanned, cameraController);
    }

    private void OnSelectionSucceeded(Transform selectedTransform)
    {
        NotifyGameplayAction(TutorialAction.TableInteracted, selectedTransform);
    }

    private static bool TargetsMatch(Transform expected, Transform selected)
    {
        if (expected == null || selected == null)
            return false;

        return expected == selected || selected.IsChildOf(expected) || expected.IsChildOf(selected);
    }

    private void CaptureAndSuppressAutomaticSpawning()
    {
        if (!suppressAutomaticSpawningDuringOpening || groupSpawner == null || openingComplete)
            return;

        if (!spawnStateCaptured)
        {
            rememberedAutoSpawn = groupSpawner.AutoSpawnEnabled;
            spawnStateCaptured = true;
        }

        groupSpawner.SetAutoSpawn(false);
    }

    private void RestoreAutomaticSpawning()
    {
        if (!restoreAutomaticSpawningAfterOpening || !spawnStateCaptured || groupSpawner == null)
            return;

        groupSpawner.SetAutoSpawn(rememberedAutoSpawn);
        spawnStateCaptured = false;
    }
}
