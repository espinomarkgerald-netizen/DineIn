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

    [Header("Today Finance")]
    [SerializeField] private int employeeCostToday;
    [SerializeField] private int marketingCostToday;
    [SerializeField] private int billsCostToday;
    [SerializeField] private int ingredientCostToday;

    public int CurrentDay => currentDay;
    public GamePhase CurrentPhase => currentPhase;
    public DayHalf CurrentDayHalf => currentDayHalf;
    public bool LobbyCompleted => lobbyCompleted;
    public bool KitchenCompleted => kitchenCompleted;

    public bool IsMorning => currentDayHalf == DayHalf.Morning;
    public bool IsAfternoon => currentDayHalf == DayHalf.Afternoon;

    public int EmployeeCostToday => employeeCostToday;
    public int MarketingCostToday => marketingCostToday;
    public int BillsCostToday => billsCostToday;
    public int IngredientCostToday => ingredientCostToday;

    public int TotalRequiredToday =>
        Mathf.Max(0, employeeCostToday) +
        Mathf.Max(0, marketingCostToday) +
        Mathf.Max(0, billsCostToday) +
        Mathf.Max(0, ingredientCostToday);

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

    public void SetTodayFinance(int employeeCost, int marketingCost, int billsCost, int ingredientCost)
    {
        employeeCostToday = Mathf.Max(0, employeeCost);
        marketingCostToday = Mathf.Max(0, marketingCost);
        billsCostToday = Mathf.Max(0, billsCost);
        ingredientCostToday = Mathf.Max(0, ingredientCost);
    }

    public void ResetTodayFinance()
    {
        employeeCostToday = 0;
        marketingCostToday = 0;
        billsCostToday = 0;
        ingredientCostToday = 0;
    }

    public void StartNewDay()
    {
        lobbyCompleted = false;
        kitchenCompleted = false;
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Management;
        SceneManager.LoadScene(managementSceneName);
    }

    public void NextDay()
    {
        currentDay++;
        RecipeManager.Instance.UnlockByDay(currentDay);
        // Equipment UI can refresh using EquipmentManager.Instance.GetPurchasable(currentDay)
    }

    public void StartNewDay(int employeeCost, int marketingCost, int billsCost, int ingredientCost)
    {
        SetTodayFinance(employeeCost, marketingCost, billsCost, ingredientCost);
        StartNewDay();
    }

    public void StartLobbyShift()
    {
        currentDayHalf = DayHalf.Morning;
        currentPhase = GamePhase.Lobby;
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

    public void AdvanceDay()
    {
        currentDay++;
        lobbyCompleted = false;
        kitchenCompleted = false;
        currentPhase = GamePhase.Management;
        currentDayHalf = DayHalf.None;
    }

    public void ResetRun()
    {
        currentDay = 1;
        currentPhase = GamePhase.Management;
        currentDayHalf = DayHalf.None;
        lobbyCompleted = false;
        kitchenCompleted = false;

        ResetTodayFinance();

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.ResetToStartingMoney();

        Debug.Log("[GameFlow] Run reset to Day 1 (Bankruptcy)");
    }

    public void StartDay()
    {
        StartNewDay();
    }
}