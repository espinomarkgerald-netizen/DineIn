using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Prefab-backed presentation for Casual Dining objectives. Existing finance,
/// approval and customer mood managers remain the authoritative data sources.
/// </summary>
public sealed class CasualDiningProgressHUD : MonoBehaviour
{
    public enum DayTimeLayoutMode
    {
        Auto,
        SideBySide,
        Stacked
    }

    private const string ResourcePath = "UI/CasualDiningProgressHUD";
    private const string ExpandedPreference = "DineIn.CasualDining.ProgressExpandedV2";

    public static CasualDiningProgressHUD Instance { get; private set; }
    public bool IsExpanded => expanded;
    public bool PreserveReferenceResolutionOnMobile => preserveReferenceResolutionOnMobile;

    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private string supportedScene = "Lobby1";
    [SerializeField] private bool hideWhileGameplayUIBlocked = true;

    [Header("Responsive Canvas & Safe Area")]
    [Tooltip("This HUD owns its responsive layout, so the global mobile pass must not replace its 1920 x 1080 reference resolution.")]
    [SerializeField] private bool preserveReferenceResolutionOnMobile = true;
    [SerializeField] private bool respectDeviceSafeArea = true;
    [Tooltip("Extra padding inside the device safe area: X left, Y right, Z top, W bottom.")]
    [SerializeField] private Vector4 safeAreaPadding = new Vector4(14f, 14f, 0f, 10f);
    [SerializeField, Range(0.5f, 2.5f)] private float compactAspectThreshold = 1.5f;
    [SerializeField, Range(0.7f, 1f)] private float compactObjectivesScale = 0.9f;
    [SerializeField, Min(0f)] private float portraitClockSecondRowGap = 12f;

    [Header("Editable Layout (1920 x 1080)")]
    [Tooltip("Upper-center position. Its top edge matches the pause button.")]
    [SerializeField] private Vector2 panelPosition = new Vector2(0f, -28f);
    [SerializeField] private Vector2 expandedSize = new Vector2(620f, 154f);
    [SerializeField, Min(44f)] private float moneyBarHeight = 66f;
    [SerializeField, Min(28f)] private float approvalBarHeight = 42f;
    [SerializeField, Min(20f)] private float moodBarHeight = 32f;
    [SerializeField, Min(10f)] private float moneyLaneHeight = 24f;
    [SerializeField, Min(7f)] private float approvalLaneHeight = 16f;
    [SerializeField, Min(5f)] private float moodLaneHeight = 11f;
    [SerializeField, Min(0f)] private float barGap = 7f;
    [SerializeField, Min(0f)] private float moodGap = 12f;
    [SerializeField, Min(2f)] private float laneSidePadding = 8f;
    [SerializeField, Min(2f)] private float laneBottomPadding = 5f;
    [SerializeField, Min(0f)] private float depthOffset = 3f;

    [Header("Editable Day & Time")]
    [Tooltip("Auto uses side-by-side on wide screens and stacked on compact screens. Both layouts are real editable prefab children.")]
    [SerializeField] private DayTimeLayoutMode dayTimeLayout = DayTimeLayoutMode.Auto;
    [SerializeField] private TMP_FontAsset dayTimeFont;
    [SerializeField] private string previewDayText = "DAY 3";
    [SerializeField] private string previewTimeText = "10:00 AM";

    [Header("Authored Responsive References")]
    [SerializeField] private RectTransform safeAreaContent;
    [SerializeField] private RectTransform objectivesResponsiveRoot;
    [SerializeField] private RectTransform dayTimeRoot;
    [SerializeField] private GameObject sideBySideDayTime;
    [SerializeField] private GameObject stackedDayTime;
    [SerializeField] private TMP_Text sideBySideDayText;
    [SerializeField] private TMP_Text sideBySideTimeText;
    [SerializeField] private TMP_Text stackedDayText;
    [SerializeField] private TMP_Text stackedTimeText;

    [Header("Project Style Assets")]
    [SerializeField] private Sprite nineSlicedFrame;
    [SerializeField] private Sprite roundedBarFrame;
    [SerializeField] private Sprite glossyBadgeFrame;
    [SerializeField] private Sprite moneyIcon;
    [SerializeField] private Sprite approvalIcon;
    [SerializeField] private Sprite neutralIcon;
    [SerializeField] private Sprite angryIcon;
    [SerializeField] private TMP_FontAsset font;

