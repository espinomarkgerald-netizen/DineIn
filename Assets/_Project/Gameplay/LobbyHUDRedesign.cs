using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Canva-inspired utility layer for Lobby1. This class is presentation only:
/// it reads existing gameplay state and routes buttons through existing APIs.
/// The saved Resources prefab is the designer-editable source of truth.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyHUDRedesign : MonoBehaviour
{
    private const string LobbySceneName = "Lobby1";
    private const string ResourcePath = "UI/LobbyHUDRedesign";

    public static LobbyHUDRedesign Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private bool respectDeviceSafeArea = true;
    [SerializeField] private Vector4 safeAreaPadding = new Vector4(18f, 18f, 18f, 18f);

    [Header("Live Customer Panel")]
    [SerializeField] private RectTransform livePanel;
    [SerializeField] private Button livePanelToggle;
    [SerializeField] private RectTransform liveToggleRect;
    [SerializeField] private Image liveToggleArrow;
    [SerializeField] private TMP_Text liveCountsText;
    [SerializeField] private bool livePanelStartsExpanded = true;
    [SerializeField] private Vector2 livePanelShownPosition = new Vector2(24f, 20f);
    [SerializeField] private Vector2 livePanelHiddenPosition = new Vector2(-286f, 20f);
    [SerializeField] private Vector2 liveToggleShownPosition = new Vector2(302f, 20f);
    [SerializeField] private Vector2 liveToggleHiddenPosition = new Vector2(26f, 20f);
    [SerializeField, Min(0.15f)] private float liveCountRefreshSeconds = 0.6f;
    [SerializeField, Min(0.05f)] private float panelAnimationSeconds = 0.22f;

    [Header("Utility Buttons")]
    [SerializeField] private Button cameraButton;
    [SerializeField] private Button computerButton;
    [SerializeField] private Button newspaperButton;
    [SerializeField] private TMP_Text interactionLabel;

    [Header("Editable Style Assets")]
    [SerializeField] private Sprite blueFrame;
    [SerializeField] private Sprite neutralButtonFrame;
    [SerializeField] private Sprite blueArrow;
    [SerializeField] private Sprite cameraIcon;
    [SerializeField] private Sprite computerIcon;
    [SerializeField] private Sprite newspaperIcon;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField, HideInInspector] private int authoredVisualVersion;
    [SerializeField] private Color blue = new Color(0.12f, 0.59f, 0.95f, 0.98f);
    [SerializeField] private Color white = new Color(0.98f, 0.99f, 1f, 1f);
    [SerializeField] private Color ink = new Color(0.04f, 0.12f, 0.19f, 1f);

    private bool liveExpanded;
    private Vector2 livePanelVelocity;
    private Vector2 liveToggleVelocity;
    private float nextCountRefresh;
    private float nextInteractionRefresh;
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __) => EnsureInstance()?.RefreshVisibility();
    private static void HandleSceneUnloaded(Scene _) => Instance?.RefreshVisibility();
    private static void HandleActiveSceneChanged(Scene _, Scene __) => EnsureInstance()?.RefreshVisibility();

    private static LobbyHUDRedesign EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        LobbyHUDRedesign existing = FindFirstObjectByType<LobbyHUDRedesign>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        LobbyHUDRedesign prefab = Resources.Load<LobbyHUDRedesign>(ResourcePath);
        if (prefab != null)
            return Instantiate(prefab);

        // Build-safe fallback. The editor authoring utility normally saves the
        // same hierarchy as a prefab, but a missing prefab must never remove HUD access.
        GameObject root = new GameObject(
            "LobbyHUDRedesign (Runtime Fallback)",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(LobbyHUDRedesign));
        return root.GetComponent<LobbyHUDRedesign>();
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
        ResolveCanvas();
        if (!TryBindVisualTree())
            BuildVisualTree();
        WireButtons();
        liveExpanded = livePanelStartsExpanded;
        ApplyPanelImmediate();
        ApplySafeArea(true);
        RefreshCounts();
        RefreshVisibility();
    }

    private void OnDestroy()
    {
        UnwireButtons();
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        liveCountRefreshSeconds = Mathf.Max(0.15f, liveCountRefreshSeconds);
        panelAnimationSeconds = Mathf.Max(0.05f, panelAnimationSeconds);
        safeAreaPadding.x = Mathf.Max(0f, safeAreaPadding.x);
        safeAreaPadding.y = Mathf.Max(0f, safeAreaPadding.y);
        safeAreaPadding.z = Mathf.Max(0f, safeAreaPadding.z);
        safeAreaPadding.w = Mathf.Max(0f, safeAreaPadding.w);
    }

    private void Update()
    {
        ApplySafeArea(false);
        RefreshVisibility();
        AnimateLivePanel();

        float now = Time.unscaledTime;
        if (now >= nextCountRefresh)
        {
            nextCountRefresh = now + liveCountRefreshSeconds;
            RefreshCounts();
        }

        if (now >= nextInteractionRefresh)
        {
            nextInteractionRefresh = now + 0.15f;
            RefreshInteractionLabel();
        }
    }

    public void RefreshVisibility()
    {
        bool visible = SceneManager.GetActiveScene().name == LobbySceneName && !GameplayUIBlocker.IsBlocked();
        if (hudCanvas != null)
            hudCanvas.enabled = SceneManager.GetActiveScene().name == LobbySceneName;
        if (hudGroup != null)
        {
            hudGroup.alpha = visible ? 1f : 0f;
            hudGroup.interactable = visible;
            hudGroup.blocksRaycasts = visible;
        }
    }

    private void ToggleLivePanel()
    {
        liveExpanded = !liveExpanded;
    }

    private void AnimateLivePanel()
    {
        if (livePanel == null || liveToggleRect == null)
            return;

        Vector2 panelTarget = liveExpanded ? livePanelShownPosition : livePanelHiddenPosition;
        Vector2 toggleTarget = liveExpanded ? liveToggleShownPosition : liveToggleHiddenPosition;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            livePanel.anchoredPosition = panelTarget;
            liveToggleRect.anchoredPosition = toggleTarget;
        }
        else
        {
            livePanel.anchoredPosition = Vector2.SmoothDamp(
                livePanel.anchoredPosition, panelTarget, ref livePanelVelocity,
                panelAnimationSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            liveToggleRect.anchoredPosition = Vector2.SmoothDamp(
                liveToggleRect.anchoredPosition, toggleTarget, ref liveToggleVelocity,
                panelAnimationSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        if (liveToggleArrow != null)
            liveToggleArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, liveExpanded ? 180f : 0f);
    }

    private void ApplyPanelImmediate()
    {
        if (livePanel != null)
            livePanel.anchoredPosition = liveExpanded ? livePanelShownPosition : livePanelHiddenPosition;
        if (liveToggleRect != null)
            liveToggleRect.anchoredPosition = liveExpanded ? liveToggleShownPosition : liveToggleHiddenPosition;
        if (liveToggleArrow != null)
            liveToggleArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, liveExpanded ? 180f : 0f);
    }

    private void RefreshCounts()
    {
        if (liveCountsText == null)
            return;

        int inLine = 0;
        int readyToOrder = 0;
        int waitingForFood = 0;
        int eating = 0;
        int waitingForBill = 0;
        int takeout = 0;

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || !group.isActiveAndEnabled)
                continue;

            if (group.IsTakeout)
            {
                if (group.state != CustomerGroup.GroupState.Leaving &&
                    group.state != CustomerGroup.GroupState.AngryLeft &&
                    group.state != CustomerGroup.GroupState.UnhappyLeft)
                    takeout++;
                continue;
            }

            switch (group.state)
            {
                case CustomerGroup.GroupState.WalkingToLobby:
                case CustomerGroup.GroupState.Waiting:
                    inLine++;
                    break;
                case CustomerGroup.GroupState.ReadyToOrder:
                    readyToOrder++;
                    break;
                case CustomerGroup.GroupState.OrderTaken:
                    waitingForFood++;
                    break;
                case CustomerGroup.GroupState.Eating:
                    eating++;
                    break;
                case CustomerGroup.GroupState.NeedsBill:
                    waitingForBill++;
                    break;
            }
        }

        string value =
            $"IN LINE: {inLine}\n" +
            $"READY TO ORDER: {readyToOrder}\n" +
            $"WAITING FOR FOOD: {waitingForFood}\n" +
            $"EATING: {eating}\n" +
            $"WAITING FOR BILL: {waitingForBill}";
        if (takeout > 0)
            value += $"\nTAKEOUT: {takeout}";
        liveCountsText.text = value;
    }

    private void RefreshInteractionLabel()
    {
        PlayerMovement movement = ManagerPlayer.Active != null ? ManagerPlayer.Active.Movement : null;
        IInteractable target = movement != null
            ? movement.LockedTarget ?? movement.CurrentTarget
            : null;
        string targetName = target != null ? GetReadableInteractionName(target) : string.Empty;
        bool show = !string.IsNullOrWhiteSpace(targetName);
        if (interactionLabel != null)
        {
            interactionLabel.gameObject.SetActive(show);
            if (show && interactionLabel.text != targetName)
                interactionLabel.text = targetName;
        }
    }

    private static string GetReadableInteractionName(IInteractable target)
    {
        if (target is ManagementComputerStation)
            return "Computer";
        if (target is CashierBoothInteractable)
            return "Cashier";
        if (target is TakeoutCustomerInteractable)
            return "Takeout Customer";
        if (target is CustomerDeliverInteractable)
            return "Customer Table";

        Component component = target as Component;
        if (component == null)
            return "Work Station";

        LobbyInteractableDisplayName overrideName =
            component.GetComponentInParent<LobbyInteractableDisplayName>(true);
        if (overrideName != null && !string.IsNullOrWhiteSpace(overrideName.DisplayName))
            return overrideName.DisplayName;

        Booth booth = component.GetComponentInParent<Booth>();
        if (booth != null)
            return NicifyObjectName(booth.gameObject.name, "Booth");

        return NicifyObjectName(component.gameObject.name, "Work Station");
    }

    private static string NicifyObjectName(string rawName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return fallback;

        string value = rawName.Replace("(Clone)", string.Empty)
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace('_', ' ')
            .Trim();
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            char previous = i > 0 ? value[i - 1] : '\0';
            bool needsSpace = i > 0 && current != ' ' && previous != ' ' &&
                              ((char.IsUpper(current) && char.IsLower(previous)) ||
                               (char.IsDigit(current) && !char.IsDigit(previous)));
            if (needsSpace)
                builder.Append(' ');
            builder.Append(current);
        }

        value = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void FocusCameraOnManager()
    {
        CameraController cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null)
        {
            cameraController.EnterFollowMode();
            return;
        }

        MainCameraController mainCamera = FindFirstObjectByType<MainCameraController>();
        if (mainCamera != null && ManagerPlayer.Active != null)
            mainCamera.SetRigTargetPosition(ManagerPlayer.Active.transform.position, false);
    }

    private void OpenComputerThroughManager()
    {
        ManagerPlayer manager = ManagerPlayer.Active;
        if (manager == null || manager.Movement == null)
            return;

        ManagementComputerStation station = FindFirstObjectByType<ManagementComputerStation>();
        if (station != null && station.CanInteract())
            manager.Movement.UI_MoveTo(station);
    }

    private static void OpenNewspaper()
    {
        CasualDiningPolishManager.Instance?.OpenCurrentIssue();
    }

    private void WireButtons()
    {
        UnwireButtons();
        livePanelToggle?.onClick.AddListener(ToggleLivePanel);
        cameraButton?.onClick.AddListener(FocusCameraOnManager);
        computerButton?.onClick.AddListener(OpenComputerThroughManager);
        newspaperButton?.onClick.AddListener(OpenNewspaper);
    }

    private void UnwireButtons()
    {
        livePanelToggle?.onClick.RemoveListener(ToggleLivePanel);
        cameraButton?.onClick.RemoveListener(FocusCameraOnManager);
        computerButton?.onClick.RemoveListener(OpenComputerThroughManager);
        newspaperButton?.onClick.RemoveListener(OpenNewspaper);
    }

    private void ResolveCanvas()
    {
        hudCanvas = hudCanvas != null ? hudCanvas : GetComponent<Canvas>();
        hudGroup = hudGroup != null ? hudGroup : GetComponent<CanvasGroup>();
        if (hudCanvas == null)
            hudCanvas = gameObject.AddComponent<Canvas>();
        if (hudGroup == null)
            hudGroup = gameObject.AddComponent<CanvasGroup>();

        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 225;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void ApplySafeArea(bool force)
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        Rect area = respectDeviceSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
        if (!force && size == lastScreenSize && area == lastSafeArea)
            return;

        lastScreenSize = size;
        lastSafeArea = area;
        safeAreaRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeAreaRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeAreaRoot.offsetMin = new Vector2(safeAreaPadding.x, safeAreaPadding.w);
        safeAreaRoot.offsetMax = new Vector2(-safeAreaPadding.y, -safeAreaPadding.z);
    }

    private bool TryBindVisualTree()
    {
        Transform safe = transform.Find("SafeArea");
        if (safe == null)
            return false;
        safeAreaRoot = safe as RectTransform;
        livePanel = safe.Find("LivePanel") as RectTransform;
        Transform toggle = safe.Find("LiveToggle");
        livePanelToggle = toggle != null ? toggle.GetComponent<Button>() : null;
        liveToggleRect = toggle as RectTransform;
        liveToggleArrow = toggle != null ? toggle.Find("Arrow")?.GetComponent<Image>() : null;
        liveCountsText = livePanel != null ? livePanel.Find("Counts")?.GetComponent<TMP_Text>() : null;
        cameraButton = safe.Find("CameraButton")?.GetComponent<Button>();
        computerButton = safe.Find("ComputerButton")?.GetComponent<Button>();
        newspaperButton = safe.Find("NewspaperButton")?.GetComponent<Button>();
        interactionLabel = safe.Find("InteractionLabel")?.GetComponent<TMP_Text>();
        return livePanel != null && livePanelToggle != null && liveCountsText != null &&
               cameraButton != null && computerButton != null && newspaperButton != null &&
               interactionLabel != null;
    }

    private void BuildVisualTree()
    {
        Transform existing = transform.Find("SafeArea");
        if (existing != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(existing.gameObject);
            else
#endif
                Destroy(existing.gameObject);
        }

        GameObject safe = CreateUIObject("SafeArea", transform);
        safeAreaRoot = safe.GetComponent<RectTransform>();
        Stretch(safeAreaRoot);

        BuildLivePanel(safe.transform);
        BuildCameraButton(safe.transform);
        BuildComputerButton(safe.transform);
        BuildNewspaperButton(safe.transform);
        BuildInteractionLabel(safe.transform);
    }

    private void BuildLivePanel(Transform parent)
    {
        GameObject panel = CreateImage("LivePanel", parent, blueFrame, blue);
        livePanel = panel.GetComponent<RectTransform>();
        SetAnchor(livePanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        livePanel.anchoredPosition = livePanelShownPosition;
        livePanel.sizeDelta = new Vector2(272f, 312f);

        TMP_Text title = CreateText("Title", panel.transform, "CUSTOMERS", 30f, TextAlignmentOptions.Center, white);
        SetAnchored(title.rectTransform, new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.97f));

        liveCountsText = CreateText(
            "Counts", panel.transform,
            "IN LINE: 0\nREADY TO ORDER: 0\nWAITING FOR FOOD: 0\nEATING: 0\nWAITING FOR BILL: 0",
            22f, TextAlignmentOptions.Left, white);
        liveCountsText.fontStyle = FontStyles.Bold;
        liveCountsText.enableAutoSizing = true;
        liveCountsText.fontSizeMin = 16f;
        liveCountsText.fontSizeMax = 22f;
        liveCountsText.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnchored(liveCountsText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.96f, 0.80f));

        GameObject toggle = CreateImage("LiveToggle", parent, blueFrame, blue);
        liveToggleRect = toggle.GetComponent<RectTransform>();
        SetAnchor(liveToggleRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        liveToggleRect.anchoredPosition = liveToggleShownPosition;
        liveToggleRect.sizeDelta = new Vector2(70f, 88f);
        livePanelToggle = toggle.AddComponent<Button>();
        ConfigureButton(livePanelToggle);

        GameObject arrow = CreateImage("Arrow", toggle.transform, blueArrow, white);
        liveToggleArrow = arrow.GetComponent<Image>();
        liveToggleArrow.preserveAspect = true;
        Stretch(liveToggleArrow.rectTransform, 14f);
    }

    private void BuildCameraButton(Transform parent)
    {
        GameObject root = CreateImage("CameraButton", parent, cameraIcon, Color.white);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        rect.anchoredPosition = new Vector2(30f, -160f);
        rect.sizeDelta = new Vector2(108f, 92f);
        root.GetComponent<Image>().preserveAspect = true;
        cameraButton = root.AddComponent<Button>();
        ConfigureButton(cameraButton);
    }

    private void BuildComputerButton(Transform parent)
    {
        GameObject root = CreateImage("ComputerButton", parent, neutralButtonFrame, Color.white);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchor(rect, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchoredPosition = new Vector2(28f, 22f);
        rect.sizeDelta = new Vector2(178f, 178f);
        computerButton = root.AddComponent<Button>();
        ConfigureButton(computerButton);

        GameObject icon = CreateImage("Icon", root.transform, computerIcon, Color.white);
        Image image = icon.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        Stretch(image.rectTransform, 25f);
    }

    private void BuildNewspaperButton(Transform parent)
    {
        GameObject root = CreateImage("NewspaperButton", parent, newspaperIcon, Color.white);
        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        rect.anchoredPosition = new Vector2(-76f, -82f);
        rect.sizeDelta = new Vector2(90f, 112f);
        root.GetComponent<Image>().preserveAspect = true;
        newspaperButton = root.AddComponent<Button>();
        ConfigureButton(newspaperButton);

        if (newspaperIcon == null)
        {
            TMP_Text label = CreateText("Label", root.transform, "DAILY\nNEWS", 20f, TextAlignmentOptions.Center, ink);
            Stretch(label.rectTransform, 8f);
        }
    }

    private void BuildInteractionLabel(Transform parent)
    {
        interactionLabel = CreateText(
            "InteractionLabel", parent, "Booth 1", 42f,
            TextAlignmentOptions.BottomRight, white);
        RectTransform rect = interactionLabel.rectTransform;
        Vector2 bottomRight = new Vector2(1f, 0f);
        SetAnchor(rect, bottomRight, bottomRight, bottomRight);
        rect.anchoredPosition = new Vector2(-120f, 30f);
        rect.sizeDelta = new Vector2(520f, 90f);
        interactionLabel.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        interactionLabel.outlineWidth = 0.2f;
        interactionLabel.gameObject.SetActive(false);
    }

    private static void ConfigureButton(Button button)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.97f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.86f, 0.95f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private TMP_Text CreateText(
        string objectName, Transform parent, string value, float size,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject root = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject root = CreateUIObject(name, parent, typeof(Image));
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return root;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        System.Type[] all = new System.Type[components.Length + 1];
        all[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++)
            all[i + 1] = components[i];
        GameObject root = new GameObject(name, all);
        root.layer = 5;
        root.transform.SetParent(parent, false);
        return root;
    }

    private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
    }

    private static void SetAnchored(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
    }

#if UNITY_EDITOR
    public int AuthoredVisualVersion => authoredVisualVersion;

    public void ConfigureForEditor(
        Sprite configuredBlueFrame,
        Sprite configuredNeutralButtonFrame,
        Sprite configuredBlueArrow,
        Sprite configuredCameraIcon,
        Sprite configuredComputerIcon,
        Sprite configuredNewspaperIcon,
        TMP_FontAsset configuredFont,
        int configuredVisualVersion)
    {
        blueFrame = configuredBlueFrame;
        neutralButtonFrame = configuredNeutralButtonFrame;
        blueArrow = configuredBlueArrow;
        cameraIcon = configuredCameraIcon;
        computerIcon = configuredComputerIcon;
        newspaperIcon = configuredNewspaperIcon;
        font = configuredFont;
        authoredVisualVersion = configuredVisualVersion;
        ResolveCanvas();
        BuildVisualTree();
        TryBindVisualTree();
        liveExpanded = livePanelStartsExpanded;
        ApplyPanelImmediate();
    }
#endif
}
