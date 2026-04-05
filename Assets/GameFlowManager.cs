using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private string kitchenSceneName = "Kitchen1";

    [Header("Session")]
    [SerializeField] private int currentDay = 1;
    [SerializeField] private GamePhase currentPhase = GamePhase.None;
    [SerializeField] private DayHalf currentDayHalf = DayHalf.Morning;
    [SerializeField] private bool lobbyCompleted;
    [SerializeField] private bool kitchenCompleted;

    public int CurrentDay => currentDay;
    public GamePhase CurrentPhase => currentPhase;
    public DayHalf CurrentDayHalf => currentDayHalf;
    public bool LobbyCompleted => lobbyCompleted;
    public bool KitchenCompleted => kitchenCompleted;

    public bool IsMorning => currentDayHalf == DayHalf.Morning;
    public bool IsAfternoon => currentDayHalf == DayHalf.Afternoon;

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

        EquipmentManager.Instance.UnlockByDay(currentDay);
        EquipmentShopManager shop = FindObjectOfType<EquipmentShopManager>();
        shop?.InitializeShop();

        // RecipeManager lives in the Office scene and may not exist yet during
        // this transition — UnlockByDay will also run in RecipeManager.Start
        // once the Office scene loads, so this call is safe to skip if null.
        RecipeManager.Instance?.UnlockByDay(currentDay);

        SceneManager.LoadScene(managementSceneName);
    }

    public void StartLobbyShift()
    {
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Lobby;
        EmployeeManager.Instance?.LockAllSlots();
        SceneManager.LoadScene(lobbySceneName);
    }

    public void ReturnToManagementFromLobby()
    {
        lobbyCompleted = true;
        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Management;
        SceneManager.LoadScene(managementSceneName);
    }

    public void StartKitchenShift()
    {
        currentDayHalf = DayHalf.Afternoon;
        currentPhase = GamePhase.Kitchen;
        SceneManager.LoadScene(kitchenSceneName);
    }

    public void ReturnToManagementFromKitchen()
    {
        kitchenCompleted = true;
        currentPhase = GamePhase.Management;
        SceneManager.LoadScene(managementSceneName);
    }

    public void LoadManagementScene()
    {
        currentPhase = GamePhase.Management;
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
        currentDayHalf = DayHalf.None;
        lobbyCompleted = false;
        kitchenCompleted = false;

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.ResetToStartingMoney();

        Debug.Log("[GameFlow] Run reset to Day 1 (Bankruptcy)");
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
}