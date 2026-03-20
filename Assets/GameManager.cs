using System;
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

    public enum TaskRole
    {
        Host,
        Waiter,
        Cashier,
        Busser
    }

    public enum TaskType
    {
        SeatGroups,
        TakeOrders,
        ProcessOrders,
        ServeFood,
        DeliverBills,
        CleanTrays,
        CompletePayments
    }

    [Serializable]
    public class DayTask
    {
        public TaskRole role;
        public TaskType taskType;
        public int targetAmount = 1;
    }

    [Serializable]
    public class DaySettings
    {
        [Tooltip("Auto-synced from array index. Element 0 = Day 1, Element 1 = Day 2, etc.")]
        public int dayNumber = 1;

        [Tooltip("Length of the day in minutes. Use decimals like 0.2 for quick debugging.")]
        public float dayLengthMinutes = 8f;

        public int maxCustomersToSpawn = 12;
        public int maxGroupsPerMinute = 2;
        public float spawnIntervalMin = 6f;
        public float spawnIntervalMax = 12f;
        public DayTask[] tasks;

        public float DayLengthSeconds => dayLengthMinutes * 60f;
    }

    private const string SaveCurrentDayKey = "DineIn_CurrentDayIndex";
    private const string PendingDayKey = "DineIn_PendingDayIndex";

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "Lobby1";

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

    [Header("Days")]
    [SerializeField] private DaySettings[] days = new DaySettings[7];
    [SerializeField] private bool autoShowDayIntroOnPlay = true;
    [SerializeField] private int startDayIndex = 0;

    [Header("HUD UI")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Slider progressBar;

    [Header("Day Intro UI")]
    [SerializeField] private GameObject dayIntroPanel;
    [SerializeField] private TMP_Text dayIntroTitleText;
    [SerializeField] private TMP_Text dayIntroTasksText;
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
    [SerializeField] private Color activeStarColor = Color.white;
    [SerializeField] private Color inactiveStarColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Runtime")]
    [SerializeField] private int currentDayIndex;
    [SerializeField] private bool dayRunning;
    [SerializeField] private float timeRemaining;
    [SerializeField] private int groupsSpawnedToday;
    [SerializeField] private int groupsSpawnedThisMinute;
    [SerializeField] private float minuteWindowTimer;

    [Header("Mood Counts")]
    [SerializeField] private int happyCustomers;
    [SerializeField] private int neutralCustomers;
    [SerializeField] private int angryCustomers;

    [Header("Debug Progress")]
    [SerializeField] private int seatedGroupsDone;
    [SerializeField] private int ordersTakenDone;
    [SerializeField] private int ordersProcessedDone;
    [SerializeField] private int foodDeliveredDone;
    [SerializeField] private int billsDeliveredDone;
    [SerializeField] private int traysCleanedDone;
    [SerializeField] private int paymentsCompletedDone;

    private readonly Dictionary<TaskType, int> taskProgress = new Dictionary<TaskType, int>();
    private Coroutine spawnRoutine;

    private bool passedCurrentDay;
    private int lastDayStars;

    public bool DayRunning => dayRunning;
    public int CurrentDayIndex => currentDayIndex;
    public int CurrentDayNumber => ClampDayIndex(currentDayIndex) + 1;
    public float TimeRemaining => timeRemaining;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveManagerComponents();
        ValidateDays();
    }

    private void Start()
    {
        Debug.Log("[GameDayManager] Start");

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(ConfirmStartCurrentDay);
            playButton.onClick.AddListener(ConfirmStartCurrentDay);
        }

        if (resultsActionButton != null)
        {
            resultsActionButton.onClick.RemoveListener(OnResultsActionPressed);
            resultsActionButton.onClick.AddListener(OnResultsActionPressed);
        }

        RefreshUI();

        int pendingDayIndex = LoadPendingDayIndex();
        Debug.Log("[GameDayManager] Pending day index = " + pendingDayIndex);

        if (pendingDayIndex >= 0)
        {
            ClearPendingDayIndex();
            Debug.Log("[GameDayManager] Loading pending day intro for day index = " + pendingDayIndex);
            ShowDayIntro(ClampDayIndex(pendingDayIndex));
            return;
        }

        if (autoShowDayIntroOnPlay)
        {
            int savedDayIndex = LoadSavedCurrentDayIndex();
            Debug.Log("[GameDayManager] Loading saved day intro for day index = " + savedDayIndex);
            ShowDayIntro(ClampDayIndex(savedDayIndex));
        }
    }

    private void Update()
    {
        if (!dayRunning)
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

        if (timeRemaining <= 0f)
            EndDay();
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

    private void ValidateDays()
    {
        if (days == null || days.Length != 7)
            Array.Resize(ref days, 7);

        for (int i = 0; i < days.Length; i++)
        {
            if (days[i] == null)
                days[i] = new DaySettings();

            days[i].dayNumber = i + 1;

            if (days[i].dayLengthMinutes <= 0f)
                days[i].dayLengthMinutes = 8f;

            if (days[i].maxCustomersToSpawn < 0)
                days[i].maxCustomersToSpawn = 0;

            if (days[i].maxGroupsPerMinute < 1)
                days[i].maxGroupsPerMinute = 1;

            if (days[i].spawnIntervalMin <= 0f)
                days[i].spawnIntervalMin = 6f;

            if (days[i].spawnIntervalMax < days[i].spawnIntervalMin)
                days[i].spawnIntervalMax = days[i].spawnIntervalMin + 1f;

            if (days[i].tasks == null)
                days[i].tasks = Array.Empty<DayTask>();
        }
    }

    public void ShowDayIntro(int dayIndex)
    {
        if (days == null || days.Length == 0)
            return;

        dayIndex = ClampDayIndex(dayIndex);
        currentDayIndex = dayIndex;

        DaySettings settings = GetCurrentSettings();
        if (settings == null)
            return;

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(true);

        if (dayIntroTitleText != null)
            dayIntroTitleText.text = "Day " + CurrentDayNumber + "\nToday's Tasks";

        if (dayIntroTasksText != null)
            dayIntroTasksText.text = BuildTaskListText(settings);

        SaveCurrentDayIndex(currentDayIndex);
        RefreshUI();

        Debug.Log("[GameDayManager] ShowDayIntro -> currentDayIndex = " + currentDayIndex + " | dayNumber = " + CurrentDayNumber);
    }

    public void ConfirmStartCurrentDay()
    {
        StartCoroutine(StartDayRoutine());
    }

    private IEnumerator StartDayRoutine()
    {
        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

        yield return new WaitForSeconds(0.2f);

        StartDay(currentDayIndex);
    }

    public void StartDay(int dayIndex)
    {
        if (days == null || days.Length == 0)
            return;

        dayIndex = ClampDayIndex(dayIndex);
        ResolveManagerComponents();

        currentDayIndex = dayIndex;
        SaveCurrentDayIndex(currentDayIndex);

        ResetDayRuntime();

        DaySettings settings = GetCurrentSettings();
        if (settings == null)
            return;

        timeRemaining = Mathf.Max(1f, settings.DayLengthSeconds);
        dayRunning = true;
        passedCurrentDay = false;
        lastDayStars = 0;

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (dayIntroPanel != null)
            dayIntroPanel.SetActive(false);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnCustomersRoutine());

        RefreshUI();

        Debug.Log("[GameDayManager] StartDay -> currentDayIndex = " + currentDayIndex + " | dayNumber = " + CurrentDayNumber + " | dayLengthMinutes = " + settings.dayLengthMinutes);
    }

    public void EndDay()
    {
        if (!dayRunning)
            return;

        dayRunning = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        int stars = CalculateStars();
        lastDayStars = stars;
        passedCurrentDay = stars >= 3;

        if (passedCurrentDay)
        {
            int nextDay = Mathf.Min(currentDayIndex + 1, days.Length - 1);
            SaveCurrentDayIndex(nextDay);
            Debug.Log("[GameDayManager] Day passed. Saved next day index = " + nextDay);
        }

        ShowResults(stars);
    }

    public void ShowNextDayIntro()
    {
        int nextIndex = currentDayIndex + 1;
        if (nextIndex >= days.Length)
            nextIndex = currentDayIndex;

        ShowDayIntro(nextIndex);
    }

    public void RestartCurrentDay()
    {
        ShowDayIntro(currentDayIndex);
    }

    public void OnResultsActionPressed()
    {
        if (passedCurrentDay)
        {
            if (currentDayIndex < days.Length - 1)
                ReloadSceneForDay(currentDayIndex + 1);
            else
                ReloadSceneForDay(currentDayIndex);
        }
        else
        {
            ReloadSceneForDay(currentDayIndex);
        }
    }

    private void ReloadSceneForDay(int dayIndex)
    {
        dayIndex = ClampDayIndex(dayIndex);

        SavePendingDayIndex(dayIndex);
        SaveCurrentDayIndex(dayIndex);

        Debug.Log("[GameDayManager] ReloadSceneForDay -> loading scene '" + gameplaySceneName + "' with day index = " + dayIndex);

        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private void ResetDayRuntime()
    {
        groupsSpawnedToday = 0;
        groupsSpawnedThisMinute = 0;
        minuteWindowTimer = 0f;

        happyCustomers = 0;
        neutralCustomers = 0;
        angryCustomers = 0;

        seatedGroupsDone = 0;
        ordersTakenDone = 0;
        ordersProcessedDone = 0;
        foodDeliveredDone = 0;
        billsDeliveredDone = 0;
        traysCleanedDone = 0;
        paymentsCompletedDone = 0;

        taskProgress.Clear();

        DaySettings settings = GetCurrentSettings();
        if (settings != null && settings.tasks != null)
        {
            for (int i = 0; i < settings.tasks.Length; i++)
            {
                DayTask task = settings.tasks[i];
                if (task == null) continue;

                if (!taskProgress.ContainsKey(task.taskType))
                    taskProgress.Add(task.taskType, 0);
            }
        }
    }

    private IEnumerator SpawnCustomersRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (dayRunning)
        {
            DaySettings settings = GetCurrentSettings();
            if (settings == null)
                yield break;

            bool canSpawnMoreToday = groupsSpawnedToday < settings.maxCustomersToSpawn;
            bool canSpawnThisMinute = groupsSpawnedThisMinute < settings.maxGroupsPerMinute;

            if (canSpawnMoreToday && canSpawnThisMinute)
            {
                bool spawned = TrySpawnCustomerGroup();
                if (spawned)
                {
                    float delay = UnityEngine.Random.Range(settings.spawnIntervalMin, settings.spawnIntervalMax);
                    yield return new WaitForSeconds(delay);
                    continue;
                }
            }

            yield return null;
        }
    }

    private bool TrySpawnCustomerGroup()
    {
        if (!dayRunning)
            return false;

        DaySettings settings = GetCurrentSettings();
        if (settings == null)
            return false;

        if (groupsSpawnedToday >= settings.maxCustomersToSpawn)
            return false;

        if (groupsSpawnedThisMinute >= settings.maxGroupsPerMinute)
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

        groupsSpawnedToday++;
        groupsSpawnedThisMinute++;
        return true;
    }

    private DaySettings GetCurrentSettings()
    {
        if (days == null || days.Length == 0)
            return null;

        if (currentDayIndex < 0 || currentDayIndex >= days.Length)
            return null;

        return days[currentDayIndex];
    }

    private string BuildTaskListText(DaySettings settings)
    {
        if (settings == null || settings.tasks == null || settings.tasks.Length == 0)
            return "No tasks assigned.";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < settings.tasks.Length; i++)
        {
            DayTask task = settings.tasks[i];
            if (task == null) continue;

            sb.Append("• ");
            sb.Append(GetTaskText(task));

            if (i < settings.tasks.Length - 1)
                sb.Append("\n");
        }

        return sb.ToString();
    }

    private string GetTaskText(DayTask task)
    {
        switch (task.taskType)
        {
            case TaskType.SeatGroups:
                return "Seat " + task.targetAmount + " customer groups";
            case TaskType.TakeOrders:
                return "Take " + task.targetAmount + " orders";
            case TaskType.ProcessOrders:
                return "Process " + task.targetAmount + " orders";
            case TaskType.ServeFood:
                return "Serve " + task.targetAmount + " foods";
            case TaskType.DeliverBills:
                return "Deliver " + task.targetAmount + " bills";
            case TaskType.CleanTrays:
                return "Clean " + task.targetAmount + " trays";
            case TaskType.CompletePayments:
                return "Complete " + task.targetAmount + " payments";
            default:
                return "Complete " + task.targetAmount + " tasks";
        }
    }

    private void RefreshUI()
    {
        DaySettings settings = GetCurrentSettings();

        if (dayText != null)
            dayText.text = "Day " + CurrentDayNumber;

        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        if (progressBar != null)
            progressBar.value = CalculateProgress01();
    }

    private float CalculateProgress01()
    {
        DaySettings settings = GetCurrentSettings();
        if (settings == null || settings.tasks == null || settings.tasks.Length == 0)
            return 0f;

        float totalTarget = 0f;
        float totalDone = 0f;

        for (int i = 0; i < settings.tasks.Length; i++)
        {
            DayTask task = settings.tasks[i];
            if (task == null) continue;

            totalTarget += Mathf.Max(0, task.targetAmount);

            int current = GetTaskProgress(task.taskType);
            totalDone += Mathf.Min(current, task.targetAmount);
        }

        if (totalTarget <= 0f)
            return 0f;

        return Mathf.Clamp01(totalDone / totalTarget);
    }

    private int GetTaskProgress(TaskType taskType)
    {
        if (taskProgress.TryGetValue(taskType, out int value))
            return value;

        return 0;
    }

    private void AddTaskProgress(TaskType taskType, int amount = 1)
    {
        if (!dayRunning)
            return;

        if (!taskProgress.ContainsKey(taskType))
            taskProgress[taskType] = 0;

        taskProgress[taskType] += amount;
        RefreshUI();
    }

    private int CalculateStars()
    {
        int totalRatedGroups = happyCustomers + neutralCustomers + angryCustomers;
        float progress = CalculateProgress01();

        if (totalRatedGroups <= 0)
        {
            if (progress >= 1f) return 2;
            if (progress >= 0.6f) return 1;
            return 0;
        }

        float happyRatio = (float)happyCustomers / totalRatedGroups;

        if (happyRatio >= 0.85f && progress >= 1f)
            return 3;

        if (happyRatio >= 0.60f && progress >= 0.75f)
            return 2;

        if (happyRatio >= 0.35f && progress >= 0.40f)
            return 1;

        return 0;
    }

    private void ShowResults(int stars)
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (resultsTitleText != null)
            resultsTitleText.text = passedCurrentDay ? "Day " + CurrentDayNumber + " Complete" : "Day " + CurrentDayNumber + " Failed";

        if (resultsSummaryText != null)
        {
            StringBuilder sb = new StringBuilder();

            DaySettings settings = GetCurrentSettings();
            if (settings != null && settings.tasks != null && settings.tasks.Length > 0)
            {
                sb.AppendLine("Tasks");
                for (int i = 0; i < settings.tasks.Length; i++)
                {
                    DayTask task = settings.tasks[i];
                    if (task == null) continue;

                    int current = GetTaskProgress(task.taskType);
                    sb.AppendLine(GetTaskText(task) + "  (" + Mathf.Min(current, task.targetAmount) + "/" + task.targetAmount + ")");
                }

                sb.AppendLine();
            }

            sb.AppendLine("Happy: " + happyCustomers);
            sb.AppendLine("Neutral: " + neutralCustomers);
            sb.AppendLine("Angry: " + angryCustomers);

            resultsSummaryText.text = sb.ToString();
        }

        if (resultsStarsText != null)
            resultsStarsText.text = stars + " / 3 Stars";

        SetStarVisual(star1, stars >= 1);
        SetStarVisual(star2, stars >= 2);
        SetStarVisual(star3, stars >= 3);

        if (resultsActionButton != null)
        {
            resultsActionButton.gameObject.SetActive(true);

            bool hasNextDay = currentDayIndex < days.Length - 1;

            if (passedCurrentDay)
            {
                if (resultsActionButtonText != null)
                    resultsActionButtonText.text = hasNextDay ? "Next Day" : "Restart";
            }
            else
            {
                if (resultsActionButtonText != null)
                    resultsActionButtonText.text = "Restart";
            }
        }
    }

    private void SetStarVisual(Image star, bool active)
    {
        if (star == null)
            return;

        star.color = active ? activeStarColor : inactiveStarColor;
    }

    private int ClampDayIndex(int index)
    {
        if (days == null || days.Length == 0)
            return 0;

        return Mathf.Clamp(index, 0, days.Length - 1);
    }

    private void SaveCurrentDayIndex(int index)
    {
        PlayerPrefs.SetInt(SaveCurrentDayKey, ClampDayIndex(index));
        PlayerPrefs.Save();
    }

    private int LoadSavedCurrentDayIndex()
    {
        return PlayerPrefs.GetInt(SaveCurrentDayKey, ClampDayIndex(startDayIndex));
    }

    private void SavePendingDayIndex(int index)
    {
        PlayerPrefs.SetInt(PendingDayKey, ClampDayIndex(index));
        PlayerPrefs.Save();
    }

    private int LoadPendingDayIndex()
    {
        return PlayerPrefs.GetInt(PendingDayKey, -1);
    }

    private void ClearPendingDayIndex()
    {
        PlayerPrefs.DeleteKey(PendingDayKey);
        PlayerPrefs.Save();
    }

    public void ResetSavedProgressToDay1()
    {
        PlayerPrefs.SetInt(SaveCurrentDayKey, 0);
        PlayerPrefs.DeleteKey(PendingDayKey);
        PlayerPrefs.Save();
    }

    public void RegisterGroupSeated()
    {
        seatedGroupsDone++;
        AddTaskProgress(TaskType.SeatGroups);
    }

    public void RegisterOrderTaken()
    {
        ordersTakenDone++;
        AddTaskProgress(TaskType.TakeOrders);
    }

    public void RegisterOrderProcessed()
    {
        ordersProcessedDone++;
        AddTaskProgress(TaskType.ProcessOrders);
    }

    public void RegisterFoodDelivered()
    {
        foodDeliveredDone++;
        AddTaskProgress(TaskType.ServeFood);
    }

    public void RegisterBillDelivered()
    {
        billsDeliveredDone++;
        AddTaskProgress(TaskType.DeliverBills);
    }

    public void RegisterTrayCleaned()
    {
        traysCleanedDone++;
        AddTaskProgress(TaskType.CleanTrays);
    }

    public void RegisterPaymentCompleted()
    {
        paymentsCompletedDone++;
        AddTaskProgress(TaskType.CompletePayments);
    }

    public void RegisterHappyCustomer()
    {
        if (!dayRunning)
            return;

        happyCustomers++;
    }

    public void RegisterNeutralCustomer()
    {
        if (!dayRunning)
            return;

        neutralCustomers++;
    }

    public void RegisterAngryCustomer()
    {
        if (!dayRunning)
            return;

        angryCustomers++;
    }

    public int GetCurrentStarsPreview()
    {
        return CalculateStars();
    }

    public float GetProgress01()
    {
        return CalculateProgress01();
    }

    public int GetTaskCurrent(TaskType taskType)
    {
        return GetTaskProgress(taskType);
    }
}