    [Header("Colors")]
    [SerializeField] private Color depthShadowColor = new Color(0.005f, 0.025f, 0.075f, 0.92f);
    [SerializeField] private Color trackColor = new Color(0.07f, 0.48f, 0.75f, 1f);
    [SerializeField] private Color badgeRimColor = new Color(0.92f, 0.95f, 1f, 1f);
    [SerializeField] private Color badgeCoreColor = Color.white;
    [SerializeField] private Color salesFillColor = new Color(0.13f, 0.82f, 0.33f, 1f);
    [SerializeField] private Color approvalFillColor = new Color(0.08f, 0.67f, 1f, 1f);
    [SerializeField] private Color neutralFillColor = new Color(1f, 0.72f, 0.08f, 1f);
    [SerializeField] private Color angryFillColor = new Color(0.92f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color textOutlineColor = new Color(0.01f, 0.035f, 0.09f, 1f);
    [SerializeField, Range(0f, 0.35f)] private float textOutlineWidth = 0.16f;

    [Header("Progress Texture")]
    [SerializeField, Min(4f)] private float stripeWidth = 13f;
    [SerializeField, Min(8f)] private float stripeSpacing = 30f;
    [SerializeField, Range(-45f, 45f)] private float stripeAngle = -18f;
    [SerializeField] private Color stripeColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color glossColor = new Color(1f, 1f, 1f, 0.13f);
    [SerializeField, Min(1f)] private float badgeRimThickness = 3f;

    [Header("Motion")]
    [SerializeField, Min(0.02f)] private float fillSmoothTime = 0.22f;
    [SerializeField, Min(0.02f)] private float toggleAnimationSeconds = 0.24f;
    [SerializeField, Range(1f, 1.15f)] private float changePulseScale = 1.045f;
    [SerializeField, Min(0.1f)] private float changePulseSeconds = 0.46f;
    [SerializeField, Min(0.5f)] private float shineInterval = 2.8f;
    [SerializeField, Min(0.1f)] private float shineDuration = 0.8f;
    [SerializeField] private bool rememberToggleState = true;

    [Header("Prefab Preview Defaults")]
    [Tooltip("Sample fill amounts stored in the prefab for Edit Mode. Runtime gameplay values replace them.")]
    [SerializeField, Range(0f, 1f)] private float previewMoneyFill = 0.6f;
    [SerializeField, Range(0f, 1f)] private float previewApprovalFill = 0.7f;
    [SerializeField, Range(0f, 1f)] private float previewAngryFill = 0.2f;
    [SerializeField, Range(0f, 1f)] private float previewNeutralFill = 0.4f;

    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private RectTransform moneyRow, approvalRow, neutralRow, angryRow;
    private RectTransform moneyFill, approvalFill, neutralFill, angryFill;
    private RectTransform moneyShine, approvalShine, neutralShine, angryShine;
    private TMP_Text moneyValue, approvalValue, neutralValue, angryValue;

    private MoneyManager boundMoney;
    private AlienApprovalManager boundApproval;
    private bool supportedSceneVisible;
    private bool expanded;
    private bool initializedValues;
    private float currentMoneyFill, targetMoneyFill, moneyFillVelocity;
    private float currentApprovalFill, targetApprovalFill, approvalFillVelocity;
    private float currentNeutralFill, targetNeutralFill, neutralFillVelocity;
    private float currentAngryFill, targetAngryFill, angryFillVelocity;
    private int lastEarned = int.MinValue;
    private int lastRequired = int.MinValue;
    private int lastApproval = int.MinValue;
    private int lastNeutral = int.MinValue;
    private int lastAngry = int.MinValue;
    private float moneyPulseStarted = float.NegativeInfinity;
    private float approvalPulseStarted = float.NegativeInfinity;
    private float neutralPulseStarted = float.NegativeInfinity;
    private float angryPulseStarted = float.NegativeInfinity;
    private float nextSourceRefresh;
    private float nextLegacyHideAttempt;
    private Vector2Int lastResponsiveScreenSize = new Vector2Int(-1, -1);
    private Rect lastResponsiveSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector2 authoredDayTimePosition;
    private bool hasAuthoredDayTimePosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        SceneManager.activeSceneChanged -= HandleActiveSceneChangedStatic;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneLoaded += HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        SceneManager.sceneUnloaded += HandleSceneUnloadedStatic;
        SceneManager.activeSceneChanged -= HandleActiveSceneChangedStatic;
        SceneManager.activeSceneChanged += HandleActiveSceneChangedStatic;
    }

    private static void HandleSceneLoadedStatic(Scene _, LoadSceneMode __) => EnsureInstance()?.RefreshSceneVisibility();
    private static void HandleSceneUnloadedStatic(Scene _) => Instance?.RefreshSceneVisibility();
    private static void HandleActiveSceneChangedStatic(Scene _, Scene __) => Instance?.RefreshSceneVisibility();

