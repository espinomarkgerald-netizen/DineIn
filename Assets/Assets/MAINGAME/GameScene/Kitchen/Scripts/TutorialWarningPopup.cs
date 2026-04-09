using UnityEngine;
using TMPro;

public class TutorialWarningPopup : MonoBehaviour {
    public static TutorialWarningPopup Instance;

    [Header("UI References")]
    public GameObject popupPanel; // The actual box with text
    public GameObject warningDarkOverlay; // --- NEW: The dark screen behind it ---
    public TextMeshProUGUI warningText;

    void Awake() {
        Instance = this;

        // Hide both immediately when the game starts
        if (popupPanel != null) popupPanel.SetActive(false);
        if (warningDarkOverlay != null) warningDarkOverlay.SetActive(false);
    }

    // The Bouncer calls this!
    public void ShowWarning(string message) {
        if (warningText != null) warningText.text = message;

        // Turn ON the dark screen, then turn ON the popup box!
        if (warningDarkOverlay != null) warningDarkOverlay.SetActive(true);
        if (popupPanel != null) popupPanel.SetActive(true);
    }

    // The "OK" Button calls this!
    public void CloseWarning() {
        // Turn OFF the dark screen, and turn OFF the popup box!
        if (warningDarkOverlay != null) warningDarkOverlay.SetActive(false);
        if (popupPanel != null) popupPanel.SetActive(false);
    }
}