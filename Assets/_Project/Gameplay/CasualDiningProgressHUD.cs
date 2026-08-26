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
    private const string ResourcePath = "UI/CasualDiningProgressHUD";
    private const string ExpandedPreference = "DineIn.CasualDining.ProgressExpandedV2";

    public static CasualDiningProgressHUD Instance { get; private set; }
    public bool IsExpanded => expanded;

    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private string supportedScene = "Lobby1";
    [SerializeField] private bool hideWhileGameplayUIBlocked = true;

    [Header("Editable Layout (1920 x 1080)")]
    [Tooltip("Upper-center position. Its top edge matches the pause button.")]
    [SerializeField] private Vector2 panelPosition = new Vector2(0f, -28f);
    [SerializeField] private Vector2 expandedSize = new Vector2(820f, 140f);
    [SerializeField, Min(44f)] private float moneyBarHeight = 58f;
    [SerializeField, Min(28f)] private float approvalBarHeight = 38f;
    [SerializeField, Min(20f)] private float moodBarHeight = 28f;
    [SerializeField, Min(0f)] private float barGap = 8f;
    [SerializeField, Min(0f)] private float moodGap = 12f;

    [Header("Project Style Assets")]
    [SerializeField] private Sprite nineSlicedFrame;
    [SerializeField] private Sprite moneyIcon;
    [SerializeField] private Sprite approvalIcon;
    [SerializeField] private Sprite neutralIcon;
    [SerializeField] private Sprite angryIcon;
    [SerializeField] private TMP_FontAsset font;

    [Header("Colors")]
    [SerializeField] private Color trackColor = new Color(0.035f, 0.16f, 0.29f, 0.96f);
    [SerializeField] private Color salesFillColor = new Color(0.16f, 0.86f, 0.38f, 1f);
    [SerializeField] private Color neutralFillColor = new Color(1f, 0.72f, 0.08f, 1f);
    [SerializeField] private Color angryFillColor = new Color(0.92f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color lowApprovalColor = new Color(0.92f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color middleApprovalColor = new Color(1f, 0.72f, 0.08f, 1f);
    [SerializeField] private Color highApprovalColor = new Color(0.15f, 0.82f, 0.34f, 1f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Motion")]
    [SerializeField, Min(0.02f)] private float fillSmoothTime = 0.22f;
    [SerializeField, Min(0.02f)] private float toggleAnimationSeconds = 0.24f;
    [SerializeField, Range(1f, 1.15f)] private float changePulseScale = 1.045f;
    [SerializeField, Min(0.1f)] private float changePulseSeconds = 0.46f;
    [SerializeField, Min(0.5f)] private float shineInterval = 2.8f;
    [SerializeField, Min(0.1f)] private float shineDuration = 0.8f;
    [SerializeField] private bool rememberToggleState = true;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedStatic;
        SceneManager.sceneLoaded += HandleSceneLoadedStatic;
        SceneManager.sceneUnloaded -= HandleSceneUnloadedStatic;
        SceneManager.sceneUnloaded += HandleSceneUnloadedStatic;
        EnsureInstance();
    }

    private static void HandleSceneLoadedStatic(Scene _, LoadSceneMode __) => EnsureInstance()?.RefreshSceneVisibility();
    private static void HandleSceneUnloadedStatic(Scene _) => Instance?.RefreshSceneVisibility();

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
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildVisualTree();
        expanded = !rememberToggleState || PlayerPrefs.GetInt(ExpandedPreference, 1) != 0;
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
        expandedSize.x = Mathf.Max(640f, expandedSize.x);
        expandedSize.y = Mathf.Max(110f, expandedSize.y);
        moneyBarHeight = Mathf.Max(44f, moneyBarHeight);
        approvalBarHeight = Mathf.Max(28f, approvalBarHeight);
        moodBarHeight = Mathf.Max(20f, moodBarHeight);
        fillSmoothTime = Mathf.Max(0.02f, fillSmoothTime);
        toggleAnimationSeconds = Mathf.Max(0.02f, toggleAnimationSeconds);
        changePulseSeconds = Mathf.Max(0.1f, changePulseSeconds);
        shineDuration = Mathf.Max(0.1f, shineDuration);
        shineInterval = Mathf.Max(shineDuration + 0.1f, shineInterval);
    }

    private void Update()
    {
        bool canPresent = supportedSceneVisible && (!hideWhileGameplayUIBlocked || !GameplayUIBlocker.IsBlocked());
        if (hudGroup != null)
        {
            hudGroup.alpha = canPresent ? 1f : 0f;
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
        }

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

    public void RefreshSceneVisibility()
    {
        supportedSceneVisible = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name != supportedScene) continue;
            supportedSceneVisible = true;
            break;
        }
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
        if (approvalFill != null && approvalFill.TryGetComponent(out Image image))
            image.color = EvaluateApprovalColor(currentApprovalFill);

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
        GameObject panel = CreateUIObject("ObjectivesPanel", transform, typeof(CanvasGroup));
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
            Vector2.zero, expandedSize.x, moneyBarHeight, 40f, 20f, salesFillColor,
            out moneyFill, out moneyShine, out moneyValue);
        approvalRow = CreateProgressRow(content.transform, "ApprovalProgress", "ALIEN APPROVAL", approvalIcon,
            new Vector2(0f, -(moneyBarHeight + barGap)), expandedSize.x, approvalBarHeight, 28f, 17f,
            middleApprovalColor,
            out approvalFill, out approvalShine, out approvalValue);
        angryRow = CreateProgressRow(content.transform, "AngryProgress", "ANGRY CUSTOMERS", angryIcon,
            new Vector2(0f, moodY), moodWidth, moodBarHeight, 20f, 14f, angryFillColor,
            out angryFill, out angryShine, out angryValue);
        neutralRow = CreateProgressRow(content.transform, "NeutralProgress", "NEUTRAL CUSTOMERS", neutralIcon,
            new Vector2(moodWidth + moodGap, moodY), moodWidth, moodBarHeight, 20f, 14f, neutralFillColor,
            out neutralFill, out neutralShine, out neutralValue);
    }

    private RectTransform CreateProgressRow(Transform parent, string objectName, string label, Sprite icon,
        Vector2 position, float width, float height, float iconSize, float fontSize, Color fillColor,
        out RectTransform fill,
        out RectTransform shine, out TMP_Text value)
    {
        GameObject row = CreateUIObject(objectName, parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = position;
        rowRect.sizeDelta = new Vector2(width, height);

        GameObject track = CreateUIObject("Track", row.transform, typeof(Image), typeof(Mask));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        Stretch(trackRect);
        ConfigureSlicedImage(track.GetComponent<Image>(), nineSlicedFrame, trackColor);
        track.GetComponent<Mask>().showMaskGraphic = true;

        GameObject fillObject = CreateUIObject("Fill", track.transform, typeof(Image), typeof(RectMask2D));
        fill = fillObject.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        ConfigureSlicedImage(fillObject.GetComponent<Image>(), nineSlicedFrame, fillColor);

        GameObject shineObject = CreateUIObject("Shine", fillObject.transform, typeof(Image));
        shine = shineObject.GetComponent<RectTransform>();
        shine.anchorMin = new Vector2(-0.12f, 0f);
        shine.anchorMax = new Vector2(-0.12f, 1f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(Mathf.Clamp(height * 0.28f, 8f, 18f), -4f);
        Image shineImage = shineObject.GetComponent<Image>();
        shineImage.color = new Color(1f, 1f, 1f, 0.38f);
        shineImage.raycastTarget = false;

        GameObject iconObject = CreateUIObject("Icon", row.transform, typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(height * 0.5f, 0f);
        iconRect.sizeDelta = Vector2.one * Mathf.Min(iconSize, height - 6f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TMP_Text title = CreateText(row.transform, "Label", label, fontSize, TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(height + 6f, 0f);
        titleRect.sizeDelta = new Vector2(Mathf.Max(90f, width - height - 150f), height - 4f);

        value = CreateText(row.transform, "Value", "0", fontSize, TextAlignmentOptions.Right);
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-12f, 0f);
        valueRect.sizeDelta = new Vector2(Mathf.Min(145f, width * 0.3f), height - 4f);
        return rowRect;
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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

    private Color EvaluateApprovalColor(float normalized)
    {
        if (normalized < 0.5f)
            return Color.Lerp(lowApprovalColor, middleApprovalColor, normalized / 0.5f);
        return Color.Lerp(middleApprovalColor, highApprovalColor, (normalized - 0.5f) / 0.5f);
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
