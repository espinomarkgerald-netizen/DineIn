using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Static UI (default)")]
    [SerializeField] private GameObject staticUI;
    [SerializeField] private Button settingsButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject audioSettings;
    [SerializeField] private GameObject videoSettings;

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
        WireSettingsButton();
    }

    // Shows the static UI when settings panel is closed
    public void ShowStaticUI()
    {
        if (staticUI != null)
            staticUI.SetActive(true);
    }

    // Toggles the settings panel
    public void ToggleSettings()
    {
        if (settingsPanel == null)
            return;

        // Hide main UI while settings panel is active
        if (staticUI != null)
            staticUI.SetActive(false);

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    // Closes the settings panel and restores main UI
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        HideSettingsSubPanels();

        if (staticUI != null)
            staticUI.SetActive(true);
    }

    // Opens audio settings panel
    public void OpenAudioSettings()
    {
        HideSettingsSubPanels();

        if (audioSettings != null)
            audioSettings.SetActive(true);
    }

    // Opens video settings panel (if needed)
    public void OpenVideoSettings()
    {
        HideSettingsSubPanels();

        if (videoSettings != null)
            videoSettings.SetActive(true);
    }

    // Back button in subpanels returns to main settings panel
    public void SettingsBackButton()
    {
        HideSettingsSubPanels();

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    // Hides both audio and video subpanels
    private void HideSettingsSubPanels()
    {
        if (audioSettings != null)
            audioSettings.SetActive(false);

        if (videoSettings != null)
            videoSettings.SetActive(false);
    }

    // Wires the settings button to toggle the panel
    private void WireSettingsButton()
    {
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ToggleSettings);
        }
    }
}