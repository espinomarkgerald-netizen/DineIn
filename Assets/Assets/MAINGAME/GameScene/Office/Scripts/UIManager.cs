using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Static UI (default)")]
    [SerializeField] private GameObject staticUI;
    [SerializeField] private GameObject sceneChanger;
    [SerializeField] private TMP_Text currentDayText;
    [SerializeField] private string currentDayTextObjectName = "CurrentDayText";
    [SerializeField] private Button settingsButton;

    [Header("Active UI Panels")]
    [SerializeField] private List<GameObject> activeUIs;
    [SerializeField] private GameObject employeeBoard;
    [SerializeField] private GameObject restockShop;
    [SerializeField] private GameObject equipmentShop;
    [SerializeField] private GameObject recipeBook;
    [SerializeField] private GameObject receiptPanel;
    [SerializeField] private GameObject objectivesPanel;
    [SerializeField] private GameObject almanacPanel;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject audioSettings;
    [SerializeField] private GameObject videoSettings;

    [Header("Department Buttons")]
    [SerializeField] private GameObject lobbyButton;
    [SerializeField] private GameObject kitchenButton;

    [Header("HR UI Panels")]
    [SerializeField] private GameObject kitchenUI;
    [SerializeField] private GameObject lobbyUI;

    [Header("HR Manager")]
    [SerializeField] private HRManager hrManager;

    [Header("Scene transition")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private GameObject currentActiveUI;
    private int lastShownDay = -1;
    private GameFlowManager.DayHalf lastShownDayHalf = (GameFlowManager.DayHalf)(-999);

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshPhaseUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ShowStaticUI();
        TryFindCurrentDayText();
        RefreshPhaseUI();
        WireSettingsButton();
    }

    private void Update()
    {
        if (GameFlowManager.Instance == null)
            return;

        if (currentDayText == null)
            TryFindCurrentDayText();

        int currentDay = GameFlowManager.Instance.CurrentDay;
        var currentHalf = GameFlowManager.Instance.CurrentDayHalf;

        if (currentDay != lastShownDay || currentHalf != lastShownDayHalf)
            RefreshPhaseUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RefreshAfterSceneLoad());
    }

    private IEnumerator RefreshAfterSceneLoad()
    {
        yield return null;
        TryFindCurrentDayText();
        WireSettingsButton();
        RefreshPhaseUI();
    }

    private void TryFindCurrentDayText()
    {
        if (currentDayText != null)
            return;

        GameObject dayObj = GameObject.Find(currentDayTextObjectName);
        if (dayObj != null)
            currentDayText = dayObj.GetComponent<TMP_Text>();

        if (currentDayText == null)
        {
            TMP_Text[] allTexts = FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null && allTexts[i].gameObject.name == currentDayTextObjectName)
                {
                    currentDayText = allTexts[i];
                    break;
                }
            }
        }
    }

    public void ShowStaticUI()
    {
        if (staticUI != null)
            staticUI.SetActive(true);

        HideCurrentActiveUI();
    }

    public void RefreshPhaseUI()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("GameFlowManager not found.");
            return;
        }

        TryFindCurrentDayText();

        if (currentDayText != null)
            currentDayText.text = $"Day {GameFlowManager.Instance.CurrentDay}";

        var phase = GameFlowManager.Instance.CurrentDayHalf;

        lastShownDay = GameFlowManager.Instance.CurrentDay;
        lastShownDayHalf = phase;

        Debug.Log("Current DayHalf: " + phase);

        if (lobbyButton == null || kitchenButton == null)
            return;

        switch (phase)
        {
            case GameFlowManager.DayHalf.Morning:
                lobbyButton.SetActive(true);
                kitchenButton.SetActive(false);
                break;

            case GameFlowManager.DayHalf.Afternoon:
                lobbyButton.SetActive(false);
                kitchenButton.SetActive(true);
                break;

            default:
                lobbyButton.SetActive(false);
                kitchenButton.SetActive(false);
                break;
        }
    }

    public void ForceRefreshDayText()
    {
        currentDayText = null;
        TryFindCurrentDayText();
        RefreshPhaseUI();
    }

    public void ShowActiveUI(GameObject ui)
    {
        if (ui == null)
            return;

        if (!activeUIs.Contains(ui))
        {
            Debug.LogWarning($"UIManager: {ui.name} is not registered in Active UIs.");
            return;
        }

        if (staticUI != null)
            staticUI.SetActive(false);

        HideCurrentActiveUI();

        currentActiveUI = ui;
        currentActiveUI.SetActive(true);
    }

    public void CloseActiveUI()
    {
        HideCurrentActiveUI();

        if (staticUI != null)
            staticUI.SetActive(true);
    }

    private void HideCurrentActiveUI()
    {
        if (currentActiveUI != null)
        {
            currentActiveUI.SetActive(false);
            currentActiveUI = null;
        }
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null)
            return;

        if (staticUI != null)
            staticUI.SetActive(false);

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (restockShop != null)
            restockShop.SetActive(false);

        if (employeeBoard != null)
            employeeBoard.SetActive(false);

        if (kitchenUI != null)
            kitchenUI.SetActive(false);

        if (lobbyUI != null)
            lobbyUI.SetActive(false);

        if (equipmentShop != null)
            equipmentShop.SetActive(false);

        if (recipeBook != null)
            recipeBook.SetActive(false);

        if (receiptPanel != null)
            receiptPanel.SetActive(false);

        if (objectivesPanel != null)
            objectivesPanel.SetActive(false);

        if (almanacPanel != null)
            almanacPanel.SetActive(false);

        currentActiveUI = null;

        if (staticUI != null)
            staticUI.SetActive(true);
    }

    public void OpenAudioSettings()
    {
        HideSettingsSubPanels();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (audioSettings != null)
            audioSettings.SetActive(true);
    }

    public void OpenAlmanacPanel()
    {
        HideSettingsSubPanels();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (almanacPanel != null)
            almanacPanel.SetActive(true);
    }

    public void SettingsBackButton()
    {
        HideSettingsSubPanels();

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void HideSettingsSubPanels()
    {
        if (audioSettings != null)
            audioSettings.SetActive(false);

        if (videoSettings != null)
            videoSettings.SetActive(false);

        if (almanacPanel != null)
            almanacPanel.SetActive(false);
    }

    public void SettingsToggle()
    {
        if (settingsPanel == null)
            return;

        if (staticUI != null)
            staticUI.SetActive(false);

        HideSettingsSubPanels();
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OpenEmployeeBoard()
    {
        if (employeeBoard != null)
            employeeBoard.SetActive(true);

        if (staticUI != null)
            staticUI.SetActive(false);

        currentActiveUI = employeeBoard;
    }

    public void OpenRestockShop()
    {
        if (restockShop != null)
            restockShop.SetActive(true);

        if (staticUI != null)
            staticUI.SetActive(false);

        currentActiveUI = restockShop;
    }

    public void OpenEquipmentShop()
    {
        if (equipmentShop != null)
            equipmentShop.SetActive(true);

        if (staticUI != null)
            staticUI.SetActive(false);

        currentActiveUI = equipmentShop;
    }

    public void OpenRecipeBook()
    {
        if (recipeBook != null)
            recipeBook.SetActive(true);

        if (staticUI != null)
            staticUI.SetActive(false);

        currentActiveUI = recipeBook;
    }

    public void OpenHRUI()
    {
        if (hrManager == null)
        {
            Debug.LogError("UIManager: HRManager not assigned!");
            return;
        }

        if (kitchenUI != null)
            kitchenUI.SetActive(true);

        if (lobbyUI != null)
            lobbyUI.SetActive(true);

        hrManager.PopulateRows(hrManager.lobbyRows);
        hrManager.PopulateRows(hrManager.kitchenRows);

        currentActiveUI = lobbyUI;

        if (staticUI != null)
            staticUI.SetActive(false);
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private IEnumerator DelayedSceneLoad(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadMainMenu();
    }

    private void WireSettingsButton()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(LoadMainMenu);
        }
    }
}