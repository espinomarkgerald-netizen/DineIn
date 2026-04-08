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

    [Header("Runtime")]
    [SerializeField] private bool shiftRunning;
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

    private Coroutine spawnRoutine;
    private float angryBarVisual;
    private float neutralBarVisual;
    private bool warnedLastMinute;

    public bool ShiftRunning => shiftRunning;
    public float TimeRemaining => timeRemaining;
    public int HappyCustomers => happyCustomers;
    public int NeutralCustomers => neutralCustomers;
    public int AngryCustomers => angryCustomers;
    public int CustomersServed => happyCustomers + neutralCustomers + angryCustomers;
    public float ShiftLengthSeconds => Mathf.Max(1f, shiftLengthMinutes * 60f);
    public int TipsEarned => tipsEarned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

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

        ApplyTakeoutUnlock();

        RefreshUI();
        SetupMoodBars(true);
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

        if (dayIntroTitleText != null)
            dayIntroTitleText.text = "Start Shift";

        int minutes = Mathf.FloorToInt(ShiftLengthSeconds / 60f);
        int seconds = Mathf.FloorToInt(ShiftLengthSeconds % 60f);

        if (dayIntroSummaryLeftText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Shift Info</b>");
            sb.AppendLine("Department: Lobby");
            sb.AppendLine("Phase: First Half");
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
        ResetShiftRuntime();

        timeRemaining = ShiftLengthSeconds;
        shiftRunning = true;

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

    private void ApplyDifficultyScaling()
    {
        if (GameFlowManager.Instance == null || maxScalingDay <= 1)
            return;

        int day = GameFlowManager.Instance.CurrentDay;
        float t = Mathf.Clamp01((float)(day - 1) / (maxScalingDay - 1));

        maxCustomersToSpawn = Mathf.RoundToInt(maxCustomersCurve.Evaluate(t));
        maxGroupsPerMinute = Mathf.Max(1, Mathf.RoundToInt(groupsPerMinuteCurve.Evaluate(t)));
        spawnIntervalMin = Mathf.Max(1f, spawnIntervalMinCurve.Evaluate(t));
        spawnIntervalMax = Mathf.Max(spawnIntervalMin + 1f, spawnIntervalMaxCurve.Evaluate(t));
    }

    public void EndShift()
    {
        if (!shiftRunning)
            return;

        shiftRunning = false;

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

        ShowResults();
    }

    public void RestartShift()
    {
        ShowShiftIntro();
    }

    public void OnResultsActionPressed()
    {
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

        groupsSeated = 0;
        ordersTaken = 0;
        ordersProcessed = 0;
        foodDelivered = 0;
        billsDelivered = 0;
        traysCleaned = 0;
        paymentsCompleted = 0;
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

        bool shouldEnable = GameFlowManager.Instance != null
            && GameFlowManager.Instance.CurrentDay >= takeoutUnlockDay;

        groupSpawner.SetTakeoutEnabled(shouldEnable);
        Debug.Log($"[GameDayManager] Takeout {(shouldEnable ? "ENABLED" : "DISABLED")} " +
                  $"(current day: {GameFlowManager.Instance?.CurrentDay}, unlock day: {takeoutUnlockDay}).");
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
            dayText.text = "Shift";

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
            $"₱{DailyFinanceBridge.Instance.EarnedToday} / ₱{DailyFinanceBridge.Instance.TotalRequiredEarningsToday}";
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

        if (resultsTitleText != null)
            resultsTitleText.text = "Half-Day Report";

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
                resultsActionButtonText.text = "Back to Management";
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