using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the appropriate narrative ending screen based on the GameOverReason.
/// Singleton — persists across all scene loads.
/// IMPORTANT: This script's GameObject must be a scene root (no parent). If it is nested
/// inside another Canvas or GameObject, move it to the scene root before play. DontDestroyOnLoad
/// only works on root-level GameObjects and will fail silently otherwise.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI headlineText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI statsText;
    [Tooltip("A separate TMP_Text for the run debrief (days passed, angry count, cash errors). " +
             "Can be a child of the same panel or a second scrollable block.")]
    [SerializeField] private TextMeshProUGUI debriefText;

    [Header("Buttons")]
    [SerializeField] private Button tryAgainButton;

    private static readonly string HeadlineConquered = "Earth Has Been Conquered";
    private static readonly string HeadlineSaved     = "Earth Has Been Saved";

    private static readonly string BodyBankruptcy =
        "Your restaurant ran out of funds.\n" +
        "Without food service, the alien fleet lost patience.\n\n" +
        "Earth has fallen.";

    private static readonly string BodyApprovalCollapsed =
        "The alien fleet reported back to their Commander.\n" +
        "The food was unacceptable.\n\n" +
        "Earth has fallen.";

    private static readonly string BodyEarthSaved =
        "The Fleet Commander has tasted your cuisine.\n" +
        "After 30 days of exceptional service, Earth has been spared.\n\n" +
        "Humanity owes you everything.";

    private static readonly string BodyEarthConqueredDay30 =
        "You survived 30 days, but the aliens remain unconvinced.\n" +
        "The Fleet Commander calls for invasion.\n\n" +
        "Earth has fallen — but you came closer than anyone expected.";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // This GameObject must be a scene root for DontDestroyOnLoad to work correctly.
        // If it is a child of another GameObject, move it in the Inspector so it has no parent.
        if (transform.parent != null)
        {
            Debug.LogError("[GameOverScreen] GameObject is not at scene root. DontDestroyOnLoad requires a root-level object. " +
                           "Detaching from parent to avoid corrupting parent Canvas hierarchy.");
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnTryAgainClicked);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Activates and populates the screen with the correct narrative text, run stats,
    /// and a debrief block showing the student what drove the outcome.
    /// Called by GameFlowManager.TriggerGameOver().
    /// </summary>
    public void Show(GameOverReason reason, int finalApproval, int finalMoney, int daysReached)
    {
        gameObject.SetActive(true);

        headlineText.text = reason == GameOverReason.EarthSaved
            ? HeadlineSaved
            : HeadlineConquered;

        bodyText.text = reason switch
        {
            GameOverReason.Bankruptcy          => BodyBankruptcy,
            GameOverReason.ApprovalCollapsed   => BodyApprovalCollapsed,
            GameOverReason.EarthSaved          => BodyEarthSaved,
            GameOverReason.EarthConqueredDay30 => BodyEarthConqueredDay30,
            _                                  => string.Empty
        };

        statsText.text =
            $"Days Survived: {daysReached} / 30\n" +
            $"Alien Approval: {finalApproval} / 100\n" +
            $"Remaining Funds: ₱{finalMoney}";

        BuildDebrief(daysReached);
    }

    /// <summary>
    /// Builds the debrief block from DailyObjectiveManager and GameDayManager data.
    /// Gives the student actionable context for why the run ended.
    /// </summary>
    private void BuildDebrief(int daysReached)
    {
        if (debriefText == null)
            return;

        var objMgr = DailyObjectiveManager.Instance;
        var dayMgr = GameDayManager.Instance;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("── RUN SUMMARY ─────────────────");

        // Objective performance across the run
        int daysPassed = objMgr != null ? objMgr.TotalDaysPassed : 0;
        int daysFailed = daysReached - daysPassed;
        sb.AppendLine($"Mandatory Objective");
        sb.AppendLine($"  Passed:  {daysPassed} day{(daysPassed == 1 ? "" : "s")}");
        sb.AppendLine($"  Failed:  {daysFailed} day{(daysFailed == 1 ? "" : "s")}");
        sb.AppendLine();

        // Customer mood across the last shift (GameDayManager resets each shift)
        if (dayMgr != null)
        {
            int served = dayMgr.CustomersServed;
            int angry  = dayMgr.AngryCustomers;
            float angryPct = served > 0 ? (angry / (float)served) * 100f : 0f;

            sb.AppendLine($"Last Shift — Customers");
            sb.AppendLine($"  Served:  {served}");
            sb.AppendLine($"  Angry:   {angry} ({angryPct:F0}%)");
            sb.AppendLine();

            int cash = dayMgr.CashErrors;
            sb.AppendLine($"Last Shift — Cash Handling");
            sb.AppendLine(cash == 0
                ? "  ✓ No errors"
                : $"  ⚠ {cash} abandoned transaction{(cash == 1 ? "" : "s")}");
        }

        sb.AppendLine("────────────────────────────────");
        debriefText.text = sb.ToString();
    }

    /// <summary>
    /// Resets time scale and game state, then hides this screen.
    /// Triggered by the Try Again button.
    /// </summary>
    private void OnTryAgainClicked()
    {
        Time.timeScale = 1f;

        AlienApprovalManager.Instance?.ResetApproval();
        DailyObjectiveManager.Instance?.ResetForNewRun();
        GameFlowManager.Instance?.ResetRun();

        gameObject.SetActive(false);
    }
}
