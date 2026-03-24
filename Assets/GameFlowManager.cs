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

    public int CurrentDay => currentDay;
    public GamePhase CurrentPhase => currentPhase;
    public bool LobbyCompleted => lobbyCompleted;

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

    public void StartDay()
    {
        lobbyCompleted = false;
        currentPhase = GamePhase.Lobby;
        SceneManager.LoadScene(lobbySceneName);
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
}