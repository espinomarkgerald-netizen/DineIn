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
    /// Opens the panel. Populates yesterday's scorecard and today's objectives.
    /// Call this from the LobbyButton's onClick instead of OfficeStartButtons.StartLobby().
    /// </summary>
    public void ShowPanel()
    {
        var mgr = DailyObjectiveManager.Instance;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;

        if (dayHeaderLabel != null)
            dayHeaderLabel.text = $"ALIEN DEMANDS — Day {day}";

        // ── Today's Objectives ──────────────────────────────────────────
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

        // ── Yesterday's Scorecard ───────────────────────────────────────
        bool hasPrior = mgr != null && mgr.HasPreviousDayResult;

        if (yesterdaySection != null)
            yesterdaySection.SetActive(hasPrior);

        if (firstShiftSection != null)
            firstShiftSection.SetActive(!hasPrior);

        if (hasPrior && mgr != null)
        {
            if (yesterdayHeaderLabel != null)
                yesterdayHeaderLabel.text = $"Day {mgr.LastResultDay} Results — Grade: {mgr.LastGrade}";

            SetObjectiveResultLabel(yesterdayMandatoryLabel, "MANDATORY",
                mgr.ActiveMandatory, mgr.LastMandatoryPassed);

            SetObjectiveResultLabel(yesterdayServiceLabel, "SERVICE",
                mgr.ActiveSecondary, mgr.LastSecondaryPassed);

            SetObjectiveResultLabel(yesterdayBonusLabel, "BONUS",
                mgr.ActiveBonus, mgr.LastBonusPassed);
        }
        else if (!hasPrior && firstShiftLabel != null)
        {
            firstShiftLabel.text = "First Shift — No prior data.";
        }

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
