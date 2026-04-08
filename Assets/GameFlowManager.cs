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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Called by Unity after each scene load. Applies shift scaling once the lobby
    /// scene is live so that GroupSpawner is guaranteed to exist.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == lobbySceneName && currentPhase == GamePhase.Lobby)
            ShiftScaler.Instance?.ApplyScaling(currentDay);
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
        DailyObjectiveManager.Instance?.RollObjectivesForDay(currentDay);

        EquipmentManager.Instance?.UnlockByDay(currentDay);
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

        AlienApprovalManager.Instance?.ResetApproval();

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

    /// <summary>
    /// Evaluates end-of-day win/loss conditions after all finances have been settled.
    /// Call this after EndOfDayFinance() — it will either advance to the next day
    /// or trigger the appropriate game over screen.
    /// </summary>
    public void EvaluateEndOfDay()
    {
        int money    = MoneyManager.Instance != null         ? MoneyManager.Instance.Money           : 0;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        // Evaluate objectives and apply grade bonus/penalty to approval before win/loss check
        DailyObjectiveManager.Instance?.EvaluateAndApply();

        // Re-read approval after the grade delta has been applied
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

    /// <summary>
    /// Shows the appropriate game over / win screen and pauses the game.
    /// Uses GameOverScreen.Instance so the screen is reachable from any scene,
    /// including the Lobby shift before the Kitchen scene is loaded.
    /// Falls back to the serialized reference if Instance is unavailable.
    /// </summary>
    public void TriggerGameOver(GameOverReason reason)
    {
        int money    = MoneyManager.Instance != null         ? MoneyManager.Instance.Money           : 0;
        int approval = AlienApprovalManager.Instance != null ? AlienApprovalManager.Instance.Approval : 0;

        GameOverScreen screen = GameOverScreen.Instance != null ? GameOverScreen.Instance : gameOverScreen;

        if (screen != null)
            screen.Show(reason, approval, money, currentDay);
        else
            Debug.LogWarning("[GameFlowManager] No GameOverScreen found. " +
                             "Assign it in the Inspector or ensure it is present in the Kitchen scene.");

        Time.timeScale = 0f;
    }

    [Header("Game Over")]
    [SerializeField] private GameOverScreen gameOverScreen;
}