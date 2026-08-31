using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent, prefab-backed task button for the Casual Dining flow. It is a
/// presentation layer only: task ownership and completion remain authoritative
/// in their existing gameplay systems.
/// </summary>
public sealed class PlayerTaskHUD : MonoBehaviour
{
    private const string ResourcePath = "UI/PlayerTaskHUD";

    public static PlayerTaskHUD Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private string[] supportedScenes = { "Lobby1", "RestockScene" };
    [SerializeField] private bool hideWhileGameplayUIBlocked = true;

    [Header("Task Button")]
    [SerializeField] private Button taskButton;
    [SerializeField] private RectTransform buttonRect;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Image taskIcon;
    [SerializeField] private GameObject reminderBadge;
    [SerializeField] private RectTransform reminderBadgeRect;
    [SerializeField] private TMP_Text reminderBadgeText;

    [Header("Objectives Button (Lobby Only)")]
    [SerializeField] private Button objectivesButton;
    [SerializeField] private RectTransform objectivesButtonRect;
    [SerializeField] private Image objectivesIcon;
    [SerializeField] private Color objectivesOpenColor = Color.white;
    [SerializeField] private Color objectivesClosedColor = new Color(1f, 1f, 1f, 0.7f);

    [Header("Sliding Message")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Image panelImage;
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Vector2 panelShownPosition = new Vector2(286f, -154f);
    [SerializeField] private Vector2 panelHiddenPosition = new Vector2(92f, -154f);

    [Header("Lobby HUD Redesign")]
    [Tooltip("Uses the mobile-first right-side layout from the Lobby HUD design. Disable to use the authored legacy positions.")]
    [SerializeField] private bool useLobbyHudRedesignLayout = true;
    [SerializeField] private bool hideLegacyObjectivesButton = true;
    [SerializeField] private bool manualPanelStaysOpen = true;
    [Tooltip("Use the transforms, sizes, colors, and icon tints saved in the combined LobbyHUD prefab.")]
    [SerializeField] private bool preserveAuthoredPresentation;
    [SerializeField] private Vector2 authoredPanelSlideOffset = new Vector2(80f, 0f);
    [SerializeField] private Vector2 redesignedButtonPosition = new Vector2(-76f, -350f);
    [SerializeField] private Vector2 redesignedButtonSize = new Vector2(96f, 116f);
    [SerializeField] private Vector2 redesignedPanelSize = new Vector2(292f, 164f);
    [SerializeField] private Vector2 redesignedPanelShownPosition = new Vector2(-186f, -344f);
    [SerializeField] private Vector2 redesignedPanelHiddenPosition = new Vector2(-78f, -344f);
    [SerializeField, HideInInspector] private int lobbyHudLayoutVersion;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] private float slideSeconds = 0.28f;
    [SerializeField, Min(0.5f)] private float automaticVisibleSeconds = 2.6f;
    [SerializeField, Min(0.5f)] private float manualVisibleSeconds = 4f;
    [SerializeField, Min(0.5f)] private float reminderDelaySeconds = 5f;
    [SerializeField, Min(0.1f)] private float completionVisibleSeconds = 1.15f;

    [Header("Motion")]
    [SerializeField, Range(1f, 1.4f)] private float newTaskBounceScale = 1.14f;
    [SerializeField, Min(0.05f)] private float buttonBounceSeconds = 0.32f;
    [SerializeField, Range(0.75f, 1f)] private float panelHiddenScale = 0.92f;
    [SerializeField, Range(1f, 1.35f)] private float badgePopScale = 1.18f;

    [Header("Colors")]
    [SerializeField] private Color activeButtonColor = new Color(0.04f, 0.64f, 0.88f, 1f);
    [SerializeField] private Color idleButtonColor = new Color(0.28f, 0.45f, 0.56f, 0.92f);
    [SerializeField] private Color panelColor = new Color(0.035f, 0.16f, 0.31f, 0.98f);
    [SerializeField] private Color completionColor = new Color(0.08f, 0.45f, 0.26f, 0.98f);
    [SerializeField, Range(0.08f, 0.75f)] private float backgroundTaskBubbleAlpha = 0.28f;

