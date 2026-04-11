using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Tutorial button GameObject in MainMenu alongside SceneButtonBinder.
/// On every enable, re-registers the button so that clicking it first clears any
/// saved tutorial progress (day + auto-start keys), then triggers the normal
/// SceneManagerUI single-load into LobbyTutorial.
///
/// This runs after SceneButtonBinder.OnEnable because Unity executes OnEnable on
/// components in component order on the same GameObject. To guarantee ordering,
/// this component must be placed BELOW SceneButtonBinder in the Inspector.
/// Alternatively, it is safe even if it runs first — it calls RemoveAllListeners
/// and re-adds a fresh combined callback.
/// </summary>
public class TutorialResetOnLaunch : MonoBehaviour
{
    private const string SavedDayKey  = "DineIn_Tutorial_CurrentDay";
    private const string AutoStartKey = "DineIn_Tutorial_AutoStart";

    [SerializeField] private Button tutorialButton;
    [SerializeField] private string tutorialSceneName = "LobbyTutorial";

    private void Start()
    {
        Bind();
    }

    private void OnEnable()
    {
        Bind();
    }

    /// <summary>Wipes saved tutorial progress and loads LobbyTutorial fresh.</summary>
    private void LaunchFreshTutorial()
    {
        PlayerPrefs.DeleteKey(SavedDayKey);
        PlayerPrefs.DeleteKey(AutoStartKey);
        PlayerPrefs.Save();

        Debug.Log("[TutorialResetOnLaunch] Tutorial progress cleared. Loading fresh Day 1.");

        if (SceneManagerUI.Instance == null)
        {
            Debug.LogError("[TutorialResetOnLaunch] SceneManagerUI not found. Start from Bootstrap scene.");
            return;
        }

        SceneManagerUI.Instance.LoadSingle(tutorialSceneName);
    }

    private void Bind()
    {
        if (tutorialButton == null) return;

        // Override whatever SceneButtonBinder registered — we own this button.
        tutorialButton.onClick.RemoveAllListeners();
        tutorialButton.onClick.AddListener(LaunchFreshTutorial);
    }
}
