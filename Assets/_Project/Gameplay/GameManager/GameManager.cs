using System.Collections;
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

    [Header("Shift Settings")]
    [SerializeField] private float shiftLengthMinutes = 4f;

    [Header("Spawn Settings")]
    [SerializeField] private int maxCustomersToSpawn = 12;
    [SerializeField] private int maxGroupsPerMinute = 2;
    [SerializeField] private float spawnIntervalMin = 6f;
    [SerializeField] private float spawnIntervalMax = 12f;

    [Header("Spawn Difficulty Scaling")]
    [Tooltip("X = normalized day (0 = Day 1, 1 = max day). Y = max customer groups to spawn.")]
    [SerializeField] private AnimationCurve maxCustomersCurve = new AnimationCurve(
        new Keyframe(0f, 4f),
        new Keyframe(0.25f, 7f),
        new Keyframe(0.6f, 10f),
        new Keyframe(1f, 14f));

    [Tooltip("X = normalized day. Y = max groups allowed per minute.")]
    [SerializeField] private AnimationCurve groupsPerMinuteCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.25f, 1f),
        new Keyframe(0.6f, 2f),
        new Keyframe(1f, 3f));

    [Tooltip("X = normalized day. Y = minimum seconds between spawns.")]
    [SerializeField] private AnimationCurve spawnIntervalMinCurve = new AnimationCurve(
        new Keyframe(0f, 60f),
        new Keyframe(0.25f, 40f),
        new Keyframe(0.6f, 20f),
        new Keyframe(1f, 8f));

    [Tooltip("X = normalized day. Y = maximum seconds between spawns.")]
    [SerializeField] private AnimationCurve spawnIntervalMaxCurve = new AnimationCurve(
        new Keyframe(0f, 90f),
        new Keyframe(0.25f, 60f),
        new Keyframe(0.6f, 35f),
        new Keyframe(1f, 14f));

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
    [SerializeField] private TMP_Text resultsStarsText;
    [SerializeField] private Image star1;
    [SerializeField] private Image star2;
    [SerializeField] private Image star3;
    [SerializeField] private Button resultsActionButton;
    [SerializeField] private TMP_Text resultsActionButtonText;

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
    private float angryBarVisual;
    private float neutralBarVisual;
    private bool warnedLastMinute;

    public bool ShiftRunning => shiftRunning;
    public bool ServiceActive => shiftRunning || closingOut;
    public float TimeRemaining => timeRemaining;
    public int HappyCustomers => happyCustomers;
    public int NeutralCustomers => neutralCustomers;
    public int AngryCustomers => angryCustomers;
    public int CustomersServed => happyCustomers + neutralCustomers + angryCustomers;
    public float ShiftLengthSeconds => Mathf.Max(1f, shiftLengthMinutes * 60f);
    public int CashErrors => cashErrors;
    public int TipsEarned => tipsEarned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (bootstrapSingleRestaurantFlow)
            GameFlowManager.EnsureSingleRestaurantFlow(restaurantSceneName);

        if (enableAutonomousServiceBots && GetComponent<LobbyAutonomousService>() == null)
            gameObject.AddComponent<LobbyAutonomousService>();

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        ResolveManagerComponents();
        ValidateSettings();
    }

    private void Start()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

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

        RefreshUI();
        SetupMoodBars(true);

        if (enableAutonomousServiceBots && autoStartServiceWhenBotsEnabled)
            StartShift();
        else
            ShowShiftIntro();
    }

    private void Update()
    {
        UpdateMoodBarsSmooth();

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
            ShowWarning("Last minute. Finish remaining customers.");
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
        if (shiftLengthMinutes <= 0f)
            shiftLengthMinutes = 4f;

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
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(true);

        bool singleRestaurantFlow = GameFlowManager.Instance != null &&
                                    GameFlowManager.Instance.UsesSingleRestaurantFlow;

        if (dayIntroTitleText != null)
            dayIntroTitleText.text = singleRestaurantFlow && GameFlowManager.Instance.IsEndlessRestaurantMode
                ? "Continue Service"
                : "Start Day";

        int minutes = Mathf.FloorToInt(ShiftLengthSeconds / 60f);
        int seconds = Mathf.FloorToInt(ShiftLengthSeconds % 60f);

        if (dayIntroSummaryLeftText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Shift Info</b>");
            if (singleRestaurantFlow)
            {
                sb.AppendLine("Restaurant: Casual Dining");
                sb.AppendLine(GameFlowManager.Instance.IsEndlessRestaurantMode
                    ? "Mode: Endless Service"
                    : "Mode: Campaign Service");
            }
            else
            {
                sb.AppendLine("Department: Lobby");
                sb.AppendLine("Phase: First Half");
            }
            sb.AppendLine("Length: " + minutes.ToString("00") + ":" + seconds.ToString("00"));
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
        StartCoroutine(StartShiftRoutine());
    }

    private IEnumerator StartShiftRoutine()
    {
        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

        yield return new WaitForSeconds(0.2f);

        StartShift();
    }

    public void StartShift()
    {
        ResolveManagerComponents();
        ApplyDifficultyScaling();
        ApplyTakeoutUnlock();
        ApplyCustomerTypeUnlocks();
        ResetShiftRuntime();

        timeRemaining = ShiftLengthSeconds;
        shiftRunning = true;
        closingOut = false;
        GameFlowManager.Instance?.MarkRestaurantServiceStarted();

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnCustomersRoutine());

        RefreshUI();
        SetupMoodBars(true);
        ShowWarning("Shift started. Keep customers satisfied.");
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
        StartCoroutine(ShowResultsWhenClear());
    }

    private IEnumerator ShowResultsWhenClear()
    {
        while (FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Length > 0)
            yield return new WaitForSeconds(1f);

        closingOut = false;
        ShowResults();
    }

    public void RestartShift()
    {
        ShowShiftIntro();
    }

    public void OnResultsActionPressed()
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.UsesSingleRestaurantFlow)
        {
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

        SetupMoodBars(true);
    }

    private IEnumerator SpawnCustomersRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (shiftRunning)
        {
            bool canSpawnMoreShift = groupsSpawnedThisShift < maxCustomersToSpawn;
            bool canSpawnThisMinute = groupsSpawnedThisMinute < maxGroupsPerMinute;

            if (canSpawnMoreShift && canSpawnThisMinute)
            {
                bool spawned = TrySpawnCustomerGroup();
                if (spawned)
                {
                    float delay = Random.Range(spawnIntervalMin, spawnIntervalMax);
                    yield return new WaitForSeconds(delay);
                    continue;
                }
            }

            yield return null;
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

    private const float StopSpawnTimeRemainingSeconds = 60f;

    private bool TrySpawnCustomerGroup()
    {
        if (!shiftRunning)
            return false;

        if (timeRemaining <= StopSpawnTimeRemainingSeconds)
            return false;

        if (groupsSpawnedThisShift >= maxCustomersToSpawn)
            return false;

        if (groupsSpawnedThisMinute >= maxGroupsPerMinute)
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
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

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
            $"₱{DailyFinanceBridge.Instance.EarnedToday}";
    }

    private float CalculateProgress01()
    {
        if (DailyFinanceBridge.Instance != null)
            return DailyFinanceBridge.Instance.GetProgress01();

        return 0f;
    }

    private void ShowResults()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        bool singleRestaurantFlow = GameFlowManager.Instance != null &&
                                    GameFlowManager.Instance.UsesSingleRestaurantFlow;

        if (resultsTitleText != null)
            resultsTitleText.text = singleRestaurantFlow ? "Day Report" : "Half-Day Report";

        if (resultsSummaryText != null)
        {
            StringBuilder sb = new StringBuilder();

            int earned = 0;
            if (DailyFinanceBridge.Instance != null)
                earned = DailyFinanceBridge.Instance.EarnedToday;

            sb.AppendLine("<b>REVENUE</b>");
            sb.AppendLine("₱" + earned);
            sb.AppendLine();
            sb.AppendLine("<b>CUSTOMERS</b>");
            sb.AppendLine("😊 " + happyCustomers + "   😐 " + neutralCustomers + "   😡 " + angryCustomers);
            sb.AppendLine();
            sb.AppendLine("<b>CASH HANDLING</b>");
            if (cashErrors == 0)
                sb.AppendLine("✓ No errors");
            else
                sb.AppendLine("⚠ " + cashErrors + " error" + (cashErrors == 1 ? "" : "s"));

            if (tipsEarned > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>TIPS</b>");
                sb.AppendLine("₱" + tipsEarned);
            }

            resultsSummaryText.text = sb.ToString().TrimEnd();
        }

        if (resultsStarsText != null)
            resultsStarsText.text = GetShiftStatusText();

        if (star1 != null) star1.gameObject.SetActive(false);
        if (star2 != null) star2.gameObject.SetActive(false);
        if (star3 != null) star3.gameObject.SetActive(false);

        if (resultsActionButton != null)
        {
            resultsActionButton.gameObject.SetActive(true);

            if (resultsActionButtonText != null)
            {
                resultsActionButtonText.text = singleRestaurantFlow
                    ? (GameFlowManager.Instance.IsEndlessRestaurantMode ? "Continue Service" : "Start Next Day")
                    : "Back to Management";
            }
        }
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
        if (!shiftRunning) return;
        groupsSeated++;
        RefreshUI();
    }

    public void RegisterOrderTaken()
    {
        if (!shiftRunning) return;
        ordersTaken++;
        RefreshUI();
    }

    public void RegisterOrderProcessed()
    {
        if (!shiftRunning) return;
        ordersProcessed++;
        RefreshUI();
    }

    public void RegisterFoodDelivered()
    {
        if (!shiftRunning) return;
        foodDelivered++;
        RefreshUI();
    }

    public void RegisterBillDelivered()
    {
        if (!shiftRunning) return;
        billsDelivered++;
        RefreshUI();
    }

    public void RegisterTrayCleaned()
    {
        if (!shiftRunning) return;
        traysCleaned++;
        RefreshUI();
    }

    public void RegisterPaymentCompleted()
    {
        if (!shiftRunning) return;
        paymentsCompleted++;
        RefreshUI();
    }

    public void RegisterHappyCustomer()
    {
        if (!shiftRunning) return;
        happyCustomers++;
    }

    public void RegisterNeutralCustomer()
    {
        if (!shiftRunning) return;
        neutralCustomers++;
    }

    public void RegisterAngryCustomer()
    {
        if (!shiftRunning) return;
        angryCustomers++;
    }

    public void RegisterTip(int amount)
    {
        if (!shiftRunning) return;
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

    /// <summary>
    /// Records a failed cash-handling session — the register was closed without a
    /// correct Confirm (group left, or session timed out before correct change was given).
    /// </summary>
    public void RegisterCashError()
    {
        if (!shiftRunning)
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
