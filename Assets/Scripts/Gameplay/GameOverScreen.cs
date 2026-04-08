using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the appropriate narrative ending screen based on the GameOverReason.
/// Singleton — persists across all scene loads via DontDestroyOnLoad on its root Canvas.
/// Attach to the GameOverPanel (child of CanvasGameMenu). The root Canvas will be
/// marked DontDestroyOnLoad automatically so this screen is always reachable,
/// regardless of which scene is currently active.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI headlineText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI statsText;

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
        // Singleton guard — destroy any duplicate that arrives when a scene reloads.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Persist this object across every scene load so TriggerGameOver()
        // can always reach this screen, regardless of which scene is active.
        // NOTE: this GameObject must be a scene root — not a child of any other
        // object — otherwise DontDestroyOnLoad will silently grab the root parent
        // and take the entire canvas hierarchy with it.
        DontDestroyOnLoad(gameObject);

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnTryAgainClicked);

        // Hide immediately — the panel must start active in the scene so Awake
        // runs and Instance is set, but we never want it visible until Show() is called.
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Activates and populates the screen with the correct narrative text and run stats.
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
            $"Remaining Funds: {finalMoney}";
    }

    /// <summary>
    /// Resets time scale and game state, then hides this screen.
    /// Triggered by the Try Again button.
    /// </summary>
    private void OnTryAgainClicked()
    {
        Time.timeScale = 1f;

        AlienApprovalManager.Instance?.ResetApproval();
        GameFlowManager.Instance?.ResetRun();

        gameObject.SetActive(false);
    }
}
