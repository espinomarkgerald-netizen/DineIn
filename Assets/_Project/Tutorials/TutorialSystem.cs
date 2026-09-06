using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

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
        Drag,
        Hold
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

    [Header("Lobby Navigation")]
    [SerializeField] private NavMeshSurface lobbyNavigationSurface;
    [SerializeField] private NavMeshData lobbyNavigationData;
    private bool lobbyNavigationReady;

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

    [Header("Tutorial Debug")]
    [SerializeField] private bool debugStartEnabled = false;
    [SerializeField] private TutorialPhase debugStartPhase = TutorialPhase.BasicControls;
    [SerializeField, Min(0)] private int debugStepOffsetInPhase = 0;
    [SerializeField, Min(-1)] private int debugStartGlobalStepIndex = -1;
    [SerializeField] private int debugResolvedGlobalStepIndex = -1;

    [Header("Tutorial Visual Debug Tuning")]
    [SerializeField, Range(.25f, 3f)] private float debugCursorScale = 1f;
    [SerializeField, Range(.25f, 3f)] private float debugMouseScale = 1f;
    [SerializeField, Range(.25f, 3f)] private float debugHandScale = 1f;
    [SerializeField, Range(0f, 40f)] private float debugFocusPadding = 8f;
    [SerializeField, Range(1f, 1.2f)] private float debugBigBossBopScale = 1.06f;
    [SerializeField, Range(.05f, 1f)] private float debugBigBossBopDuration = .22f;
    private bool debugSession;
    private Coroutine debugStartRoutine;
    public bool IsDebugSession => debugSession;

    [Header("Runtime Tracking")]
    [SerializeField] private string currentStepId;
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
        InitializeLobbyNavigation();
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

    private bool InitializeLobbyNavigation()
    {
        if (gameObject.scene.name != "Lobby1Tutorial") return true;

        // A duplicated/open Editor scene can retain a cleared Surface data field.
        // Keep the authored bake independently; never build navigation at runtime.
        if (lobbyNavigationSurface == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                    if (surface.name == "NavMesh" && surface.agentTypeID == 0)
                        lobbyNavigationSurface = surface;
        // The existing scene Surface is authoritative when it already has a bake.
        if (lobbyNavigationSurface != null && lobbyNavigationSurface.navMeshData != null)
            lobbyNavigationData = lobbyNavigationSurface.navMeshData;
#if UNITY_EDITOR
        // Covers an already-open scene when these new serialized fields are introduced.
        if (lobbyNavigationData == null)
            lobbyNavigationData = UnityEditor.AssetDatabase.LoadAssetAtPath<NavMeshData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath("a4ac8b90dfa28d9458000852c17d5a6b"));
#endif
        if (lobbyNavigationSurface == null || lobbyNavigationSurface.gameObject.scene != gameObject.scene ||
            !lobbyNavigationSurface.isActiveAndEnabled || lobbyNavigationData == null)
        {
            Debug.LogError("[Tutorial] Lobby navigation is missing or disabled. Assign the authored lobby bake before starting.", this);
            return false;
        }

        if (lobbyNavigationSurface.navMeshData != lobbyNavigationData)
        {
            lobbyNavigationSurface.RemoveData();
            lobbyNavigationSurface.navMeshData = lobbyNavigationData;
        }
        // Idempotent; the scene-owned Surface removes its own instance on unload.
        lobbyNavigationSurface.AddData();
        return true;
    }

    private System.Collections.IEnumerator Start()
    {
        // Finish scene Awake/OnEnable/Start before sampling or attaching characters.
        yield return null;
        if (!InitializeLobbyNavigation()) yield break;
        lobbyNavigationReady = gameObject.scene.name != "Lobby1Tutorial" || AttachLobbyAgents();
        if (!lobbyNavigationReady) yield break;
        if (startAutomatically)
            StartTutorial();
        else if (CurrentStep != null)
            ShowCurrentStep();
    }

    private bool AttachLobbyAgents()
    {
        // Diagnostic only: this package exposes its handle internally. The Surface
        // remains the sole owner; never add/remove an independent instance here.
        object handle = typeof(NavMeshSurface).GetField("m_NavMeshDataInstance",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(lobbyNavigationSurface);
        string registered = handle is NavMeshDataInstance instance ? instance.valid.ToString() : "unavailable";
        Debug.Log($"[TutorialNav] Surface active={lobbyNavigationSurface.isActiveAndEnabled}, data={lobbyNavigationSurface.navMeshData.name}, " +
            $"handleValid={registered}, type={lobbyNavigationSurface.agentTypeID}, position={lobbyNavigationSurface.transform.position}, " +
            $"rotation={lobbyNavigationSurface.transform.rotation.eulerAngles}, bakePosition={lobbyNavigationData.position}, bakeRotation={lobbyNavigationData.rotation.eulerAngles}", this);

        int active = 0, attached = 0;
        bool loggedStaff = false;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            {
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                Vector3 origin = agent.transform.position;
                bool sampled = NavMesh.SamplePosition(origin, out NavMeshHit hit, 2f, filter);
                bool warped = false;
                if (agent.isActiveAndEnabled)
                {
                    active++;
                    // One bounded startup attempt, only AFTER a matching sample succeeds.
                    if (!agent.isOnNavMesh && sampled) warped = agent.Warp(hit.position);
                    if (sampled && agent.isOnNavMesh) attached++;
                }
                bool player = agent.GetComponent<ManagerPlayer>() != null;
                if (player || !loggedStaff)
                {
                    if (!player) loggedStaff = true;
                    Debug.Log($"[TutorialNav] {(player ? "Player" : "Staff")} {agent.name}: position={origin}, " +
                        $"enabled={agent.isActiveAndEnabled}, type={agent.agentTypeID}, areaMask={agent.areaMask}, " +
                        $"sample={sampled}, sampledPosition={hit.position}, warp={warped}, onMeshAfter={agent.isOnNavMesh}", agent);
                }
            }
        bool ready = active > 0 && attached == active;
        Debug.Log($"[TutorialNav] Startup attached={attached}/{active}, ready={ready}, timeScale={Time.timeScale}", this);
        if (!ready)
            Debug.LogError("[TutorialNav] Navigation sampling/attachment failed; tutorial progression has not started. Check the one-time surface and agent diagnostics above.", this);
        return ready;
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
        if (debugStartRoutine != null) { StopCoroutine(debugStartRoutine); debugStartRoutine = null; }
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
        if (!IsTutorialMode || !lobbyNavigationReady)
            return;

        openingComplete = false;
        tutorialCompleted = skeletonEndpointReached = false;
        waitingForNext = waitingForPlayerAction = false;
        SetSpawnPermissions(false, false);
        TutorialUIActionAdapter.ClearSessionState();
        SetObjective(string.Empty);
        currentStepIndex = -1;
        CaptureAndSuppressAutomaticSpawning();
#if UNITY_EDITOR
        handIndicator?.ApplyDebugTuning(debugCursorScale, debugMouseScale, debugHandScale);
        uiFocusMask?.ApplyDebugPadding(debugFocusPadding);
        dialogueUI?.ApplyDebugBop(debugBigBossBopScale, debugBigBossBopDuration);
        if (debugStartRoutine != null) StopCoroutine(debugStartRoutine);
        if (debugStartEnabled && !TutorialGameModeEntry.IsMenuLaunch)
        {
            int resolved = ResolveDebugStart();
            if (resolved >= 0)
            {
                debugSession = true;
                ClearGuidance();
                debugStartRoutine = StartCoroutine(StartAtDebugStep(resolved));
                return;
            }
        }
#endif
        AdvanceToNextStep();
    }

#if UNITY_EDITOR
    private int ResolveDebugStart()
    {
        if (debugStartGlobalStepIndex >= 0 && debugStartGlobalStepIndex < StepCount)
            return debugStartGlobalStepIndex;
        if (debugStartGlobalStepIndex < 0)
        {
            int offset = Mathf.Max(0, debugStepOffsetInPhase);
            for (int i = 0; i < StepCount; i++)
                if (steps[i] != null && steps[i].Phase == debugStartPhase && offset-- == 0) return i;
        }
        Debug.LogWarning("[Tutorial Debug] Requested step does not exist; starting normally.", this);
        return -1;
    }

    private System.Collections.IEnumerator StartAtDebugStep(int index)
    {
        // Let tutorial save isolation and scene Start methods finish first.
        yield return null;
        if (!lobbyNavigationReady) { debugStartRoutine = null; yield break; }
        TutorialPhase phase = steps[index].Phase;
        if (phase >= TutorialPhase.PhysicalRestocking)
            GetComponent<TutorialRestockFlowBridge>()?.Bootstrap();
        if (phase >= TutorialPhase.ReturnToComputer)
        {
            GetComponent<TutorialDayContext>()?.PrepareCustomerMenu();
            SetSpawnPermissions(phase == TutorialPhase.StaffRoles, true);
            if (phase >= TutorialPhase.StaffRoles && phase < TutorialPhase.Completed)
                GameDayManager.Instance?.StartShift();
        }
        if (phase == TutorialPhase.NormalGameplay && steps[index].Id.StartsWith("practice_staff", StringComparison.Ordinal))
            GetComponent<TutorialBoothAvailability>()?.OpenPracticeBooths();
        debugResolvedGlobalStepIndex = index;
        currentStepIndex = index;
        Debug.Log($"[Tutorial Debug] Starting global index {index}: {steps[index].Id} ({phase}). Action-dependent objects must exist for mid-lesson jumps.", this);
        ShowCurrentStep(); // No increment: present exactly the resolved serialized step.
        debugStartRoutine = null;
    }
#endif

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
        if (!Instance.IsDebugSession)
        {
            PlayerPrefs.SetInt(TutorialCompletedSaveKey, 1);
            PlayerPrefs.Save();
        }
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
        restockTargetPending = false;
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
        currentStepId = step != null ? step.Id : string.Empty;
        if (step == null)
        {
            CompleteOpeningSequence();
            return;
        }

        restockTargetPending = false;
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
            if (step.Phase == TutorialPhase.PhysicalRestocking) { restockTargetPending = true; return; }
            Debug.LogError("[TutorialSystem] Missing UI target for step " + step.Id, this);
            dialogueUI?.ShowWaiting(step.Speaker, "This lesson's target is unavailable.", step.Portrait);
            return;
        }
        if (step.Phase == TutorialPhase.Completed)
        {
            MarkTutorialCompleted();
            return;
        }
        if (TryFrameWorldTarget(() => PresentResolvedStep(step))) return;
        PresentResolvedStep(step);
    }

    private void PresentResolvedStep(TutorialStep step)
    {
        if (CurrentStep != step) return;
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
            if (step.Phase == TutorialPhase.PhysicalRestocking)
                ShowRestockHint(step);
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
        if (step.HintMode == TutorialHintMode.Hold) handIndicator?.ShowHoldHint(currentUIFocus != null ? currentUIFocus : currentWorldFocus);
        else if (step.HintMode == TutorialHintMode.Swipe) handIndicator?.ShowSwipeHint();
        else if (step.HintMode == TutorialHintMode.Tap || step.HintMode == TutorialHintMode.None)
            handIndicator?.ShowTapHint(currentUIFocus != null ? currentUIFocus : currentWorldFocus);
        else if (step.HintMode == TutorialHintMode.Zoom)
            handIndicator?.ShowZoomHint(TutorialInputTerminology.IsMobile);
        else if (step.HintMode == TutorialHintMode.Typing)
            handIndicator?.ShowTypingHint(currentUIFocus);
    }

    /// <summary>
    /// Retargets an active tutorial action to a replacement runtime UI control.
    /// Service bubbles and cashier controls can be rebuilt or change after each
    /// real click, so retaining their old RectTransform would leave the mask stale.
    /// </summary>
    public void RefreshLiveActionTarget(RectTransform liveTarget)
    {
        TutorialStep step = CurrentStep;
        if (!waitingForPlayerAction || step == null || liveTarget == null ||
            currentUIFocus == liveTarget)
            return;

        currentUIFocus = liveTarget;
        sceneBindings?.BeginUIFocus(currentUIFocus);
        dialogueUI?.SetFocusTarget(currentUIFocus);
        uiFocusMask?.Show(currentUIFocus, true);
        uiActionAdapter?.Begin(this, currentUIFocus);

        if (step.HintMode == TutorialHintMode.Typing) handIndicator?.ShowTypingHint(currentUIFocus);
        else if (step.HintMode == TutorialHintMode.Hold) handIndicator?.ShowHoldHint(currentUIFocus);
        else handIndicator?.ShowTapHint(currentUIFocus);
    }

    private bool restockTargetPending;
    public void RefreshRestockPresentation()
    {
        TutorialStep step = CurrentStep;
        if (step == null || step.Phase != TutorialPhase.PhysicalRestocking) return;
        RectTransform live = sceneBindings.ResolveUI(step.UITargetKey);
        Transform world = sceneBindings.ResolveWorld(step.WorldTargetKey);
        if (!waitingForNext && !waitingForPlayerAction)
        {
            if (restockTargetPending && live != null) ShowCurrentStep();
            return;
        }
        if (live == currentUIFocus && world == currentWorldFocus) return;
        currentUIFocus = live;
        currentWorldFocus = world;
        targetIndicator?.Hide();
        handIndicator?.HideHint();
        if (live == null && !string.IsNullOrEmpty(step.UITargetKey))
        {
            uiFocusMask?.Hide();
            return;
        }
        sceneBindings.BeginUIFocus(live);
        dialogueUI?.SetFocusTarget(live);
        ShowFocus(waitingForPlayerAction);
        if (waitingForPlayerAction && step.HintMode != TutorialHintMode.Drag)
            uiActionAdapter?.Begin(this, live);
        ShowRestockHint(step);
    }

    private void ShowRestockHint(TutorialStep step)
    {
        if (step == null) return;
        Transform target = currentUIFocus != null ? currentUIFocus : currentWorldFocus;
        if (step.HintMode == TutorialHintMode.Drag && step.ActionKey == "Restock.BoxActionsHidden")
            handIndicator?.ShowSmallDragHint(currentWorldFocus);
        else if (step.HintMode == TutorialHintMode.Drag)
            handIndicator?.ShowDragHint(currentUIFocus, currentWorldFocus);
        else if (step.HintMode == TutorialHintMode.Hold)
            handIndicator?.ShowHoldHint(target);
        else if (step.HintMode == TutorialHintMode.Typing) handIndicator?.ShowTypingHint(target);
        else if (step.HintMode == TutorialHintMode.Tap || step.HintMode == TutorialHintMode.None)
            handIndicator?.ShowTapHint(target);
    }

    private bool framingWorld;
    private float offscreenSince = -1f, nextFrameAssist;

    private Transform WorldFramingTarget()
    {
        if (currentWorldFocus != null && !(currentWorldFocus is RectTransform)) return currentWorldFocus;
        Canvas canvas = currentUIFocus != null ? currentUIFocus.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode == RenderMode.WorldSpace ? currentUIFocus : null;
    }

    private bool TargetVisible(Transform target, float margin)
    {
        Rect screenBounds;
        Camera camera = cameraController.Cam;
        if (target is RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector3 screen = camera.WorldToScreenPoint(corner);
                if (screen.z <= 0) return false;
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            screenBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        else if (!TutorialWorldTargetGeometry.TryGetScreenRect(target, camera, out screenBounds))
        {
            Vector3 point = camera.WorldToScreenPoint(target.position);
            if (point.z <= 0) return false;
            screenBounds = new Rect(point.x, point.y, 0, 0);
        }
        Rect view = camera.pixelRect;
        return screenBounds.xMin >= view.xMin + view.width * margin && screenBounds.xMax <= view.xMax - view.width * margin &&
            screenBounds.yMin >= view.yMin + view.height * margin && screenBounds.yMax <= view.yMax - view.height * margin;
    }

    private bool TryFrameWorldTarget(Action onSettled)
    {
        Transform target = WorldFramingTarget();
        if (framingWorld || target == null || cameraController == null || !cameraController.isActiveAndEnabled ||
            cameraController.Cam == null || !cameraController.Cam.isActiveAndEnabled || TargetVisible(target, .09f)) return false;
        StartCoroutine(FrameWorldTarget(target, presentationRevision, onSettled));
        return true;
    }

    private System.Collections.IEnumerator FrameWorldTarget(Transform target, int revision, Action onSettled)
    {
        framingWorld = true;
        uiFocusMask?.Hide(); targetIndicator?.Hide(); handIndicator?.HideHint();
        Vector3 center = target is RectTransform rect ? rect.TransformPoint(rect.rect.center) : TutorialWorldTargetGeometry.Center(target);
        Plane plane = new Plane(Vector3.up, center);
        Ray ray = cameraController.Cam.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 offset = center - ray.GetPoint(distance); offset.y = 0;
            cameraController.SetRigTargetPosition(cameraController.transform.position + offset);
            float deadline = Time.unscaledTime + 2f;
            while (target != null && revision == presentationRevision && Time.unscaledTime < deadline)
            {
                yield return null;
                // Respect a new intentional camera gesture; do not recenter again immediately.
                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) break;
                if (TargetVisible(target, .09f) && Vector3.Distance(cameraController.transform.position, cameraController.GetRigTargetPosition()) < .1f) break;
            }
        }
        yield return null;
        framingWorld = false; offscreenSince = -1f; nextFrameAssist = Time.unscaledTime + 5f;
        if (revision == presentationRevision) onSettled?.Invoke();
    }

    private void Update()
    {
        if (framingWorld || Time.unscaledTime < nextFrameAssist || (!waitingForNext && !waitingForPlayerAction)) return;
        Transform target = WorldFramingTarget();
        if (target == null || cameraController == null || cameraController.Cam == null || TargetVisible(target, 0f))
        { offscreenSince = -1f; return; }
        if (Input.GetMouseButton(0) || Input.touchCount > 0) { offscreenSince = -1f; return; }
        if (offscreenSince < 0) offscreenSince = Time.unscaledTime;
        if (Time.unscaledTime - offscreenSince < 2f) return;
        TryFrameWorldTarget(() =>
        {
            ShowFocus(waitingForPlayerAction);
            if (waitingForPlayerAction) ShowRestockHint(CurrentStep);
        });
    }

    private void ShowFocus(bool allowTargetInput)
    {
        if (CurrentStep != null && CurrentStep.HintMode == TutorialHintMode.Drag)
        {
            var restock = GetComponent<TutorialRestockFlowBridge>();
            RectTransform destination = restock != null ? restock.ResolveUI("RestockSlotFocus") : null;
            if (destination != null && (CurrentStep.ActionKey == "Restock.StoreActive" || CurrentStep.ActionKey == "Restock.StoreSecond"))
            {
                uiFocusMask?.Show(destination, true);
                if (uiFocusMask != null) { uiFocusMask.GesturePassThrough = true; uiFocusMask.raycastTarget = false; }
                return;
            }
            if (CurrentStep.ActionKey == "Restock.BoxActionsHidden" && currentUIFocus != null)
            {
                uiFocusMask?.Show(currentUIFocus, true);
                if (uiFocusMask != null) { uiFocusMask.GesturePassThrough = true; uiFocusMask.raycastTarget = false; }
                return;
            }
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
