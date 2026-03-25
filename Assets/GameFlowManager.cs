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

    [Header("Scene Names")]
    [SerializeField] private string managementSceneName = "Office";
    [SerializeField] private string lobbySceneName = "Lobby1";

    [Header("Session")]
    [SerializeField] private int currentDay = 1;
    [SerializeField] private GamePhase currentPhase = GamePhase.None;
    [SerializeField] private bool lobbyCompleted;

    [Header("Today Finance")]
    [SerializeField] private int employeeCostToday;
    [SerializeField] private int marketingCostToday;
    [SerializeField] private int billsCostToday;
    [SerializeField] private int ingredientCostToday;

    public int CurrentDay => currentDay;
    public GamePhase CurrentPhase => currentPhase;
    public bool LobbyCompleted => lobbyCompleted;

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

    public void StartDay()
    {
        lobbyCompleted = false;
        currentPhase = GamePhase.Lobby;
        SceneManager.LoadScene(lobbySceneName);
    }

    public void StartDay(int employeeCost, int marketingCost, int billsCost, int ingredientCost)
    {
        SetTodayFinance(employeeCost, marketingCost, billsCost, ingredientCost);
        StartDay();
    }

    public void ReturnToManagementFromLobby()
    {
        lobbyCompleted = true;
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
        currentPhase = GamePhase.Lobby;
        SceneManager.LoadScene(lobbySceneName);
    }

    public void AdvanceDay()
    {
        currentDay++;
        lobbyCompleted = false;
        currentPhase = GamePhase.Management;
    }

    public void ResetRun()
    {
        currentDay = 1;
        currentPhase = GamePhase.Management;
        lobbyCompleted = false;

        ResetTodayFinance();

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.ResetToStartingMoney();

        Debug.Log("[GameFlow] Run reset to Day 1 (Bankruptcy)");
    }
}