    private Coroutine panelRoutine;
    private Coroutine buttonRoutine;
    private Coroutine badgeRoutine;
    private Coroutine completionRoutine;
    private Coroutine objectivesRoutine;
    private string currentCompositeKey = string.Empty;
    private bool panelOpen;
    private bool badgeVisible;
    private bool pendingAutomaticShow;
    private bool supportedSceneVisible;
    private bool objectivesSceneVisible;
    private float autoHideAt;
    private float reminderAt = float.PositiveInfinity;
    private Vector2 effectivePanelShownPosition;
    private Vector2 effectivePanelHiddenPosition;
    private Vector2Int lastLayoutScreenSize = new Vector2Int(-1, -1);
    private Rect lastLayoutSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector3 authoredButtonScale = Vector3.one;
    private Vector3 authoredPanelScale = Vector3.one;
    private Vector3 authoredBadgeScale = Vector3.one;
    private Color authoredTaskIconColor = Color.white;
    private Color authoredPanelColor = Color.white;
    private bool authoredColorsCaptured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneLoaded += HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        SceneManager.sceneUnloaded += HandleSceneUnloadedStatic;
        EnsureInstance();
    }

    private static void HandleSceneLoadedStatic(Scene _, LoadSceneMode __)
    {
        EnsureInstance()?.RefreshSceneVisibility();
    }

    private static void HandleSceneUnloadedStatic(Scene _)
    {
        if (Instance != null)
            Instance.RefreshSceneVisibility();
    }

    private static PlayerTaskHUD EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PlayerTaskHUD existing = FindFirstObjectByType<PlayerTaskHUD>(
            FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        LobbyHUDRoot combinedRoot = LobbyHUDRoot.EnsureInstance();
        PlayerTaskHUD combined = combinedRoot != null
            ? combinedRoot.GetComponentInChildren<PlayerTaskHUD>(true)
            : null;
        if (combined != null)
            return combined;

        PlayerTaskHUD prefab = Resources.Load<PlayerTaskHUD>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError("[PlayerTaskHUD] Resources/UI/PlayerTaskHUD prefab is missing.");
            return null;
        }

        return Instantiate(prefab);
    }

    /// <summary>
    /// Connects the task presenter to the designer-authored TaskButton and
    /// TaskMessage that already live inside the combined LobbyHUD prefab.
    /// This is also a runtime safety net for older copies of the prefab that
    /// have not yet been resaved by the editor authoring pass.
    /// </summary>
    public static PlayerTaskHUD EnsureCombinedBinding(LobbyHUDRedesign controls)
    {
        if (controls == null)
            return null;

        // The original task prefab also carried the detailed restock guidance
        // source. Keep that source when the visuals are hosted by LobbyHUD.
        if (controls.GetComponent<PlayerTaskRestockSource>() == null)
            controls.gameObject.AddComponent<PlayerTaskRestockSource>();

        Transform safeArea = controls.transform.Find("SafeArea");
        Transform taskButtonTransform = safeArea != null
            ? safeArea.Find("TaskButton")
            : null;
        Transform taskMessageTransform = safeArea != null
            ? safeArea.Find("TaskMessage")
            : null;
        if (taskButtonTransform == null || taskMessageTransform == null)
            return controls.GetComponent<PlayerTaskHUD>();

        Button authoredButton = taskButtonTransform.GetComponent<Button>();
        RectTransform authoredButtonRect = taskButtonTransform as RectTransform;
        Image authoredButtonImage = taskButtonTransform.GetComponent<Image>();
        Transform taskIconTransform = taskButtonTransform.Find("TaskIcon");
        Image authoredTaskIcon = taskIconTransform != null
            ? taskIconTransform.GetComponent<Image>()
            : null;

        Transform badgeTransform = taskButtonTransform.Find("ReminderBadge");
        GameObject authoredBadge = badgeTransform != null
            ? badgeTransform.gameObject
            : null;
        RectTransform authoredBadgeRect = badgeTransform as RectTransform;
        TMP_Text authoredBadgeText = badgeTransform != null
            ? badgeTransform.GetComponentInChildren<TMP_Text>(true)
            : null;

        RectTransform authoredPanelRect = taskMessageTransform as RectTransform;
        CanvasGroup authoredPanelGroup = taskMessageTransform.GetComponent<CanvasGroup>();
        Image authoredPanelImage = taskMessageTransform.GetComponent<Image>();
        Transform actionTransform = taskMessageTransform.Find("Action");
        Transform detailTransform = taskMessageTransform.Find("Detail");
        TMP_Text authoredTaskText = actionTransform != null
            ? actionTransform.GetComponent<TMP_Text>()
            : null;
        TMP_Text authoredDetailText = detailTransform != null
            ? detailTransform.GetComponent<TMP_Text>()
            : null;

        if (authoredButton == null || authoredButtonRect == null ||
            authoredPanelRect == null || authoredTaskText == null)
            return controls.GetComponent<PlayerTaskHUD>();

        PlayerTaskHUD presenter = controls.GetComponent<PlayerTaskHUD>();
        if (presenter != null && presenter.taskButton == authoredButton &&
            presenter.panelRect == authoredPanelRect &&
            presenter.preserveAuthoredPresentation)
            return presenter;

        // A legacy standalone presenter can only exist here in an upgraded
        // project session. Retire it before attaching the combined presenter
        // so it cannot draw a second Task button for one frame or longer.
        if (presenter == null && Instance != null &&
            Instance.GetComponentInParent<LobbyHUDRoot>() == null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }

        presenter = presenter != null
            ? presenter
            : controls.gameObject.AddComponent<PlayerTaskHUD>();
        presenter.ConfigureCombinedPresentation(
            controls.GetComponent<Canvas>(),
            controls.GetComponent<CanvasGroup>(),
            authoredButton,
            authoredButtonRect,
            authoredButtonImage,
            authoredTaskIcon,
            authoredBadge,
            authoredBadgeRect,
            authoredBadgeText,
            taskMessageTransform.gameObject,
            authoredPanelRect,
            authoredPanelGroup,
            authoredPanelImage,
            authoredTaskText,
            authoredDetailText);
        presenter.InitializePresentation();
        return presenter;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (GetComponentInParent<LobbyHUDRoot>() == null)
            DontDestroyOnLoad(gameObject);

        InitializePresentation();
    }

    private void InitializePresentation()
    {

        PlayerTaskBubbleFocus.BackgroundAlpha = backgroundTaskBubbleAlpha;
        if (taskButton != null)
        {
            taskButton.onClick.RemoveListener(OnTaskButtonClicked);
            taskButton.onClick.AddListener(OnTaskButtonClicked);
        }
        if (objectivesButton != null)
        {
            objectivesButton.onClick.RemoveListener(OnObjectivesButtonClicked);
            objectivesButton.onClick.AddListener(OnObjectivesButtonClicked);
        }

        if (!preserveAuthoredPresentation && panelImage != null)
            panelImage.color = panelColor;

        CaptureAuthoredPresentation();
        RefreshLobbyLayout(true);
        ApplyPanelImmediate(false);
        SetBadge(false);
        RefreshSceneVisibility();
        RefreshFromGuidance(false);
    }

    private void ConfigureCombinedPresentation(
        Canvas configuredCanvas,
        CanvasGroup configuredHudGroup,
        Button configuredTaskButton,
        RectTransform configuredButtonRect,
        Image configuredButtonImage,
        Image configuredTaskIcon,
        GameObject configuredReminderBadge,
        RectTransform configuredReminderBadgeRect,
        TMP_Text configuredReminderBadgeText,
        GameObject configuredPanelRoot,
        RectTransform configuredPanelRect,
        CanvasGroup configuredPanelGroup,
        Image configuredPanelImage,
        TMP_Text configuredTaskText,
        TMP_Text configuredDetailText)
    {
        hudCanvas = configuredCanvas;
        hudGroup = configuredHudGroup;
        supportedScenes = new[] { "Lobby1", "RestockScene" };
        taskButton = configuredTaskButton;
        buttonRect = configuredButtonRect;
        buttonImage = configuredButtonImage;
        taskIcon = configuredTaskIcon;
        reminderBadge = configuredReminderBadge;
        reminderBadgeRect = configuredReminderBadgeRect;
        reminderBadgeText = configuredReminderBadgeText;
        panelRoot = configuredPanelRoot;
        panelRect = configuredPanelRect;
        panelGroup = configuredPanelGroup;
        panelImage = configuredPanelImage;
        taskText = configuredTaskText;
        detailText = configuredDetailText;
        objectivesButton = null;
        objectivesButtonRect = null;
        objectivesIcon = null;
        useLobbyHudRedesignLayout = false;
        hideLegacyObjectivesButton = true;
        manualPanelStaysOpen = true;
        preserveAuthoredPresentation = true;
    }

    private void OnEnable()
    {
        PlayerTaskGuidance.Changed += HandleTaskChanged;
    }

    private void OnDisable()
    {
        PlayerTaskGuidance.Changed -= HandleTaskChanged;
    }

    private void OnDestroy()
    {
        PlayerTaskGuidance.Changed -= HandleTaskChanged;
        if (taskButton != null)
            taskButton.onClick.RemoveListener(OnTaskButtonClicked);
        if (objectivesButton != null)
            objectivesButton.onClick.RemoveListener(OnObjectivesButtonClicked);
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        slideSeconds = Mathf.Max(0.05f, slideSeconds);
        automaticVisibleSeconds = Mathf.Max(0.5f, automaticVisibleSeconds);
        manualVisibleSeconds = Mathf.Max(0.5f, manualVisibleSeconds);
        reminderDelaySeconds = Mathf.Max(0.5f, reminderDelaySeconds);
        completionVisibleSeconds = Mathf.Max(0.1f, completionVisibleSeconds);
    }

    private void Update()
    {
        RefreshLobbyLayout(false);
        PlayerTaskBubbleFocus.BackgroundAlpha = backgroundTaskBubbleAlpha;

        bool blocked = hideWhileGameplayUIBlocked && GameplayUIBlocker.IsBlocked();
        bool canPresent = supportedSceneVisible && !blocked;
        if (hudGroup != null)
        {
            hudGroup.alpha = canPresent ? 1f : 0f;
            hudGroup.interactable = canPresent;
            hudGroup.blocksRaycasts = canPresent;
        }

        if (taskButton != null)
            taskButton.interactable = canPresent;
        if (objectivesButton != null)
            objectivesButton.interactable = canPresent && objectivesSceneVisible;
        RefreshObjectivesButtonStyle();

        if (pendingAutomaticShow && canPresent)
        {
            pendingAutomaticShow = false;
            ShowPanel(automaticVisibleSeconds, true);
        }

        float now = Time.unscaledTime;
        if (panelOpen && now >= autoHideAt)
        {
            HidePanel(PlayerTaskGuidance.Current.IsValid);
            return;
        }

        PlayerTaskView task = PlayerTaskGuidance.Current;
        if (!task.IsValid)
            return;

        if (!panelOpen && !badgeVisible && now >= reminderAt)
            SetBadge(true);
    }

    public void RefreshSceneVisibility()
    {
        supportedSceneVisible = false;
        objectivesSceneVisible = false;
        if (useLobbyHudRedesignLayout)
        {
            // RestockScene is additive, so a loaded Lobby1 scene is not enough.
            // The redesigned HUD belongs only to the active normal Lobby view.
            bool activeLobby = SceneManager.GetActiveScene().name == "Lobby1";
            supportedSceneVisible = activeLobby;
            objectivesSceneVisible = activeLobby;
        }
        else for (int i = 0; i < SceneManager.sceneCount && !supportedSceneVisible; i++)
        {
            string sceneName = SceneManager.GetSceneAt(i).name;
            if (sceneName == "Lobby1")
                objectivesSceneVisible = true;
            if (supportedScenes == null)
                continue;

            for (int j = 0; j < supportedScenes.Length; j++)
            {
                if (sceneName == supportedScenes[j])
                {
                    supportedSceneVisible = true;
                    break;
                }
            }
        }

        if (hudCanvas != null)
            hudCanvas.enabled = supportedSceneVisible;
        if (objectivesButton != null)
            objectivesButton.gameObject.SetActive(objectivesSceneVisible && !hideLegacyObjectivesButton);

        RefreshLobbyLayout(true);
    }

    private void OnObjectivesButtonClicked()
    {
        CasualDiningProgressHUD.Instance?.ToggleObjectives();
        RefreshObjectivesButtonStyle();
        if (objectivesButtonRect == null)
            return;
        if (objectivesRoutine != null)
            StopCoroutine(objectivesRoutine);
        objectivesRoutine = StartCoroutine(ObjectivesButtonBounce());
    }

    private void RefreshObjectivesButtonStyle()
    {
        if (objectivesIcon == null)
            return;
        bool open = CasualDiningProgressHUD.Instance == null || CasualDiningProgressHUD.Instance.IsExpanded;
        objectivesIcon.color = open ? objectivesOpenColor : objectivesClosedColor;
    }

    private IEnumerator ObjectivesButtonBounce()
    {
        float elapsed = 0f;
        while (elapsed < buttonBounceSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / buttonBounceSeconds);
            float scale = t < 0.45f
                ? Mathf.Lerp(1f, newTaskBounceScale, Smooth(t / 0.45f))
                : Mathf.Lerp(newTaskBounceScale, 1f, BackOut((t - 0.45f) / 0.55f));
            objectivesButtonRect.localScale = Vector3.one * scale;
            yield return null;
        }
        objectivesButtonRect.localScale = Vector3.one;
        objectivesRoutine = null;
    }

    private void HandleTaskChanged()
    {
        RefreshFromGuidance(true);
    }

    private void RefreshFromGuidance(bool animate)
    {
        PlayerTaskView task = PlayerTaskGuidance.Current;
        if (!task.IsValid)
        {
            bool completedTask = !string.IsNullOrEmpty(currentCompositeKey);
            currentCompositeKey = string.Empty;
            pendingAutomaticShow = false;
            SetBadge(false);
            reminderAt = float.PositiveInfinity;

            if (completedTask && animate)
                StartCompletionPresentation();
            else
                ApplyIdleStyle();
            return;
        }

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        RestoreAuthoredColors();

        string compositeKey = task.Source + ":" + task.Key;
        bool completedPreviousStep = animate &&
                                     !string.IsNullOrEmpty(currentCompositeKey) &&
                                     compositeKey != currentCompositeKey;
        bool newStep = compositeKey != currentCompositeKey;
        currentCompositeKey = compositeKey;

        if (taskText != null)
            taskText.text = "CURRENT TASK\n" + task.Action;
        if (detailText != null)
        {
            detailText.text = task.Detail;
            detailText.gameObject.SetActive(!string.IsNullOrWhiteSpace(task.Detail));
        }

        if (!preserveAuthoredPresentation && taskIcon != null)
            taskIcon.color = activeButtonColor;
        if (!preserveAuthoredPresentation && panelImage != null)
            panelImage.color = panelColor;

        if (!newStep)
            return;

        SetBadge(false);
        reminderAt = float.PositiveInfinity;
        PlayButtonBounce();

        if (completedPreviousStep)
        {
            pendingAutomaticShow = false;
            StartCompletionPresentation();
            return;
        }

        pendingAutomaticShow = true;
    }

    private void OnTaskButtonClicked()
    {
        PlayButtonBounce();

        if (!PlayerTaskGuidance.Current.IsValid)
        {
            if (taskText != null)
                taskText.text = "CURRENT TASK\nNO ACTIVE TASK";
            if (detailText != null)
            {
                detailText.text = "CHOOSE A CUSTOMER OR WORK ITEM";
                detailText.gameObject.SetActive(true);
            }
            ShowPanel(2f, false);
            return;
        }

        SetBadge(false);
        if (panelOpen)
            HidePanel(true);
        else
            ShowPanel(manualPanelStaysOpen ? float.PositiveInfinity : manualVisibleSeconds, true);
    }

    private void ShowPanel(float visibleSeconds, bool resetReminder)
    {
        if (panelRoot == null || panelRect == null)
            return;

        panelRoot.SetActive(true);
        panelOpen = true;
        autoHideAt = float.IsPositiveInfinity(visibleSeconds)
            ? float.PositiveInfinity
            : Time.unscaledTime + Mathf.Max(0.25f, visibleSeconds);
        if (resetReminder)
            reminderAt = Time.unscaledTime + reminderDelaySeconds;
        StartPanelTransition(true);
    }

    private void HidePanel(bool scheduleReminder)
    {
        if (!panelOpen)
            return;

        panelOpen = false;
        if (scheduleReminder && PlayerTaskGuidance.Current.IsValid)
        {
            // Once the readable guidance panel has closed, leave a clear
            // reminder on the Task button immediately. Waiting for a second
            // long timer made the HUD look inactive even though a task was
            // still waiting for the player.
            reminderAt = Time.unscaledTime + 0.1f;
        }
        StartPanelTransition(false);
    }

    private void StartPanelTransition(bool show)
    {
        if (panelRoutine != null)
            StopCoroutine(panelRoutine);
        panelRoutine = StartCoroutine(PanelTransition(show));
    }

    private IEnumerator PanelTransition(bool show)
    {
        Vector2 startPosition = panelRect.anchoredPosition;
        Vector2 targetPosition = show ? effectivePanelShownPosition : effectivePanelHiddenPosition;
        float startAlpha = panelGroup != null ? panelGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;
        Vector3 startScale = panelRect.localScale;
        Vector3 shownScale = preserveAuthoredPresentation
            ? authoredPanelScale
            : Vector3.one;
        Vector3 targetScale = show ? shownScale : shownScale * panelHiddenScale;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideSeconds);
            float eased = t * t * (3f - 2f * t);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
            panelRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
            if (panelGroup != null)
                panelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        panelRect.anchoredPosition = targetPosition;
        panelRect.localScale = targetScale;
        if (panelGroup != null)
            panelGroup.alpha = targetAlpha;
        if (!show && panelRoot != null)
            panelRoot.SetActive(false);
        panelRoutine = null;
    }

    private void ApplyPanelImmediate(bool show)
    {
        panelOpen = show;
        if (panelRoot != null)
            panelRoot.SetActive(show);
        if (panelRect != null)
        {
            panelRect.anchoredPosition = show ? effectivePanelShownPosition : effectivePanelHiddenPosition;
            Vector3 shownScale = preserveAuthoredPresentation
                ? authoredPanelScale
                : Vector3.one;
            panelRect.localScale = show
                ? shownScale
                : shownScale * panelHiddenScale;
        }
        if (panelGroup != null)
            panelGroup.alpha = show ? 1f : 0f;
    }

    private void PlayButtonBounce()
    {
        if (buttonRect == null)
            return;
        if (buttonRoutine != null)
            StopCoroutine(buttonRoutine);
        buttonRoutine = StartCoroutine(ButtonBounce());
    }

    private IEnumerator ButtonBounce()
    {
        float elapsed = 0f;
        while (elapsed < buttonBounceSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / buttonBounceSeconds);
            float scaleMultiplier;
            if (t < 0.45f)
                scaleMultiplier = Mathf.Lerp(
                    1f,
                    newTaskBounceScale,
                    Smooth(t / 0.45f));
            else
                scaleMultiplier = Mathf.Lerp(
                    newTaskBounceScale,
                    1f,
                    BackOut((t - 0.45f) / 0.55f));
            buttonRect.localScale = authoredButtonScale * scaleMultiplier;
            yield return null;
        }

        buttonRect.localScale = authoredButtonScale;
        buttonRoutine = null;
    }

    private void SetBadge(bool show)
    {
        badgeVisible = show;
        if (reminderBadge != null)
            reminderBadge.SetActive(show);
        if (reminderBadgeText != null)
            reminderBadgeText.text = "!";

        if (!show || reminderBadgeRect == null)
            return;

        if (badgeRoutine != null)
            StopCoroutine(badgeRoutine);
        badgeRoutine = StartCoroutine(BadgePop());
    }

    private IEnumerator BadgePop()
    {
        float elapsed = 0f;
        const float duration = 0.3f;
        reminderBadgeRect.localScale = Vector3.zero;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.65f
                ? Mathf.Lerp(0f, badgePopScale, Smooth(t / 0.65f))
                : Mathf.Lerp(badgePopScale, 1f, Smooth((t - 0.65f) / 0.35f));
            reminderBadgeRect.localScale = authoredBadgeScale * scale;
            yield return null;
        }
        reminderBadgeRect.localScale = authoredBadgeScale;
        badgeRoutine = null;
    }

    private void StartCompletionPresentation()
    {
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);
        completionRoutine = StartCoroutine(CompletionPresentation());
    }

    private IEnumerator CompletionPresentation()
    {
        if (taskText != null)
            taskText.text = "CURRENT TASK\nTASK COMPLETE";
        if (detailText != null)
        {
            detailText.text = "READY FOR THE NEXT JOB";
            detailText.gameObject.SetActive(true);
        }
        // Completion is deliberate feedback, not a layout/style override.
        // It may temporarily turn green, then the designer's exact colors are
        // restored for the next task.
        if (taskIcon != null)
            taskIcon.color = completionColor;
        if (panelImage != null)
            panelImage.color = completionColor;

        PlayButtonBounce();
        ShowPanel(completionVisibleSeconds, false);
        yield return new WaitForSecondsRealtime(completionVisibleSeconds);

        if (PlayerTaskGuidance.Current.IsValid)
        {
            // A completed step may immediately unlock the next step in the
            // same guide (truck -> stockroom -> shelf). After the green
            // confirmation, present the latest authoritative task.
            completionRoutine = null;
            currentCompositeKey = string.Empty;
            RestoreAuthoredColors();
            RefreshFromGuidance(false);
            yield break;
        }
        else
        {
            HidePanel(false);
            ApplyIdleStyle();
        }
        completionRoutine = null;
    }

    private void ApplyIdleStyle()
    {
        if (preserveAuthoredPresentation)
        {
            RestoreAuthoredColors();
            return;
        }

        if (taskIcon != null)
            taskIcon.color = idleButtonColor;
        if (panelImage != null)
            panelImage.color = panelColor;
    }

    private void CaptureAuthoredPresentation()
    {
        if (!preserveAuthoredPresentation || panelRect == null)
            return;

        // The RectTransform saved in Prefab Mode is the visible position.
        // Animation may offset it temporarily, but the authored placement,
        // dimensions, anchors, color, and icon tint remain authoritative.
        panelShownPosition = panelRect.anchoredPosition;
        panelHiddenPosition = panelShownPosition + authoredPanelSlideOffset;
        effectivePanelShownPosition = panelShownPosition;
        effectivePanelHiddenPosition = panelHiddenPosition;
        authoredPanelScale = panelRect.localScale;
        authoredButtonScale = buttonRect != null
            ? buttonRect.localScale
            : Vector3.one;
        authoredBadgeScale = reminderBadgeRect != null
            ? reminderBadgeRect.localScale
            : Vector3.one;
        authoredTaskIconColor = taskIcon != null ? taskIcon.color : Color.white;
        authoredPanelColor = panelImage != null ? panelImage.color : Color.white;
        authoredColorsCaptured = true;
    }

    private void RestoreAuthoredColors()
    {
        if (!preserveAuthoredPresentation || !authoredColorsCaptured)
            return;

        if (taskIcon != null)
            taskIcon.color = authoredTaskIconColor;
        if (panelImage != null)
            panelImage.color = authoredPanelColor;
    }

    private void RefreshLobbyLayout(bool force)
    {
        if (preserveAuthoredPresentation)
        {
            effectivePanelShownPosition = panelShownPosition;
            effectivePanelHiddenPosition = panelHiddenPosition;
            return;
        }

        if (!useLobbyHudRedesignLayout || buttonRect == null || panelRect == null)
        {
            effectivePanelShownPosition = panelShownPosition;
            effectivePanelHiddenPosition = panelHiddenPosition;
            return;
        }

        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        Rect safeArea = Screen.safeArea;
        if (!force && size == lastLayoutScreenSize && safeArea == lastLayoutSafeArea)
            return;

        lastLayoutScreenSize = size;
        lastLayoutSafeArea = safeArea;
        float rightInset = Screen.width > 0
            ? (Screen.width - safeArea.xMax) * 1920f / Screen.width
            : 0f;
        float topInset = Screen.height > 0
            ? (Screen.height - safeArea.yMax) * 1080f / Screen.height
            : 0f;
        Vector2 safeOffset = new Vector2(-rightInset, -topInset);

        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = Vector2.one;
        buttonRect.anchoredPosition = redesignedButtonPosition + safeOffset;
        buttonRect.sizeDelta = redesignedButtonSize;

        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.one;
        panelRect.sizeDelta = redesignedPanelSize;
        effectivePanelShownPosition = redesignedPanelShownPosition + safeOffset;
        effectivePanelHiddenPosition = redesignedPanelHiddenPosition + safeOffset;
        if (panelRoutine == null)
            panelRect.anchoredPosition = panelOpen
                ? effectivePanelShownPosition
                : effectivePanelHiddenPosition;

        if (taskText != null)
        {
            taskText.alignment = TextAlignmentOptions.Top;
            taskText.fontSizeMax = 28f;
            taskText.fontSizeMin = 18f;
            RectTransform rect = taskText.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.42f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 4f);
            rect.offsetMax = new Vector2(-16f, -12f);
        }

        if (detailText != null)
        {
            detailText.alignment = TextAlignmentOptions.Top;
            detailText.fontSizeMax = 22f;
            detailText.fontSizeMin = 15f;
            RectTransform rect = detailText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0.43f);
            rect.offsetMin = new Vector2(16f, 10f);
            rect.offsetMax = new Vector2(-16f, -2f);
        }
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

