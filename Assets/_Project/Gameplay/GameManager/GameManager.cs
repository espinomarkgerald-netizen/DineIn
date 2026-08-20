using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
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

    public bool ShiftRunning => shiftRunning;
    public bool ServiceActive => shiftRunning || closingOut;
    public float TimeRemaining => timeRemaining;
    public int HappyCustomers => happyCustomers;
    public int NeutralCustomers => neutralCustomers;
    public int AngryCustomers => angryCustomers;
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GetComponent<LobbyPauseMenu>() == null)
            gameObject.AddComponent<LobbyPauseMenu>();

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
        UpdateMoodBarsSmooth();
        UpdateContinuePurchaseConfirmation();
        RefreshResultsResponsiveLayout();

        if (!shiftRunning)
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

        if (!warnedLastMinute && timeRemaining <= 60f)
        {
            warnedLastMinute = true;
            ShowWarning("5:00 PM — last hour. Finish the remaining customers.");
        }

        if (!rushAnnounced && timeRemaining <= rushStartTimeRemainingSeconds)
        {
            rushAnnounced = true;
            ShowWarning("Rush hour has started. Expect customers more frequently.");
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
        if (!shiftRunning && !closingOut)
            timeRemaining = ShiftLengthSeconds;

        SetPanelVisible(resultsPanel, false);
        SetPanelVisible(dayIntroPanel, true);
        AnimatePanelIn(dayIntroPanel);

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

        RefreshUI();
        SetupMoodBars(true);
    }

    public void ConfirmStartShift()
    {
        RestockFlowCoordinator restock = RestockFlowCoordinator.Instance;
        if (restock != null && restock.TryShowStartReminder(
                () => StartCoroutine(StartShiftRoutine())))
        {
            return;
        }

        StartCoroutine(StartShiftRoutine());
    }

    private IEnumerator StartShiftRoutine()
    {
        SetPanelVisible(dayIntroPanel, false);

        yield return new WaitForSecondsRealtime(0.2f);

        StartShift();
    }

    public void StartShift()
    {
        if (shiftRunning || closingOut)
            return;

        Time.timeScale = 1f;

        ResolveManagerComponents();
        ApplyDifficultyScaling();
        ApplyTakeoutUnlock();
        ApplyCustomerTypeUnlocks();
        ResetShiftRuntime();

        timeRemaining = ShiftLengthSeconds;
        shiftRunning = true;
        closingOut = false;
        GameFlowManager.Instance?.MarkRestaurantServiceStarted();

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

        int day = GameFlowManager.Instance.CurrentDay;
        float t = Mathf.Clamp01((float)(day - 1) / (maxScalingDay - 1));

        maxCustomersToSpawn = Mathf.RoundToInt(maxCustomersCurve.Evaluate(t));
        maxGroupsPerMinute  = Mathf.Max(1, Mathf.RoundToInt(groupsPerMinuteCurve.Evaluate(t)));
        spawnIntervalMin    = Mathf.Max(1f, spawnIntervalMinCurve.Evaluate(t));
        spawnIntervalMax    = Mathf.Max(spawnIntervalMin + 1f, spawnIntervalMaxCurve.Evaluate(t));

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
        if (!shiftRunning)
            return;

        shiftRunning = false;
        closingOut = true;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        ShowWarning("Shift ended. Waiting for remaining customers.");
        if (closingResultsRoutine != null)
            StopCoroutine(closingResultsRoutine);
        closingResultsRoutine = StartCoroutine(ShowResultsWhenClear());
    }

    private IEnumerator ShowResultsWhenClear()
    {
        float waited = 0f;
        while (FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Length > 0 &&
               waited < maxClosingGraceSeconds)
        {
            yield return new WaitForSeconds(1f);
            waited += 1f;
        }

        closingOut = false;
        closingResultsRoutine = null;
        ShowResults();
    }

    public bool EndDayNowDebug()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
            return false;

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
        ShowResults();
        return true;
    }

    public void RestartShift()
    {
        ShowShiftIntro();
    }

    public void OnResultsActionPressed()
    {
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

        SetupMoodBars(true);
    }

    private IEnumerator SpawnCustomersRoutine()
    {
        yield return new WaitForSeconds(1f);
        WaitForSeconds blockedSpawnWait = new WaitForSeconds(Mathf.Max(0.25f, blockedSpawnRetrySeconds));

        while (shiftRunning)
        {
            bool canSpawnMoreShift = groupsSpawnedThisShift < maxCustomersToSpawn;
            bool canSpawnThisMinute = groupsSpawnedThisMinute < CurrentGroupsPerMinuteLimit;

            if (canSpawnMoreShift && canSpawnThisMinute)
            {
                bool spawned = TrySpawnCustomerGroup();
                if (spawned)
                {
                    float intervalMultiplier = IsRushHour ? rushSpawnIntervalMultiplier : 1f;
                    float delay = Random.Range(spawnIntervalMin, spawnIntervalMax) * intervalMultiplier;
                    yield return new WaitForSeconds(delay);
                    continue;
                }
            }

            // Avoid retrying every rendered frame while the lobby or minute is full.
            yield return blockedSpawnWait;
        }
    }

    private void ApplyTakeoutUnlock()
    {
        if (groupSpawner == null)
            return;

        int currentDay = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        bool shouldEnable = currentDay >= takeoutUnlockDay;

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

        bool enablePink = currentDay >= pinkCustomerUnlockDay;
        bool enableBlue = currentDay >= blueCustomerUnlockDay;

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

    private bool TrySpawnCustomerGroup()
    {
        if (!shiftRunning)
            return false;

        if (timeRemaining <= StopSpawnTimeRemainingSeconds)
            return false;

        if (groupsSpawnedThisShift >= maxCustomersToSpawn)
            return false;

        if (groupsSpawnedThisMinute >= CurrentGroupsPerMinuteLimit)
            return false;

        int concurrentLimit = IsRushHour
            ? Mathf.Max(maxConcurrentGroups, rushMaxConcurrentGroups)
            : maxConcurrentGroups;
        if (FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length >= concurrentLimit)
            return false;

        if (groupSpawner == null)
        {
            Debug.LogWarning("[GameDayManager] GroupSpawner is missing.");
            return false;
        }

        CustomerGroup spawnedGroup = groupSpawner.SpawnGroup();
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

    private void ShowResults()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        bool singleRestaurantFlow = flow != null && flow.UsesSingleRestaurantFlow;
        if (singleRestaurantFlow)
            flow.FinalizeRestaurantDayForResults();

        resultsHaveOutcome = singleRestaurantFlow &&
                             flow.TryGetRestaurantDayOutcome(out currentResultsOutcome);
        continuePurchaseArmed = false;

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

        int earnedStars = CalculateEarnedStars();
        ConfigureResultsActions(singleRestaurantFlow);
        RefreshResultsResponsiveLayout(true);
        PrepareResultStars(earnedStars);

        AnimateResults(earnedStars);
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
            string reason = currentResultsOutcome switch
            {
                GameOverReason.Bankruptcy => "Funds reached ₱0 after expenses.",
                GameOverReason.ApprovalCollapsed => "Alien approval reached 0%.",
                GameOverReason.EarthSaved => "You completed Day 30 with enough approval.",
                GameOverReason.EarthConqueredDay30 => "Day 30 ended below the 40% approval target.",
                _ => "The campaign has ended."
            };
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
        if (CustomersServed <= 0)
            return "Seat and fully serve at least one group.";

        float angryRatio = angryCustomers / (float)CustomersServed;
        if (angryRatio >= 0.2f)
            return "Reduce waits: greet, serve food and collect bills sooner.";

        if (cashErrors > 0)
            return "Finish every payment and return the exact change.";

        if (revenueGap > 0)
            return "Serve more groups and complete every customer payment.";

        if (neutralCustomers > happyCustomers)
            return "Deliver food and bills faster to turn neutral guests happy.";

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
                                currentResultsOutcome != GameOverReason.EarthSaved;
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
        if (resultsStarsText != null)
            resultsStarsText.text = message;
    }

    private void SetOutcomeStatusText()
    {
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
            return 3;
        if (happyRatio >= 0.55f && cashErrors <= 1)
            return 2;
        return 1;
    }

    private void PrepareResultStars(int earnedStars)
    {
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
            stars[i].transform.localScale = earned ? Vector3.zero : authoredScale;
        }

        if (starRoot != null)
            starRoot.gameObject.SetActive(true);
    }

    private void AnimateResults(int earnedStars)
    {
        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);

        panelAnimationRoutine = StartCoroutine(AnimateResultsRoutine(earnedStars));
    }

    private IEnumerator AnimateResultsRoutine(int earnedStars)
    {
        yield return AnimatePanelRoutine(resultsPanel);

        Image[] stars = { star1, star2, star3 };
        for (int i = 0; i < Mathf.Min(earnedStars, stars.Length); i++)
        {
            Image star = stars[i];
            if (star == null)
                continue;

            Vector3 finalScale = GetAuthoredResultsScale(star.rectTransform);
            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
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

    private static void SetPanelVisible(GameObject panel, bool visible)
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
            root.SetActive(false);
        }
    }

    private void AnimatePanelIn(GameObject panel)
    {
        if (panelAnimationRoutine != null)
            StopCoroutine(panelAnimationRoutine);
        panelAnimationRoutine = StartCoroutine(AnimatePanelRoutine(panel));
    }

    private IEnumerator AnimatePanelRoutine(GameObject panel)
    {
        GameObject root = GetPanelPresentationRoot(panel);
        if (root == null)
            yield break;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();

        RectTransform rect = root.transform as RectTransform;
        Vector3 finalScale = Vector3.one;
        group.alpha = 0f;
        if (rect != null)
            rect.localScale = finalScale * 0.88f;

        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
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
}
