using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pre-shift "Alien Demands" panel.
///
/// Displays the three objectives rolled by DailyObjectiveManager for the current day,
/// then hands off to OfficeStartButtons.StartLobby() when the player confirms.
///
/// Setup:
///   - Attach to the AlienDemandsPanel GameObject inside CanvasMainHUD.
///   - Wire all serialized fields in the Inspector.
///   - The LobbyButton in SceneChanger should call ShowPanel() on this component
///     instead of calling OfficeStartButtons.StartLobby() directly.
///   - The "Begin Shift" button inside this panel calls OnConfirmClicked().
/// </summary>
public class AlienDemandsPanel : MonoBehaviour
{
    [Header("Objective Labels")]
    [SerializeField] private TextMeshProUGUI mandatoryLabel;
    [SerializeField] private TextMeshProUGUI secondaryLabel;
    [SerializeField] private TextMeshProUGUI bonusLabel;

    [Header("Day Header")]
    [SerializeField] private TextMeshProUGUI dayHeaderLabel;

    [Header("Confirm Button")]
    [SerializeField] private Button confirmButton;

    [Header("Lobby Starter")]
    [SerializeField] private OfficeStartButtons officeStartButtons;

    private static readonly string MandatoryPrefix = "MANDATORY  ";
    private static readonly string ServicePrefix   = "SERVICE    ";
    private static readonly string BonusPrefix     = "BONUS      ";

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Opens the panel and populates it with today's objectives.
    /// Call this from the LobbyButton's onClick instead of OfficeStartButtons.StartLobby().
    /// </summary>
    public void ShowPanel()
    {
        var mgr = DailyObjectiveManager.Instance;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;

        if (dayHeaderLabel != null)
            dayHeaderLabel.text = $"ALIEN DEMANDS — Day {day}";

        if (mandatoryLabel != null)
            mandatoryLabel.text = mgr != null && mgr.ActiveMandatory != null
                ? MandatoryPrefix + mgr.ActiveMandatory.GetDescription(day)
                : MandatoryPrefix + "—";

        if (secondaryLabel != null)
            secondaryLabel.text = mgr != null && mgr.ActiveSecondary != null
                ? ServicePrefix + mgr.ActiveSecondary.GetDescription(day)
                : ServicePrefix + "—";

        if (bonusLabel != null)
            bonusLabel.text = mgr != null && mgr.ActiveBonus != null
                ? BonusPrefix + mgr.ActiveBonus.GetDescription(day)
                : BonusPrefix + "—";

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the panel and launches the lobby shift.
    /// Triggered by the "Begin Shift" confirm button.
    /// </summary>
    public void OnConfirmClicked()
    {
        gameObject.SetActive(false);

        if (officeStartButtons != null)
            officeStartButtons.StartLobby();
        else
            Debug.LogWarning("[AlienDemandsPanel] OfficeStartButtons reference not assigned.");
    }
}