    private static CasualDiningProgressHUD EnsureInstance()
    {
        if (Instance != null) return Instance;
        CasualDiningProgressHUD existing = FindFirstObjectByType<CasualDiningProgressHUD>(FindObjectsInactive.Include);
        if (existing != null) return existing;
        CasualDiningProgressHUD prefab = Resources.Load<CasualDiningProgressHUD>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError("[CasualDiningProgressHUD] Resources/UI/CasualDiningProgressHUD prefab is missing.");
            return null;
        }
        return Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        bool usingAuthoredVisuals = TryBindAuthoredVisualTree();
        if (!usingAuthoredVisuals)
            BuildVisualTree();
        else if (panelRect != null)
            expandedSize = panelRect.sizeDelta;
        expanded = !rememberToggleState || PlayerPrefs.GetInt(ExpandedPreference, 1) != 0;
        RefreshResponsiveLayout(true);
        ApplyExpandedStateImmediate();
        RefreshSceneVisibility();
        BindSources();
        RefreshValues(false);
    }

    private void OnDestroy()
    {
        UnbindSources();
        if (Instance == this) Instance = null;
    }

    private void OnValidate()
    {
        expandedSize.x = Mathf.Max(460f, expandedSize.x);
        expandedSize.y = Mathf.Max(110f, expandedSize.y);
        moneyBarHeight = Mathf.Max(44f, moneyBarHeight);
        approvalBarHeight = Mathf.Max(28f, approvalBarHeight);
        moodBarHeight = Mathf.Max(20f, moodBarHeight);
        moneyLaneHeight = Mathf.Clamp(moneyLaneHeight, 10f, moneyBarHeight - 14f);
        approvalLaneHeight = Mathf.Clamp(approvalLaneHeight, 7f, approvalBarHeight - 11f);
        moodLaneHeight = Mathf.Clamp(moodLaneHeight, 5f, moodBarHeight - 9f);
        laneSidePadding = Mathf.Max(2f, laneSidePadding);
        laneBottomPadding = Mathf.Max(2f, laneBottomPadding);
        depthOffset = Mathf.Max(0f, depthOffset);
        stripeWidth = Mathf.Max(4f, stripeWidth);
        stripeSpacing = Mathf.Max(stripeWidth + 2f, stripeSpacing);
        badgeRimThickness = Mathf.Max(1f, badgeRimThickness);
        fillSmoothTime = Mathf.Max(0.02f, fillSmoothTime);
        toggleAnimationSeconds = Mathf.Max(0.02f, toggleAnimationSeconds);
        changePulseSeconds = Mathf.Max(0.1f, changePulseSeconds);
        shineDuration = Mathf.Max(0.1f, shineDuration);
        shineInterval = Mathf.Max(shineDuration + 0.1f, shineInterval);
        safeAreaPadding.x = Mathf.Max(0f, safeAreaPadding.x);
        safeAreaPadding.y = Mathf.Max(0f, safeAreaPadding.y);
        safeAreaPadding.z = Mathf.Max(0f, safeAreaPadding.z);
        safeAreaPadding.w = Mathf.Max(0f, safeAreaPadding.w);
        compactAspectThreshold = Mathf.Clamp(compactAspectThreshold, 0.5f, 2.5f);
        compactObjectivesScale = Mathf.Clamp(compactObjectivesScale, 0.7f, 1f);
        portraitClockSecondRowGap = Mathf.Max(0f, portraitClockSecondRowGap);

#if UNITY_EDITOR
        if (!Application.isPlaying && TryBindAuthoredVisualTree())
            ApplyEditorResponsivePreview();
#endif
    }

    private void Update()
    {
        RefreshResponsiveLayout(false);
        RefreshBlockingVisibility();

        float now = Time.unscaledTime;
        if (now >= nextSourceRefresh)
        {
            nextSourceRefresh = now + 0.12f;
            BindSources();
            RefreshValues(true);
        }
        if (supportedSceneVisible && now >= nextLegacyHideAttempt)
        {
            nextLegacyHideAttempt = now + 1f;
            HideLegacyProgressBars();
        }
        AnimatePanel();
        AnimateProgress(now);
    }

    public void RefreshBlockingVisibility()
    {
        bool canPresent = supportedSceneVisible && (!hideWhileGameplayUIBlocked || !GameplayUIBlocker.IsBlocked());
        if (hudGroup != null)
        {
            hudGroup.alpha = canPresent ? 1f : 0f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
        }
    }

    public void RefreshSceneVisibility()
    {
        // RestockScene is loaded additively while Lobby1 remains loaded. Checking
        // all loaded scenes therefore leaks this gameplay HUD into both restock rooms.
        // The active scene is authoritative: only normal Lobby gameplay may show it.
        supportedSceneVisible = SceneManager.GetActiveScene().name == supportedScene;
        if (hudCanvas != null) hudCanvas.enabled = supportedSceneVisible;
        if (supportedSceneVisible) HideLegacyProgressBars();
    }

    public void ToggleObjectives()
    {
        expanded = !expanded;
        if (!rememberToggleState) return;
        PlayerPrefs.SetInt(ExpandedPreference, expanded ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void BindSources()
    {
        if (boundMoney != MoneyManager.Instance)
        {
            if (boundMoney != null) boundMoney.OnMoneyChanged -= HandleMoneyChanged;
            boundMoney = MoneyManager.Instance;
            if (boundMoney != null) boundMoney.OnMoneyChanged += HandleMoneyChanged;
        }
        if (boundApproval != AlienApprovalManager.Instance)
        {
            if (boundApproval != null) boundApproval.OnApprovalChanged -= HandleApprovalChanged;
            boundApproval = AlienApprovalManager.Instance;
            if (boundApproval != null) boundApproval.OnApprovalChanged += HandleApprovalChanged;
        }
    }

    private void UnbindSources()
    {
        if (boundMoney != null) boundMoney.OnMoneyChanged -= HandleMoneyChanged;
        if (boundApproval != null) boundApproval.OnApprovalChanged -= HandleApprovalChanged;
        boundMoney = null;
        boundApproval = null;
    }

    private void HandleMoneyChanged(int _) => RefreshValues(true);
    private void HandleApprovalChanged(int _) => RefreshValues(true);

    private void RefreshValues(bool animateChange)
    {
        DailyFinanceBridge finance = DailyFinanceBridge.Instance;
        GameDayManager day = GameDayManager.Instance;
        int earned = finance != null ? finance.EarnedToday : 0;
        int required = finance != null ? finance.TotalRequiredEarningsToday : 0;
        int approval = boundApproval != null ? boundApproval.Approval : 0;
        int neutral = day != null ? day.NeutralCustomers : 0;
        int neutralMax = day != null ? day.NeutralCustomerObjectiveMax : 10;
        int angry = day != null ? day.AngryCustomers : 0;
        int angryMax = day != null ? day.AngryCustomerObjectiveMax : 10;

        bool moneyChanged = earned != lastEarned || required != lastRequired;
        bool approvalChanged = approval != lastApproval;
        bool neutralChanged = neutral != lastNeutral;
        bool angryChanged = angry != lastAngry;
        targetMoneyFill = required > 0 ? Mathf.Clamp01((float)earned / required) : 0f;
        targetApprovalFill = Mathf.Clamp01(approval / 100f);
        targetNeutralFill = Mathf.Clamp01((float)neutral / Mathf.Max(1, neutralMax));
        targetAngryFill = Mathf.Clamp01((float)angry / Mathf.Max(1, angryMax));

        if (moneyValue != null) moneyValue.text = $"\u20b1{earned:N0} / \u20b1{required:N0}";
        if (approvalValue != null) approvalValue.text = approval + "%";
        if (neutralValue != null) neutralValue.text = neutral + " / " + neutralMax;
        if (angryValue != null) angryValue.text = angry + " / " + angryMax;
        RefreshDayAndTime(day);

        if (!initializedValues || LevelOneUIAccessibility.ReducedMotion)
        {
            currentMoneyFill = targetMoneyFill;
            currentApprovalFill = targetApprovalFill;
            currentNeutralFill = targetNeutralFill;
            currentAngryFill = targetAngryFill;
            initializedValues = true;
        }
        else if (animateChange)
        {
            float now = Time.unscaledTime;
            if (moneyChanged && lastEarned != int.MinValue) moneyPulseStarted = now;
            if (approvalChanged && lastApproval != int.MinValue) approvalPulseStarted = now;
            if (neutralChanged && lastNeutral != int.MinValue) neutralPulseStarted = now;
            if (angryChanged && lastAngry != int.MinValue) angryPulseStarted = now;
        }

        lastEarned = earned;
        lastRequired = required;
        lastApproval = approval;
        lastNeutral = neutral;
        lastAngry = angry;
    }

    private void RefreshDayAndTime(GameDayManager dayManager)
    {
        GameFlowManager flow = GameFlowManager.Instance;
        string dayLabel;
        if (flow != null && flow.UsesSingleRestaurantFlow)
        {
            dayLabel = flow.IsEndlessRestaurantMode
                ? "ENDLESS"
                : $"DAY {flow.CurrentDay}";
        }
        else
        {
            dayLabel = flow != null ? $"DAY {flow.CurrentDay}" : previewDayText;
        }

        string timeLabel = dayManager != null ? dayManager.FormattedGameTime : previewTimeText;
        SetClockText(sideBySideDayText, dayLabel);
        SetClockText(stackedDayText, dayLabel);
        SetClockText(sideBySideTimeText, timeLabel);
        SetClockText(stackedTimeText, timeLabel);
    }

    private static void SetClockText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
            target.text = value;
    }

    private void RefreshResponsiveLayout(bool force)
    {
        if (safeAreaContent == null || objectivesResponsiveRoot == null ||
            sideBySideDayTime == null || stackedDayTime == null)
            return;

        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        Rect safeArea = respectDeviceSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
        if (!force && screenSize == lastResponsiveScreenSize && safeArea == lastResponsiveSafeArea)
            return;

        lastResponsiveScreenSize = screenSize;
        lastResponsiveSafeArea = safeArea;
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        safeAreaContent.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        safeAreaContent.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        safeAreaContent.offsetMin = new Vector2(safeAreaPadding.x, safeAreaPadding.w);
        safeAreaContent.offsetMax = new Vector2(-safeAreaPadding.y, -safeAreaPadding.z);

        float safeAspect = safeArea.height > 0.01f ? safeArea.width / safeArea.height : 1f;
        bool compact = safeAspect < compactAspectThreshold;
        bool useStacked = dayTimeLayout == DayTimeLayoutMode.Stacked ||
                          (dayTimeLayout == DayTimeLayoutMode.Auto && compact);
        ApplyDayTimeVariant(useStacked);
        float objectiveScale = compact ? compactObjectivesScale : 1f;
        objectivesResponsiveRoot.localScale = Vector3.one * objectiveScale;

        CaptureAuthoredDayTimePosition();
        if (dayTimeRoot != null && hasAuthoredDayTimePosition)
        {
            Vector2 clockPosition = authoredDayTimePosition;
            if (safeAspect < 1f)
            {
                float objectiveHeight = panelRect != null ? panelRect.sizeDelta.y : expandedSize.y;
                clockPosition.y -= objectiveHeight * objectiveScale + portraitClockSecondRowGap;
            }
            dayTimeRoot.anchoredPosition = clockPosition;
        }
    }

    private void CaptureAuthoredDayTimePosition()
    {
        if (hasAuthoredDayTimePosition || dayTimeRoot == null)
            return;

        authoredDayTimePosition = dayTimeRoot.anchoredPosition;
        hasAuthoredDayTimePosition = true;
    }

    private void ApplyDayTimeVariant(bool useStacked)
    {
        if (sideBySideDayTime != null && sideBySideDayTime.activeSelf == useStacked)
            sideBySideDayTime.SetActive(!useStacked);
        if (stackedDayTime != null && stackedDayTime.activeSelf != useStacked)
            stackedDayTime.SetActive(useStacked);
    }

#if UNITY_EDITOR
    private void ApplyEditorResponsivePreview()
    {
        if (safeAreaContent != null)
        {
            safeAreaContent.anchorMin = Vector2.zero;
            safeAreaContent.anchorMax = Vector2.one;
            safeAreaContent.offsetMin = new Vector2(safeAreaPadding.x, safeAreaPadding.w);
            safeAreaContent.offsetMax = new Vector2(-safeAreaPadding.y, -safeAreaPadding.z);
        }

        bool useStacked = dayTimeLayout == DayTimeLayoutMode.Stacked;
        ApplyDayTimeVariant(useStacked);
        if (objectivesResponsiveRoot != null)
            objectivesResponsiveRoot.localScale = Vector3.one * (useStacked ? compactObjectivesScale : 1f);
        SetClockText(sideBySideDayText, previewDayText);
        SetClockText(stackedDayText, previewDayText);
        SetClockText(sideBySideTimeText, previewTimeText);
        SetClockText(stackedTimeText, previewTimeText);
    }
#endif

    private void AnimateProgress(float now)
    {
        bool reduced = LevelOneUIAccessibility.ReducedMotion;
        float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        if (reduced)
        {
            currentMoneyFill = targetMoneyFill;
            currentApprovalFill = targetApprovalFill;
            currentNeutralFill = targetNeutralFill;
            currentAngryFill = targetAngryFill;
        }
        else
        {
            currentMoneyFill = Mathf.SmoothDamp(currentMoneyFill, targetMoneyFill, ref moneyFillVelocity, fillSmoothTime, Mathf.Infinity, delta);
            currentApprovalFill = Mathf.SmoothDamp(currentApprovalFill, targetApprovalFill, ref approvalFillVelocity, fillSmoothTime, Mathf.Infinity, delta);
            currentNeutralFill = Mathf.SmoothDamp(currentNeutralFill, targetNeutralFill, ref neutralFillVelocity, fillSmoothTime, Mathf.Infinity, delta);
            currentAngryFill = Mathf.SmoothDamp(currentAngryFill, targetAngryFill, ref angryFillVelocity, fillSmoothTime, Mathf.Infinity, delta);
        }
        SetFill(moneyFill, currentMoneyFill);
        SetFill(approvalFill, currentApprovalFill);
        SetFill(neutralFill, currentNeutralFill);
        SetFill(angryFill, currentAngryFill);
        AnimatePulse(moneyRow, now - moneyPulseStarted, reduced);
        AnimatePulse(approvalRow, now - approvalPulseStarted, reduced);
        AnimatePulse(neutralRow, now - neutralPulseStarted, reduced);
        AnimatePulse(angryRow, now - angryPulseStarted, reduced);
        AnimateShine(moneyShine, now, reduced);
        AnimateShine(approvalShine, now + shineInterval * 0.25f, reduced);
        AnimateShine(neutralShine, now + shineInterval * 0.5f, reduced);
        AnimateShine(angryShine, now + shineInterval * 0.75f, reduced);
    }

    private void AnimatePulse(RectTransform row, float age, bool reduced)
    {
        if (row == null) return;
        float scale = 1f;
        if (!reduced && age >= 0f && age < changePulseSeconds)
            scale = Mathf.Lerp(1f, changePulseScale, Mathf.Sin(age / changePulseSeconds * Mathf.PI));
        row.localScale = Vector3.one * scale;
    }

    private void AnimateShine(RectTransform shine, float now, bool reduced)
    {
        if (shine == null) return;
        shine.gameObject.SetActive(!reduced);
        if (reduced) return;
        float t = Mathf.Clamp01(Mathf.Repeat(now, shineInterval) / shineDuration);
        float x = Mathf.Lerp(-0.12f, 1.12f, Smooth(t));
        shine.anchorMin = new Vector2(x, 0f);
        shine.anchorMax = new Vector2(x, 1f);
    }

    private void AnimatePanel()
    {
        if (panelRect == null || panelGroup == null) return;
        Vector2 target = new Vector2(expandedSize.x, expanded ? expandedSize.y : 0f);
        float targetAlpha = expanded ? 1f : 0f;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            panelRect.sizeDelta = target;
            panelGroup.alpha = targetAlpha;
        }
        else
        {
            float step = Time.unscaledDeltaTime / toggleAnimationSeconds;
            panelRect.sizeDelta = Vector2.Lerp(panelRect.sizeDelta, target, 1f - Mathf.Pow(0.001f, step));
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, Time.unscaledDeltaTime / toggleAnimationSeconds);
        }
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
    }

    private void ApplyExpandedStateImmediate()
    {
        if (panelRect != null) panelRect.sizeDelta = new Vector2(expandedSize.x, expanded ? expandedSize.y : 0f);
        if (panelGroup != null) panelGroup.alpha = expanded ? 1f : 0f;
    }

    private void HideLegacyProgressBars()
    {
        GameObject mainHud = GameObject.Find("CanvasMainHUD");
        Transform money = mainHud != null ? mainHud.transform.Find("AchievementSystem/ProgressBar") : null;
        if (money != null) money.gameObject.SetActive(false);
        GameObject angry = GameObject.Find("AngryBar");
        if (angry != null) angry.SetActive(false);
        GameObject neutral = GameObject.Find("UnhappyBar");
        if (neutral != null) neutral.SetActive(false);
    }

    private void BuildVisualTree()
    {
        if (panelRect != null) return;

        GameObject safeArea = CreateUIObject("SafeAreaContent", transform);
        safeAreaContent = safeArea.GetComponent<RectTransform>();
        Stretch(safeAreaContent);

        GameObject objectivesRoot = CreateUIObject("ObjectivesResponsiveRoot", safeArea.transform);
        objectivesResponsiveRoot = objectivesRoot.GetComponent<RectTransform>();
        objectivesResponsiveRoot.anchorMin = objectivesResponsiveRoot.anchorMax =
            objectivesResponsiveRoot.pivot = new Vector2(0.5f, 1f);
        objectivesResponsiveRoot.anchoredPosition = Vector2.zero;
        objectivesResponsiveRoot.sizeDelta = Vector2.zero;

        GameObject panel = CreateUIObject("ObjectivesPanel", objectivesRoot.transform, typeof(CanvasGroup));
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = panelPosition;
        panelRect.sizeDelta = expandedSize;
        panelGroup = panel.GetComponent<CanvasGroup>();

        GameObject content = CreateUIObject("ObjectiveRows", panel.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        Stretch(contentRect);
        float moodWidth = (expandedSize.x - moodGap) * 0.5f;
        float moodY = -(moneyBarHeight + barGap + approvalBarHeight + barGap);
        moneyRow = CreateProgressRow(content.transform, "SalesProgress", "TODAY'S SALES", moneyIcon,
            Vector2.zero, expandedSize.x, moneyBarHeight, moneyLaneHeight, 40f, 20f, salesFillColor,
            out moneyFill, out moneyShine, out moneyValue);
        approvalRow = CreateProgressRow(content.transform, "ApprovalProgress", "ALIEN APPROVAL", approvalIcon,
            new Vector2(0f, -(moneyBarHeight + barGap)), expandedSize.x, approvalBarHeight,
            approvalLaneHeight, 28f, 17f,
            approvalFillColor,
            out approvalFill, out approvalShine, out approvalValue);
        angryRow = CreateProgressRow(content.transform, "AngryProgress", "ANGRY CUSTOMERS", angryIcon,
            new Vector2(0f, moodY), moodWidth, moodBarHeight, moodLaneHeight, 20f, 14f, angryFillColor,
            out angryFill, out angryShine, out angryValue);
        neutralRow = CreateProgressRow(content.transform, "NeutralProgress", "NEUTRAL CUSTOMERS", neutralIcon,
            new Vector2(moodWidth + moodGap, moodY), moodWidth, moodBarHeight, moodLaneHeight, 20f, 14f,
            neutralFillColor,
            out neutralFill, out neutralShine, out neutralValue);

        BuildDayTimeVisualTree(safeArea.transform);
    }

    private bool TryBindAuthoredVisualTree()
    {
        Transform safeArea = transform.Find("SafeAreaContent");
        if (safeArea != null)
            safeAreaContent = safeArea as RectTransform;

        Transform responsiveRoot = safeArea != null ? safeArea.Find("ObjectivesResponsiveRoot") : null;
        if (responsiveRoot != null)
            objectivesResponsiveRoot = responsiveRoot as RectTransform;

        Transform panel = responsiveRoot != null
            ? responsiveRoot.Find("ObjectivesPanel")
            : safeArea != null
                ? safeArea.Find("ObjectivesPanel")
                : transform.Find("ObjectivesPanel");
        Transform rows = panel != null ? panel.Find("ObjectiveRows") : null;
        if (panel == null || rows == null)
            return false;

        panelRect = panel as RectTransform;
        panelGroup = panel.GetComponent<CanvasGroup>();
        moneyRow = rows.Find("SalesProgress") as RectTransform;
        approvalRow = rows.Find("ApprovalProgress") as RectTransform;
        angryRow = rows.Find("AngryProgress") as RectTransform;
        neutralRow = rows.Find("NeutralProgress") as RectTransform;
        moneyFill = FindRowRect(moneyRow, "Track/Fill");
        approvalFill = FindRowRect(approvalRow, "Track/Fill");
        angryFill = FindRowRect(angryRow, "Track/Fill");
        neutralFill = FindRowRect(neutralRow, "Track/Fill");
        moneyShine = FindRowRect(moneyRow, "Track/Fill/Shine");
        approvalShine = FindRowRect(approvalRow, "Track/Fill/Shine");
        angryShine = FindRowRect(angryRow, "Track/Fill/Shine");
        neutralShine = FindRowRect(neutralRow, "Track/Fill/Shine");
        moneyValue = FindRowText(moneyRow, "Value");
        approvalValue = FindRowText(approvalRow, "Value");
        angryValue = FindRowText(angryRow, "Value");
        neutralValue = FindRowText(neutralRow, "Value");

        BindDayTimeReferences(safeArea);

        bool complete = panelRect != null && panelGroup != null &&
                        moneyRow != null && approvalRow != null && angryRow != null && neutralRow != null &&
                        moneyFill != null && approvalFill != null && angryFill != null && neutralFill != null &&
                        moneyValue != null && approvalValue != null && angryValue != null && neutralValue != null &&
                        safeAreaContent != null && objectivesResponsiveRoot != null &&
                        dayTimeRoot != null && sideBySideDayTime != null && stackedDayTime != null &&
                        sideBySideDayText != null && sideBySideTimeText != null &&
                        stackedDayText != null && stackedTimeText != null;
        if (!complete)
            ResetVisualReferences();
        return complete;
    }

    private void BuildDayTimeVisualTree(Transform parent)
    {
        GameObject root = CreateUIObject("DayTimeRoot", parent);
        dayTimeRoot = root.GetComponent<RectTransform>();
        dayTimeRoot.anchorMin = dayTimeRoot.anchorMax = dayTimeRoot.pivot = Vector2.one;
        dayTimeRoot.anchoredPosition = new Vector2(-28f, -28f);
        dayTimeRoot.sizeDelta = new Vector2(330f, 104f);

        sideBySideDayTime = CreateUIObject("SideBySide", root.transform);
        RectTransform sideRect = sideBySideDayTime.GetComponent<RectTransform>();
        sideRect.anchorMin = sideRect.anchorMax = sideRect.pivot = Vector2.one;
        sideRect.anchoredPosition = Vector2.zero;
        sideRect.sizeDelta = new Vector2(330f, 68f);

        sideBySideDayText = CreateClockText(sideRect, "DayText", previewDayText, 42f);
        RectTransform sideDayRect = sideBySideDayText.rectTransform;
        sideDayRect.anchorMin = new Vector2(0f, 0f);
        sideDayRect.anchorMax = new Vector2(0.58f, 1f);
        sideDayRect.offsetMin = Vector2.zero;
        sideDayRect.offsetMax = new Vector2(-6f, 0f);

        sideBySideTimeText = CreateClockText(sideRect, "TimeText", previewTimeText, 25f);
        RectTransform sideTimeRect = sideBySideTimeText.rectTransform;
        sideTimeRect.anchorMin = new Vector2(0.60f, 0f);
        sideTimeRect.anchorMax = Vector2.one;
        sideTimeRect.offsetMin = new Vector2(6f, 0f);
        sideTimeRect.offsetMax = Vector2.zero;

        stackedDayTime = CreateUIObject("Stacked", root.transform);
        RectTransform stackedRect = stackedDayTime.GetComponent<RectTransform>();
        stackedRect.anchorMin = stackedRect.anchorMax = stackedRect.pivot = Vector2.one;
        stackedRect.anchoredPosition = Vector2.zero;
        stackedRect.sizeDelta = new Vector2(180f, 104f);

        stackedDayText = CreateClockText(stackedRect, "DayText", previewDayText, 40f);
        RectTransform stackedDayRect = stackedDayText.rectTransform;
        stackedDayRect.anchorMin = new Vector2(0f, 0.42f);
        stackedDayRect.anchorMax = Vector2.one;
        stackedDayRect.offsetMin = Vector2.zero;
        stackedDayRect.offsetMax = Vector2.zero;

        stackedTimeText = CreateClockText(stackedRect, "TimeText", previewTimeText, 24f);
        RectTransform stackedTimeRect = stackedTimeText.rectTransform;
        stackedTimeRect.anchorMin = Vector2.zero;
        stackedTimeRect.anchorMax = new Vector2(1f, 0.42f);
        stackedTimeRect.offsetMin = Vector2.zero;
        stackedTimeRect.offsetMax = Vector2.zero;

        ApplyDayTimeVariant(dayTimeLayout == DayTimeLayoutMode.Stacked);
    }

    private TMP_Text CreateClockText(Transform parent, string objectName, string contents, float size)
    {
        TMP_Text text = CreateText(parent, objectName, contents, size, TextAlignmentOptions.MidlineRight);
        text.font = dayTimeFont != null ? dayTimeFont : font != null ? font : TMP_Settings.defaultFontAsset;
        if (text.font != null)
            text.fontSharedMaterial = text.font.material;
        text.fontSizeMin = Mathf.Max(14f, size - 10f);
        text.fontSizeMax = size;
        text.characterSpacing = 1f;
        return text;
    }

    private void BindDayTimeReferences(Transform safeArea)
    {
        Transform root = safeArea != null ? safeArea.Find("DayTimeRoot") : null;
        if (root == null)
            return;

        dayTimeRoot = root as RectTransform;
        Transform side = root.Find("SideBySide");
        Transform stacked = root.Find("Stacked");
        sideBySideDayTime = side != null ? side.gameObject : null;
        stackedDayTime = stacked != null ? stacked.gameObject : null;
        sideBySideDayText = side != null ? side.Find("DayText")?.GetComponent<TMP_Text>() : null;
        sideBySideTimeText = side != null ? side.Find("TimeText")?.GetComponent<TMP_Text>() : null;
        stackedDayText = stacked != null ? stacked.Find("DayText")?.GetComponent<TMP_Text>() : null;
        stackedTimeText = stacked != null ? stacked.Find("TimeText")?.GetComponent<TMP_Text>() : null;
    }

    private static RectTransform FindRowRect(RectTransform row, string path)
    {
        return row != null ? row.Find(path) as RectTransform : null;
    }

    private static TMP_Text FindRowText(RectTransform row, string path)
    {
        Transform child = row != null ? row.Find(path) : null;
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

#if UNITY_EDITOR
    /// <summary>Used only by the prefab authoring utility to create real, saved child objects.</summary>
    public void RebuildAuthoredVisualTreeForEditor()
    {
        Transform oldPreview = transform.Find("ObjectivesPanel (Editor Preview)");
        if (oldPreview != null)
            DestroyImmediate(oldPreview.gameObject);
        Transform existingSafeArea = transform.Find("SafeAreaContent");
        if (existingSafeArea != null)
            DestroyImmediate(existingSafeArea.gameObject);
        Transform existing = transform.Find("ObjectivesPanel");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        ResetVisualReferences();
        BuildVisualTree();
        expanded = true;
        ApplyExpandedStateImmediate();
        ApplyAuthoredPreviewValues();
        ApplyEditorResponsivePreview();
        if (hudCanvas != null) hudCanvas.enabled = true;
        if (hudGroup != null)
        {
            hudGroup.alpha = 1f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Non-destructive prefab upgrade. It preserves the existing ObjectivesPanel and creates only
    /// the saved responsive wrappers and Day/Time objects that are missing.
    /// </summary>
    public bool EnsureAuthoredResponsiveHierarchyForEditor(TMP_FontAsset configuredDayTimeFont)
    {
        bool changed = false;
        if (dayTimeFont == null && configuredDayTimeFont != null)
        {
            dayTimeFont = configuredDayTimeFont;
            changed = true;
        }

        Transform safe = transform.Find("SafeAreaContent");
        if (safe == null)
        {
            GameObject safeObject = CreateUIObject("SafeAreaContent", transform);
            safeAreaContent = safeObject.GetComponent<RectTransform>();
            Stretch(safeAreaContent);
            safe = safeObject.transform;
            changed = true;
        }
        else
        {
            safeAreaContent = safe as RectTransform;
        }

        Transform responsive = safe.Find("ObjectivesResponsiveRoot");
        if (responsive == null)
        {
            GameObject responsiveObject = CreateUIObject("ObjectivesResponsiveRoot", safe);
            objectivesResponsiveRoot = responsiveObject.GetComponent<RectTransform>();
            objectivesResponsiveRoot.anchorMin = objectivesResponsiveRoot.anchorMax =
                objectivesResponsiveRoot.pivot = new Vector2(0.5f, 1f);
            objectivesResponsiveRoot.anchoredPosition = Vector2.zero;
            objectivesResponsiveRoot.sizeDelta = Vector2.zero;
            responsive = responsiveObject.transform;
            changed = true;
        }
        else
        {
            objectivesResponsiveRoot = responsive as RectTransform;
        }

        Transform objectives = responsive.Find("ObjectivesPanel");
        if (objectives == null)
            objectives = safe.Find("ObjectivesPanel");
        if (objectives == null)
            objectives = transform.Find("ObjectivesPanel");
        if (objectives != null && objectives.parent != responsive)
        {
            objectives.SetParent(responsive, false);
            changed = true;
        }

        Transform clock = safe.Find("DayTimeRoot");
        bool clockComplete = clock != null &&
                             clock.Find("SideBySide/DayText") != null &&
                             clock.Find("SideBySide/TimeText") != null &&
                             clock.Find("Stacked/DayText") != null &&
                             clock.Find("Stacked/TimeText") != null;
        if (!clockComplete)
        {
            if (clock != null)
                DestroyImmediate(clock.gameObject);
            BuildDayTimeVisualTree(safe);
            changed = true;
        }

        if (TryBindAuthoredVisualTree())
        {
            changed |= EnsureClockFontMaterialsForEditor();
            ApplyEditorResponsivePreview();
        }
        return changed;
    }

    private bool EnsureClockFontMaterialsForEditor()
    {
        bool changed = false;
        TMP_Text[] clockTexts =
        {
            sideBySideDayText,
            sideBySideTimeText,
            stackedDayText,
            stackedTimeText
        };
        for (int i = 0; i < clockTexts.Length; i++)
        {
            TMP_Text clockText = clockTexts[i];
            if (clockText == null || clockText.font == null || clockText.fontSharedMaterial != null)
                continue;

            clockText.fontSharedMaterial = clockText.font.material;
            changed = true;
        }
        return changed;
    }

    private void ApplyAuthoredPreviewValues()
    {
        SetFill(moneyFill, previewMoneyFill);
        SetFill(approvalFill, previewApprovalFill);
        SetFill(angryFill, previewAngryFill);
        SetFill(neutralFill, previewNeutralFill);
        if (approvalFill != null && approvalFill.TryGetComponent(out Image approvalImage))
            approvalImage.color = approvalFillColor;
        if (moneyValue != null) moneyValue.text = "\u20b12,700 / \u20b14,500";
        if (approvalValue != null) approvalValue.text = Mathf.RoundToInt(previewApprovalFill * 100f) + "%";
        if (angryValue != null) angryValue.text = Mathf.RoundToInt(previewAngryFill * 10f) + " / 10";
        if (neutralValue != null) neutralValue.text = Mathf.RoundToInt(previewNeutralFill * 10f) + " / 10";
    }
#endif

    private void ResetVisualReferences()
    {
        safeAreaContent = null;
        objectivesResponsiveRoot = null;
        dayTimeRoot = null;
        sideBySideDayTime = null;
        stackedDayTime = null;
        sideBySideDayText = null;
        sideBySideTimeText = null;
        stackedDayText = null;
        stackedTimeText = null;
        hasAuthoredDayTimePosition = false;
        panelRect = null;
        panelGroup = null;
        moneyRow = approvalRow = neutralRow = angryRow = null;
        moneyFill = approvalFill = neutralFill = angryFill = null;
        moneyShine = approvalShine = neutralShine = angryShine = null;
        moneyValue = approvalValue = neutralValue = angryValue = null;
    }

    private RectTransform CreateProgressRow(Transform parent, string objectName, string label, Sprite icon,
        Vector2 position, float width, float height, float laneHeight, float iconSize, float fontSize,
        Color fillColor,
        out RectTransform fill,
        out RectTransform shine, out TMP_Text value)
    {
        GameObject row = CreateUIObject(objectName, parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = position;
        rowRect.sizeDelta = new Vector2(width, height);

        float badgeSize = Mathf.Max(iconSize + 8f, height * 0.94f);
        float contentLeft = badgeSize + laneSidePadding;
        float usableWidth = Mathf.Max(40f, width - contentLeft - laneSidePadding);
        float laneBottom = Mathf.Min(laneBottomPadding, Mathf.Max(2f, (height - laneHeight) * 0.25f));

        GameObject progressShadow = CreateUIObject("ProgressShadow", row.transform, typeof(Image));
        RectTransform progressShadowRect = progressShadow.GetComponent<RectTransform>();
        SetBottomLeftRect(progressShadowRect, new Vector2(contentLeft, laneBottom - depthOffset),
            new Vector2(usableWidth, laneHeight));
        ConfigureSlicedImage(progressShadow.GetComponent<Image>(), roundedBarFrame, depthShadowColor);

        GameObject track = CreateUIObject("Track", row.transform, typeof(Image), typeof(Mask));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        SetBottomLeftRect(trackRect, new Vector2(contentLeft, laneBottom),
            new Vector2(usableWidth, laneHeight));
        ConfigureSlicedImage(track.GetComponent<Image>(), roundedBarFrame, trackColor);
        track.GetComponent<Mask>().showMaskGraphic = true;

        GameObject fillObject = CreateUIObject("Fill", track.transform, typeof(Image), typeof(RectMask2D));
        fill = fillObject.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        ConfigureSlicedImage(fillObject.GetComponent<Image>(), roundedBarFrame, fillColor);

        CreateStripes(fillObject.transform, usableWidth, laneHeight);

        GameObject glossObject = CreateUIObject("TopGloss", fillObject.transform, typeof(Image));
        RectTransform glossRect = glossObject.GetComponent<RectTransform>();
        glossRect.anchorMin = new Vector2(0f, 0.5f);
        glossRect.anchorMax = Vector2.one;
        glossRect.offsetMin = new Vector2(1f, 0f);
        glossRect.offsetMax = new Vector2(-1f, -1f);
        Image glossImage = glossObject.GetComponent<Image>();
        glossImage.color = glossColor;
        glossImage.raycastTarget = false;

        GameObject shineObject = CreateUIObject("Shine", fillObject.transform, typeof(Image));
        shine = shineObject.GetComponent<RectTransform>();
        shine.anchorMin = new Vector2(-0.12f, 0f);
        shine.anchorMax = new Vector2(-0.12f, 1f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(Mathf.Clamp(height * 0.32f, 8f, 22f), -2f);
        shine.localRotation = Quaternion.Euler(0f, 0f, stripeAngle);
        Image shineImage = shineObject.GetComponent<Image>();
        shineImage.color = new Color(1f, 1f, 1f, 0.34f);
        shineImage.raycastTarget = false;

        GameObject badgeShadow = CreateUIObject("BadgeShadow", row.transform, typeof(Image));
        RectTransform badgeShadowRect = badgeShadow.GetComponent<RectTransform>();
        SetBadgeRect(badgeShadowRect, badgeSize, new Vector2(badgeSize * 0.5f, -depthOffset));
        ConfigureBadgeImage(badgeShadow.GetComponent<Image>(), depthShadowColor);

        GameObject badgeRim = CreateUIObject("BadgeRim", row.transform, typeof(Image));
        RectTransform badgeRimRect = badgeRim.GetComponent<RectTransform>();
        SetBadgeRect(badgeRimRect, badgeSize, new Vector2(badgeSize * 0.5f, 0f));
        ConfigureSlicedImage(badgeRim.GetComponent<Image>(), nineSlicedFrame, badgeRimColor);

        GameObject badgeCore = CreateUIObject("BadgeCore", badgeRim.transform, typeof(Image));
        RectTransform badgeCoreRect = badgeCore.GetComponent<RectTransform>();
        Stretch(badgeCoreRect);
        badgeCoreRect.offsetMin = Vector2.one * badgeRimThickness;
        badgeCoreRect.offsetMax = -Vector2.one * badgeRimThickness;
        ConfigureBadgeImage(badgeCore.GetComponent<Image>(), badgeCoreColor);

        GameObject iconObject = CreateUIObject("Icon", badgeCore.transform, typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = Vector2.one * Mathf.Min(iconSize, badgeSize - badgeRimThickness * 2f - 4f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TMP_Text title = CreateText(row.transform, "Label", label, fontSize, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        float headerHeight = Mathf.Max(9f, height - laneBottom - laneHeight);
        float headerCenterY = laneBottom + laneHeight + headerHeight * 0.5f;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 0f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(contentLeft, headerCenterY);
        titleRect.sizeDelta = new Vector2(usableWidth, headerHeight);

        value = CreateText(row.transform, "Value", "0", fontSize, TextAlignmentOptions.Right);
        value.fontSizeMin = 8f;
        value.fontSizeMax = Mathf.Min(fontSize, Mathf.Max(11f, laneHeight + 2f));
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 0f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-laneSidePadding - 6f,
            laneBottom + laneHeight * 0.5f);
        valueRect.sizeDelta = new Vector2(Mathf.Min(155f, width * 0.32f), laneHeight + 6f);
        return rowRect;
    }

    private void CreateStripes(Transform parent, float maximumWidth, float laneHeight)
    {
        int count = Mathf.CeilToInt(maximumWidth / stripeSpacing) + 2;
        float stripeHeight = Mathf.Max(16f, laneHeight * 2.4f);
        for (int i = 0; i < count; i++)
        {
            GameObject stripeObject = CreateUIObject("Stripe", parent, typeof(Image));
            RectTransform stripeRect = stripeObject.GetComponent<RectTransform>();
            stripeRect.anchorMin = stripeRect.anchorMax = stripeRect.pivot = new Vector2(0f, 0.5f);
            stripeRect.anchoredPosition = new Vector2(i * stripeSpacing - stripeSpacing * 0.35f, 0f);
            stripeRect.sizeDelta = new Vector2(stripeWidth, stripeHeight);
            stripeRect.localRotation = Quaternion.Euler(0f, 0f, stripeAngle);
            Image stripeImage = stripeObject.GetComponent<Image>();
            stripeImage.color = stripeColor;
            stripeImage.raycastTarget = false;
        }
    }

    private TMP_Text CreateText(Transform parent, string objectName, string contents, float size,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = contents;
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = textColor;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size - 5f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineColor = textOutlineColor;
        text.outlineWidth = textOutlineWidth;
        return text;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent, params System.Type[] components)
    {
        System.Type[] all = new System.Type[components.Length + 1];
        all[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++) all[i + 1] = components[i];
        GameObject created = new GameObject(objectName, all);
        created.layer = 5;
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void ConfigureSlicedImage(Image image, Sprite sprite, Color color)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
    }

    private void ConfigureBadgeImage(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = glossyBadgeFrame != null ? glossyBadgeFrame : nineSlicedFrame;
        image.type = glossyBadgeFrame != null ? Image.Type.Simple : Image.Type.Sliced;
        image.preserveAspect = glossyBadgeFrame != null;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetBottomLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetBadgeRect(RectTransform rect, float size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * size;
    }

    private static void SetFill(RectTransform fill, float value)
    {
        if (fill == null) return;
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(value);
        fill.anchorMax = max;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
