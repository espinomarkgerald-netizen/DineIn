using System;
using UnityEngine;

[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public sealed class TutorialSystem : MonoBehaviour
{
    public const string TutorialCompletedSaveKey = "TutorialCompleted";

    public enum TutorialPhase
    {
        BasicControls, HUDTour, Management, PhysicalRestocking,
        ReturnToComputer, StaffRoles, NormalGameplay, Completed
    }

    public enum TutorialStepType
    {
        ManualContinue,
        WaitForGameplayAction,
        Success
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
        Tap,
        Zoom,
        Typing,
        Drag
    }

    [Serializable]
    public sealed class TutorialStep
    {
        [SerializeField] private string id;
        [SerializeField] private TutorialPhase phase;
        [SerializeField] private string speaker = "Big Boss";
        [SerializeField, TextArea(2, 5)] private string message;
        [SerializeField] private Sprite portrait;
        [SerializeField] private TutorialStepType stepType = TutorialStepType.ManualContinue;
        [SerializeField] private TutorialAction requiredAction = TutorialAction.None;
        [SerializeField] private TutorialHintMode hintMode = TutorialHintMode.None;
        [SerializeField] private Transform highlightTarget;
        [Tooltip("Optional tutorial-side binding for world objects created at runtime.")]
        [SerializeField] private string worldTargetKey;
        [SerializeField] private bool restrictUnrelatedInteractions;
        [SerializeField] private RectTransform uiFocusTarget;
        [Tooltip("Optional scene-local binding for UI created at runtime (e.g. AlienApproval).")]
        [SerializeField] private string uiTargetKey;
        [SerializeField] private string actionKey;
        [SerializeField] private UnityEngine.Object requiredContext;
        [SerializeField] private string objective;

        [Header("Completion Effects")]
        [Tooltip("IMPORTANT: use this only on the FINAL Staff lesson step. Staff stay blocked until this step completes.")]
        [SerializeField] private bool enableStaffSpawningOnComplete;
        [Tooltip("Use later on the Start Shift / controlled customer milestone, not during early tutorial phases.")]
        [SerializeField] private bool enableCustomerSpawningOnComplete;

        [Tooltip("Stops here until this lesson is implemented. Never auto-completes gameplay.")]
        [SerializeField] private bool isPlaceholder;

        public string Id => id;
        public TutorialPhase Phase => phase;
        public string Speaker => speaker;
        public string Message => message;
        public Sprite Portrait => portrait;
        public TutorialStepType StepType => stepType;
        public TutorialAction RequiredAction => requiredAction;
        public TutorialHintMode HintMode => hintMode;
        public Transform HighlightTarget => highlightTarget;
        public string WorldTargetKey => worldTargetKey;
        public bool RestrictUnrelatedInteractions => restrictUnrelatedInteractions;
        public RectTransform UIFocusTarget => uiFocusTarget;
        public string UITargetKey => uiTargetKey;
        public string ActionKey => actionKey;
        public UnityEngine.Object RequiredContext => requiredContext;
        public string Objective => objective;
        public bool EnableStaffSpawningOnComplete => enableStaffSpawningOnComplete;
        public bool EnableCustomerSpawningOnComplete => enableCustomerSpawningOnComplete;
        public bool IsPlaceholder => isPlaceholder;
    }

    public static TutorialSystem Instance { get; private set; }
    public static bool IsTutorialMode =>
        Instance != null && Instance.isActiveAndEnabled && Instance.gameObject.activeInHierarchy;
    public static bool TutorialCompleted => Instance != null && Instance.tutorialCompleted;

    [Header("Tutorial UI")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private TutorialTargetIndicator targetIndicator;
    [SerializeField] private TutorialHandIndicator handIndicator;
    [SerializeField] private TutorialUIFocusMask uiFocusMask;
    [SerializeField] private TutorialSceneBindings sceneBindings;

    [Header("Tutorial Input Presentation")]
    [Tooltip("Auto follows the runtime platform. Mobile and PC force tutorial wording and hint art for Editor testing.")]
    [SerializeField] private TutorialInputMode tutorialInputMode = TutorialInputMode.Auto;

    [Header("Gameplay Event Sources")]
    [SerializeField] private MainCameraController cameraController;
    [SerializeField] private TapOutlineSelector tapSelector;

    [Header("Opening Control")]
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private bool suppressAutomaticSpawningDuringOpening = true;
    [SerializeField] private bool restoreAutomaticSpawningAfterOpening = true;
    [SerializeField] private bool startAutomatically = true;

    [Header("Linear Steps (stop at the first TODO)")]
    [SerializeField] private TutorialStep[] steps = Array.Empty<TutorialStep>();

    [Header("Runtime Tracking")]
    [SerializeField] private TutorialPhase currentPhase;
    [SerializeField] private int currentStepIndex = -1;
    [SerializeField] private bool waitingForNext;
    [SerializeField] private bool waitingForPlayerAction;
    [SerializeField] private bool skeletonEndpointReached;
    [SerializeField] private bool tutorialCompleted;
    [SerializeField] private string currentObjective;
    [SerializeField] private bool allowCustomerSpawning;
    [SerializeField] private bool allowStaffSpawning;
    private RectTransform currentUIFocus;
    private Transform currentWorldFocus;
    private TutorialUIActionAdapter uiActionAdapter;
    private TutorialUIAutoScroller uiAutoScroller;
    private int presentationRevision;
    private bool openingComplete;
    private bool rememberedAutoSpawn;
    private bool spawnStateCaptured;

    public event Action<int, TutorialStep> StepChanged;
    public event Action<TutorialAction, UnityEngine.Object> GameplayActionReported;
    public event Action<bool> InteractionRestrictionChanged;
    public event Action OpeningSequenceCompleted;
    public event Action<TutorialPhase> PhaseChanged;
    public event Action<string> ObjectiveChanged;
    public event Action<bool, bool> SpawnPermissionsChanged;
    public event Action<TutorialStep> StepCompleted;
    public event Action TutorialCompletedChanged;
    public event Action SkeletonEndpointReached;

    public TutorialPhase CurrentPhase => currentPhase;
    public bool IsWaitingForNext => waitingForNext;
    public bool IsSkeletonEndpointReached => skeletonEndpointReached;
    public bool IsComplete => tutorialCompleted;
    public bool AllowCustomerSpawning => allowCustomerSpawning;
    public bool AllowStaffSpawning => allowStaffSpawning;
    public string CurrentObjective => currentObjective;
    public int CurrentStepIndex => currentStepIndex;
    public TutorialStep CurrentStep =>
        steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Length ? steps[currentStepIndex] : null;
    public int StepCount => steps != null ? steps.Length : 0;
    public bool IsOpeningComplete => openingComplete;
    public bool IsWaitingForGameplayAction =>
        waitingForPlayerAction;
    public bool AreUnrelatedInteractionsRestricted =>
        !skeletonEndpointReached && !tutorialCompleted && isActiveAndEnabled &&
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
        TutorialInputTerminology.Configure(tutorialInputMode);
        if (gameObject.scene.name == "Lobby1Tutorial" && GetComponent<TutorialRestockFlowBridge>() == null)
            gameObject.AddComponent<TutorialRestockFlowBridge>();
        if (gameObject.scene.name == "Lobby1Tutorial" && GetComponent<TutorialCustomerFlowBridge>() == null)
            gameObject.AddComponent<TutorialCustomerFlowBridge>();
        if (gameObject.scene.name == "Lobby1Tutorial" && GetComponent<TutorialShiftReadinessBridge>() == null)
            gameObject.AddComponent<TutorialShiftReadinessBridge>();
        if (gameObject.scene.name == "Lobby1Tutorial" && GetComponent<TutorialCompletionFlow>() == null)
            gameObject.AddComponent<TutorialCompletionFlow>();

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

        if (sceneBindings == null)
            sceneBindings = GetComponent<TutorialSceneBindings>();
        if (sceneBindings == null)
            sceneBindings = gameObject.AddComponent<TutorialSceneBindings>();
        uiActionAdapter = GetComponent<TutorialUIActionAdapter>();
        if (uiActionAdapter == null) uiActionAdapter = gameObject.AddComponent<TutorialUIActionAdapter>();
        uiAutoScroller = GetComponent<TutorialUIAutoScroller>();
        if (uiAutoScroller == null) uiAutoScroller = gameObject.AddComponent<TutorialUIAutoScroller>();
        if (GetComponent<TutorialCameraZoomObserver>() == null)
            gameObject.AddComponent<TutorialCameraZoomObserver>();
        if (GetComponent<TutorialBoothAvailability>() == null)
            gameObject.AddComponent<TutorialBoothAvailability>();
        if (GetComponent<TutorialStaffSpawnGate>() == null)
            gameObject.AddComponent<TutorialStaffSpawnGate>();
        if (GetComponent<TutorialRestaurantCatalogContext>() == null)
            gameObject.AddComponent<TutorialRestaurantCatalogContext>();
        if (GetComponent<TutorialDayContext>() == null)
            gameObject.AddComponent<TutorialDayContext>();
        if (uiFocusMask == null && dialogueUI != null)
            uiFocusMask = TutorialUIFocusMask.Create(dialogueUI.transform.parent);

        SubscribeToGameplayEvents();
        CaptureAndSuppressAutomaticSpawning();
    }

    private void Start()
    {
        if (startAutomatically)
            StartTutorial();
        else if (CurrentStep != null)
            ShowCurrentStep(); // Rebind an inspector-selected step after dialogue Awake.
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameplayEvents();
        TutorialInputTerminology.Configure(TutorialInputMode.Auto);

        if (Instance == this)
            Instance = null;

        RestoreAutomaticSpawning();
    }

    private void OnDisable()
    {
        ClearGuidance(false);
        PlayerTaskGuidance.ClearTask("Lobby1Tutorial");
        waitingForNext = waitingForPlayerAction = false;
        InteractionRestrictionChanged?.Invoke(false);
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        SubscribeToGameplayEvents();
        if (CurrentStep != null && !tutorialCompleted)
            ShowCurrentStep();
    }

    public void StartTutorial()
    {
        if (!IsTutorialMode)
            return;

        openingComplete = false;
        tutorialCompleted = skeletonEndpointReached = false;
        waitingForNext = waitingForPlayerAction = false;
        SetSpawnPermissions(false, false);
        TutorialUIActionAdapter.ClearSessionState();
        SetObjective(string.Empty);
        currentStepIndex = -1;
        CaptureAndSuppressAutomaticSpawning();
        AdvanceToNextStep();
    }

    public void AdvanceManualStep()
    {
        TutorialStep step = CurrentStep;
        if (step == null || !waitingForNext)
            return;

        waitingForNext = false; // Debounce NEXT while the authored panel animates out.
        Action continuation = step.StepType == TutorialStepType.WaitForGameplayAction
            ? BeginWaitingForAction
            : CompleteCurrentStepAndAdvance;
        if (dialogueUI != null)
            dialogueUI.HideDialogueAnimated(continuation);
        else
            continuation();
    }

    public bool NotifyGameplayAction(TutorialAction action, UnityEngine.Object context = null)
    {
        GameplayActionReported?.Invoke(action, context);

        TutorialStep step = CurrentStep;
        if (step == null || !waitingForPlayerAction || !string.IsNullOrEmpty(step.ActionKey))
            return false;

        if (action == TutorialAction.None || step.RequiredAction != action || !ContextMatches(step, context))
            return false;

        if (action == TutorialAction.TableInteracted &&
            !TargetsMatch(step.HighlightTarget, context as Transform))
            return false;

        CompleteCurrentStepAndAdvance();
        return true;
    }

    // Tutorial-side adapters subscribe to REAL system events, then report their key here.
    // A future Button.onClick relay can use this without changing its gameplay script.
    public bool NotifyAction(string actionKey, UnityEngine.Object context = null)
    {
        TutorialStep step = CurrentStep;
        bool keyMatches = step != null && string.Equals(step.ActionKey, actionKey, StringComparison.Ordinal);
        bool contextMatches = step != null && ContextMatches(step, context);
        if (string.Equals(actionKey, "Management.MenuSavePrice", StringComparison.Ordinal) ||
            (step != null && string.Equals(step.ActionKey, "Management.MenuSavePrice", StringComparison.Ordinal)))
            Debug.Log($"[TutorialMenu] NotifyAction received at index {currentStepIndex}: sent={actionKey}, " +
                      $"expected={(step != null ? step.ActionKey : "<no step>")}, waiting={waitingForPlayerAction}, " +
                      $"keyMatch={keyMatches}, contextMatch={contextMatches}", this);
        if (!waitingForPlayerAction || step == null || string.IsNullOrEmpty(actionKey) ||
            !keyMatches || !contextMatches)
            return false;
        CompleteCurrentStepAndAdvance();
        return true;
    }

    private static bool ContextMatches(TutorialStep step, UnityEngine.Object context)
    {
        if (step.RequiredContext == null) return true;
        if (step.RequiredContext == context) return true;
        Transform expected = ObjectTransform(step.RequiredContext);
        Transform actual = ObjectTransform(context);
        return TargetsMatch(expected, actual);
    }

    private static Transform ObjectTransform(UnityEngine.Object value) =>
        value is GameObject go ? go.transform : value is Component component ? component.transform : null;

    public static bool ReportGameplayAction(TutorialAction action, UnityEngine.Object context = null)
    {
        return IsTutorialMode && Instance.NotifyGameplayAction(action, context);
    }

    public static void MarkTutorialCompleted()
    {
        // Only the implemented final step may complete the tutorial; TODOs never do.
        if (!IsTutorialMode || Instance.CurrentPhase != TutorialPhase.Completed ||
            Instance.CurrentStep == null || Instance.CurrentStep.IsPlaceholder) return;
        Instance.tutorialCompleted = true;
        PlayerPrefs.SetInt(TutorialCompletedSaveKey, 1);
        PlayerPrefs.Save();
        Instance.SetSpawnPermissions(true, true);
        Instance.TutorialCompletedChanged?.Invoke();
        Instance.CompleteOpeningSequence();
    }

    public void SetObjective(string message)
    {
        currentObjective = message ?? string.Empty;
        ObjectiveChanged?.Invoke(currentObjective);
        PlayerTaskGuidance.SetTask("Lobby1Tutorial", "tutorial_objective", currentObjective,
            string.Empty, 10000, null, PlayerTaskCategory.None);
    }

    public void SetSpawnPermissions(bool customers, bool staff)
    {
        if (allowCustomerSpawning == customers && allowStaffSpawning == staff)
            return;

        allowCustomerSpawning = customers;
        allowStaffSpawning = staff;
        SpawnPermissionsChanged?.Invoke(allowCustomerSpawning, allowStaffSpawning);

        // IMPORTANT: TutorialSystem owns only permission state. Tutorial-only gates/adapters
        // apply it to scene instances. Shared customer/staff gameplay scripts stay untouched.
    }

    private void CompleteCurrentStepAndAdvance()
    {
        TutorialStep completed = CurrentStep;
        if (completed == null)
            return;

        bool customers = allowCustomerSpawning || completed.EnableCustomerSpawningOnComplete;
        bool staff = allowStaffSpawning || completed.EnableStaffSpawningOnComplete;
        if (customers != allowCustomerSpawning || staff != allowStaffSpawning)
            SetSpawnPermissions(customers, staff);

        StepCompleted?.Invoke(completed);
        AdvanceToNextStep();
    }

    private void AdvanceToNextStep()
    {
        ClearGuidance(true);
        waitingForNext = waitingForPlayerAction = false;
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

        ClearGuidance(true);
        waitingForNext = waitingForPlayerAction = false;
        if (currentPhase != step.Phase)
        {
            currentPhase = step.Phase;
            PhaseChanged?.Invoke(currentPhase);
        }
        if (!string.IsNullOrEmpty(step.Objective)) SetObjective(step.Objective);
        if (step.IsPlaceholder)
        {
            uiFocusMask?.Hide();
            skeletonEndpointReached = true;
            openingComplete = true;
            PlayerTaskGuidance.ClearTask("Lobby1Tutorial");
            dialogueUI?.ShowWaiting(step.Speaker, "That's all for now.", step.Portrait);
            InteractionRestrictionChanged?.Invoke(false);
            StepChanged?.Invoke(currentStepIndex, step);
            SkeletonEndpointReached?.Invoke();
            return;
        }
        skeletonEndpointReached = false;
        sceneBindings.PrepareForStep(step.UITargetKey);
        currentUIFocus = step.UIFocusTarget != null ? step.UIFocusTarget : sceneBindings.ResolveUI(step.UITargetKey);
        currentWorldFocus = step.HighlightTarget != null
            ? step.HighlightTarget
            : sceneBindings.ResolveWorld(step.WorldTargetKey);
        sceneBindings.BeginUIFocus(currentUIFocus);
        if ((!string.IsNullOrEmpty(step.UITargetKey) || step.UIFocusTarget != null) && currentUIFocus == null)
        {
            uiFocusMask?.Hide();
            Debug.LogError("[TutorialSystem] Missing UI target for step " + step.Id, this);
            dialogueUI?.ShowWaiting(step.Speaker, "This lesson's target is unavailable.", step.Portrait);
            return;
        }
        if (step.Phase == TutorialPhase.Completed)
        {
            MarkTutorialCompleted();
            return;
        }
        if (currentUIFocus != null)
        {
            int revision = ++presentationRevision;
            uiFocusMask?.Hold();
            uiAutoScroller.Prepare(currentUIFocus, () =>
            {
                if (revision != presentationRevision || CurrentStep != step)
                    return;
                if (uiFocusMask != null)
                    uiFocusMask.TransitionTo(currentUIFocus, false, () =>
                    {
                        if (revision == presentationRevision && CurrentStep == step)
                            PresentCurrentStep(step, true);
                    });
                else
                    PresentCurrentStep(step, true);
            });
            return;
        }
        uiFocusMask?.Hide();
        PresentCurrentStep(step, false);
    }

    private void PresentCurrentStep(TutorialStep step, bool focusReady)
    {
        if (step.StepType == TutorialStepType.WaitForGameplayAction && string.IsNullOrEmpty(step.Message))
            BeginWaitingForAction(); // Existing Basic Controls already has separate explanation steps.
        else
        {
            if (!focusReady) ShowFocus(false);
            waitingForNext = true;
            dialogueUI?.ShowManual(step.Speaker,
                TutorialInputTerminology.Resolve(step.Message), step.Portrait, AdvanceManualStep);
            dialogueUI?.SetFocusTarget(currentUIFocus);
        }
        InteractionRestrictionChanged?.Invoke(step.RestrictUnrelatedInteractions);
        StepChanged?.Invoke(currentStepIndex, step);
    }

    public void BeginWaitingForAction()
    {
        TutorialStep step = CurrentStep;
        if (step == null || step.IsPlaceholder || step.StepType != TutorialStepType.WaitForGameplayAction ||
            (!waitingForNext && waitingForPlayerAction)) return;
        waitingForNext = false;
        waitingForPlayerAction = true;
        dialogueUI?.HideDialogue();
        ShowFocus(true);
        if (step.HintMode == TutorialHintMode.Drag)
        {
            if (string.Equals(step.ActionKey, "Restock.BoxActionsHidden", StringComparison.Ordinal))
                handIndicator?.ShowSmallDragHint(currentWorldFocus);
            else
                handIndicator?.ShowDragHint(currentUIFocus, currentWorldFocus);
            return;
        }
        // UI observers and visual hints run together. The observer verifies the
        // real click/state; it must not suppress the hand/cursor demonstration.
        uiActionAdapter?.Begin(this, currentUIFocus);
        if (step.HintMode == TutorialHintMode.Swipe) handIndicator?.ShowSwipeHint();
        else if (step.HintMode == TutorialHintMode.Tap)
            handIndicator?.ShowTapHint(currentUIFocus != null ? currentUIFocus : currentWorldFocus);
        else if (step.HintMode == TutorialHintMode.Zoom)
            handIndicator?.ShowZoomHint(TutorialInputTerminology.IsMobile);
        else if (step.HintMode == TutorialHintMode.Typing)
            handIndicator?.ShowTypingHint(currentUIFocus);
    }

    private void ShowFocus(bool allowTargetInput)
    {
        if (CurrentStep != null && CurrentStep.HintMode == TutorialHintMode.Drag)
        {
            // A hotbar-to-world drag must be able to leave the UI target and reach
            // the shelf. The hand/cursor and world indicator provide focus without
            // a fullscreen raycast surface intercepting the real drop.
            uiFocusMask?.Hide();
            if (currentWorldFocus != null) targetIndicator?.Show(currentWorldFocus);
            return;
        }
        if (currentUIFocus != null) uiFocusMask?.Show(currentUIFocus, allowTargetInput);
        else if (currentWorldFocus != null) targetIndicator?.Show(currentWorldFocus);
    }

    private void ClearGuidance(bool preserveMask = false)
    {
        presentationRevision++;
        currentWorldFocus = null;
        uiAutoScroller?.Cancel();
        dialogueUI?.SetFocusTarget(null);
        uiActionAdapter?.StopWaiting();
        if (sceneBindings != null) sceneBindings.EndUIFocus();
        if (uiFocusMask != null)
        {
            if (preserveMask) uiFocusMask.Hold();
            else uiFocusMask.Hide();
        }
        if (targetIndicator != null) targetIndicator.Hide();
        if (handIndicator != null) handIndicator.HideHint();
        if (dialogueUI != null) dialogueUI.HideDialogue();
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
        waitingForNext = waitingForPlayerAction = false;
        uiFocusMask?.Hide();
        // Reaching the end of a skeleton is not full tutorial completion.
        if (tutorialCompleted) RestoreAutomaticSpawning();
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
