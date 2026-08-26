using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public enum GamePhase
    {
        None,
        Management,
        Lobby,
        Kitchen,
        Restaurant
    }

    public enum DayHalf
    {
        None,
        Morning,
        Afternoon
    }

    public enum RestaurantSessionState
    {
        None,
        PreOpen,
        Running,
        DayComplete,
        Endless
    }

    [Header("Scene Names")]
    [SerializeField] private string managementSceneName = "Office";
    [SerializeField] private string lobbySceneName = "Lobby1";
    [SerializeField] private string kitchenSceneName = "Kitchen";

    [Header("Single Restaurant Flow")]
    [Tooltip("Uses one restaurant scene for the full campaign instead of Office, Lobby, and Kitchen scene transitions.")]
    [SerializeField] private bool useSingleRestaurantFlow;
    [SerializeField] private string restaurantSceneName = "Lobby1";
    [SerializeField, Min(1)] private int campaignDayLimit = 30;
    [SerializeField, Range(0, 100)] private int campaignApprovalTarget = 40;
    [SerializeField] private RestaurantSessionState restaurantSessionState = RestaurantSessionState.None;
    [SerializeField] private bool campaignCompleted;

    [Header("Session")]
    [SerializeField] private int currentDay = 1;
    [SerializeField] private GamePhase currentPhase = GamePhase.None;
    [SerializeField] private DayHalf currentDayHalf = DayHalf.Morning;
    [SerializeField] private bool lobbyCompleted;
    [SerializeField] private bool kitchenCompleted;

    [Header("UI")]
    [SerializeField] private TMP_Text dayText;

    [Header("Game Over")]
    [SerializeField] private GameOverScreen gameOverScreen;

    public int CurrentDay => currentDay;
    public GamePhase CurrentPhase => currentPhase;
    public DayHalf CurrentDayHalf => currentDayHalf;
    public bool LobbyCompleted => lobbyCompleted;
    public bool KitchenCompleted => kitchenCompleted;

    public bool IsMorning => currentDayHalf == DayHalf.Morning;
    public bool IsAfternoon => currentDayHalf == DayHalf.Afternoon;
    public bool UsesSingleRestaurantFlow => useSingleRestaurantFlow;
    public bool CampaignCompleted => campaignCompleted;
    public bool IsEndlessRestaurantMode => useSingleRestaurantFlow && campaignCompleted;
    public RestaurantSessionState CurrentRestaurantSessionState => restaurantSessionState;
    public bool HasUnfinishedRestaurantDay => useSingleRestaurantFlow &&
        (restaurantSessionState == RestaurantSessionState.PreOpen ||
         restaurantSessionState == RestaurantSessionState.Running);
    public bool HasRunningRestaurantDay => useSingleRestaurantFlow &&
        restaurantSessionState == RestaurantSessionState.Running;
    public bool RestaurantDayHasTerminalOutcome => TryGetRestaurantDayOutcome(out _);

    public event Action<int> OnDayChanged;

    /// <summary>
    /// Ensures a direct play of the Casual Dining scene has the same persistent
    /// campaign flow as a scene entered through a menu. This keeps the gameplay
    /// scene self-contained while legacy scenes can continue using their setup.
    /// </summary>
    public static GameFlowManager EnsureSingleRestaurantFlow(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[GameFlowManager] Cannot bootstrap Single Restaurant Flow without a scene name.");
            return Instance;
        }

        if (Instance == null)
        {
            GameObject managerObject = new GameObject("GameFlowManager");
            Instance = managerObject.AddComponent<GameFlowManager>();
        }

        Instance.ConfigureSingleRestaurantFlow(sceneName);
        Instance.EnsureRestaurantDayPrepared();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A persistent GameFlowManager from a previous scene already exists.
            // Transfer any scene-local serialized references (dayText, gameOverScreen)
            // from this newly spawned copy to the live singleton so they always point
            // to objects in the currently loaded scene and never become stale.
            if (dayText != null)
                Instance.dayText = dayText;

            if (gameOverScreen != null)
                Instance.gameOverScreen = gameOverScreen;

            if (useSingleRestaurantFlow)
                Instance.CopySingleRestaurantConfiguration(this);

            // Destroy only this component — sibling components on the same GameObject
            // (OfficeStartDayButton, OfficeStartButtons, etc.) must survive.
            Debug.Log($"[GameFlowManager] Duplicate on '{gameObject.name}' in '{gameObject.scene.name}'. " +
                      $"Transferring scene refs to persistent singleton and destroying this component.");
            Destroy(this);
            return;
        }

        Instance = this;

        if (currentDayHalf == DayHalf.None)
            currentDayHalf = DayHalf.Morning;

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (useSingleRestaurantFlow && SceneManager.GetActiveScene().name == restaurantSceneName)
            EnsureRestaurantDayPrepared();

        NotifyDayChanged();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (useSingleRestaurantFlow && scene.name == restaurantSceneName)
        {
            currentPhase = GamePhase.Restaurant;
            currentDayHalf = DayHalf.None;
            EnsureRestaurantDayPrepared();
        }

        if (scene.name == lobbySceneName && currentPhase == GamePhase.Lobby)
            ShiftScaler.Instance?.ApplyScaling(currentDay);

        RefreshDayText();
        NotifyDayChanged();
    }

    public void StartNewDay()
    {
        if (useSingleRestaurantFlow)
        {
            if (campaignCompleted)
            {
                StartEndlessRestaurantDay();
                return;
            }

            currentDay = Mathf.Min(currentDay + 1, campaignDayLimit);
            PrepareRestaurantDay();
            LoadRestaurantScene();
            return;
        }

        currentDay++;
        lobbyCompleted = false;
        kitchenCompleted = false;
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Management;

        DailyRevenueTracker.Instance?.ResetForNewDay();
        DailyFinanceBridge.Instance?.ResetDay();
        AlienApprovalManager.Instance?.BeginNewDay();
        FinanceManager.Instance?.ResetDailyExpenses();
        EmployeeManager.Instance?.ResetDailyAssignments();

        int maxGroupsThisShift = ShiftScaler.Instance != null
            ? ShiftScaler.Instance.CurrentGroupCount
            : 5;

        DailyObjectiveManager.Instance?.RollObjectivesForDay(currentDay, maxGroupsThisShift);

        EquipmentManager.Instance?.UnlockByDay(currentDay);
        EquipmentShopManager shop = FindFirstObjectByType<EquipmentShopManager>();
        shop?.InitializeShop();

        RecipeManager.Instance?.UnlockByDay(currentDay);

        NotifyDayChanged();
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartLobbyShift()
    {
        if (useSingleRestaurantFlow)
        {
            BeginRestaurantDay();
            return;
        }

        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Lobby;
        EmployeeManager.Instance?.LockAllSlots();
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(lobbySceneName);
    }

    public void ReturnToManagementFromLobby()
    {
        if (useSingleRestaurantFlow)
        {
            CompleteRestaurantDay();
            return;
        }

        lobbyCompleted = true;
        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Management;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartKitchenShift()
    {
        if (useSingleRestaurantFlow)
        {
            BeginRestaurantDay();
            return;
        }

        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Kitchen;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(kitchenSceneName);
    }

    public void ReturnToManagementFromKitchen()
    {
        if (useSingleRestaurantFlow)
        {
            CompleteRestaurantDay();
            return;
        }

        kitchenCompleted = true;
        currentPhase = GamePhase.Management;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void LoadManagementScene()
    {
        if (useSingleRestaurantFlow)
        {
            BeginRestaurantDay();
            return;
        }

        currentPhase = GamePhase.Management;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void LoadLobbyScene()
    {
        StartLobbyShift();
    }

    public void LoadKitchenScene()
    {
        StartKitchenShift();
    }

    public bool CanStartLobby()
    {
        if (useSingleRestaurantFlow)
            return true;

        return !lobbyCompleted;
    }

    public bool CanStartKitchen()
    {
        if (useSingleRestaurantFlow)
            return false;

        return lobbyCompleted && !kitchenCompleted;
    }

    public bool IsDayFullyCompleted()
    {
        if (useSingleRestaurantFlow)
            return restaurantSessionState == RestaurantSessionState.DayComplete;

        return lobbyCompleted && kitchenCompleted;
    }

    public void ResetRun()
    {
        GameSaveManager.Instance?.CommitDayCheckpoint();
        currentDay = 1;
        currentPhase = GamePhase.Management;
        currentDayHalf = DayHalf.Morning;
        lobbyCompleted = false;
        kitchenCompleted = false;
        campaignCompleted = false;
        restaurantSessionState = RestaurantSessionState.None;

        MoneyManager.Instance?.ResetToStartingMoney();
        AlienApprovalManager.Instance?.ResetApproval();
        DailyObjectiveManager.Instance?.ResetForNewRun();

        FinanceManager.Instance?.ResetDailyExpenses();
        DailyRevenueTracker.Instance?.ResetForNewDay();
        DailyFinanceBridge.Instance?.ResetDay();

        InventoryManager.Instance?.ResetStock();
        RestockOrderManager.EnsureInstance()?.ClearAll();
        EmployeeManager.Instance?.ClearAllEmployees();
        CasualDiningPolishManager.EnsureInstance()?.ResetRun();
        ManagerComplaintSystem.EnsureInstance()?.ResetRun();
        EquipmentManager.Instance?.ResetPurchases();
        UnlockManager.Instance?.ResetAll();

        EquipmentManager.Instance?.UnlockByDay(currentDay);

        NotifyDayChanged();
        GameSaveManager.Instance?.RequestSave();

        Debug.Log("[GameFlow] Run fully reset to Day 1.");

        if (useSingleRestaurantFlow)
        {
            BeginRestaurantDay();
            return;
        }

        SceneManager.LoadScene(managementSceneName);
    }

    public void StartDay()
    {
        if (useSingleRestaurantFlow)
        {
            BeginRestaurantDay();
            return;
        }

        StartLobbyShift();
    }

    /// <summary>Called by GameDayManager after the player confirms the day-start panel.</summary>
    public void MarkRestaurantServiceStarted()
    {
        if (!useSingleRestaurantFlow)
            return;

        // Preparation choices must persist normally. The rollback checkpoint is
        // captured only when service begins, after those choices are complete.
        GameSaveManager.Instance?.CaptureDayStartCheckpoint();
        restaurantSessionState = campaignCompleted
            ? RestaurantSessionState.Endless
            : RestaurantSessionState.Running;
    }

    public void EndOfDayFinance()
    {
        if (EmployeeManager.Instance != null)
        {
            int payroll = EmployeeManager.Instance.CalculateTotalPayroll();
            FinanceManager.Instance?.RecordExpense("Payroll", payroll);
        }

        FinanceManager.Instance?.DeductAllExpenses();
        FinanceManager.Instance?.PrintDailyReport();
    }

    public void EvaluateEndOfDay()
    {
        if (useSingleRestaurantFlow)
        {
            EvaluateRestaurantDay();
            return;
        }

        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        DailyObjectiveManager.Instance?.EvaluateAndApply();

        approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        if (money <= 0)
        {
            TriggerGameOver(GameOverReason.Bankruptcy);
            return;
        }

        if (approval <= 0)
        {
            TriggerGameOver(GameOverReason.ApprovalCollapsed);
            return;
        }

        if (currentDay >= 30)
        {
            GameOverReason reason = approval >= 40
                ? GameOverReason.EarthSaved
                : GameOverReason.EarthConqueredDay30;

            TriggerGameOver(reason);
            return;
        }

        StartNewDay();
    }

    public void TriggerGameOver(GameOverReason reason)
    {
        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        GameOverScreen screen = GameOverScreen.Instance != null ? GameOverScreen.Instance : gameOverScreen;
        if (screen == null)
            screen = GameOverScreen.CreateRuntimeFallback();

        if (screen != null)
        {
            screen.Show(reason, approval, money, currentDay);
            Time.timeScale = 0f;
            return;
        }

        Debug.LogError("[GameFlowManager] Could not create a GameOverScreen. The run cannot present its outcome.");
    }

    public bool TrySetCurrentDayDebug(int day)
    {
        if (day < 1 || day > 30)
            return false;

        currentDay = day;

        EquipmentManager.Instance?.UnlockByDay(currentDay);
        RecipeManager.Instance?.UnlockByDay(currentDay);

        EquipmentShopManager shop = FindFirstObjectByType<EquipmentShopManager>();
        shop?.InitializeShop();

        if (currentPhase == GamePhase.Lobby || currentPhase == GamePhase.Restaurant)
            ShiftScaler.Instance?.ApplyScaling(currentDay);

        CasualDiningPolishManager.EnsureInstance()?.PrepareDay(currentDay, campaignCompleted);
        NotifyDayChanged();
        GameSaveManager.Instance?.RequestSave();
        return true;
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.currentDay = currentDay;
        data.currentPhase = (int)currentPhase;
        data.currentDayHalf = (int)currentDayHalf;
        data.lobbyCompleted = lobbyCompleted;
        data.kitchenCompleted = kitchenCompleted;
        data.campaignCompleted = campaignCompleted;
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        currentDay = Mathf.Clamp(data.currentDay, 1, 30);
        currentPhase = (GamePhase)Mathf.Clamp(data.currentPhase, 0, Enum.GetValues(typeof(GamePhase)).Length - 1);
        currentDayHalf = (DayHalf)Mathf.Clamp(data.currentDayHalf, 0, Enum.GetValues(typeof(DayHalf)).Length - 1);
        lobbyCompleted = data.lobbyCompleted;
        kitchenCompleted = data.kitchenCompleted;
        campaignCompleted = data.campaignCompleted;

        if (useSingleRestaurantFlow)
        {
            currentPhase = GamePhase.Restaurant;
            currentDayHalf = DayHalf.None;
            restaurantSessionState = campaignCompleted
                ? RestaurantSessionState.Endless
                : RestaurantSessionState.PreOpen;
        }

        RefreshDayText();
        NotifyDayChanged();
    }

    private void NotifyDayChanged()
    {
        RefreshDayText();
        OnDayChanged?.Invoke(currentDay);
    }

    private void RefreshDayText()
    {
        if (dayText == null)
            return;

        dayText.text = $"Day {currentDay}";
    }

    /// <summary>
    /// Starts the one-scene restaurant flow. The scene's GameDayManager presents
    /// the start panel and begins service after the player confirms it.
    /// </summary>
    public void BeginRestaurantDay()
    {
        if (!useSingleRestaurantFlow)
        {
            StartLobbyShift();
            return;
        }

        Time.timeScale = 1f;
        PrepareRestaurantDay();
        LoadRestaurantScene();
    }

    /// <summary>
    /// Called by the restaurant results screen after all active customers have left.
    /// It is intentionally the only single-scene path that applies daily finance,
    /// objectives, approval, and campaign progression.
    /// </summary>
    public void CompleteRestaurantDay()
    {
        if (!useSingleRestaurantFlow)
            return;

        FinalizeRestaurantDayForResults();
        if (restaurantSessionState != RestaurantSessionState.DayComplete)
            return;

        EvaluateRestaurantDay();
    }

    /// <summary>
    /// Applies payroll, expenses, and objective approval before the Day Report
    /// is populated. This lets that existing panel present the real final
    /// result, including a terminal outcome, instead of changing state only
    /// after its button has already been pressed.
    /// </summary>
    public void FinalizeRestaurantDayForResults()
    {
        if (!useSingleRestaurantFlow || restaurantSessionState == RestaurantSessionState.DayComplete)
            return;

        restaurantSessionState = RestaurantSessionState.DayComplete;
        GameSaveManager.Instance?.CommitDayCheckpoint();
        EndOfDayFinance();

        if (!campaignCompleted)
            DailyObjectiveManager.Instance?.EvaluateAndApply();

        CasualDiningPolishManager.EnsureInstance()?.FinalizeDay(currentDay);

        GameSaveManager.Instance?.RequestSave();
    }

    public bool TryGetRestaurantDayOutcome(out GameOverReason reason)
    {
        reason = default;
        if (!useSingleRestaurantFlow || campaignCompleted)
            return false;

        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        if (money <= 0)
        {
            reason = GameOverReason.Bankruptcy;
            return true;
        }

        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        if (approval <= 0)
        {
            reason = GameOverReason.ApprovalCollapsed;
            return true;
        }

        if (currentDay < campaignDayLimit)
            return false;

        reason = approval >= campaignApprovalTarget
            ? GameOverReason.EarthSaved
            : GameOverReason.EarthConqueredDay30;
        return true;
    }

    /// <summary>
    /// Paid recovery preserves the restaurant and all earned unlocks. It only
    /// restores the failed survival resource and moves the campaign forward;
    /// Day 30 losses replay Day 30 so the ending can still be improved.
    /// </summary>
    public bool ContinueRestaurantCampaignAfterRecovery()
    {
        if (!TryGetRestaurantDayOutcome(out GameOverReason reason) ||
            reason == GameOverReason.EarthSaved)
            return false;

        if (MoneyManager.Instance != null && MoneyManager.Instance.Money <= 0)
            MoneyManager.Instance.ResetToStartingMoney();

        int minimumApproval = currentDay >= campaignDayLimit
            ? Mathf.Max(30, campaignApprovalTarget)
            : 30;
        int currentApproval = AlienApprovalManager.Instance != null
            ? AlienApprovalManager.Instance.Approval
            : 0;
        AlienApprovalManager.Instance?.RestoreApprovalForContinue(
            Mathf.Max(currentApproval, minimumApproval));

        Time.timeScale = 1f;
        if (currentDay >= campaignDayLimit)
        {
            PrepareRestaurantDay();
            LoadRestaurantScene();
        }
        else
        {
            StartNewDay();
        }

        return true;
    }

    private void EvaluateRestaurantDay()
    {
        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;

        if (money <= 0)
        {
            TriggerGameOver(GameOverReason.Bankruptcy);
            return;
        }

        // Endless Casual Dining deliberately no longer changes or checks approval.
        if (campaignCompleted)
        {
            StartEndlessRestaurantDay();
            return;
        }

        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;
        if (approval <= 0)
        {
            TriggerGameOver(GameOverReason.ApprovalCollapsed);
            return;
        }

        if (currentDay >= campaignDayLimit)
        {
            if (approval < campaignApprovalTarget)
            {
                TriggerGameOver(GameOverReason.EarthConqueredDay30);
                return;
            }

            campaignCompleted = true;
            StartEndlessRestaurantDay();
            return;
        }

        StartNewDay();
    }

    private void StartEndlessRestaurantDay()
    {
        Time.timeScale = 1f;
        PrepareRestaurantDay();
        restaurantSessionState = RestaurantSessionState.Endless;
        LoadRestaurantScene();
    }

    private void EnsureRestaurantDayPrepared()
    {
        if (restaurantSessionState != RestaurantSessionState.None)
            return;

        PrepareRestaurantDay();
    }

    private void PrepareRestaurantDay()
    {
        currentDay = Mathf.Clamp(currentDay, 1, campaignDayLimit);
        currentPhase = GamePhase.Restaurant;
        currentDayHalf = DayHalf.None;
        lobbyCompleted = false;
        kitchenCompleted = false;
        restaurantSessionState = campaignCompleted
            ? RestaurantSessionState.Endless
            : RestaurantSessionState.PreOpen;

        DailyRevenueTracker.Instance?.ResetForNewDay();
        DailyFinanceBridge.Instance?.ResetDay();
        AlienApprovalManager.Instance?.BeginNewDay();
        FinanceManager.Instance?.ResetDailyExpenses();
        EmployeeManager.Instance?.ResetDailyAssignments();
        ShiftScaler.Instance?.ApplyScaling(currentDay);

        int maxGroupsThisDay = ShiftScaler.Instance != null
            ? ShiftScaler.Instance.CurrentGroupCount
            : 5;

        if (!campaignCompleted)
            DailyObjectiveManager.Instance?.RollObjectivesForDay(currentDay, maxGroupsThisDay);
        else
            DailyObjectiveManager.Instance?.ResetForNewDay();

        EquipmentManager.Instance?.UnlockByDay(currentDay);
        EquipmentShopManager shop = FindFirstObjectByType<EquipmentShopManager>();
        shop?.InitializeShop();
        RecipeManager.Instance?.UnlockByDay(currentDay);

        CasualDiningPolishManager.EnsureInstance()?.PrepareDay(currentDay, campaignCompleted);

        NotifyDayChanged();
    }

    private void LoadRestaurantScene()
    {
        if (string.IsNullOrWhiteSpace(restaurantSceneName))
        {
            Debug.LogError("[GameFlowManager] Single Restaurant Flow has no restaurant scene assigned.");
            return;
        }

        SceneManager.LoadScene(restaurantSceneName);
    }

    private void ConfigureSingleRestaurantFlow(string sceneName)
    {
        useSingleRestaurantFlow = true;
        restaurantSceneName = sceneName;
        campaignDayLimit = Mathf.Max(1, campaignDayLimit);
        campaignApprovalTarget = Mathf.Clamp(campaignApprovalTarget, 0, 100);
    }

    private void CopySingleRestaurantConfiguration(GameFlowManager source)
    {
        ConfigureSingleRestaurantFlow(source.restaurantSceneName);
        campaignDayLimit = source.campaignDayLimit;
        campaignApprovalTarget = source.campaignApprovalTarget;
    }
}
