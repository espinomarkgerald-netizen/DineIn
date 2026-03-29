using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Static UI (default)")]
    [SerializeField] private GameObject staticUI;
    [SerializeField] private GameObject sceneChanger;

    [Header("Active UI Panels")]
    [SerializeField] private List<GameObject> activeUIs;
    [SerializeField] private GameObject employeeBoard;
    [SerializeField] private GameObject restockShop;

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

    private GameObject currentActiveUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ShowStaticUI();
        RefreshPhaseUI();
    }

    public void ShowStaticUI()
    {
        if (staticUI != null)
            staticUI.SetActive(true);

        HideCurrentActiveUI();
    }

     private void OnEnable()
    {
        RefreshPhaseUI();
    }

    public void RefreshPhaseUI()
    {
        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("GameFlowManager not found.");
            return;
        }
        Debug.Log("Phase detected: " + GameFlowManager.Instance.CurrentDayHalf);
        Debug.Log("Lobby Button: " + (lobbyButton != null));
        Debug.Log("Kitchen Button: " + (kitchenButton != null));

        Debug.Log("Current DayHalf: " + GameFlowManager.Instance.CurrentDayHalf);

        var phase = GameFlowManager.Instance.CurrentDayHalf;
        Debug.Log("Phase detected: " + phase);

        bool isMorning = phase == GameFlowManager.DayHalf.Morning;
        bool isAfternoon = phase == GameFlowManager.DayHalf.Afternoon;

        if (lobbyButton != null)
            lobbyButton.gameObject.SetActive(isMorning);

        if (kitchenButton != null)
            kitchenButton.gameObject.SetActive(isAfternoon);
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

    public void OpenHRUI()
    {
        if (hrManager == null)
        {
            Debug.LogError("UIManager: HRManager not assigned!");
            return;
        }

        // Show BOTH UIs regardless of phase
        if (kitchenUI != null)
            kitchenUI.SetActive(true);

        if (lobbyUI != null)
            lobbyUI.SetActive(true);

        // Populate ALL rows
        hrManager.PopulateRows(hrManager.lobbyRows);
        hrManager.PopulateRows(hrManager.kitchenRows);

        // Pick a parent container as active UI (your choice, here lobbyUI)
        currentActiveUI = lobbyUI;

        if (staticUI != null)
            staticUI.SetActive(false);
    }
}