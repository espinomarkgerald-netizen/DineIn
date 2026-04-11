using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shown at the end of the Kitchen Tutorial. Lets the player start the game or return to the main menu.
/// </summary>
public class TutorialCompletePopup : MonoBehaviour {
    private const string MainMenuSceneName = "MainMenu";
    private const string GameplaySceneName = "Office";

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake() {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private void Start() {
        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    /// <summary>Activates the completion popup panel.</summary>
    public void Show() {
        if (popupPanel != null) popupPanel.SetActive(true);
    }

    private void OnStartGame() {
        SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
    }

    private void OnMainMenu() {
        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
    }
}