#if UNITY_EDITOR
    public bool UsesCombinedAuthoredPresentation => preserveAuthoredPresentation;

    public void ConfigureCombinedPrefabForEditor(
        Canvas configuredCanvas,
        CanvasGroup configuredHudGroup,
        Button configuredTaskButton,
        RectTransform configuredButtonRect,
        Image configuredButtonImage,
        Image configuredTaskIcon,
        GameObject configuredReminderBadge,
        RectTransform configuredReminderBadgeRect,
        TMP_Text configuredReminderBadgeText,
        GameObject configuredPanelRoot,
        RectTransform configuredPanelRect,
        CanvasGroup configuredPanelGroup,
        Image configuredPanelImage,
        TMP_Text configuredTaskText,
        TMP_Text configuredDetailText)
    {
        ConfigureCombinedPresentation(
            configuredCanvas,
            configuredHudGroup,
            configuredTaskButton,
            configuredButtonRect,
            configuredButtonImage,
            configuredTaskIcon,
            configuredReminderBadge,
            configuredReminderBadgeRect,
            configuredReminderBadgeText,
            configuredPanelRoot,
            configuredPanelRect,
            configuredPanelGroup,
            configuredPanelImage,
            configuredTaskText,
            configuredDetailText);
    }

    public bool EnsureLobbyHudLayoutForEditor(Sprite configuredTaskIcon)
    {
        const int currentVersion = 1;
        if (lobbyHudLayoutVersion >= currentVersion)
            return false;

        useLobbyHudRedesignLayout = true;
        hideLegacyObjectivesButton = true;
        manualPanelStaysOpen = true;
        redesignedButtonPosition = new Vector2(-76f, -350f);
        redesignedButtonSize = new Vector2(96f, 116f);
        redesignedPanelSize = new Vector2(292f, 164f);
        redesignedPanelShownPosition = new Vector2(-186f, -344f);
        redesignedPanelHiddenPosition = new Vector2(-78f, -344f);
        if (taskIcon != null && configuredTaskIcon != null)
            taskIcon.sprite = configuredTaskIcon;
        lobbyHudLayoutVersion = currentVersion;
        return true;
    }
#endif

    private static float BackOut(float value)
    {
        value = Mathf.Clamp01(value) - 1f;
        const float overshoot = 1.25f;
        return 1f + value * value * ((overshoot + 1f) * value + overshoot);
    }
}
