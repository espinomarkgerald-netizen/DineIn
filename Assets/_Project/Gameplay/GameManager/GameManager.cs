using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameDayManager : MonoBehaviour
{
    public static GameDayManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string managementSceneName = "Office";

    [Header("Single Restaurant Flow")]
    [Tooltip("Creates the persistent campaign flow when Casual Dining is played directly.")]
    [SerializeField] private bool bootstrapSingleRestaurantFlow;
    [SerializeField] private string restaurantSceneName = "Lobby1";

    [Header("Autonomous Service")]
    [Tooltip("Uses the four Lobby staff as bots and disables manual role switching in this scene.")]
    [SerializeField] private bool enableAutonomousServiceBots;
    [SerializeField] private bool autoStartServiceWhenBotsEnabled = true;
    [Tooltip("Keeps restaurant service paused until the manager starts the shift from the computer.")]
    [SerializeField] private bool useManagementComputerForDayStart;

    [Header("Restaurant Clock")]
    [SerializeField, Range(0, 23)] private int openingHour = 10;
    [SerializeField, Range(1, 24)] private int closingHour = 18;
    [Tooltip("One real minute equals one in-game hour when this is 60.")]
    [SerializeField, Min(1f)] private float realSecondsPerGameHour = 60f;
    [SerializeField, Min(0f)] private float maxClosingGraceSeconds = 5f;

    [Header("Spawn Settings")]
    [SerializeField] private int maxCustomersToSpawn = 12;
    [SerializeField] private int maxGroupsPerMinute = 2;
    [SerializeField] private float spawnIntervalMin = 6f;
    [SerializeField] private float spawnIntervalMax = 12f;
    [Tooltip("How often a blocked customer spawn is retried. This prevents a full-frame retry loop.")]
    [SerializeField, Min(0.25f)] private float blockedSpawnRetrySeconds = 1f;
    [Tooltip("Limits simultaneous customer groups while still allowing the full daily total to spawn.")]
    [SerializeField, Min(1)] private int maxConcurrentGroups = 8;
    [SerializeField, Min(1)] private int rushMaxConcurrentGroups = 10;
    [Tooltip("Android performance cap. Blocked groups are delayed, not removed from the day's total.")]
    [SerializeField, Min(1)] private int mobileMaxConcurrentGroups = 4;
    [Tooltip("Android rush-hour cap. Keeps the rush busier without allowing an unbounded crowd.")]
    [SerializeField, Min(1)] private int mobileRushMaxConcurrentGroups = 5;

    [Header("Rush Hour")]
    [Tooltip("Rush hour begins when this many real seconds remain in the shift.")]
    [SerializeField, Min(0f)] private float rushStartTimeRemainingSeconds = 240f;
    [SerializeField, Range(0.1f, 1f)] private float rushSpawnIntervalMultiplier = 0.4f;
    [SerializeField, Min(0)] private int rushAdditionalGroupsPerMinute = 2;

    [Header("Spawn Difficulty Scaling")]
    [Tooltip("X = normalized day (0 = Day 1, 1 = max day). Y = max customer groups to spawn.")]
    [SerializeField] private AnimationCurve maxCustomersCurve = new AnimationCurve(
        new Keyframe(0f, 18f),
        new Keyframe(0.25f, 24f),
        new Keyframe(0.6f, 32f),
        new Keyframe(1f, 42f));

    [Tooltip("X = normalized day. Y = max groups allowed per minute.")]
    [SerializeField] private AnimationCurve groupsPerMinuteCurve = new AnimationCurve(
        new Keyframe(0f, 2f),
        new Keyframe(0.25f, 3f),
        new Keyframe(0.6f, 4f),
        new Keyframe(1f, 5f));

    [Tooltip("X = normalized day. Y = minimum seconds between spawns.")]
    [SerializeField] private AnimationCurve spawnIntervalMinCurve = new AnimationCurve(
        new Keyframe(0f, 35f),
        new Keyframe(0.25f, 28f),
        new Keyframe(0.6f, 18f),
        new Keyframe(1f, 10f));

    [Tooltip("X = normalized day. Y = maximum seconds between spawns.")]
    [SerializeField] private AnimationCurve spawnIntervalMaxCurve = new AnimationCurve(
        new Keyframe(0f, 50f),
        new Keyframe(0.25f, 42f),
        new Keyframe(0.6f, 30f),
        new Keyframe(1f, 18f));

    [Tooltip("The day number that counts as the difficulty ceiling (1 = disabled, scales up to this day).")]
    [SerializeField] private int maxScalingDay = 20;

    [Header("Manager Objects")]
    [SerializeField] private GameObject roleManagerObject;
    [SerializeField] private GameObject restaurantManagerObject;
    [SerializeField] private GameObject customerSystemObject;
    [SerializeField] private GameObject foodManagerObject;
    [SerializeField] private GameObject lobbyLineManagerObject;
    [SerializeField] private GameObject kitchenManagerObject;
    [SerializeField] private GameObject orderFlowManagerObject;
    [SerializeField] private GameObject billManagerObject;

    [Header("Resolved Components")]
    [SerializeField] private RoleManager roleManager;
    [SerializeField] private GroupSpawner groupSpawner;
    [SerializeField] private LobbyLineManager lobbyLineManager;
    [SerializeField] private KitchenManager kitchenManager;
    [SerializeField] private OrderFlowManager orderFlowManager;
    [SerializeField] private BillManager billManager;

    [Header("HUD UI")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressMoneyText;

    [Header("Mood Bars")]
    [SerializeField] private Slider angryBar;
    [SerializeField] private Slider neutralBar;
    [SerializeField] private int angryBarMax = 10;
    [SerializeField] private int neutralBarMax = 10;
    [SerializeField] private float moodBarSmoothSpeed = 8f;

    [Header("Shift Intro UI")]
    [SerializeField] private GameObject dayIntroPanel;
    [SerializeField] private TMP_Text dayIntroTitleText;
    [SerializeField] private TMP_Text dayIntroSummaryLeftText;
    [SerializeField] private TMP_Text dayIntroSummaryMiddleText;
    [SerializeField] private TMP_Text dayIntroSummaryRightText;
    [SerializeField] private Button playButton;

    [Header("Results UI")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsTitleText;
    [SerializeField] private TMP_Text resultsSummaryText;
    [SerializeField] private TMP_Text resultsCustomersText;
    [SerializeField] private TMP_Text resultsCashText;
    [SerializeField] private TMP_Text resultsStarsText;
    [SerializeField] private Image star1;
    [SerializeField] private Image star2;
    [SerializeField] private Image star3;
    [SerializeField] private Button resultsActionButton;
    [SerializeField] private TMP_Text resultsActionButtonText;

    [Header("Results Recovery")]
    [SerializeField, Min(1)] private int continueGoldCoinCost = 500;
    [SerializeField] private string goldCoinStoreUrl = "https://dine-in-website.vercel.app/";

    [Header("Takeout Unlock")]
    [SerializeField] private int takeoutUnlockDay = 20;

    [Header("Customer Type Unlocks")]
    [SerializeField] private int pinkCustomerUnlockDay = 5;
    [SerializeField] private int blueCustomerUnlockDay = 10;

    [Header("Runtime")]
    [SerializeField] private bool shiftRunning;
    [SerializeField] private bool closingOut;
    [SerializeField] private float timeRemaining;
    [SerializeField] private int groupsSpawnedThisShift;
    [SerializeField] private int groupsSpawnedThisMinute;
    [SerializeField] private float minuteWindowTimer;

    [Header("Shift Stats")]
    [SerializeField] private int groupsSeated;
    [SerializeField] private int ordersTaken;
    [SerializeField] private int ordersProcessed;
    [SerializeField] private int foodDelivered;
    [SerializeField] private int billsDelivered;
    [SerializeField] private int traysCleaned;
    [SerializeField] private int paymentsCompleted;
    [SerializeField] private int tipsEarned;

    [Header("Mood Counts")]
    [SerializeField] private int happyCustomers;
    [SerializeField] private int neutralCustomers;
    [SerializeField] private int angryCustomers;

    [Header("Cash Handling")]
    [SerializeField] private int cashErrors;

    private Coroutine spawnRoutine;
    private Coroutine closingResultsRoutine;
    private float angryBarVisual;
    private float neutralBarVisual;
    private bool warnedLastMinute;
    private bool rushAnnounced;
    private bool rushSoonAnnounced;
    private Coroutine panelAnimationRoutine;
    private Coroutine storeRedirectRoutine;
    private Button resultsContinueButton;
    private TMP_Text resultsContinueButtonText;
    private RectTransform resultsActionRect;
    private readonly Dictionary<RectTransform, ResultsRectLayout> authoredResultsLayout =
        new Dictionary<RectTransform, ResultsRectLayout>();
    private Vector2Int resultsLayoutScreenSize = new Vector2Int(-1, -1);
    private Rect resultsLayoutSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private bool resultsHaveOutcome;
    private GameOverReason currentResultsOutcome;
    private bool continuePurchaseArmed;
    private float continuePurchaseConfirmUntil;
    private PlayFabWalletManager resultsWallet;

    // Optional day authority hooks; unset in the original single-player scenes.
    public System.Func<bool> StartShiftInterception { get; set; }
    public bool ObserveDayOnly { get; set; }
    private bool fineDiningTutorialClockPaused;
    public void SetFineDiningTutorialClockPaused(bool paused)
    {
        if (gameObject.scene.name == "Lobby3Tutorial") fineDiningTutorialClockPaused = paused;
    }
    public bool ClosingOut => closingOut;
    private bool multiplayerDayEnded;
    public bool HasDayResults => MultiplayerDayBridge.IsActive ? multiplayerDayEnded : resultsHaveOutcome;
    public int[] CaptureMultiplayerReport() => new[] { ordersProcessed, foodDelivered,
        happyCustomers, neutralCustomers, angryCustomers, cashErrors, tipsEarned };
    public void ApplyMultiplayerReport(int[] values)
    {
        if (!ObserveDayOnly || values == null || values.Length != 7) return;
        ordersProcessed = values[0]; foodDelivered = values[1];
        happyCustomers = values[2]; neutralCustomers = values[3]; angryCustomers = values[4];
        cashErrors = values[5]; tipsEarned = values[6];
    }

    public void ApplyObservedDay(int day, bool running, bool closing, bool ended, float remaining)
    {
        if (!ObserveDayOnly) return;
        if ((running || closing) && MultiplayerSessionManager.Instance?.IsRecovering != true) observedLiveService = true;
        shiftRunning = running;
        closingOut = closing;
        if (!MultiplayerDayBridge.IsActive) resultsHaveOutcome = ended;
        timeRemaining = Mathf.Clamp(remaining, 0f, ShiftLengthSeconds);
        GameFlowManager.Instance?.ApplyObservedRestaurantDay(day, running || closing, ended);
        SetPanelVisible(dayIntroPanel, false);
        RefreshUI();
        if (ended && MultiplayerDayBridge.IsActive && !multiplayerDayEnded) ShowResults();
    }

    public bool ShiftRunning => shiftRunning;
    public int OpeningHour => openingHour;
    public int ClosingHour => closingHour;
    public bool ServiceActive => shiftRunning || closingOut;
    public float TimeRemaining => timeRemaining;
    public int HappyCustomers => happyCustomers;
    public int NeutralCustomers => neutralCustomers;
    public int AngryCustomers => angryCustomers;
    public int NeutralCustomerObjectiveMax => Mathf.Max(1, neutralBarMax);
    public int AngryCustomerObjectiveMax => Mathf.Max(1, angryBarMax);
    public TMP_Text DayHudText => dayText;
    public TMP_Text TimeHudText => timerText;
    public int CustomersServed => happyCustomers + neutralCustomers + angryCustomers;
    public float ShiftLengthSeconds =>
        Mathf.Max(1f, (closingHour - openingHour) * realSecondsPerGameHour);
    public float CurrentGameHour => Mathf.Clamp(
        openingHour + (ShiftLengthSeconds - timeRemaining) / realSecondsPerGameHour,
        openingHour,
        closingHour);
    public string FormattedGameTime => FormatClock(CurrentGameHour);
    public int CashErrors => cashErrors;
    public int TipsEarned => tipsEarned;
    public int MaxCustomersThisShift => maxCustomersToSpawn;
    public bool UsesManagementComputerForDayStart => useManagementComputerForDayStart;
    public int GroupsSpawnedThisShift => groupsSpawnedThisShift;
    public int GroupsSeated => groupsSeated;
    public int OrdersTaken => ordersTaken;
    public int OrdersProcessed => ordersProcessed;
    public int FoodDelivered => foodDelivered;
    public int BillsDelivered => billsDelivered;
    public int TraysCleaned => traysCleaned;
    public int PaymentsCompleted => paymentsCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (MultiplayerDayBridge.IsActive) ObserveDayOnly = !MultiplayerSessionManager.Instance.IsAuthority;

        LobbyHUDRoot.EnsureInstance()?.EnsurePauseController();

        if (bootstrapSingleRestaurantFlow)
            GameFlowManager.EnsureSingleRestaurantFlow(restaurantSceneName);

        if (enableAutonomousServiceBots && GetComponent<LobbyAutonomousService>() == null)
            gameObject.AddComponent<LobbyAutonomousService>();

        SetPanelVisible(resultsPanel, false);

        CacheResultsButtonLayout();
        CacheAuthoredResultsLayout();

        ResolveManagerComponents();
        ValidateSettings();
    }

    private void Start()
    {
        SetPanelVisible(resultsPanel, false);
        SetPanelVisible(dayIntroPanel, false);

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(ConfirmStartShift);
            playButton.onClick.AddListener(ConfirmStartShift);
        }

        if (resultsActionButton != null)
        {
            resultsActionButton.onClick.RemoveListener(OnResultsActionPressed);
            resultsActionButton.onClick.AddListener(OnResultsActionPressed);
        }

        SubscribeToDayChanges();
        ApplyCurrentDayConfiguration();

        // Pre-opening HUD and computer clock both begin at the restaurant's
        // authored opening time, even before the intro panel is confirmed.
        timeRemaining = ShiftLengthSeconds;

        RefreshUI();
        SetupMoodBars(true);

        if ((gameObject.scene.name == "Lobby2" || gameObject.scene.name == "Lobby3") && GameFlowManager.Instance != null &&
            GameFlowManager.Instance.CurrentRestaurantSessionState == GameFlowManager.RestaurantSessionState.DayComplete)
        {
            restoringResultsPresentation = true;
            ShowResults();
            restoringResultsPresentation = false;
            return;
        }
        if (useManagementComputerForDayStart)
        {
            SetPanelVisible(dayIntroPanel, false);
        }
        else if (enableAutonomousServiceBots && autoStartServiceWhenBotsEnabled)
            StartShift();
        else
            ShowShiftIntro();
    }

    private void Update()
    {
        if (playButton != null && TutorialSceneRuntimeMarker.UsesMinimalHUD)
            playButton.gameObject.SetActive(TutorialSceneRuntimeMarker.AllowsHUDControl("ShiftPlayButton", playButton.transform));
        RefreshStyledDayPanelLayout();
        if (ObserveDayOnly || (MultiplayerDayBridge.IsActive && !MultiplayerSessionManager.Instance.IsAuthority)) return;
        UpdateMoodBarsSmooth();
        UpdateContinuePurchaseConfirmation();
        RefreshResultsResponsiveLayout();

        if (!shiftRunning || fineDiningTutorialClockPaused)
            return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining < 0f)
            timeRemaining = 0f;

        minuteWindowTimer += Time.deltaTime;
        if (minuteWindowTimer >= 60f)
        {
            minuteWindowTimer = 0f;
            groupsSpawnedThisMinute = 0;
        }

        RefreshUI();

        if (!warnedLastMinute && timeRemaining <= realSecondsPerGameHour)
        {
            warnedLastMinute = true;
            ShowWarning("1 hour before store closing — finish the remaining customers.");
        }

        if (!rushSoonAnnounced && rushStartTimeRemainingSeconds > 0 && timeRemaining <= rushStartTimeRemainingSeconds + realSecondsPerGameHour)
        {
            rushSoonAnnounced = true;
            if(timeRemaining > rushStartTimeRemainingSeconds)
                ShowWarning("Rush hour in " + Mathf.CeilToInt((timeRemaining-rushStartTimeRemainingSeconds)/realSecondsPerGameHour*60) + " minutes — check your supplies.");
        }
        if (!rushAnnounced && rushStartTimeRemainingSeconds > 0 && timeRemaining <= rushStartTimeRemainingSeconds)
        {
            rushAnnounced = true;
            ShowWarning(FormattedGameTime + " — rush hour started!");
        }

        if (timeRemaining <= 0f)
            EndShift();
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(ConfirmStartShift);

        if (resultsActionButton != null)
            resultsActionButton.onClick.RemoveListener(OnResultsActionPressed);

        if (resultsContinueButton != null)
            resultsContinueButton.onClick.RemoveListener(OnContinueWithGoldCoinsPressed);

        if (resultsWallet != null)
            resultsWallet.OnWalletUpdated -= HandleResultsWalletUpdated;

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnDayChanged -= HandleDayChanged;

        if (Instance == this)
            Instance = null;
    }

    private void SubscribeToDayChanges()
    {
        if (GameFlowManager.Instance == null)
            return;

        GameFlowManager.Instance.OnDayChanged -= HandleDayChanged;
        GameFlowManager.Instance.OnDayChanged += HandleDayChanged;
    }

    private void HandleDayChanged(int _)
    {
        ApplyCurrentDayConfiguration();
        FindFirstObjectByType<LobbyUnlockManager>()?.ApplyUnlocks();
        RefreshUI();
    }

    private void ApplyCurrentDayConfiguration()
    {
        ResolveManagerComponents();
        ApplyDifficultyScaling();
        ApplyTakeoutUnlock();
        ApplyCustomerTypeUnlocks();
    }

    private void ResolveManagerComponents()
    {
        if (roleManagerObject != null)
            roleManager = roleManagerObject.GetComponent<RoleManager>();

        if (customerSystemObject != null)
            groupSpawner = customerSystemObject.GetComponent<GroupSpawner>();

        if (lobbyLineManagerObject != null)
            lobbyLineManager = lobbyLineManagerObject.GetComponent<LobbyLineManager>();

        if (kitchenManagerObject != null)
            kitchenManager = kitchenManagerObject.GetComponent<KitchenManager>();

        if (orderFlowManagerObject != null)
            orderFlowManager = orderFlowManagerObject.GetComponent<OrderFlowManager>();

        if (billManagerObject != null)
            billManager = billManagerObject.GetComponent<BillManager>();
    }

    private void ValidateSettings()
    {
        openingHour = Mathf.Clamp(openingHour, 0, 23);
        closingHour = Mathf.Clamp(closingHour, openingHour + 1, 24);
        realSecondsPerGameHour = Mathf.Max(1f, realSecondsPerGameHour);
        maxClosingGraceSeconds = Mathf.Max(0f, maxClosingGraceSeconds);

        if (maxCustomersToSpawn < 0)
            maxCustomersToSpawn = 0;

        if (maxGroupsPerMinute < 1)
            maxGroupsPerMinute = 1;

        if (spawnIntervalMin <= 0f)
            spawnIntervalMin = 6f;

        if (spawnIntervalMax < spawnIntervalMin)
            spawnIntervalMax = spawnIntervalMin + 1f;

        maxConcurrentGroups = Mathf.Max(1, maxConcurrentGroups);
        rushMaxConcurrentGroups = Mathf.Max(maxConcurrentGroups, rushMaxConcurrentGroups);
        mobileMaxConcurrentGroups = Mathf.Max(1, mobileMaxConcurrentGroups);
        mobileRushMaxConcurrentGroups = Mathf.Max(mobileMaxConcurrentGroups, mobileRushMaxConcurrentGroups);

        angryBarMax = Mathf.Max(1, angryBarMax);
        neutralBarMax = Mathf.Max(1, neutralBarMax);

        pinkCustomerUnlockDay = Mathf.Max(1, pinkCustomerUnlockDay);
        blueCustomerUnlockDay = Mathf.Max(1, blueCustomerUnlockDay);
        takeoutUnlockDay = Mathf.Max(1, takeoutUnlockDay);
    }

    private void SetupMoodBars(bool snapToCurrent)
    {
        if (angryBar != null)
        {
            angryBar.minValue = 0f;
            angryBar.maxValue = angryBarMax;

            if (snapToCurrent)
                angryBarVisual = angryCustomers;

            angryBar.value = angryBarVisual;
        }

        if (neutralBar != null)
        {
            neutralBar.minValue = 0f;
            neutralBar.maxValue = neutralBarMax;

            if (snapToCurrent)
                neutralBarVisual = neutralCustomers;

            neutralBar.value = neutralBarVisual;
        }
    }

    private void UpdateMoodBarsSmooth()
    {
        angryBarVisual = Mathf.Lerp(angryBarVisual, angryCustomers, Time.deltaTime * moodBarSmoothSpeed);
        neutralBarVisual = Mathf.Lerp(neutralBarVisual, neutralCustomers, Time.deltaTime * moodBarSmoothSpeed);

        if (Mathf.Abs(angryBarVisual - angryCustomers) < 0.01f) angryBarVisual = angryCustomers;
        if (Mathf.Abs(neutralBarVisual - neutralCustomers) < 0.01f) neutralBarVisual = neutralCustomers;

        if (angryBar != null) angryBar.value = angryBarVisual;
        if (neutralBar != null) neutralBar.value = neutralBarVisual;
    }

    public void ShowShiftIntro()
    {
        CancelShiftIntroTransition();
        if (!shiftRunning && !closingOut)
            timeRemaining = ShiftLengthSeconds;

        SetPanelVisible(resultsPanel, false);
        SetPanelVisible(dayIntroPanel, true);

        bool singleRestaurantFlow = GameFlowManager.Instance != null &&
                                    GameFlowManager.Instance.UsesSingleRestaurantFlow;

        if (dayIntroTitleText != null)
        {
            int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
            dayIntroTitleText.text = singleRestaurantFlow && GameFlowManager.Instance.IsEndlessRestaurantMode
                ? "Continue Service"
                : $"Day {day}";
        }


        if (dayIntroSummaryLeftText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Restaurant Hours</b>");
            sb.AppendLine($"{FormatClock(openingHour)} – {FormatClock(closingHour)}");
            sb.AppendLine("1 real minute = 1 in-game hour");
            sb.AppendLine();
            sb.AppendLine("Prepare stock and staff, then press PLAY to open.");
            dayIntroSummaryLeftText.text = sb.ToString().TrimEnd();
        }

        if (dayIntroSummaryMiddleText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Service Overview</b>");

            if (DailyFinanceBridge.Instance != null)
                sb.AppendLine("Target Revenue: ₱" + DailyFinanceBridge.Instance.TotalRequiredEarningsToday);
            else
                sb.AppendLine("Target Revenue: ₱0");

            sb.AppendLine("Max Customers: " + maxCustomersToSpawn);
            sb.AppendLine("Spawn Rate: " + maxGroupsPerMinute + "/min");
            dayIntroSummaryMiddleText.text = sb.ToString().TrimEnd();
        }

        if (dayIntroSummaryRightText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Reminder</b>");
            sb.AppendLine("Keep customers satisfied.");
            sb.AppendLine("Avoid angry walkouts.");
            sb.AppendLine("Review performance after shift.");
            dayIntroSummaryRightText.text = sb.ToString().TrimEnd();
        }

        if (styledDayPanels)
        {
            PopulateStyledDayIntro();
            RefreshStyledDayPanelLayout(true);
        }
        AnimatePanelIn(dayIntroPanel);
        RefreshUI();
        SetupMoodBars(true);
    }

    public void ConfirmStartShift()
    {
        if (introStartTransition) return;
        if (!TutorialSceneRuntimeMarker.AllowsHUDControl("ShiftPlayButton", playButton != null ? playButton.transform : null)) return;
        if (StartShiftInterception?.Invoke() == true) return;
        if (!CasualDiningPolishManager.EnsureInstance().TryAllowStartShift())
            return;

        RestockFlowCoordinator restock = RestockFlowCoordinator.Instance;
        if (restock != null && restock.TryShowStartReminder(
                BeginShiftStartTransition))
        {
            return;
        }

        BeginShiftStartTransition();
    }

    private IEnumerator StartShiftRoutine()
    {
        if (introStartTransition) yield break;
        introStartTransition = true;
        if (styledDayPanels)
            yield return DismissStyledDayIntro();
        if (styledDayPanels && (dayIntroPanel == null || !dayIntroPanel.activeInHierarchy))
        {
            introStartTransition = false;
            shiftIntroTransitionRoutine = null;
            yield break;
        }
        SetPanelVisible(dayIntroPanel, false);

        if (!styledDayPanels) yield return new WaitForSecondsRealtime(0.2f);

        introStartTransition = false;
        shiftIntroTransitionRoutine = null;
        StartShift();
    }

    public void StartShift()
    {
        var fastFood = FastFoodRestaurant.For(this);
        if (fastFood != null && (GameSaveManager.Instance == null || GameFlowManager.Instance == null ||
            !GameSaveManager.Instance.HasCompletedInitialLoad || (!MultiplayerDayBridge.IsActive && CampaignSaveStore.NeedsReload)))
        { ShowWarning("Restaurant progress is not ready. Please wait for loading to finish."); return; }
        if (fastFood != null && !fastFood.ValidateServiceSetup(out var setupProblem))
        {
            Debug.LogError("Lobby2 service setup: " + setupProblem, fastFood);
            ShowWarning("The restaurant setup needs attention before opening.");
            return;
        }
        if (fastFood != null && !fastFood.HasNavigation)
        {
            Debug.LogError("Lobby2 needs its navigation baked and validated using Dine In > Fast Food > Bake Lobby2 Navigation.", fastFood);
            ShowWarning("The restaurant is not ready to open yet.");
            return;
        }
        if (gameObject.scene.name == "Lobby3" &&
            !gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>())
                .Any(surface => surface.isActiveAndEnabled && surface.navMeshData != null))
        {
            Debug.LogError("Lobby3 needs a baked navigation asset. Use Dine In > Fine Dining > Bake Lobby3 Navigation.", this);
            ShowWarning("Fine Dining navigation is not ready. Bake Lobby3 navigation before opening.");
            return;
        }
        if (MultiplayerDayBridge.IsActive && multiplayerDayEnded) return;
        if (StartShiftInterception?.Invoke() == true || ObserveDayOnly) return;
        if (shiftRunning || closingOut)
            return;

        var multiplayerMissing = MultiplayerDayBridge.IsActive
            ? ManagementComputerController.GetMissingMultiplayerStaffRoles() : null;
        if (useManagementComputerForDayStart && (multiplayerMissing != null
            ? multiplayerMissing.Count > 0
            : EmployeeManager.Instance == null || !EmployeeManager.Instance.HasAllRequiredRolesAssigned))
        {
            string missing = multiplayerMissing != null ? string.Join(", ", multiplayerMissing)
                : EmployeeManager.Instance == null
                ? "STAFF SYSTEM UNAVAILABLE"
                : string.Join(", ", EmployeeManager.Instance.GetMissingRequiredRoles().ConvertAll(EmployeeRoleCatalog.DisplayName));
            ShowWarning("COVER EVERY ROLE BEFORE STARTING: " + missing.ToUpperInvariant());
            return;
        }

        Time.timeScale = 1f;

        ResolveManagerComponents();
        ApplyDifficultyScaling();
        ApplyTakeoutUnlock();
        ApplyCustomerTypeUnlocks();
        var fineDining = FineDiningReservations.For(this);
        fineDining?.EnsureDay();
        if (fineDining != null) maxCustomersToSpawn = fineDining.GroupBudget;
        if (fastFood != null || fineDining != null)
        {
            try { GameFlowManager.Instance.MarkRestaurantServiceStarted(); }
            catch (System.Exception error)
            {
                Debug.LogError("[FastFood] Could not save the start of service: " + error.Message);
                ShowWarning("Your preparation could not be saved. Please try opening again.");
                return;
            }
        }
        ResetShiftRuntime();
        fastFood?.BeginServiceDay();
        HygieneManager.Instance?.ResetForShift();

        timeRemaining = ShiftLengthSeconds;
        // Lock HR only after every start gate and preparation save has succeeded.
        EmployeeManager.Instance?.LockAllSlots();
        shiftRunning = true;
        closingOut = false;
        if (fastFood == null && fineDining == null) GameFlowManager.Instance?.MarkRestaurantServiceStarted();

        SetPanelVisible(resultsPanel, false);
        SetPanelVisible(dayIntroPanel, false);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnCustomersRoutine());

        RefreshUI();
        SetupMoodBars(true);
        ShowWarning($"Doors open at {FormatClock(openingHour)}. Keep customers satisfied.");
    }

    /// <summary>
    /// Reads the current day from GameFlowManager and evaluates each AnimationCurve
    /// to override flat spawn settings before the shift starts.
    /// Also applies the AlienApprovalManager spawn modifier so high approval
    /// brings more alien visitors and low approval reduces foot traffic.
    /// </summary>
    private void ApplyDifficultyScaling()
    {
        if (GameFlowManager.Instance == null || maxScalingDay <= 1)
            return;

        int day = GameFlowManager.Instance.ProgressionDay;
        float t = Mathf.Clamp01((float)(day - 1) / (maxScalingDay - 1));

        maxCustomersToSpawn = Mathf.RoundToInt(maxCustomersCurve.Evaluate(t));
        maxGroupsPerMinute  = Mathf.Max(1, Mathf.RoundToInt(groupsPerMinuteCurve.Evaluate(t)));
        spawnIntervalMin    = Mathf.Max(1f, spawnIntervalMinCurve.Evaluate(t));
        spawnIntervalMax    = Mathf.Max(spawnIntervalMin + 1f, spawnIntervalMaxCurve.Evaluate(t));
        var fastFood = FastFoodProgressionSettings.Current;
        if (fastFood != null)
        {
            maxCustomersToSpawn = Mathf.Max(1, Mathf.RoundToInt(fastFood.Evaluate(fastFood.groups, day)));
            maxGroupsPerMinute = Mathf.Max(1, Mathf.RoundToInt(fastFood.Evaluate(fastFood.groupsPerMinute, day)));
            spawnIntervalMin = Mathf.Max(1f, fastFood.Evaluate(fastFood.spawnSeconds, day));
            spawnIntervalMax = spawnIntervalMin + 6f;
            maxConcurrentGroups = Mathf.Max(1, Mathf.RoundToInt(fastFood.Evaluate(fastFood.concurrentGroups, day)));
            rushMaxConcurrentGroups = maxConcurrentGroups + 1;
        }

        // Apply approval-based spawn modifier: word of mouth from happy aliens
        // brings more visitors; repeated dissatisfaction drives them away.
        if (AlienApprovalManager.Instance != null)
        {
            int approvalModifier = AlienApprovalManager.Instance.GetSpawnModifier();
            maxCustomersToSpawn = Mathf.Max(1, maxCustomersToSpawn + approvalModifier);
        }
    }

    public void EndShift()
    {
        if (ObserveDayOnly || (MultiplayerDayBridge.IsActive && !MultiplayerSessionManager.Instance.IsAuthority)) return;
        if (!shiftRunning)
            return;

        shiftRunning = false;
        closingOut = true;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        ShowWarning(FormatClock(closingHour) + " — closed to new customers. Finishing remaining orders.");
        if (closingResultsRoutine != null)
            StopCoroutine(closingResultsRoutine);
        closingResultsRoutine = StartCoroutine(ShowResultsWhenClear());
    }

    private IEnumerator ShowResultsWhenClear()
    {
        var fastFood = FastFoodRestaurant.For(this);
        fastFood?.StopAdmissions();
        float serviceDeadline = Time.time + (fastFood != null ? fastFood.ClosingServiceSeconds : maxClosingGraceSeconds);
        while ((fastFood != null ? fastFood.ActiveCustomerCount : FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Length) > 0 &&
               Time.time < serviceDeadline)
        {
            if (ObserveDayOnly || (MultiplayerDayBridge.IsActive && !MultiplayerSessionManager.Instance.IsAuthority)) yield break;
            // Lobby2's registered count is cheap; preserve the legacy scene-scan cadence elsewhere.
            yield return fastFood != null ? null : new WaitForSeconds(1f);
        }

        if (ObserveDayOnly || (MultiplayerDayBridge.IsActive && !MultiplayerSessionManager.Instance.IsAuthority)) yield break;
        if (fastFood != null)
        {
            fastFood.BeginClosingExit();
            float exitDeadline = Time.time + fastFood.ClosingExitSeconds;
            while (fastFood.ActiveCustomerCount > 0 && Time.time < exitDeadline) yield return null;
            fastFood.FinishClosing();
        }
        if (FineDiningReservations.For(this) != null)
        {
            foreach (var group in FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None))
                if (group.gameObject.scene == gameObject.scene) group.BeginFineDiningClosing();
            float exitDeadline = Time.time + maxClosingGraceSeconds;
            while (FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Any(g => g.gameObject.scene == gameObject.scene) &&
                   Time.time < exitDeadline) yield return new WaitForSeconds(1f);
            foreach (var group in FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None))
                if (group.gameObject.scene == gameObject.scene) group.FinishFineDiningClosing();
            yield return null;
        }
        closingOut = false;
        closingResultsRoutine = null;
        if (MultiplayerDayBridge.IsActive)
        {
            foreach (var customer in FindObjectsByType<MultiplayerCustomerSpawn>(FindObjectsSortMode.None)) customer.FinishClosing();
            yield return null;
        }
        ShowResults();
    }

    public bool EndDayNowDebug()
    {
        if (MultiplayerDayBridge.IsActive) return false;
        if (!Application.isEditor && !Debug.isDebugBuild)
            return false;
        rewardDebugCompletion = true;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (closingResultsRoutine != null)
        {
            StopCoroutine(closingResultsRoutine);
            closingResultsRoutine = null;
        }

        shiftRunning = false;
        closingOut = false;
        timeRemaining = 0f;
        FastFoodRestaurant.For(this)?.BeginClosingExit();
        FastFoodRestaurant.For(this)?.FinishClosing();
        ShowResults();
        return true;
    }

    public void RestartShift()
    {
        if (MultiplayerDayBridge.IsActive) return;
        ShowShiftIntro();
    }

    public void OnResultsActionPressed()
    {
        if (MultiplayerDayBridge.IsActive)
        {
            var session = MultiplayerSessionManager.Instance;
            if (session.Ended) session.LeaveToMenu();
            else MultiplayerDayBridge.TryRequestNextDay();
            return;
        }
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.UsesSingleRestaurantFlow)
        {
            if (resultsHaveOutcome && currentResultsOutcome != GameOverReason.EarthSaved)
            {
                Time.timeScale = 1f;
                GameFlowManager.Instance.ResetRun();
                return;
            }

            GameFlowManager.Instance.CompleteRestaurantDay();
            return;
        }

        if (string.IsNullOrWhiteSpace(managementSceneName))
        {
            Debug.LogWarning("[GameDayManager] Management scene name is empty.");
            return;
        }

        SceneManager.LoadScene(managementSceneName, LoadSceneMode.Single);
    }

    private void ResetShiftRuntime()
    {
        rewardDebugCompletion = false;
        TutorialGameModeEntry.MarkRegularShiftStarted();
        groupsSpawnedThisShift = 0;
        groupsSpawnedThisMinute = 0;
        minuteWindowTimer = 0f;
        closingOut = false;

        groupsSeated = 0;
        ordersTaken = 0;
        ordersProcessed = 0;
        foodDelivered = 0;
        billsDelivered = 0;
        traysCleaned = 0;
        paymentsCompleted = 0;
        cashErrors = 0;
        tipsEarned = 0;

        happyCustomers = 0;
        neutralCustomers = 0;
        angryCustomers = 0;

        angryBarVisual = 0f;
        neutralBarVisual = 0f;
        warnedLastMinute = false;
        rushAnnounced = false;
        rushSoonAnnounced = false;

        SetupMoodBars(true);
    }

    public void PrepareNextMultiplayerDay()
    {
        if (!MultiplayerDayBridge.IsActive) return;
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        if (closingResultsRoutine != null) StopCoroutine(closingResultsRoutine);
        spawnRoutine = null; closingResultsRoutine = null;
        shiftRunning = false; multiplayerDayEnded = false; resultsHaveOutcome = false;
        continuePurchaseArmed = false;
        SetPanelVisible(resultsPanel, false); SetPanelVisible(dayIntroPanel, false);
        ResetShiftRuntime();
        ApplyCurrentDayConfiguration();
        timeRemaining = ShiftLengthSeconds;
        MultiplayerSessionManager.Instance.GetComponent<MultiplayerTaskClaims>()?.ResetForNextDay();
        MultiplayerServiceActions.Active?.ResetDay();
        MultiplayerSessionManager.Instance.GetComponent<MultiplayerObjectBridge>()?.ResetObservedObjects();
        TakeoutFlowManager.Instance?.ResetMultiplayerDay();
        foreach (var bot in FindObjectsByType<AutonomousStaffBot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) bot.ResetMultiplayerDay();
        foreach (var trolley in FindObjectsByType<BotTrolleyCarrier>(FindObjectsInactive.Include, FindObjectsSortMode.None)) trolley.ResetMultiplayerDay();
        foreach (var hands in FindObjectsByType<WaiterHands>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { hands.ClearTicket(); hands.ClearBill(); hands.ClearMoney(); if (hands.holdingTray != null) hands.holdingTray.NetworkCarryLocked = false; hands.ClearTray(); }
        foreach (var hands in FindObjectsByType<BusserHands>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hands.ClearTray();
        foreach (var bag in FindObjectsByType<TakeoutBagInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(bag.gameObject);
        foreach (var money in FindObjectsByType<MoneyPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(money.gameObject);
        FindFirstObjectByType<KitchenManager>()?.ResetMultiplayerDay();
        BillManager.Instance?.ResetMultiplayerDay();
        foreach (var paper in FindObjectsByType<BillPaper>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(paper.gameObject);
        foreach (var tray in FindObjectsByType<FoodTray>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(tray.gameObject);
        foreach (var booth in FindObjectsByType<Booth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            booth.ResetForMultiplayerDay();
        RefreshUI();
    }

    private IEnumerator SpawnCustomersRoutine()
    {
        yield return WaitForCustomerArrival(1f);
        WaitForSeconds blockedSpawnWait = new WaitForSeconds(Mathf.Max(0.25f, blockedSpawnRetrySeconds));

        var reservations = FineDiningReservations.For(this);
        float nextWalkIn = Time.time;
        while (shiftRunning)
        {
            if (reservations != null)
            {
                var booking = reservations.DueBooking();
                if (booking != null) TrySpawnCustomerGroup(booking);
                else if (Time.time >= nextWalkIn && reservations.CanAdmitWalkIn && groupsSpawnedThisShift + reservations.UnspawnedGroups < reservations.GroupBudget
                    && TrySpawnCustomerGroup())
                    nextWalkIn = Time.time + Mathf.Max(reservations.WalkInIntervalSeconds, Random.Range(spawnIntervalMin, spawnIntervalMax));
                yield return blockedSpawnWait;
                continue;
            }
            bool canSpawnMoreShift = groupsSpawnedThisShift < maxCustomersToSpawn;
            bool canSpawnThisMinute = groupsSpawnedThisMinute < CurrentGroupsPerMinuteLimit;

            if (canSpawnMoreShift && canSpawnThisMinute)
            {
                bool spawned = TrySpawnCustomerGroup();
                if (spawned)
                {
                    float intervalMultiplier = IsRushHour ? rushSpawnIntervalMultiplier : 1f;
                    float delay = Random.Range(spawnIntervalMin, spawnIntervalMax) * intervalMultiplier;
                    yield return WaitForCustomerArrival(delay);
                    continue;
                }
            }

            // Avoid retrying every rendered frame while the lobby or minute is full.
            yield return blockedSpawnWait;
        }
    }

    private IEnumerator WaitForCustomerArrival(float remaining)
    {
        while (shiftRunning && remaining > 0f)
        {
            yield return null;
            remaining -= Time.deltaTime * (RestaurantStockout.Shortage == 0 ? 1f : RestaurantStockout.SpawnRate);
        }
    }

    private void ApplyTakeoutUnlock()
    {
        if (groupSpawner == null)
            return;

        int currentDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        bool shouldEnable = FineDiningReservations.For(this) == null && (FastFoodProgressionSettings.Current != null || currentDay >= takeoutUnlockDay);

        groupSpawner.SetTakeoutEnabled(shouldEnable);

        Debug.Log($"[GameDayManager] Takeout {(shouldEnable ? "ENABLED" : "DISABLED")} (current day: {currentDay}, unlock day: {takeoutUnlockDay}).");
    }


    private bool IsRushHour => shiftRunning &&
                               rushStartTimeRemainingSeconds > 0f &&
                               timeRemaining <= rushStartTimeRemainingSeconds;

    private int CurrentGroupsPerMinuteLimit => IsRushHour
        ? maxGroupsPerMinute + rushAdditionalGroupsPerMinute
        : maxGroupsPerMinute;

    private void ApplyCustomerTypeUnlocks()
    {
        if (groupSpawner == null)
            return;

        int currentDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;

        var fastFood = FastFoodProgressionSettings.Current;
        bool enablePink = groupSpawner.NormalFastFood || currentDay >= (fastFood != null ? fastFood.pinkCustomerDay : pinkCustomerUnlockDay);
        bool enableBlue = groupSpawner.NormalFastFood || currentDay >= (fastFood != null ? fastFood.blueCustomerDay : blueCustomerUnlockDay);

        groupSpawner.SetCustomerTypeAvailability(true, enablePink, enableBlue);

        Debug.Log($"[GameDayManager] Customer type unlocks applied | Day: {currentDay} | Green: ENABLED | Pink: {(enablePink ? "ENABLED" : "DISABLED")} (unlock day: {pinkCustomerUnlockDay}) | Blue: {(enableBlue ? "ENABLED" : "DISABLED")} (unlock day: {blueCustomerUnlockDay})");
    }

    public void SetTakeoutEnabled(bool enabled)
    {
        if (groupSpawner == null)
        {
            Debug.LogWarning("[GameDayManager] SetTakeoutEnabled — GroupSpawner not resolved.");
            return;
        }

        groupSpawner.SetTakeoutEnabled(enabled);
        Debug.Log($"[GameDayManager] Takeout {(enabled ? "ENABLED" : "DISABLED")}.");
    }

    public void SetCustomerTypeEnabled(CustomerGroup.CustomerType type, bool enabled)
    {
        if (groupSpawner == null)
        {
            Debug.LogWarning("[GameDayManager] SetCustomerTypeEnabled — GroupSpawner not resolved.");
            return;
        }

        groupSpawner.SetCustomerTypeEnabled(type, enabled);
        Debug.Log($"[GameDayManager] Customer type {type} {(enabled ? "ENABLED" : "DISABLED")}.");
    }

    private const float StopSpawnTimeRemainingSeconds = 15f;

    private bool TrySpawnCustomerGroup(FineDiningBooking booking = null)
    {
        if (!shiftRunning)
            return false;

        // Already accepted Fine Dining reservations are commitments, not new demand.
        var fineDining = FineDiningReservations.For(this);
        bool committedArrival = booking != null && fineDining != null;
        if (!committedArrival && timeRemaining <= StopSpawnTimeRemainingSeconds)
            return false;

        if (groupsSpawnedThisShift >= (fineDining != null ? fineDining.GroupBudget : maxCustomersToSpawn))
            return false;

        // Confirmed reservations are scheduled by the service clock, not the
        // random walk-in cadence (one real minute is an entire restaurant hour).
        // Queue capacity, the shift budget and concurrent actor limits still apply.
        if (booking == null && groupsSpawnedThisMinute >= CurrentGroupsPerMinuteLimit)
            return false;

        int concurrentLimit = IsRushHour
            ? Mathf.Max(maxConcurrentGroups, rushMaxConcurrentGroups)
            : maxConcurrentGroups;
        if (Application.isMobilePlatform)
        {
            int mobileLimit = IsRushHour
                ? Mathf.Max(mobileMaxConcurrentGroups, mobileRushMaxConcurrentGroups)
                : mobileMaxConcurrentGroups;
            concurrentLimit = Mathf.Min(concurrentLimit, mobileLimit);
        }

        if (FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length >= concurrentLimit)
            return false;

        if (groupSpawner == null)
        {
            Debug.LogWarning("[GameDayManager] GroupSpawner is missing.");
            return false;
        }

        var admission = FastFoodRestaurant.For(this);
        if (admission != null && !admission.CanAdmitCustomer) return false;
        CustomerGroup spawnedGroup = booking != null ? groupSpawner.SpawnGroup(booking.size, booking.id) : groupSpawner.SpawnGroup();
        if (spawnedGroup == null)
        {
            Debug.LogWarning("[GameDayManager] SpawnGroup failed.");
            return false;
        }

        groupsSpawnedThisShift++;
        groupsSpawnedThisMinute++;
        return true;
    }

    private void RefreshUI()
    {
        if (dayText != null)
        {
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.UsesSingleRestaurantFlow)
            {
                dayText.text = GameFlowManager.Instance.IsEndlessRestaurantMode
                    ? "Endless Service"
                    : $"Day {GameFlowManager.Instance.CurrentDay}";
            }
            else
            {
                dayText.text = "Shift";
            }
        }

        if (timerText != null)
            timerText.text = FormattedGameTime;

        if (progressBar != null)
            progressBar.value = CalculateProgress01();

        RefreshProgressMoneyText();
    }

    private void RefreshProgressMoneyText()
    {
        if (progressMoneyText == null)
            return;

        if (DailyFinanceBridge.Instance == null)
        {
            progressMoneyText.text = "₱0 / ₱0";
            return;
        }

        progressMoneyText.text =
            $"₱{DailyFinanceBridge.Instance.EarnedToday} / " +
            $"₱{DailyFinanceBridge.Instance.TotalRequiredEarningsToday}";
    }

    private float CalculateProgress01()
    {
        if (DailyFinanceBridge.Instance != null)
            return DailyFinanceBridge.Instance.GetProgress01();

        return 0f;
    }

    public int[] CaptureFastFoodReport() => new[] { groupsSeated, ordersTaken, ordersProcessed, foodDelivered, billsDelivered, traysCleaned, paymentsCompleted, tipsEarned, happyCustomers, neutralCustomers, angryCustomers, cashErrors, groupsSpawnedThisShift };
    public void RestoreFastFoodReport(int[] stats)
    {
        if ((!FastFoodScene.Contains(gameObject.scene) && gameObject.scene.name != "Lobby3") || stats == null || stats.Length != 13) return;
        groupsSeated = Mathf.Max(0, stats[0]);
        ordersTaken = Mathf.Max(0, stats[1]);
        ordersProcessed = Mathf.Max(0, stats[2]);
        foodDelivered = Mathf.Max(0, stats[3]);
        billsDelivered = Mathf.Max(0, stats[4]);
        traysCleaned = Mathf.Max(0, stats[5]);
        paymentsCompleted = Mathf.Max(0, stats[6]);
        tipsEarned = Mathf.Max(0, stats[7]);
        happyCustomers = Mathf.Max(0, stats[8]);
        neutralCustomers = Mathf.Max(0, stats[9]);
        angryCustomers = Mathf.Max(0, stats[10]);
        cashErrors = Mathf.Max(0, stats[11]);
        groupsSpawnedThisShift = Mathf.Max(0, stats[12]);
    }

    private bool rewardResultsPending;
    private bool rewardDebugCompletion;
    private void ShowResults()
    {
        if (rewardResultsPending) return;
        FineDiningReservations.For(this)?.FinishDay();
        bool multiplayer = MultiplayerDayBridge.IsActive;
        if (multiplayer && multiplayerDayEnded) return;
        if (multiplayer) multiplayerDayEnded = true;
        GameFlowManager flow = GameFlowManager.Instance;
        bool singleRestaurantFlow = flow != null && flow.UsesSingleRestaurantFlow;
        int earnedStars = CalculateEarnedStars();
        if (singleRestaurantFlow && (!multiplayer || MultiplayerSessionManager.Instance.IsAuthority))
            flow.FinalizeRestaurantDayForResults(earnedStars);
        else if (!multiplayer)
            AlienApprovalManager.Instance?.RegisterDailyStarRating(
                earnedStars,
                flow != null ? flow.CurrentDay : 1);

        resultsHaveOutcome = singleRestaurantFlow &&
                             flow.TryGetRestaurantDayOutcome(out currentResultsOutcome);
        continuePurchaseArmed = false;

        if (multiplayer && MultiplayerSessionManager.Instance.IsAuthority)
        {
            MultiplayerSessionManager.Instance.CompleteDay(flow.CurrentDay,
                resultsHaveOutcome && currentResultsOutcome != GameOverReason.EarthSaved ? currentResultsOutcome.ToString() : null);
            MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.PublishNow();
        }
        bool succeeded = multiplayer ? MultiplayerDayBridge.RewardDaySucceeded : singleRestaurantFlow && flow.LastRestaurantDaySucceeded;
        if (succeeded && !rewardDebugCompletion && !restoringResultsPresentation &&
            (!ObserveDayOnly || observedLiveService) && GameSaveManager.Instance?.IsApplyingSave != true)
        {
            rewardResultsPending = true;
            if (DailyRewardSlotUI.TryOfferBeforeResults(() =>
            {
                rewardResultsPending = false;
                if (this != null && isActiveAndEnabled) PresentResultsAfterReward(earnedStars, singleRestaurantFlow, multiplayer);
            })) return;
            rewardResultsPending = false;
        }
        PresentResultsAfterReward(earnedStars, singleRestaurantFlow, multiplayer);
    }
    private void PresentResultsAfterReward(int earnedStars, bool singleRestaurantFlow, bool multiplayer)
    {
        if (multiplayer) MultiplayerDayBridge.MarkRewardPresentationFinished();
        var flow = GameFlowManager.Instance;
        SetPanelVisible(resultsPanel, true);

        int dayRevenue = DailyFinanceBridge.Instance != null
            ? DailyFinanceBridge.Instance.EarnedToday
            : 0;
        int targetRevenue = DailyFinanceBridge.Instance != null
            ? DailyFinanceBridge.Instance.TotalRequiredEarningsToday
            : 0;

        if (resultsHaveOutcome)
            PopulateOutcomeResults(dayRevenue, targetRevenue);
        else
            PopulateStandardResults(singleRestaurantFlow, dayRevenue, targetRevenue);

        if (FineDiningReservations.For(this) is { } reservations && resultsSummaryText != null)
            resultsSummaryText.text += "\n\n" + reservations.ResultSummary;
        ConfigureResultsActions(singleRestaurantFlow);
        if (multiplayer)
        {
            RefreshMultiplayerResultsAction();
            if (resultsHaveOutcome && currentResultsOutcome != GameOverReason.EarthSaved && resultsCashText != null)
                resultsCashText.text = "<b>RUN RECORDED</b>\nThis session has ended. Start a fresh run from the multiplayer menu.";
            MultiplayerSessionManager.Instance.GetComponent<MultiplayerDayBridge>()?.PublishNow();
        }
        if (HasDayPanelV2)
            dayPanelV2View.BindReport(CaptureDayPanelReport(dayRevenue, targetRevenue, earnedStars));
        RefreshResultsResponsiveLayout(true);
        instantResults = restoringResultsPresentation || ObserveDayOnly && !observedLiveService || MultiplayerSessionManager.Instance?.IsRecovering == true;
        resultsCuePending = !instantResults && !restoringResultsPresentation && GameSaveManager.Instance?.IsApplyingSave != true &&
            (!MultiplayerDayBridge.IsActive || (!ObserveDayOnly || observedLiveService) && MultiplayerSessionManager.Instance?.IsRecovering != true) &&
            (!resultsHaveOutcome || currentResultsOutcome == GameOverReason.EarthSaved);
        PrepareResultStars(earnedStars);

        AnimateResults(earnedStars);
    }

    public void RefreshMultiplayerResultsAction()
    {
        if (!MultiplayerDayBridge.IsActive) return;
        var session = MultiplayerSessionManager.Instance;
        if (resultsActionButton != null)
        {
            resultsActionButton.gameObject.SetActive(true);
            resultsActionButton.interactable = session.Ended || session.IsHostConnection;
        }
        if (resultsActionButtonText != null) resultsActionButtonText.text = session.Ended
            ? "RETURN TO MENU" : session.IsHostConnection ? "NEXT DAY" : "WAITING FOR HOST";
        if (resultsContinueButton != null) resultsContinueButton.gameObject.SetActive(false);
        if (HasDayPanelV2) dayPanelV2View.LayoutActions(resultsActionButton, resultsContinueButton);
    }

    private DayPanelReportSnapshot CaptureDayPanelReport(int revenue, int targetRevenue, int earnedStars)
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        DailyObjectiveManager objectives = DailyObjectiveManager.Instance;
        FineDiningReservations reservations = FineDiningReservations.For(this);
        bool hasGrade = objectives != null && objectives.HasPreviousDayResult;
        bool victory = resultsHaveOutcome && currentResultsOutcome == GameOverReason.EarthSaved;
        return new DayPanelReportSnapshot(
            title: resultsTitleText != null ? resultsTitleText.text : $"Day {day} Report",
            status: resultsHaveOutcome ? (victory ? "CAMPAIGN COMPLETE" : "RUN ENDED") : GetShiftStatusText(),
            coaching: GetMostUsefulImprovement(Mathf.Max(0, targetRevenue - revenue)),
            notice: !resultsHaveOutcome ? string.Empty : victory ? "Continue your restaurant in Endless Service."
                : MultiplayerDayBridge.IsActive ? "This session is recorded and has ended. Start a fresh run from the multiplayer menu."
                : $"<b>Restart free:</b> Day 1 and 30% approval. Staff, stock, money, purchases and unlocks reset; your restaurant stays.\n" +
                  $"<b>Continue • {continueGoldCoinCost} GC:</b> Keep this day, restaurant and unlocks.",
            outcomeReason: resultsHaveOutcome ? GetDayPanelOutcomeReason(currentResultsOutcome) : string.Empty,
            objectiveGrade: hasGrade ? objectives.LastGrade.ToString() : string.Empty,
            day: day, revenue: revenue, targetRevenue: targetRevenue, tips: tipsEarned, cashErrors: cashErrors,
            ordersProcessed: ordersProcessed, foodServed: foodDelivered, happy: happyCustomers,
            neutral: neutralCustomers, angry: angryCustomers, approval: approval,
            reservationsBooked: reservations != null ? reservations.AcceptedGroups : 0,
            reservationsOnTime: reservations != null ? reservations.Bookings.Count(booking => booking.seatedOnTime) : 0,
            reservationsUnserved: reservations != null ? reservations.Bookings.Count(booking => booking.status == FineDiningBookingStatus.Left) : 0,
            earnedStars: earnedStars, hasObjectiveGrade: hasGrade, hasReservations: reservations != null,
            isOutcome: resultsHaveOutcome, isVictory: victory);
    }

    private static string GetDayPanelOutcomeReason(GameOverReason reason)
    {
        return reason switch
        {
            GameOverReason.Bankruptcy => "Funds reached ₱0 after expenses.",
            GameOverReason.ApprovalCollapsed => "Alien approval reached 0%.",
            GameOverReason.EarthSaved => "You completed Day 30 with enough approval.",
            GameOverReason.EarthConqueredDay30 => "Day 30 ended below the 40% approval target.",
            _ => "The campaign has ended."
        };
    }

    private void PopulateStandardResults(bool singleRestaurantFlow, int revenue, int targetRevenue)
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        int revenueGap = Mathf.Max(0, targetRevenue - revenue);

        if (resultsTitleText != null)
            resultsTitleText.text = singleRestaurantFlow ? $"Day {day} Report" : "Half-Day Report";

        if (resultsSummaryText != null)
        {
            string revenueResult = revenueGap == 0
                ? "Target reached"
                : $"Need ₱{revenueGap} more";
            resultsSummaryText.text =
                $"<b>REVENUE</b>\n₱{revenue} / ₱{targetRevenue}\n{revenueResult}\n\n" +
                $"<b>ORDERS</b>\n{ordersProcessed} processed • {foodDelivered} served";
        }

        if (resultsCustomersText != null)
        {
            resultsCustomersText.text =
                $"<b>CUSTOMERS</b>\nHappy {happyCustomers} • Neutral {neutralCustomers}\nAngry {angryCustomers}\n\n" +
                $"<b>WORK ON NEXT</b>\n{GetMostUsefulImprovement(revenueGap)}";
        }

        if (resultsCashText != null)
        {
            string cashStatus = cashErrors == 0
                ? "Perfect cash handling"
                : $"Fix {cashErrors} cash error{(cashErrors == 1 ? string.Empty : "s")}";
            string objectiveResult = GetObjectiveResultSummary();
            resultsCashText.text =
                $"<b>APPROVAL</b>\n{approval}%\n{objectiveResult}\n\n" +
                $"<b>CASH & TIPS</b>\n{cashStatus} • ₱{tipsEarned} tips";
        }

        if (resultsStarsText != null)
            resultsStarsText.text = $"{GetShiftStatusText()}  •  Approval {approval}%";
    }

    private void PopulateOutcomeResults(int revenue, int targetRevenue)
    {
        bool savedEarth = currentResultsOutcome == GameOverReason.EarthSaved;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        if (resultsTitleText != null)
        {
            resultsTitleText.text = savedEarth
                ? "Earth Is Safe!"
                : currentResultsOutcome == GameOverReason.Bankruptcy
                    ? "Restaurant Bankrupt"
                    : "Game Over";
        }

        if (resultsSummaryText != null)
        {
            string reason = GetDayPanelOutcomeReason(currentResultsOutcome);
            resultsSummaryText.text =
                $"<b>{(savedEarth ? "CAMPAIGN COMPLETE" : "WHY THE RUN ENDED")}</b>\n{reason}\n\n" +
                $"<b>FINAL DAY</b>\nDay {day} • ₱{revenue} / ₱{targetRevenue}";
        }

        if (resultsCustomersText != null)
        {
            resultsCustomersText.text =
                $"<b>LAST SHIFT</b>\nHappy {happyCustomers} • Neutral {neutralCustomers}\nAngry {angryCustomers} • Cash errors {cashErrors}\n\n" +
                $"<b>{(savedEarth ? "WHAT WORKED" : "TRY NEXT RUN")}</b>\n" +
                GetMostUsefulImprovement(Mathf.Max(0, targetRevenue - revenue));
        }

        if (resultsCashText != null)
        {
            resultsCashText.text = savedEarth
                ? $"<b>FINAL APPROVAL</b>\n{approval}%\n\n<b>NEXT</b>\nContinue with your restaurant in Endless Service."
                : $"<b>FREE RESTART</b>\nReturns to Day 1 and 30% approval.\n" +
                  "Staff, stock, money, purchases and unlocks restart.\nYour restaurant itself stays.\n\n" +
                  $"<b>CONTINUE • {continueGoldCoinCost} GC</b>\nKeep this run, restaurant and unlocks.";
        }

        if (resultsStarsText != null)
        {
            resultsStarsText.text = savedEarth
                ? $"CAMPAIGN COMPLETE  •  Approval {approval}%"
                : $"RUN ENDED  •  Approval {approval}%";
        }
    }

    private string GetMostUsefulImprovement(int revenueGap)
    {
        bool fastFood = FastFoodRestaurant.For(this) != null;
        if (CustomersServed <= 0)
            return fastFood ? "Complete at least one customer order." : "Seat and fully serve at least one group.";

        float angryRatio = angryCustomers / (float)CustomersServed;
        if (angryRatio >= 0.2f)
            return fastFood ? "Keep counters staffed, meals available and tables clean to reduce waits." : "Reduce waits: greet, serve food and collect bills sooner.";

        if (cashErrors > 0)
            return "Finish every payment and return the exact change.";

        if (revenueGap > 0)
            return "Serve more groups and complete every customer payment.";

        if (neutralCustomers > happyCustomers)
            return fastFood ? "Shorten ordering and pickup waits to turn neutral guests happy." : "Deliver food and bills faster to turn neutral guests happy.";

        return "Keep your service speed and accurate cash handling.";
    }

    private static string GetObjectiveResultSummary()
    {
        DailyObjectiveManager objectives = DailyObjectiveManager.Instance;
        if (objectives == null || !objectives.HasPreviousDayResult)
            return "Objective not scored";

        return $"Objective grade: {objectives.LastGrade}";
    }

    private string GetResultsActionLabel(bool singleRestaurantFlow)
    {
        if (!singleRestaurantFlow || GameFlowManager.Instance == null)
            return "Back to Management";

        if (GameFlowManager.Instance.IsEndlessRestaurantMode)
            return "Continue Service";

        if (!resultsHaveOutcome)
            return "Start Next Day";

        return currentResultsOutcome == GameOverReason.EarthSaved
            ? "Continue Endless"
            : "Restart Day 1";
    }

    private void CacheResultsButtonLayout()
    {
        if (resultsActionButton == null)
            return;

        resultsActionRect = resultsActionButton.transform as RectTransform;
        ConfigureResultsText(resultsActionButtonText, 10f, 22f,
            TextAlignmentOptions.Center, false);
    }

    private void ConfigureResultsActions(bool singleRestaurantFlow)
    {
        if (resultsActionButton == null)
            return;

        resultsActionButton.gameObject.SetActive(true);
        resultsActionButton.interactable = true;
        if (resultsActionButtonText != null)
            resultsActionButtonText.text = GetResultsActionLabel(singleRestaurantFlow);

        bool showPaidContinue = resultsHaveOutcome &&
                                currentResultsOutcome != GameOverReason.EarthSaved && !MultiplayerDayBridge.IsActive;
        if (!showPaidContinue)
        {
            RestoreSingleResultsButtonLayout();
            if (resultsContinueButton != null)
                resultsContinueButton.gameObject.SetActive(false);
            return;
        }

        EnsureContinueButton();
        if (resultsContinueButton == null)
            return;

        resultsContinueButton.gameObject.SetActive(true);
        LayoutRecoveryButtons();
        BindResultsWallet();
        RefreshContinueButtonState();
    }

    private void EnsureContinueButton()
    {
        if (resultsContinueButton != null || resultsActionButton == null)
            return;

        GameObject clone = Instantiate(
            resultsActionButton.gameObject,
            resultsActionButton.transform.parent,
            false);
        clone.name = "ContinueWithGoldCoinsButton";
        resultsContinueButton = clone.GetComponent<Button>();
        if (resultsContinueButton == null)
        {
            Destroy(clone);
            return;
        }

        resultsContinueButton.onClick.RemoveAllListeners();
        resultsContinueButton.onClick.AddListener(OnContinueWithGoldCoinsPressed);
        resultsContinueButtonText = clone.GetComponentInChildren<TMP_Text>(true);
        if (resultsContinueButtonText != null)
            ConfigureResultsText(resultsContinueButtonText, 9f, 20f,
                TextAlignmentOptions.Center, false);
    }

    private void LayoutRecoveryButtons()
    {
        if (styledDayPanels) { LayoutStyledResultActions(); return; }
        if (resultsActionRect == null || resultsContinueButton == null)
            return;

        RectTransform continueRect = resultsContinueButton.transform as RectTransform;
        if (continueRect == null)
            return;

        bool portrait = IsResultsLayoutPortrait();
        SetResultsRect(resultsActionRect,
            portrait ? new Vector2(0.04f, 0.035f) : new Vector2(0.08f, 0.055f),
            portrait ? new Vector2(0.49f, 0.16f) : new Vector2(0.475f, 0.185f),
            new Vector2(4f, 3f), new Vector2(-4f, -3f));
        SetResultsRect(continueRect,
            portrait ? new Vector2(0.51f, 0.035f) : new Vector2(0.525f, 0.055f),
            portrait ? new Vector2(0.96f, 0.16f) : new Vector2(0.92f, 0.185f),
            new Vector2(4f, 3f), new Vector2(-4f, -3f));
    }

    private void RestoreSingleResultsButtonLayout()
    {
        if (styledDayPanels) { LayoutStyledResultActions(); return; }
        if (resultsActionRect == null)
            return;

        bool portrait = IsResultsLayoutPortrait();
        if (!portrait && TryRestoreAuthoredResultsRect(resultsActionRect))
            return;

        SetResultsRect(resultsActionRect,
            portrait ? new Vector2(0.17f, 0.035f) : new Vector2(0.34f, 0.055f),
            portrait ? new Vector2(0.83f, 0.16f) : new Vector2(0.66f, 0.185f),
            new Vector2(4f, 3f), new Vector2(-4f, -3f));
    }

    /// <summary>
    /// Converts the original fixed 500x350 report into a safe-area layout. Every
    /// visual type owns a separate normalized region, so longer localized/runtime
    /// copy can shrink or ellipsize without covering the stars or action buttons.
    /// </summary>
    private void RefreshResultsResponsiveLayout(bool force = false)
    {
        if (styledDayPanels)
        {
            RefreshStyledDayPanelLayout(force);
            return;
        }
        if (resultsPanel == null || !resultsPanel.activeInHierarchy ||
            Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        if (!force &&
            resultsLayoutScreenSize.x == Screen.width &&
            resultsLayoutScreenSize.y == Screen.height &&
            resultsLayoutSafeArea == safeArea)
        {
            return;
        }

        resultsLayoutScreenSize = new Vector2Int(Screen.width, Screen.height);
        resultsLayoutSafeArea = safeArea;

        RectTransform presentationRoot =
            GetPanelPresentationRoot(resultsPanel)?.transform as RectTransform;
        RectTransform panelRect = resultsPanel.transform as RectTransform;
        RectTransform backgroundRect =
            resultsTitleText != null ? resultsTitleText.rectTransform.parent as RectTransform : null;
        if (presentationRoot == null || panelRect == null || backgroundRect == null)
            return;

        presentationRoot.anchorMin = new Vector2(
            safeArea.xMin / Screen.width,
            safeArea.yMin / Screen.height);
        presentationRoot.anchorMax = new Vector2(
            safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height);
        presentationRoot.offsetMin = Vector2.zero;
        presentationRoot.offsetMax = Vector2.zero;
        presentationRoot.localScale = Vector3.one;

        if (panelRect != presentationRoot)
            SetResultsRect(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        bool portrait = IsResultsLayoutPortrait();
        if (portrait)
        {
            SetResultsRect(backgroundRect,
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f),
                Vector2.zero, Vector2.zero);
            LayoutResultsHeader(backgroundRect, true);
            LayoutResultsStars(backgroundRect, true);
            LayoutResultsColumns(backgroundRect, true);
            LayoutResultsDecorations(backgroundRect);
        }
        else
        {
            RestoreAuthoredResultsLayout();
        }

        if (resultsContinueButton != null && resultsContinueButton.gameObject.activeSelf)
            LayoutRecoveryButtons();
        else
            RestoreSingleResultsButtonLayout();

        ConfigureResultsText(resultsTitleText, 20f, 40f,
            TextAlignmentOptions.Center, false);
        ConfigureResultsText(resultsStarsText, 10f, 23f,
            TextAlignmentOptions.Center, true);
        ConfigureResultsText(resultsSummaryText, 9f, 21f,
            TextAlignmentOptions.TopLeft, true);
        ConfigureResultsText(resultsCustomersText, 9f, 21f,
            TextAlignmentOptions.TopLeft, true);
        ConfigureResultsText(resultsCashText, 9f, 21f,
            TextAlignmentOptions.TopLeft, true);
        ConfigureResultsText(resultsActionButtonText, 10f, 22f,
            TextAlignmentOptions.Center, false);
        ConfigureResultsText(resultsContinueButtonText, 9f, 20f,
            TextAlignmentOptions.Center, false);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
        ForceResultsTextUpdate();
    }

    private void CacheAuthoredResultsLayout()
    {
        authoredResultsLayout.Clear();
        RectTransform background =
            resultsTitleText != null ? resultsTitleText.rectTransform.parent as RectTransform : null;
        if (background == null)
            return;

        RectTransform[] rects = background.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rect in rects)
        {
            if (rect != null)
                authoredResultsLayout[rect] = ResultsRectLayout.Capture(rect);
        }
    }

    private void RestoreAuthoredResultsLayout()
    {
        foreach (KeyValuePair<RectTransform, ResultsRectLayout> entry in authoredResultsLayout)
        {
            if (entry.Key != null)
                entry.Value.Apply(entry.Key);
        }
    }

    private bool TryRestoreAuthoredResultsRect(RectTransform rect)
    {
        if (rect == null || !authoredResultsLayout.TryGetValue(rect, out ResultsRectLayout layout))
            return false;

        layout.Apply(rect);
        return true;
    }

    private readonly struct ResultsRectLayout
    {
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 offsetMin;
        private readonly Vector2 offsetMax;
        private readonly Vector2 pivot;
        private readonly Vector3 localScale;

        private ResultsRectLayout(RectTransform rect)
        {
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            offsetMin = rect.offsetMin;
            offsetMax = rect.offsetMax;
            pivot = rect.pivot;
            localScale = rect.localScale;
        }

        public static ResultsRectLayout Capture(RectTransform rect)
        {
            return new ResultsRectLayout(rect);
        }

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = pivot;
            rect.localScale = localScale;
        }

        public Vector3 LocalScale => localScale;
    }

    private void LayoutResultsHeader(RectTransform backgroundRect, bool portrait)
    {
        if (resultsTitleText != null)
        {
            SetResultsRect(resultsTitleText.rectTransform,
                portrait ? new Vector2(0.06f, 0.89f) : new Vector2(0.07f, 0.85f),
                portrait ? new Vector2(0.94f, 0.97f) : new Vector2(0.93f, 0.955f),
                new Vector2(4f, 2f), new Vector2(-4f, -2f));
        }

        if (resultsStarsText != null)
        {
            SetResultsRect(resultsStarsText.rectTransform,
                portrait ? new Vector2(0.07f, 0.82f) : new Vector2(0.08f, 0.775f),
                portrait ? new Vector2(0.93f, 0.89f) : new Vector2(0.92f, 0.845f),
                new Vector2(4f, 2f), new Vector2(-4f, -2f));
        }
    }

    private void LayoutResultsStars(RectTransform backgroundRect, bool portrait)
    {
        RectTransform starsRoot = star1 != null ? star1.rectTransform.parent as RectTransform : null;
        if (starsRoot == null)
            return;

        for (int layerIndex = 0; layerIndex < backgroundRect.childCount; layerIndex++)
        {
            RectTransform layer = backgroundRect.GetChild(layerIndex) as RectTransform;
            if (layer == null || !IsResultsStarLayer(layer, starsRoot))
                continue;

            SetResultsRect(layer,
                portrait ? new Vector2(0.19f, 0.695f) : new Vector2(0.29f, 0.575f),
                portrait ? new Vector2(0.81f, 0.815f) : new Vector2(0.71f, 0.77f),
                Vector2.zero, Vector2.zero);

            for (int starIndex = 0; starIndex < layer.childCount; starIndex++)
            {
                Image star = layer.GetChild(starIndex).GetComponent<Image>();
                if (star == null)
                    continue;

                float minX = starIndex / 3f;
                float maxX = (starIndex + 1) / 3f;
                SetResultsRect(star.rectTransform,
                    new Vector2(minX, 0f), new Vector2(maxX, 1f),
                    new Vector2(5f, 2f), new Vector2(-5f, -2f));
                star.preserveAspect = true;
            }
        }
    }

    private static bool IsResultsStarLayer(RectTransform candidate, RectTransform earnedStarsRoot)
    {
        if (candidate == earnedStarsRoot)
            return true;
        if (candidate.childCount != 3)
            return false;

        for (int i = 0; i < candidate.childCount; i++)
        {
            if (candidate.GetChild(i).GetComponent<Image>() == null)
                return false;
        }

        return true;
    }

    private void LayoutResultsColumns(RectTransform backgroundRect, bool portrait)
    {
        RectTransform columnsRoot =
            resultsSummaryText != null ? resultsSummaryText.rectTransform.parent as RectTransform : null;
        if (columnsRoot == null)
            return;

        LayoutGroup authoredLayout = columnsRoot.GetComponent<LayoutGroup>();
        if (authoredLayout != null)
            authoredLayout.enabled = false;

        SetResultsRect(columnsRoot,
            portrait ? new Vector2(0.055f, 0.18f) : new Vector2(0.045f, 0.235f),
            portrait ? new Vector2(0.945f, 0.69f) : new Vector2(0.955f, 0.57f),
            Vector2.zero, Vector2.zero);

        if (portrait)
        {
            SetColumnRect(resultsSummaryText, new Vector2(0f, 0.70f), Vector2.one);
            SetColumnRect(resultsCustomersText, new Vector2(0f, 0.40f), new Vector2(1f, 0.68f));
            SetColumnRect(resultsCashText, Vector2.zero, new Vector2(1f, 0.38f));
            return;
        }

        SetColumnRect(resultsSummaryText, Vector2.zero, new Vector2(0.315f, 1f));
        SetColumnRect(resultsCustomersText, new Vector2(0.342f, 0f), new Vector2(0.658f, 1f));
        SetColumnRect(resultsCashText, new Vector2(0.685f, 0f), Vector2.one);
    }

    private static void LayoutResultsDecorations(RectTransform backgroundRect)
    {
        SetNamedResultsRect(backgroundRect, "Header Surface",
            new Vector2(0.025f, 0.69f), new Vector2(0.975f, 0.97f));
        SetNamedResultsRect(backgroundRect, "Gold Section Divider",
            new Vector2(0.055f, 0.688f), new Vector2(0.945f, 0.694f));
        SetNamedResultsRect(backgroundRect, "Report Card - Revenue And Orders",
            new Vector2(0.045f, 0.532f), new Vector2(0.955f, 0.688f));
        SetNamedResultsRect(backgroundRect, "Report Card - Customers And Coaching",
            new Vector2(0.045f, 0.38f), new Vector2(0.955f, 0.528f));
        SetNamedResultsRect(backgroundRect, "Report Card - Approval Cash And Recovery",
            new Vector2(0.045f, 0.175f), new Vector2(0.955f, 0.376f));
    }

    private static void SetNamedResultsRect(
        RectTransform parent,
        string childName,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        if (parent == null)
            return;

        Transform child = parent.Find(childName);
        if (child is RectTransform rect)
        {
            SetResultsRect(rect, anchorMin, anchorMax,
                new Vector2(4f, 3f), new Vector2(-4f, -3f));
        }
    }

    private static void SetColumnRect(TMP_Text text, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (text == null)
            return;

        SetResultsRect(text.rectTransform, anchorMin, anchorMax,
            new Vector2(3f, 3f), new Vector2(-3f, -3f));
    }

    private bool IsResultsLayoutPortrait()
    {
        Rect safeArea = Screen.safeArea;
        return safeArea.height > safeArea.width * 1.05f;
    }

    private static void SetResultsRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void ConfigureResultsText(
        TMP_Text text,
        float minSize,
        float maxSize,
        TextAlignmentOptions alignment,
        bool wrap)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = new Vector4(2f, 2f, 2f, 2f);
    }

    private void ForceResultsTextUpdate()
    {
        TMP_Text[] texts =
        {
            resultsTitleText,
            resultsStarsText,
            resultsSummaryText,
            resultsCustomersText,
            resultsCashText,
            resultsActionButtonText,
            resultsContinueButtonText
        };

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.activeInHierarchy)
                text.ForceMeshUpdate(true, true);
        }
    }

    private void BindResultsWallet()
    {
        PlayFabWalletManager wallet = PlayFabWalletManager.Instance;
        if (resultsWallet != wallet)
        {
            if (resultsWallet != null)
                resultsWallet.OnWalletUpdated -= HandleResultsWalletUpdated;

            resultsWallet = wallet;
            if (resultsWallet != null)
                resultsWallet.OnWalletUpdated += HandleResultsWalletUpdated;
        }

        if (resultsWallet != null && !resultsWallet.HasLoadedWallet && !resultsWallet.IsRefreshing)
            resultsWallet.RefreshWallet();
    }

    private void HandleResultsWalletUpdated(int _, int __)
    {
        RefreshContinueButtonState();
    }

    private void RefreshContinueButtonState()
    {
        if (resultsContinueButton == null || !resultsContinueButton.gameObject.activeSelf)
            return;

        bool spending = resultsWallet != null && resultsWallet.IsGoldCoinSpendInFlight;
        resultsContinueButton.interactable = !spending;

        if (resultsContinueButtonText == null)
            return;

        if (spending)
        {
            resultsContinueButtonText.text = "PROCESSING...";
            return;
        }

        if (continuePurchaseArmed)
        {
            resultsContinueButtonText.text = $"CONFIRM • {continueGoldCoinCost} GC";
            return;
        }

        if (resultsWallet == null)
        {
            resultsContinueButtonText.text = $"GET {continueGoldCoinCost} GC";
            return;
        }

        if (!resultsWallet.HasLoadedWallet)
        {
            resultsContinueButtonText.text = "CHECK GC / STORE";
            return;
        }

        resultsContinueButtonText.text = resultsWallet.GoldCoins >= continueGoldCoinCost
            ? $"CONTINUE • {continueGoldCoinCost} GC"
            : $"GET GC • {resultsWallet.GoldCoins}/{continueGoldCoinCost}";
    }

    private void OnContinueWithGoldCoinsPressed()
    {
        if (MultiplayerDayBridge.IsActive) return;
        if (!resultsHaveOutcome || currentResultsOutcome == GameOverReason.EarthSaved)
            return;

        BindResultsWallet();
        if (resultsWallet == null)
        {
            RedirectToGoldCoinStore("Connect your account and get Gold Coins to continue this run.");
            return;
        }

        if (!resultsWallet.HasLoadedWallet)
        {
            if (!resultsWallet.IsRefreshing)
                resultsWallet.RefreshWallet();
            SetResultsPrompt("Checking your Gold Coin balance...");
            RefreshContinueButtonState();
            return;
        }

        if (!resultsWallet.HasEnoughGoldCoins(continueGoldCoinCost))
        {
            RedirectToGoldCoinStore(
                $"You need {continueGoldCoinCost} GC to continue. Opening the Gold Coin store...");
            return;
        }

        if (!continuePurchaseArmed)
        {
            continuePurchaseArmed = true;
            continuePurchaseConfirmUntil = Time.unscaledTime + 8f;
            int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
            SetResultsPrompt(
                $"Confirm {continueGoldCoinCost} GC: keep Day {day}, your restaurant and all unlocks. Click again to pay.");
            RefreshContinueButtonState();
            return;
        }

        continuePurchaseArmed = false;
        if (resultsActionButton != null)
            resultsActionButton.interactable = false;
        RefreshContinueButtonState();
        SetResultsPrompt($"Processing {continueGoldCoinCost} GC continue...");

        resultsWallet.TrySpendGoldCoins(
            continueGoldCoinCost,
            () =>
            {
                GameFlowManager flow = GameFlowManager.Instance;
                if (flow == null || !flow.ContinueRestaurantCampaignAfterRecovery())
                {
                    Debug.LogError("[GameDayManager] GC was charged, but restaurant recovery could not start.");
                    SetResultsPrompt("Recovery could not start. Please contact support; your wallet was refreshed.");
                    if (resultsActionButton != null)
                        resultsActionButton.interactable = true;
                    RefreshContinueButtonState();
                }
            },
            error =>
            {
                SetResultsPrompt("Continue failed: " + error);
                if (resultsActionButton != null)
                    resultsActionButton.interactable = true;
                RefreshContinueButtonState();
            });
    }

    private void UpdateContinuePurchaseConfirmation()
    {
        if (!continuePurchaseArmed || Time.unscaledTime <= continuePurchaseConfirmUntil)
            return;

        continuePurchaseArmed = false;
        SetOutcomeStatusText();
        RefreshContinueButtonState();
    }

    private void RedirectToGoldCoinStore(string message)
    {
        continuePurchaseArmed = false;
        SetResultsPrompt(message);
        RefreshContinueButtonState();

        if (NotificationPopupController.Instance != null)
        {
            NotificationPopupController.Instance.Show(
                message,
                NotificationPopupController.PopupType.Info,
                2f);
        }

        if (storeRedirectRoutine != null)
            StopCoroutine(storeRedirectRoutine);
        storeRedirectRoutine = StartCoroutine(OpenGoldCoinStoreRoutine());
    }

    private IEnumerator OpenGoldCoinStoreRoutine()
    {
        yield return new WaitForSecondsRealtime(1f);

        if (string.IsNullOrWhiteSpace(goldCoinStoreUrl))
            Debug.LogError("[GameDayManager] Gold Coin store URL is empty.");
        else
            Application.OpenURL(goldCoinStoreUrl);

        storeRedirectRoutine = null;
    }

    private void SetResultsPrompt(string message)
    {
        if (HasDayPanelV2) dayPanelV2View.SetNotice(message);
        if (resultsStarsText != null)
            resultsStarsText.text = message;
    }

    private void SetOutcomeStatusText()
    {
        if (HasDayPanelV2 && resultsHaveOutcome) dayPanelV2View.ResetNotice();
        if (resultsStarsText == null || !resultsHaveOutcome)
            return;

        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        resultsStarsText.text = currentResultsOutcome == GameOverReason.EarthSaved
            ? $"CAMPAIGN COMPLETE  •  Approval {approval}%"
            : $"RUN ENDED  •  Approval {approval}%";
    }

    private int CalculateEarnedStars()
    {
        if (CustomersServed <= 0)
            return 0;

        float happyRatio = (float)happyCustomers / CustomersServed;
        if (happyRatio >= 0.8f && cashErrors == 0)
            return TutorialCustomerFlowBridge.CapTutorialStars(3);
        if (happyRatio >= 0.55f && cashErrors <= 1)
            return TutorialCustomerFlowBridge.CapTutorialStars(2);
        return 1;
    }

    private bool restoringResultsPresentation;
    private bool observedLiveService;
    private bool resultsCuePending;
    private bool instantResults;
    private readonly Dictionary<RectTransform, Vector3> panelPresentationScales = new();
    private void PrepareResultStars(int earnedStars)
    {
        if (HasDayPanelV2)
        {
            dayPanelV2View.Prepare(false, instantResults || LevelOneUIAccessibility.ReducedMotion, earnedStars);
            return;
        }
        Image[] stars = { star1, star2, star3 };
        Transform starRoot = null;
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
                continue;

            if (starRoot == null)
                starRoot = stars[i].transform.parent;
            bool earned = i < earnedStars;
            stars[i].gameObject.SetActive(earned);
            stars[i].preserveAspect = true;
            Vector3 authoredScale = GetAuthoredResultsScale(stars[i].rectTransform);
            stars[i].transform.localScale = earned && !instantResults && !LevelOneUIAccessibility.ReducedMotion ? Vector3.zero : authoredScale;
        }

        if (starRoot != null)
            starRoot.gameObject.SetActive(true);
    }

    private void AnimateResults(int earnedStars)
    {
        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);

        animatingDayPanel = resultsPanel;
        panelAnimationRoutine = StartCoroutine(AnimateResultsRoutine(earnedStars));
    }

    private IEnumerator AnimateResultsRoutine(int earnedStars)
    {
        if (styledDayPanels)
        {
            yield return AnimateStyledResultsRoutine(earnedStars);
            panelAnimationRoutine = null;
            animatingDayPanel = null;
            yield break;
        }
        TMP_Text[] totals = { resultsSummaryText, resultsCustomersText, resultsCashText };
        foreach (var text in totals) if (text != null)
        {
            text.ForceMeshUpdate();
            text.maxVisibleCharacters = instantResults || LevelOneUIAccessibility.ReducedMotion ? int.MaxValue : 0;
        }
        if (instantResults)
        {
            var root = GetPanelPresentationRoot(resultsPanel);
            if (root != null)
            {
                var group = root.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
                var rect = root.transform as RectTransform;
                if (rect != null) rect.localScale = panelPresentationScales.TryGetValue(rect, out var scale) ? scale : GetAuthoredResultsScale(rect);
            }
            panelAnimationRoutine = null;
            yield break;
        }
        yield return AnimatePanelRoutine(resultsPanel);
        if (resultsCuePending)
        {
            resultsCuePending = false;
            GameplaySfx.Play(GameplaySfx.Cue.EndDay);
        }
        // Totals remain real settlement values; reveal existing text without rewriting game data.
        if (!LevelOneUIAccessibility.ReducedMotion)
        {
            for (float t = 0f; t < .24f; t += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
            {
                foreach (var text in totals) if (text != null) text.maxVisibleCharacters = Mathf.CeilToInt(text.textInfo.characterCount * Mathf.Clamp01(t / .24f));
                yield return null;
            }
        }
        foreach (var text in totals) if (text != null) text.maxVisibleCharacters = int.MaxValue;

        Image[] stars = { star1, star2, star3 };
        for (int i = 0; i < Mathf.Min(earnedStars, stars.Length); i++)
        {
            Image star = stars[i];
            if (star == null)
                continue;

            Vector3 finalScale = GetAuthoredResultsScale(star.rectTransform);
            if (LevelOneUIAccessibility.ReducedMotion) { star.transform.localScale = finalScale; continue; }
            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration)
            {
                elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.035f;
                star.transform.localScale = finalScale * (t * overshoot);
                yield return null;
            }

            star.transform.localScale = finalScale;
            yield return new WaitForSecondsRealtime(0.08f);
        }

        panelAnimationRoutine = null;
    }

    private Vector3 GetAuthoredResultsScale(RectTransform rect)
    {
        return rect != null && authoredResultsLayout.TryGetValue(rect, out ResultsRectLayout layout)
            ? layout.LocalScale
            : Vector3.one;
    }

    private string GetShiftStatusText()
    {
        if (TutorialSystem.IsTutorialMode)
            return CalculateEarnedStars() switch
            {
                3 => "Excellent Service",
                2 => "Service Needs Improvement",
                1 => "Needs Improvement",
                _ => "Quiet Shift"
            };
        if (CustomersServed <= 0)
            return "Quiet Shift";

        float happyRatio = (float)happyCustomers / CustomersServed;

        if (happyRatio >= 0.80f) return "Excellent Service";
        if (happyRatio >= 0.55f) return "Good Service";
        if (happyRatio >= 0.35f) return "Average Service";

        return "Poor Service";
    }

    public void RegisterGroupSeated()
    {
        if (!ServiceActive) return;
        groupsSeated++;
        RefreshUI();
    }

    public void RegisterOrderTaken()
    {
        if (!ServiceActive) return;
        ordersTaken++;
        RefreshUI();
    }

    public void RegisterOrderProcessed()
    {
        if (!ServiceActive) return;
        ordersProcessed++;
        RefreshUI();
    }

    public void PresentActionFeedback(GameplaySfx.Cue cue)
    {
        Transform target = cue == GameplaySfx.Cue.Payment && progressMoneyText != null ? progressMoneyText.transform : progressBar != null ? progressBar.transform : null;
        if (target == null || !target.gameObject.activeInHierarchy || target.GetComponent<Animator>() != null) return;
        var effect = target.GetComponent<UISubtlePressFeedback>();
        if (effect == null) effect = target.gameObject.AddComponent<UISubtlePressFeedback>();
        effect.PlayConfirmation(.025f);
    }

    public void RegisterFoodDelivered()
    {
        if (!ServiceActive) return;
        foodDelivered++;
        RefreshUI();
    }

    public void RegisterBillDelivered()
    {
        if (!ServiceActive) return;
        billsDelivered++;
        RefreshUI();
    }

    public void RegisterTrayCleaned()
    {
        if (!ServiceActive) return;
        HygieneManager.Instance?.RecordLobbyIncident(false);
        traysCleaned++;
        RefreshUI();
    }

    public void RegisterPaymentCompleted()
    {
        if (!ServiceActive) return;
        paymentsCompleted++;
        RefreshUI();
    }

    public void RegisterHappyCustomer()
    {
        if (!ServiceActive) return;
        happyCustomers++;
    }

    public void RegisterNeutralCustomer()
    {
        if (!ServiceActive) return;
        neutralCustomers++;
    }

    public void RegisterAngryCustomer()
    {
        if (!ServiceActive) return;
        angryCustomers++;
    }

    public void RegisterTip(int amount)
    {
        if (!ServiceActive) return;
        if (amount <= 0) return;

        tipsEarned += amount;

        DailyFinanceBridge.Instance?.AddEarnings(amount);

        RefreshUI();
        Debug.Log($"[GameDayManager] Tip registered: ₱{amount} | Total tips this shift: ₱{tipsEarned}");
    }

    private void ShowWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if(FastFoodCookingController.ForScene()?.ShowKitchenNotice(message)!=true)
            WarningSlideUI.Instance?.Show(message);
    }

    private static string FormatClock(float hourValue)
    {
        int totalMinutes = Mathf.RoundToInt(hourValue * 60f);
        totalMinutes = Mathf.Clamp(totalMinutes, 0, 24 * 60);
        int hour24 = (totalMinutes / 60) % 24;
        int minute = totalMinutes % 60;
        string suffix = hour24 < 12 ? "AM" : "PM";
        int hour12 = hour24 % 12;
        if (hour12 == 0)
            hour12 = 12;
        return $"{hour12}:{minute:00} {suffix}";
    }

    private static GameObject GetPanelPresentationRoot(GameObject panel)
    {
        if (panel == null)
            return null;

        Transform current = panel.transform;
        while (current.parent != null && current.parent.GetComponent<Canvas>() == null)
            current = current.parent;
        return current.gameObject;
    }

    private void SetPanelVisible(GameObject panel, bool visible)
    {
        GameObject root = GetPanelPresentationRoot(panel);
        if (root == null)
            return;

        if (visible)
        {
            root.SetActive(true);
            if (panel != root)
                panel.SetActive(true);
        }
        else
        {
            CancelStyledDayPanelPresentation(panel);
            if (panel == resultsPanel)
            {
                if (panelAnimationRoutine != null) StopCoroutine(panelAnimationRoutine);
                panelAnimationRoutine = null; resultsCuePending = false;
                foreach (var text in new[] { resultsSummaryText, resultsCustomersText, resultsCashText })
                    if (text != null) text.maxVisibleCharacters = int.MaxValue;
                foreach (var star in new[] { star1, star2, star3 })
                    if (star != null) star.transform.localScale = GetAuthoredResultsScale(star.rectTransform);
            }
            root.SetActive(false);
        }
    }

    private void AnimatePanelIn(GameObject panel)
    {
        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);
        animatingDayPanel = panel;
        panelAnimationRoutine = StartCoroutine(panel == dayIntroPanel && styledDayPanels
            ? AnimateStyledIntroRoutine() : AnimatePanelRoutine(panel));
    }

    private IEnumerator AnimatePanelRoutine(GameObject panel)
    {
        if (styledDayPanels)
        {
            yield return AnimateStyledPanelRoutine(panel);
            yield break;
        }
        GameObject root = GetPanelPresentationRoot(panel);
        if (root == null)
            yield break;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();

        RectTransform rect = root.transform as RectTransform;
        if (rect != null && !panelPresentationScales.ContainsKey(rect))
            panelPresentationScales[rect] = authoredResultsLayout.ContainsKey(rect) ? GetAuthoredResultsScale(rect) : rect.localScale;
        Vector3 finalScale = rect != null ? panelPresentationScales[rect] : Vector3.one;
        if (LevelOneUIAccessibility.ReducedMotion) { group.alpha = 1f; if (rect != null) rect.localScale = finalScale; yield break; }
        UISubtlePressFeedback.EnsureButtons(root.transform);
        group.alpha = 0f;
        if (rect != null)
            rect.localScale = finalScale * 0.96f;

        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = t;
            if (rect != null)
                rect.localScale = Vector3.LerpUnclamped(finalScale * 0.88f, finalScale, t);
            yield return null;
        }

        group.alpha = 1f;
        if (rect != null)
            rect.localScale = finalScale;
    }

    /// <summary>
    /// Records a failed cash-handling session — the register was closed without a
    /// correct Confirm (group left, or session timed out before correct change was given).
    /// </summary>
    public void RegisterCashError()
    {
        if (!ServiceActive)
            return;

        cashErrors++;
        CasualDiningPolishManager.EnsureInstance().RegisterIncident(
            DailyIncidentType.PaymentError);
        RefreshUI();
    }

    public float GetProgress01()
    {
        return CalculateProgress01();
    }

    public void RefreshRevenueUI()
    {
        RefreshProgressMoneyText();

        if (progressBar != null)
            progressBar.value = CalculateProgress01();
    }

    [Header("Day Panel Presentation")]
    [SerializeField] private DayPanelView dayPanelV2View;
    [SerializeField] private bool styledDayPanels;
    [SerializeField] private RectTransform dayIntroSurface;
    [SerializeField] private RectTransform resultsSurface;
    [SerializeField] private TMP_Text dayIntroDetailsText;
    [SerializeField] private Sprite dayPanelSecondaryButtonSprite;
    [SerializeField] private RectTransform[] dayIntroRevealRoots = new RectTransform[0];
    [SerializeField] private RectTransform[] resultsRevealRoots = new RectTransform[0];
    [SerializeField, Min(.05f)] private float dayPanelEnterSeconds = .28f;
    [SerializeField, Min(.05f)] private float dayPanelSectionSeconds = .18f;
    [SerializeField, Min(0f)] private float dayPanelSectionStagger = .06f;
    [SerializeField, Min(.05f)] private float dayPanelStarSeconds = .24f;
    [SerializeField, Range(0f, .2f)] private float dayPanelDismissSeconds = .16f;

    private GameObject animatingDayPanel;
    private bool introStartTransition;
    private Coroutine shiftIntroTransitionRoutine;
    private Vector2Int dayPanelScreen = new Vector2Int(-1, -1);
    private Rect dayPanelSafeArea;
    private bool introWasVisible, resultsWereVisible, dayPanelLargeText;
    private readonly Dictionary<RectTransform, Vector2> dayPanelRestPositions = new();
    private static readonly Color DayPanelInk = new Color(.055f, .11f, .20f, 1f);

    private TMP_Text[] IntroSectionTexts => new[] { dayIntroSummaryLeftText, dayIntroSummaryMiddleText, dayIntroSummaryRightText };
    private TMP_Text[] ResultSectionTexts => new[] { resultsSummaryText, resultsCustomersText, resultsCashText };
    private bool HasDayPanelV2 => styledDayPanels && dayPanelV2View != null;

    private void PopulateStyledDayIntro()
    {
        if (HasDayPanelV2)
        {
            dayPanelV2View.BindIntro(new DayPanelIntroSnapshot(
                dayIntroTitleText != null ? dayIntroTitleText.text : "Start Day",
                FormatCompactDayHours(openingHour, closingHour),
                DailyFinanceBridge.Instance != null ? DailyFinanceBridge.Instance.TotalRequiredEarningsToday : 0,
                maxCustomersToSpawn, maxGroupsPerMinute, realSecondsPerGameHour));
            return;
        }
        if (dayIntroSummaryLeftText != null)
            dayIntroSummaryLeftText.text = $"{FormatClock(openingHour)}\n– {FormatClock(closingHour)}";
        if (dayIntroSummaryMiddleText != null)
            dayIntroSummaryMiddleText.text = "₱" + (DailyFinanceBridge.Instance != null
                ? DailyFinanceBridge.Instance.TotalRequiredEarningsToday : 0).ToString("N0");
        if (dayIntroSummaryRightText != null)
            dayIntroSummaryRightText.text = "STOCK\n& STAFF";
        if (dayIntroDetailsText != null)
            dayIntroDetailsText.text = $"<b>{maxCustomersToSpawn} customers • {maxGroupsPerMinute} groups/min</b>\n" +
                "1 real minute = 1 in-game hour. Prepare stock and staff, then press PLAY.\n" +
                "Keep guests satisfied, avoid angry walkouts, and review your report after service.";
    }

    private static string FormatCompactDayHours(int opening, int closing)
    {
        int start = ((opening % 24) + 24) % 24;
        int end = ((closing % 24) + 24) % 24;
        int startHour = (start + 11) % 12 + 1;
        int endHour = (end + 11) % 12 + 1;
        string startPeriod = start < 12 ? "AM" : "PM";
        string endPeriod = end < 12 ? "AM" : "PM";
        return startPeriod == endPeriod
            ? $"{startHour}–{endHour} {endPeriod}"
            : $"{startHour} {startPeriod}–{endHour} {endPeriod}";
    }

    private void RefreshStyledDayPanelLayout(bool force = false)
    {
        if (!styledDayPanels || Screen.width <= 0 || Screen.height <= 0) return;
        bool introVisible = dayIntroPanel != null && dayIntroPanel.activeInHierarchy;
        bool resultVisible = resultsPanel != null && resultsPanel.activeInHierarchy;
        bool changed = dayPanelScreen != new Vector2Int(Screen.width, Screen.height) ||
            dayPanelSafeArea != Screen.safeArea || dayPanelLargeText != LevelOneUIAccessibility.LargeText;
        if (!force && !changed && introVisible == introWasVisible && resultVisible == resultsWereVisible) return;
        // A rotation/resize finishes the current cosmetic sequence instead of retaining stale offsets.
        if (changed && animatingDayPanel != null) CancelStyledDayPanelPresentation(animatingDayPanel);
        dayPanelScreen = new Vector2Int(Screen.width, Screen.height);
        dayPanelSafeArea = Screen.safeArea;
        dayPanelLargeText = LevelOneUIAccessibility.LargeText;
        introWasVisible = introVisible;
        resultsWereVisible = resultVisible;
        bool portrait = IsResultsLayoutPortrait();
        if (HasDayPanelV2)
        {
            dayPanelV2View.RefreshLayout(portrait, LevelOneUIAccessibility.LargeText);
            dayPanelV2View.LayoutActions(resultsActionButton, resultsContinueButton);
            return;
        }
        if (introVisible && dayIntroSurface != null)
        {
            FitDayPanel(dayIntroPanel, dayIntroSurface, portrait, 1.05f);
            PlaceDayRect(dayIntroTitleText != null ? dayIntroTitleText.rectTransform : null, .31f, .835f, .69f, .943f);
            var texts = IntroSectionTexts;
            for (int i = 0; i < dayIntroRevealRoots.Length && i < 3; i++)
            {
                float top = .805f - i * .19f;
                float rowWidth = portrait ? .88f : .60f;
                // ObjectiveRow contains a round badge and a value pill. Preserve its native aspect.
                float rowHeight = dayIntroSurface.rect.width * rowWidth * 185f / 650f / dayIntroSurface.rect.height;
                PlaceDayRect(dayIntroRevealRoots[i], (1 - rowWidth) * .5f, top - rowHeight, (1 + rowWidth) * .5f, top);
                if (i < texts.Length && texts[i] != null)
                {
                    PlaceDayRect(texts[i].rectTransform, .675f, .14f, .97f, .86f);
                    StyleDayText(texts[i], dayIntroSurface.rect.height * .031f, TextAlignmentOptions.Center, true);
                }
            }
            PlaceDayRect(dayIntroDetailsText != null ? dayIntroDetailsText.rectTransform : null, .13f, .188f, .87f, .244f);
            StyleDayText(dayIntroDetailsText, dayIntroSurface.rect.height * .018f, TextAlignmentOptions.Center, true);
            PlaceDayRect(playButton != null ? playButton.transform as RectTransform : null, .315f, .063f, .685f, .185f);
            StyleDayText(dayIntroTitleText, dayIntroSurface.rect.height * .067f, TextAlignmentOptions.Center, false, true);
            StyleDayButton(playButton, dayIntroSurface.rect.height * .047f);
            CacheDaySectionPositions(dayIntroRevealRoots, IntroSectionTexts);
        }
        if (resultVisible && resultsSurface != null)
        {
            FitDayPanel(resultsPanel, resultsSurface, portrait);
            float height = resultsSurface.rect.height;
            PlaceDayRect(resultsTitleText != null ? resultsTitleText.rectTransform : null, .275f, .843f, .725f, .943f);
            PlaceDayRect(resultsStarsText != null ? resultsStarsText.rectTransform : null, .13f, .782f, .87f, .834f);
            StyleDayText(resultsTitleText, height * .061f, TextAlignmentOptions.Center, false, true);
            StyleDayText(resultsStarsText, height * .023f, TextAlignmentOptions.Center, false);
            LayoutDayStarLayers(portrait);
            RectTransform columns = resultsSummaryText != null ? resultsSummaryText.transform.parent as RectTransform : null;
            if (columns != null)
            {
                var layout = columns.GetComponent<LayoutGroup>();
                if (layout != null) layout.enabled = false;
                PlaceDayRect(columns, 0, 0, 1, 1);
            }
            TMP_Text[] texts = ResultSectionTexts;
            for (int i = 0; i < 3; i++)
            {
                float x0 = portrait ? .09f : .065f + i * .292f;
                float x1 = portrait ? .91f : x0 + .285f;
                float y0 = portrait ? (i == 0 ? .535f : i == 1 ? .385f : .18f) : .227f;
                float y1 = portrait ? (i == 0 ? .675f : i == 1 ? .53f : .38f) : .625f;
                if (i < resultsRevealRoots.Length)
                    PlaceDayRect(resultsRevealRoots[i], x0, y0, x1, y1);
                if (texts[i] != null)
                {
                    PlaceDayRect(texts[i].rectTransform, x0 + .012f, y0 + .012f, x1 - .012f,
                        y1 - (portrait ? .035f : .065f));
                    StyleDayText(texts[i], height * (portrait ? .023f : .026f), TextAlignmentOptions.TopLeft, true);
                }
            }
            LayoutStyledResultActions();
            CacheDaySectionPositions(resultsRevealRoots, ResultSectionTexts);
        }
        Canvas.ForceUpdateCanvases();
        if (introVisible && dayIntroSurface != null) RefreshDaySpriteDensity(dayIntroSurface);
        if (resultVisible && resultsSurface != null) RefreshDaySpriteDensity(resultsSurface);
    }

    private void CacheDaySectionPositions(RectTransform[] roots, TMP_Text[] texts)
    {
        foreach (var root in roots) if (root != null) dayPanelRestPositions[root] = root.anchoredPosition;
        foreach (var text in texts) if (text != null) dayPanelRestPositions[text.rectTransform] = text.rectTransform.anchoredPosition;
    }

    private static void PlaceDayRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void FitDayPanel(GameObject panel, RectTransform surface, bool portrait, float landscapeAspect = 1.5f)
    {
        var root = GetPanelPresentationRoot(panel)?.transform as RectTransform;
        if (root == null) return;
        PlaceDayRect(root, 0, 0, 1, 1);
        if (panel.transform != root) PlaceDayRect(panel.transform as RectTransform, 0, 0, 1, 1);
        Rect safe = Screen.safeArea;
        float availableWidth = root.rect.width * safe.width / Screen.width * .94f;
        float availableHeight = root.rect.height * safe.height / Screen.height * .94f;
        float aspect = portrait ? .69f : landscapeAspect;
        float width = Mathf.Min(availableWidth, availableHeight * aspect);
        surface.anchorMin = surface.anchorMax = new Vector2(.5f, .5f);
        surface.pivot = new Vector2(.5f, .5f);
        surface.sizeDelta = new Vector2(width, width / aspect);
        surface.anchoredPosition = new Vector2((safe.center.x / Screen.width - .5f) * root.rect.width,
            (safe.center.y / Screen.height - .5f) * root.rect.height);
        surface.localScale = Vector3.one;
        surface.localRotation = Quaternion.identity;
    }

    private static void StyleDayText(TMP_Text text, float maxSize, TextAlignmentOptions alignment, bool wrap, bool light = false)
    {
        if (text == null) return;
        // These authored SDF atlases contain Latin text, without a global Unicode fallback.
        // Keep every amount and sentence readable without modifying shared font assets or game values.
        if (text.font != null)
        {
            if (!text.font.HasCharacter('₱')) text.text = text.text.Replace("₱", "PHP ");
            if (!text.font.HasCharacter('•')) text.text = text.text.Replace("•", "|");
            if (!text.font.HasCharacter('–')) text.text = text.text.Replace("–", "-");
        }
        text.color = light ? new Color(1f, .97f, .88f) : DayPanelInk;
        text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(10, maxSize * (LevelOneUIAccessibility.LargeText ? 1.15f : 1));
        text.fontSizeMin = Mathf.Max(7, text.fontSizeMax * .58f);
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = new Vector4(3, 2, 3, 2);
    }

    private static void StyleDayButton(Button button, float textSize)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        StyleDayText(text, textSize, TextAlignmentOptions.Center, false, true);
        if (text != null) PlaceDayRect(text.rectTransform, .07f, .12f, .93f, .88f);
    }

    private static void RefreshDaySpriteDensity(RectTransform surface)
    {
        foreach (Image image in surface.GetComponentsInChildren<Image>(true))
        {
            if (image.type != Image.Type.Sliced || image.sprite == null) continue;
            Rect rect = image.rectTransform.rect;
            image.pixelsPerUnitMultiplier = Mathf.Max(.01f, Mathf.Max(image.sprite.rect.width / Mathf.Max(1, rect.width),
                image.sprite.rect.height / Mathf.Max(1, rect.height)));
        }
    }

    private void LayoutDayStarLayers(bool portrait)
    {
        var earnedRoot = star1 != null ? star1.transform.parent as RectTransform : null;
        if (earnedRoot == null) return;
        // Existing outline and earned layers remain aligned; unrelated skin groups are not star layers.
        foreach (Transform child in resultsSurface)
        {
            var layer = child as RectTransform;
            if (layer == null || !IsResultsStarLayer(layer, earnedRoot)) continue;
            PlaceDayRect(layer, .32f, portrait ? .681f : .665f, .68f, portrait ? .78f : .774f);
            for (int i = 0; i < layer.childCount; i++)
            {
                var image = layer.GetChild(i).GetComponent<Image>();
                if (image == null) continue;
                PlaceDayRect(image.rectTransform, i / 3f, 0, (i + 1) / 3f, 1);
                image.rectTransform.offsetMin = new Vector2(4, 1);
                image.rectTransform.offsetMax = new Vector2(-4, -1);
                image.preserveAspect = true;
            }
        }
    }

    private void LayoutStyledResultActions()
    {
        if (HasDayPanelV2)
        {
            dayPanelV2View.LayoutActions(resultsActionButton, resultsContinueButton);
            return;
        }
        if (!styledDayPanels || resultsSurface == null) return;
        bool recovery = resultsContinueButton != null && resultsContinueButton.gameObject.activeSelf;
        PlaceDayRect(resultsActionButton != null ? resultsActionButton.transform as RectTransform : null,
            recovery ? .15f : .30f, .065f, recovery ? .49f : .70f, .18f);
        if (recovery) PlaceDayRect(resultsContinueButton.transform as RectTransform, .51f, .065f, .85f, .18f);
        StyleDayButton(resultsActionButton, resultsSurface.rect.height * .037f);
        StyleDayButton(resultsContinueButton, resultsSurface.rect.height * .032f);
        if (resultsContinueButton != null && dayPanelSecondaryButtonSprite != null)
        {
            Image image = resultsContinueButton.GetComponent<Image>();
            if (image != null) { image.sprite = dayPanelSecondaryButtonSprite; image.type = Image.Type.Sliced; }
        }
    }

    private static CanvasGroup DayGroup(Component component)
    {
        if (component == null) return null;
        var group = component.GetComponent<CanvasGroup>();
        return group != null ? group : component.gameObject.AddComponent<CanvasGroup>();
    }

    private static float DayPop(float t)
    {
        t = Mathf.Clamp01(t) - 1;
        return 1 + 2.4f * t * t * t + 1.4f * t * t;
    }

    private IEnumerator AnimateStyledPanelRoutine(GameObject panel)
    {
        var root = GetPanelPresentationRoot(panel);
        RectTransform surface = panel == dayIntroPanel ? dayIntroSurface : resultsSurface;
        if (root == null || surface == null) yield break;
        Vector3 fittedScale = HasDayPanelV2 ? dayPanelV2View.FittedScale(panel == dayIntroPanel) : Vector3.one;
        CanvasGroup group = DayGroup(root.transform);
        group.blocksRaycasts = true;
        group.interactable = false;
        UISubtlePressFeedback.EnsureButtons(root.transform);
        if (!LevelOneUIAccessibility.ReducedMotion)
        {
            for (float elapsed = 0; elapsed < dayPanelEnterSeconds; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
            {
                if (LevelOneUIAccessibility.ReducedMotion) break;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, dayPanelEnterSeconds));
                group.alpha = 1 - Mathf.Pow(1 - t, 3);
                surface.localScale = fittedScale * Mathf.LerpUnclamped(.92f, 1, DayPop(t));
                yield return null;
            }
        }
        group.alpha = 1;
        group.interactable = true;
        surface.localScale = fittedScale;
    }

    private void SetDaySection(RectTransform rect, float progress)
    {
        if (rect == null) return;
        if (!dayPanelRestPositions.ContainsKey(rect)) dayPanelRestPositions[rect] = rect.anchoredPosition;
        DayGroup(rect).alpha = progress;
        rect.localScale = Vector3.one * Mathf.Lerp(.975f, 1, progress);
        rect.anchoredPosition = dayPanelRestPositions[rect] + Vector2.down * (1 - progress) * 8;
    }

    private void SetDaySections(RectTransform[] roots, TMP_Text[] texts, float progress)
    {
        for (int i = 0; i < 3; i++) SetDaySectionPair(roots, texts, i, progress);
    }

    private void SetDaySectionPair(RectTransform[] roots, TMP_Text[] texts, int i, float progress)
    {
        RectTransform rect = i < roots.Length ? roots[i] : null;
        SetDaySection(rect, progress);
        if (i < texts.Length && texts[i] != null)
        {
            texts[i].maxVisibleCharacters = int.MaxValue;
            if (rect == null || !texts[i].transform.IsChildOf(rect)) SetDaySection(texts[i].rectTransform, progress);
        }
    }

    private IEnumerator RevealDaySections(RectTransform[] roots, TMP_Text[] texts)
    {
        float duration = Mathf.Max(.05f, dayPanelSectionSeconds);
        float total = duration + dayPanelSectionStagger * 2;
        for (float elapsed = 0; elapsed < total && !LevelOneUIAccessibility.ReducedMotion;
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            for (int i = 0; i < 3; i++)
            {
                float t = Mathf.Clamp01((elapsed - dayPanelSectionStagger * i) / duration);
                SetDaySectionPair(roots, texts, i, 1 - Mathf.Pow(1 - t, 3));
            }
            yield return null;
        }
        SetDaySections(roots, texts, 1);
    }

    private IEnumerator AnimateStyledIntroRoutine()
    {
        if (HasDayPanelV2)
        {
            dayPanelV2View.Prepare(true, LevelOneUIAccessibility.ReducedMotion, 0);
            yield return AnimateStyledPanelRoutine(dayIntroPanel);
            yield return RevealDayPanelV2Sections(true);
            dayPanelV2View.Finish(true);
            panelAnimationRoutine = null;
            animatingDayPanel = null;
            yield break;
        }
        var texts = IntroSectionTexts;
        SetDaySections(dayIntroRevealRoots, texts, LevelOneUIAccessibility.ReducedMotion ? 1 : 0);
        yield return AnimateStyledPanelRoutine(dayIntroPanel);
        yield return RevealDaySections(dayIntroRevealRoots, texts);
        panelAnimationRoutine = null;
        animatingDayPanel = null;
    }

    private IEnumerator AnimateStyledResultsRoutine(int earnedStars)
    {
        if (HasDayPanelV2)
        {
            if (instantResults)
            {
                ResetStyledDayPanel(resultsPanel);
                resultsCuePending = false;
                yield break;
            }
            yield return AnimateStyledPanelRoutine(resultsPanel);
            if (resultsCuePending)
            {
                resultsCuePending = false;
                GameplaySfx.Play(GameplaySfx.Cue.EndDay);
            }
            yield return RevealDayPanelV2Sections(false);
            bool celebrateV2 = !resultsHaveOutcome || currentResultsOutcome == GameOverReason.EarthSaved;
            for (int i = 0; i < Mathf.Clamp(earnedStars, 0, 3); i++)
            {
                if (celebrateV2 && !LevelOneUIAccessibility.ReducedMotion)
                {
                    for (float elapsed = 0; elapsed < dayPanelStarSeconds; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
                    {
                        if (LevelOneUIAccessibility.ReducedMotion) break;
                        dayPanelV2View.SetStarProgress(i, Mathf.Clamp01(elapsed / Mathf.Max(.05f, dayPanelStarSeconds)));
                        yield return null;
                    }
                }
                dayPanelV2View.SetStarProgress(i, 1);
                if (celebrateV2 && !LevelOneUIAccessibility.ReducedMotion)
                    yield return new WaitForSecondsRealtime(.08f);
            }
            dayPanelV2View.Finish(false);
            yield break;
        }
        var texts = ResultSectionTexts;
        SetDaySections(resultsRevealRoots, texts, instantResults || LevelOneUIAccessibility.ReducedMotion ? 1 : 0);
        if (instantResults)
        {
            ResetStyledDayPanel(resultsPanel);
            resultsCuePending = false;
            yield break;
        }
        yield return AnimateStyledPanelRoutine(resultsPanel);
        if (resultsCuePending)
        {
            resultsCuePending = false;
            GameplaySfx.Play(GameplaySfx.Cue.EndDay);
        }
        yield return RevealDaySections(resultsRevealRoots, texts);
        bool celebrate = !resultsHaveOutcome || currentResultsOutcome == GameOverReason.EarthSaved;
        foreach (Image star in new[] { star1, star2, star3 })
        {
            if (star == null || !star.gameObject.activeSelf) continue;
            Vector3 scale = GetAuthoredResultsScale(star.rectTransform);
            if (celebrate && !LevelOneUIAccessibility.ReducedMotion)
            {
                for (float elapsed = 0; elapsed < dayPanelStarSeconds; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
                {
                    if (LevelOneUIAccessibility.ReducedMotion) break;
                    star.transform.localScale = scale * Mathf.Max(0, DayPop(elapsed / Mathf.Max(.05f, dayPanelStarSeconds)));
                    yield return null;
                }
            }
            star.transform.localScale = scale;
            if (celebrate && !LevelOneUIAccessibility.ReducedMotion) yield return new WaitForSecondsRealtime(.08f);
        }
    }

    private IEnumerator RevealDayPanelV2Sections(bool intro)
    {
        float sectionSeconds = Mathf.Max(.05f, dayPanelSectionSeconds);
        float metricSeconds = intro ? 0 : .45f;
        float duration = Mathf.Max(sectionSeconds + dayPanelSectionStagger * 2, metricSeconds);
        for (float elapsed = 0; elapsed < duration && !LevelOneUIAccessibility.ReducedMotion;
             elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            for (int i = 0; i < 3; i++)
            {
                float t = Mathf.Clamp01((elapsed - dayPanelSectionStagger * i) / sectionSeconds);
                dayPanelV2View.SetSectionProgress(intro, i, 1 - Mathf.Pow(1 - t, 3));
            }
            if (!intro)
            {
                float t = Mathf.Clamp01(elapsed / metricSeconds);
                dayPanelV2View.SetMetricProgress(1 - Mathf.Pow(1 - t, 3));
            }
            yield return null;
        }
        for (int i = 0; i < 3; i++) dayPanelV2View.SetSectionProgress(intro, i, 1);
        if (!intro) dayPanelV2View.SetMetricProgress(1);
    }

    private IEnumerator DismissStyledDayIntro()
    {
        CancelStyledDayPanelPresentation(dayIntroPanel);
        var root = GetPanelPresentationRoot(dayIntroPanel);
        CanvasGroup group = root != null ? DayGroup(root.transform) : null;
        if (group == null || dayIntroSurface == null) { yield return new WaitForSecondsRealtime(.2f); yield break; }
        Vector3 fittedScale = HasDayPanelV2 ? dayPanelV2View.FittedScale(true) : Vector3.one;
        group.interactable = false;
        group.blocksRaycasts = true;
        float duration = Mathf.Clamp(dayPanelDismissSeconds, 0, .2f);
        // Preserve the existing 0.2 second pre-service transition, including reduced motion.
        for (float elapsed = 0; elapsed < .2f; elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime)
        {
            float t = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
            group.alpha = 1 - t;
            if (!LevelOneUIAccessibility.ReducedMotion) dayIntroSurface.localScale = fittedScale * Mathf.Lerp(1, .97f, t);
            yield return null;
        }
    }

    private void BeginShiftStartTransition()
    {
        if (introStartTransition || shiftIntroTransitionRoutine != null || !isActiveAndEnabled) return;
        shiftIntroTransitionRoutine = StartCoroutine(StartShiftRoutine());
    }

    private void CancelShiftIntroTransition()
    {
        if (shiftIntroTransitionRoutine != null) StopCoroutine(shiftIntroTransitionRoutine);
        shiftIntroTransitionRoutine = null;
        introStartTransition = false;
        CancelStyledDayPanelPresentation(dayIntroPanel);
    }

    private void ResetStyledDayPanel(GameObject panel)
    {
        if (!styledDayPanels || panel == null) return;
        var root = GetPanelPresentationRoot(panel);
        if (root != null)
        {
            var group = DayGroup(root.transform);
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
            root.transform.localScale = Vector3.one;
        }
        bool intro = panel == dayIntroPanel;
        var surface = intro ? dayIntroSurface : resultsSurface;
        if (surface != null) surface.localScale = HasDayPanelV2 ? dayPanelV2View.FittedScale(intro) : Vector3.one;
        if (HasDayPanelV2)
        {
            dayPanelV2View.Finish(intro);
            return;
        }
        SetDaySections(intro ? dayIntroRevealRoots : resultsRevealRoots,
            intro ? IntroSectionTexts : ResultSectionTexts, 1);
        if (!intro) foreach (var star in new[] { star1, star2, star3 })
            if (star != null) star.transform.localScale = GetAuthoredResultsScale(star.rectTransform);
    }

    private void CancelStyledDayPanelPresentation(GameObject panel)
    {
        if (!styledDayPanels) return;
        if (animatingDayPanel == panel)
        {
            if (panelAnimationRoutine != null) StopCoroutine(panelAnimationRoutine);
            panelAnimationRoutine = null;
            animatingDayPanel = null;
            if (panel == resultsPanel) resultsCuePending = false;
        }
        ResetStyledDayPanel(panel);
    }

    private void OnDisable()
    {
        CancelShiftIntroTransition();
        CancelStyledDayPanelPresentation(dayIntroPanel);
        CancelStyledDayPanelPresentation(resultsPanel);
        introStartTransition = false;
    }

}
