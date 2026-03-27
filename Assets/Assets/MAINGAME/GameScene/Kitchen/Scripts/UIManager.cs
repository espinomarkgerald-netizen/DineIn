using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Static UI (default)")]
    [SerializeField] private GameObject staticUI;
    [Header("Ticket UI References")]
    public GameObject ticketPrefab;
    public Transform ticketContainer;

    // --- NEW CLOCK VARIABLE ---
    [Header("Shift Clock UI")]
    public TextMeshProUGUI shiftTimerText;
    private List<GameObject> spawnedTickets = new List<GameObject>();

    [Header("Active UI Panels")]
    [SerializeField] private List<GameObject> activeUIs;
    [SerializeField] private GameObject employeeBoard;
    [SerializeField] private GameObject restockShop;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject audioSettings;
    [SerializeField] private GameObject videoSettings;

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
    }

    public void ShowStaticUI()
    {
        if (staticUI != null)
            staticUI.SetActive(true);

        HideCurrentActiveUI();
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

        hrManager.SyncPhaseFromGameFlow();

        if (kitchenUI != null)
            kitchenUI.SetActive(false);

        if (lobbyUI != null)
            lobbyUI.SetActive(false);

        if (hrManager.CurrentPhase == HRManager.DayPhase.Morning)
        {
            if (lobbyUI != null)
                lobbyUI.SetActive(true);

            hrManager.PopulateRows(hrManager.lobbyRows);
            currentActiveUI = lobbyUI;
        }
        else
        {
            if (kitchenUI != null)
                kitchenUI.SetActive(true);

            hrManager.PopulateRows(hrManager.kitchenRows);
            currentActiveUI = kitchenUI;
        }

        if (staticUI != null)
            staticUI.SetActive(false);
    }

    void Update() {
        if (OrderManager.Instance == null)
        return;

        // --- THE MASTER CLOCK LOGIC ---
        if (shiftTimerText != null) {
            float timeRemaining = OrderManager.Instance.currentShiftTime;

            if (timeRemaining > 0) {
                // Convert raw seconds into standard Minutes:Seconds
                int minutes = Mathf.FloorToInt(timeRemaining / 60F);
                int seconds = Mathf.FloorToInt(timeRemaining - minutes * 60);

                // Format it nicely so "3 minutes and 5 seconds" looks like "03:05"
                shiftTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

                // Turn the clock RED if there are 30 seconds or less left!
                if (timeRemaining <= 30f) {
                    shiftTimerText.color = Color.red;
                } else {
                    shiftTimerText.color = Color.white; // Default color
                }
            } else {
                shiftTimerText.text = "CLOSED";
                shiftTimerText.color = Color.red;
            }
        }
        // ------------------------------

        // --- EXISTING TICKET LOGIC (Untouched so Combos still work!) ---
        if (spawnedTickets.Count != OrderManager.Instance.activeOrders.Count) {
            RebuildTicketUI();
        }

        for (int i = 0; i < spawnedTickets.Count; i++) {

            var orderData = OrderManager.Instance.activeOrders[i];
            TextMeshProUGUI[] texts = spawnedTickets[i].GetComponentsInChildren<TextMeshProUGUI>();

            foreach (var textItem in texts) {
                if (textItem.gameObject.name == "RecipeNameText") {

                    string missingListText = "";
                    foreach (var item in orderData.missingItems) {
                        missingListText += "\n+ " + item.ToString();
                    }

                    textItem.text = orderData.ticketName + "\n<size=60%>" + missingListText + "</size>";
                } else if (textItem.gameObject.name == "TimerText") {
                    textItem.text = Mathf.CeilToInt(orderData.timeLeft).ToString() + "s";
                }
            }
        }
    }

    private void RebuildTicketUI() {
        foreach (GameObject ticket in spawnedTickets) {
            Destroy(ticket);
        }
        spawnedTickets.Clear();

        for (int i = 0; i < OrderManager.Instance.activeOrders.Count; i++) {
            GameObject newTicket = Instantiate(ticketPrefab, ticketContainer);
            spawnedTickets.Add(newTicket);
        }
    }
}