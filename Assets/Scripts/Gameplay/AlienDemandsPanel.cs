using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pre-shift "Alien Demands" panel.
///
/// Shows yesterday's objective scorecard (grade + pass/fail per objective) at the top,
/// then the three objectives rolled by DailyObjectiveManager for today at the bottom.
/// Hands off to OfficeStartButtons.StartLobby() when the player confirms.
///
/// Setup:
///   - Attach to the AlienDemandsPanel GameObject inside CanvasMainHUD.
///   - Wire all serialized fields in the Inspector.
///   - The LobbyButton in SceneChanger should call ShowPanel() on this component
///     instead of calling OfficeStartButtons.StartLobby() directly.
///   - The "Begin Shift" button inside this panel calls OnConfirmClicked().
///   - yesterdaySection: assign a parent GameObject that holds all yesterday labels.
///     It will be deactivated on Day 1 and replaced by the firstShiftLabel.
/// </summary>
public class AlienDemandsPanel : MonoBehaviour
{
    [Header("Day Header")]
    [SerializeField] private TextMeshProUGUI dayHeaderLabel;

    [Header("Today's Objectives")]
    [SerializeField] private TextMeshProUGUI mandatoryLabel;
    [SerializeField] private TextMeshProUGUI secondaryLabel;
    [SerializeField] private TextMeshProUGUI bonusLabel;

    [Header("Yesterday's Results")]
    [Tooltip("Parent object that holds all four yesterday labels. Hidden on Day 1.")]
    [SerializeField] private GameObject yesterdaySection;
    [SerializeField] private TextMeshProUGUI yesterdayHeaderLabel;
    [SerializeField] private TextMeshProUGUI yesterdayMandatoryLabel;
    [SerializeField] private TextMeshProUGUI yesterdayServiceLabel;
    [SerializeField] private TextMeshProUGUI yesterdayBonusLabel;

    [Header("First-Shift Placeholder (shown when no prior data)")]
    [SerializeField] private GameObject firstShiftSection;
    [SerializeField] private TextMeshProUGUI firstShiftLabel;

    [Header("Confirm Button")]
    [SerializeField] private Button confirmButton;

    [Header("Lobby Starter")]
    [SerializeField] private OfficeStartButtons officeStartButtons;

    [Header("Colors")]
    [SerializeField] private Color passColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color failColor = new Color(0.9f, 0.2f, 0.2f);

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
    /// Opens the panel. Rolls today's objectives, populates yesterday's scorecard,
    /// then makes the panel visible. Wire directly to LobbyButton.onClick.
    /// </summary>
    public void ShowPanel()
    {
        int day = GameFlowManager.Instance?.CurrentDay ?? 1;
        int maxGroupsThisShift = ShiftScaler.Instance?.CurrentGroupCount ?? 5;

        DailyObjectiveManager.Instance?.RollObjectivesForDay(day, maxGroupsThisShift);

        var mgr = DailyObjectiveManager.Instance;

        dayHeaderLabel.text = $"Objectives — Day {day}";
        mandatoryLabel.text = mgr?.ActiveMandatory?.GetDescription(day) ?? "MANDATORY —";
        secondaryLabel.text = mgr?.ActiveSecondary?.GetDescription(day) ?? "SERVICE —";
        bonusLabel.text = mgr?.ActiveBonus?.GetDescription(day) ?? "BONUS —";

        bool hasPrior = mgr?.HasPreviousDayResult ?? false;
        yesterdaySection.SetActive(hasPrior);
        firstShiftSection.SetActive(!hasPrior);
        
        if (!hasPrior && firstShiftLabel != null)
            firstShiftLabel.text = "First Shift — No prior data.";

        gameObject.SetActive(true);
    }

    private void SetObjectiveResultLabel(TextMeshProUGUI label, string prefix,
        ObjectiveDefinition obj, bool passed)
    {
        if (label == null) return;

        string status = passed ? "[PASS]" : "[FAIL]";
        string desc   = obj != null ? obj.descriptionTemplate : "—";
        label.text  = $"{status} {prefix}: {desc}";
        label.color = passed ? passColor : failColor;
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
