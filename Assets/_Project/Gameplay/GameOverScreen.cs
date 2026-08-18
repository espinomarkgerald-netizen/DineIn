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

    [Header("Responsive Layout")]
    [SerializeField] private RectTransform safeAreaRoot;

    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;

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

    /// <summary>
    /// Game-over UI was never authored into the restaurant scenes. Build a
    /// complete screen on demand so terminal campaign states cannot leave the
    /// Day Report button in a silent, permanently completed state.
    /// </summary>
    public static GameOverScreen CreateRuntimeFallback()
    {
        if (Instance != null)
            return Instance;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
            uiLayer = 5;

        GameObject root = new GameObject("Runtime Game Over Screen", typeof(RectTransform));
        root.layer = uiLayer;
        root.SetActive(false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 450f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();

        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0.015f, 0.035f, 0.07f, 0.985f);
        backdrop.raycastTarget = true;

        RectTransform safeRoot = CreateRect("Safe Area", root.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image panel = CreateImage("Outcome Panel", safeRoot,
            new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.945f),
            new Color(0.045f, 0.16f, 0.25f, 0.99f));

        TextMeshProUGUI headline = CreateText("Headline", panel.rectTransform,
            new Vector2(0.055f, 0.79f), new Vector2(0.945f, 0.95f),
            28f, 52f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(0.25f, 0.86f, 1f));
        TextMeshProUGUI body = CreateText("Outcome", panel.rectTransform,
            new Vector2(0.075f, 0.51f), new Vector2(0.925f, 0.79f),
            18f, 30f, FontStyles.Normal, TextAlignmentOptions.Center,
            Color.white);
        TextMeshProUGUI stats = CreateText("Statistics", panel.rectTransform,
            new Vector2(0.075f, 0.35f), new Vector2(0.925f, 0.51f),
            17f, 27f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(1f, 0.83f, 0.28f));
        TextMeshProUGUI debrief = CreateText("Debrief", panel.rectTransform,
            new Vector2(0.075f, 0.14f), new Vector2(0.925f, 0.35f),
            14f, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Color(0.82f, 0.91f, 0.96f));
        debrief.textWrappingMode = TextWrappingModes.Normal;

        Button retry = CreateButton("Try Again", panel.rectTransform,
            new Vector2(0.34f, 0.025f), new Vector2(0.66f, 0.13f), "TRY AGAIN");

        GameOverScreen screen = root.AddComponent<GameOverScreen>();
        screen.headlineText = headline;
        screen.bodyText = body;
        screen.statsText = stats;
        screen.debriefText = debrief;
        screen.tryAgainButton = retry;
        screen.safeAreaRoot = safeRoot;

        // Run Awake once. Awake deliberately leaves the persistent screen
        // inactive until Show supplies the final outcome.
        root.SetActive(true);
        return screen;
    }

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

        ApplySafeArea();

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnTryAgainClicked);

        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        if (safeAreaRoot != null &&
            (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height ||
             lastSafeArea != Screen.safeArea))
        {
            ApplySafeArea();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Activates and populates the screen with the correct narrative text, run stats,
    /// and a debrief block showing the student what drove the outcome.
    /// Called by GameFlowManager.TriggerGameOver().
    /// </summary>
    public void Show(GameOverReason reason, int finalApproval, int finalMoney, int daysReached)
    {
        gameObject.SetActive(true);

        ApplySafeArea();

        if (headlineText != null)
        {
            headlineText.text = reason == GameOverReason.EarthSaved
                ? HeadlineSaved
                : HeadlineConquered;
        }

        if (bodyText != null)
        {
            bodyText.text = reason switch
            {
                GameOverReason.Bankruptcy          => BodyBankruptcy,
                GameOverReason.ApprovalCollapsed   => BodyApprovalCollapsed,
                GameOverReason.EarthSaved          => BodyEarthSaved,
                GameOverReason.EarthConqueredDay30 => BodyEarthConqueredDay30,
                _                                  => string.Empty
            };
        }

        if (statsText != null)
        {
            statsText.text =
                $"Days Survived: {daysReached} / 30\n" +
                $"Alien Approval: {finalApproval} / 100\n" +
                $"Remaining Funds: ₱{finalMoney}";
        }

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

    private void ApplySafeArea()
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safe = Screen.safeArea;
        safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = safe;
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax,
            Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float minFontSize,
        float maxFontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax,
            new Vector2(8f, 5f), new Vector2(-8f, -5f));
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = maxFontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label)
    {
        Image image = CreateImage(objectName, parent, anchorMin, anchorMax,
            new Color(0.05f, 0.61f, 0.84f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Label", image.rectTransform,
            Vector2.zero, Vector2.one, 17f, 29f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        text.text = label;
        return button;
    }
}
