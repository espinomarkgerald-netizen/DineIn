using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Static UI (default)")]
    [SerializeField] private GameObject staticUI;

    [Header("Active UI Panels")]
    [SerializeField] private List<GameObject> activeUIs;

    [Header("Settings Panel")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject audioSettings;
    [SerializeField] GameObject videoSettings;

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

    void Start()
    {
        ShowStaticUI();
    }

    public void ShowStaticUI()
    {
        staticUI.SetActive(true);
        HideCurrentActiveUI();
    }

    public void ShowActiveUI(GameObject ui)
    {
        if (!activeUIs.Contains(ui))
        {
            Debug.LogWarning($"UIManager: {ui.name} is not registered in Active UIs.");
            return;
        }

        staticUI.SetActive(false);
        HideCurrentActiveUI();

        currentActiveUI = ui;
        currentActiveUI.SetActive(true);
    }

    public void CloseActiveUI()
    {
        HideCurrentActiveUI();
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
        if (settingsPanel == null) return;

        staticUI.SetActive(false);
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void CloseSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(false);
        staticUI.SetActive(true);
    }

    public void OpenAudioSettings()
    {
        HideSettingsSubPanels();
        settingsPanel.SetActive(false);
        audioSettings.SetActive(true);
    }

    public void SettingsBackButton()
    {
        HideSettingsSubPanels();
        settingsPanel.SetActive(true);
    }
    void HideSettingsSubPanels()
    {
        if (audioSettings != null) audioSettings.SetActive(false);
        if (videoSettings != null) videoSettings.SetActive(false);
    }
    public void SettingsToggle()
    {
        if (settingsPanel == null) return;

        staticUI.SetActive(false);
        HideSettingsSubPanels();
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}