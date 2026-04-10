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
        Kitchen
    }

    public enum DayHalf
    {
        None,
        Morning,
        Afternoon
    }

    [Header("Scene Names")]
    [SerializeField] private string managementSceneName = "Office";
    [SerializeField] private string lobbySceneName = "Lobby1";
    [SerializeField] private string kitchenSceneName = "Kitchen";

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

    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
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
        NotifyDayChanged();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == lobbySceneName && currentPhase == GamePhase.Lobby)
            ShiftScaler.Instance?.ApplyScaling(currentDay);

        RefreshDayText();
        NotifyDayChanged();
    }

    public void StartNewDay()
    {
        currentDay++;
        lobbyCompleted = false;
        kitchenCompleted = false;
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Management;

        DailyRevenueTracker.Instance?.ResetForNewDay();
        DailyFinanceBridge.Instance?.ResetDay();
        FinanceManager.Instance?.ResetDailyExpenses();
        EmployeeManager.Instance?.ResetDailyAssignments();

        int maxGroupsThisShift = ShiftScaler.Instance != null
            ? ShiftScaler.Instance.CurrentGroupCount
            : 5;

        DailyObjectiveManager.Instance?.RollObjectivesForDay(currentDay, maxGroupsThisShift);

        EquipmentManager.Instance?.UnlockByDay(currentDay);
        EquipmentShopManager shop = FindObjectOfType<EquipmentShopManager>();
        shop?.InitializeShop();

        RecipeManager.Instance?.UnlockByDay(currentDay);

        NotifyDayChanged();
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartLobbyShift()
    {
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Lobby;
        EmployeeManager.Instance?.LockAllSlots();
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(lobbySceneName);
    }

    public void ReturnToManagementFromLobby()
    {
        lobbyCompleted = true;
        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Management;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartKitchenShift()
    {
        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Kitchen;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(kitchenSceneName);
    }

    public void ReturnToManagementFromKitchen()
    {
        kitchenCompleted = true;
        currentPhase = GamePhase.Management;
        GameSaveManager.Instance?.RequestSave();
        SceneManager.LoadScene(managementSceneName);
    }

    public void LoadManagementScene()
    {
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
        return !lobbyCompleted;
    }

    public bool CanStartKitchen()
    {
        return lobbyCompleted && !kitchenCompleted;
    }

    public bool IsDayFullyCompleted()
    {
        return lobbyCompleted && kitchenCompleted;
    }

    public void ResetRun()
    {
        currentDay = 1;
        currentPhase = GamePhase.Management;
        currentDayHalf = DayHalf.Morning;
        lobbyCompleted = false;
        kitchenCompleted = false;

        MoneyManager.Instance?.ResetToStartingMoney();
        AlienApprovalManager.Instance?.ResetApproval();
        DailyObjectiveManager.Instance?.ResetForNewRun();

        FinanceManager.Instance?.ResetDailyExpenses();
        DailyRevenueTracker.Instance?.ResetForNewDay();
        DailyFinanceBridge.Instance?.ResetDay();

        InventoryManager.Instance?.ResetStock();
        EmployeeManager.Instance?.ClearAllEmployees();
        EquipmentManager.Instance?.ResetPurchases();
        UnlockManager.Instance?.ResetAll();

        EquipmentManager.Instance?.UnlockByDay(currentDay);

        NotifyDayChanged();
        GameSaveManager.Instance?.RequestSave();

        Debug.Log("[GameFlow] Run fully reset to Day 1.");
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartDay()
    {
        StartLobbyShift();
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

        if (screen != null)
            screen.Show(reason, approval, money, currentDay);
        else
            Debug.LogWarning("[GameFlowManager] No GameOverScreen found. Assign it in the Inspector or ensure it is present in the Kitchen scene.");

        Time.timeScale = 0f;
    }

    public bool TrySetCurrentDayDebug(int day)
    {
        if (day < 1 || day > 30)
            return false;

        currentDay = day;

        EquipmentManager.Instance?.UnlockByDay(currentDay);
        RecipeManager.Instance?.UnlockByDay(currentDay);

        EquipmentShopManager shop = FindObjectOfType<EquipmentShopManager>();
        shop?.InitializeShop();

        if (currentPhase == GamePhase.Lobby)
            ShiftScaler.Instance?.ApplyScaling(currentDay);

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
}