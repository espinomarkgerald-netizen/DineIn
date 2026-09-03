using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the management desktop inside the device safe area and switches the
/// app window between a right-hand desktop layout and a full compact layout.
/// The containing CanvasScaler supplies uniform resolution scaling.
/// </summary>
public sealed class ManagementComputerResponsiveLayout : MonoBehaviour
{
    [Header("Canvas Scale (Editable)")]
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private bool previewMobileLayoutInEditor;
    [SerializeField] private Vector2 mobileReferenceResolution = new Vector2(1600f, 900f);
    [SerializeField, Range(0f, 1f)] private float mobileMatchWidthOrHeight = 0.5f;

    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private RectTransform appWindow;
    [SerializeField] private RectTransform[] appButtons;
    [SerializeField, Min(0.5f)] private float compactAspectThreshold = 1.25f;
    [SerializeField] private Vector2 landscapeWindowMin = new Vector2(0.15f, 0.055f);
    [SerializeField] private Vector2 landscapeWindowMax = new Vector2(0.985f, 0.955f);
    [SerializeField] private Vector2 compactWindowMin = new Vector2(0.015f, 0.045f);
    [SerializeField] private Vector2 compactWindowMax = new Vector2(0.985f, 0.955f);

    [Header("Mobile Landscape (Editable)")]
    [SerializeField] private Vector2 mobileLandscapeWindowMin = MobileLandscapeWindowMin;
    [SerializeField] private Vector2 mobileLandscapeWindowMax = MobileLandscapeWindowMax;
    [SerializeField, Min(0f)] private float mobileWindowInset = 8f;

    [Header("App Button Grid (Editable)")]
    [SerializeField, Min(1)] private int appButtonColumns = 2;
    [SerializeField, Min(0f)] private float appButtonGap = 12f;
    [SerializeField, Min(0f)] private float appButtonLabelSpace = 34f;
    [SerializeField, Min(0f)] private float appButtonBottomMargin = 18f;
    [SerializeField, Min(0f)] private float appButtonLandscapeLeft = 24f;
    [SerializeField, Min(0f)] private float appButtonLandscapeTop = 70f;
    [SerializeField, Min(0f)] private float appButtonCompactTop = 92f;
    [SerializeField, Min(24f)] private float minimumAppButtonSize = 72f;
    [SerializeField, Min(24f)] private float maximumAppButtonSize = 168f;

    [Header("Mobile Touch & Blue Double Theme (Editable)")]
    [SerializeField] private Sprite wideButtonSprite;
    [SerializeField] private Sprite squareButtonSprite;

    public Sprite WideButtonSprite => wideButtonSprite;
    [SerializeField, Min(44f)] private float minimumTouchTarget = 68f;
    [SerializeField, Min(0f)] private float minimumScrollbarThickness = 28f;
    [SerializeField, Min(8f)] private float mobileBodyTextMinimum = 19f;
    [SerializeField, Min(8f)] private float mobileButtonTextMinimum = 20f;

    public static readonly Vector2 MobileLandscapeWindowMin = new Vector2(0.02f, 0.035f);
    public static readonly Vector2 MobileLandscapeWindowMax = new Vector2(0.985f, 0.96f);

    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;

    public RectTransform SafeAreaRoot => safeAreaRoot;
    public RectTransform AppWindow => appWindow;
    public RectTransform[] AppButtons => appButtons;
    // Layout selection must be deterministic across Editor, Device Simulator,
    // Windows and Android. This explicit authored preview toggle is serialized in
    // the prefab, so a platform can no longer silently select another composition.
    public bool UsesMobileLayout => previewMobileLayoutInEditor;

    public void ConfigureReferences(
        RectTransform configuredSafeAreaRoot,
        RectTransform configuredAppWindow,
        RectTransform[] configuredAppButtons)
    {
        safeAreaRoot = configuredSafeAreaRoot;
        appWindow = configuredAppWindow;
        appButtons = configuredAppButtons;
        RefreshLayout();
    }

    private void Awake() => RefreshLayout();

    private void OnEnable() => RefreshLayout();

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            RefreshLayout();
    }

    private void Update()
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (screenSize != lastScreenSize || Screen.safeArea != lastSafeArea)
            RefreshLayout();
    }

    public void RefreshLayout()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
        }

        ApplyCanvasScale();

        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;

        Rect safe = Screen.safeArea;
        safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;

        float safeAspect = safe.height > 0f ? safe.width / safe.height : 1f;
        bool compact = safeAspect < compactAspectThreshold;
        ApplyWindowAnchors(compact);

        Vector2 rootSize = rootRect != null ? rootRect.rect.size : Vector2.zero;
        Vector2 logicalSafeSize = new Vector2(
            rootSize.x * safe.width / Screen.width,
            rootSize.y * safe.height / Screen.height);
        ApplyAppButtonGrid(compact, logicalSafeSize);
        RefreshDynamicContent();
    }

    /// <summary>
    /// Reapplies mobile touch and visual rules after an app creates its rows or cards.
    /// All values and sprites remain editable from this component in the Inspector.
    /// </summary>
    public void RefreshDynamicContent()
    {
        if (!UsesMobileLayout || safeAreaRoot == null)
            return;

        Button[] buttons = safeAreaRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || IsDesktopAppButton(button))
                continue;

            EnforceTouchTarget(button.transform as RectTransform);
            ApplyBlueDoubleButton(button);
        }

        TMP_InputField[] inputs = safeAreaRoot.GetComponentsInChildren<TMP_InputField>(true);
        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField input = inputs[i];
            if (input == null)
                continue;

            EnforceTouchTarget(input.transform as RectTransform);
            ConfigureText(input.textComponent, mobileBodyTextMinimum);
            ConfigureText(input.placeholder as TMP_Text, mobileBodyTextMinimum - 1f);
        }

        TMP_Text[] texts = safeAreaRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            // Catalog cards already own carefully fitted phone-readable type.
            // Raising every dense card label to the global body minimum can
            // leave TMP with no glyph that fits its fixed stat/status rect.
            if (text.GetComponentInParent<ManagementComputerCatalogCardUI>() != null)
                continue;

            bool buttonLabel = text.GetComponentInParent<Button>() != null;
            ConfigureText(text, buttonLabel ? mobileButtonTextMinimum : mobileBodyTextMinimum);
        }

        Scrollbar[] scrollbars = safeAreaRoot.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++)
            EnforceScrollbarThickness(scrollbars[i]);
    }

    private void ApplyCanvasScale()
    {
        if (canvasScaler == null)
            canvasScaler = GetComponentInParent<CanvasScaler>(true);
        if (canvasScaler == null || !UsesMobileLayout)
            return;

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(
            Mathf.Max(320f, mobileReferenceResolution.x),
            Mathf.Max(180f, mobileReferenceResolution.y));
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = mobileMatchWidthOrHeight;
    }

    private void ApplyWindowAnchors(bool compact)
    {
        if (appWindow == null)
            return;

        bool phoneLandscape = UsesMobileLayout && !compact;
        appWindow.anchorMin = compact
            ? compactWindowMin
            : phoneLandscape ? mobileLandscapeWindowMin : landscapeWindowMin;
        appWindow.anchorMax = compact
            ? compactWindowMax
            : phoneLandscape ? mobileLandscapeWindowMax : landscapeWindowMax;
        appWindow.pivot = new Vector2(0.5f, 0.5f);
        float inset = UsesMobileLayout ? mobileWindowInset : 10f;
        appWindow.offsetMin = new Vector2(inset, inset);
        appWindow.offsetMax = new Vector2(-inset, -inset);
        appWindow.localScale = Vector3.one;
    }

    private void ApplyAppButtonGrid(bool compact, Vector2 safeSize)
    {
        if (appButtons == null || appButtons.Length == 0)
            return;

        float safeWidth = safeSize.x;
        float safeHeight = safeSize.y;
        if (safeWidth <= 0f || safeHeight <= 0f)
            return;

        int columns = Mathf.Max(1, appButtonColumns);
        float gap = Mathf.Max(0f, appButtonGap);
        float labelSpace = Mathf.Max(0f, appButtonLabelSpace);
        float bottomMargin = Mathf.Max(0f, appButtonBottomMargin);
        float availableWidth = compact ? safeWidth * 0.92f : safeWidth * 0.27f;
        float left = compact ? safeWidth * 0.04f : appButtonLandscapeLeft;
        float top = compact ? appButtonCompactTop : appButtonLandscapeTop;
        int rows = Mathf.CeilToInt(appButtons.Length / (float)columns);
        float maxFromWidth = (availableWidth - gap * (columns - 1)) / columns;
        float maxFromHeight = rows > 0
            ? (safeHeight - top - bottomMargin - gap * (rows - 1) - labelSpace * rows) / rows
            : 150f;
        float buttonSize = Mathf.Clamp(
            Mathf.Min(maxFromWidth, maxFromHeight),
            Mathf.Min(minimumAppButtonSize, maximumAppButtonSize),
            Mathf.Max(minimumAppButtonSize, maximumAppButtonSize));

        for (int i = 0; i < appButtons.Length; i++)
        {
            RectTransform button = appButtons[i];
            if (button == null)
                continue;

            int column = i % columns;
            int row = i / columns;
            button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
            button.pivot = new Vector2(0.5f, 0.5f);
            button.sizeDelta = Vector2.one * buttonSize;
            button.anchoredPosition = new Vector2(
                left + buttonSize * 0.5f + column * (buttonSize + gap),
                -(top + buttonSize * 0.5f + row * (buttonSize + labelSpace + gap)));

            Image icon = button.GetComponent<Image>();
            if (icon != null)
                icon.preserveAspect = true;
        }
    }

    private bool IsDesktopAppButton(Button button)
    {
        if (button == null || appButtons == null)
            return false;

        Transform buttonTransform = button.transform;
        for (int i = 0; i < appButtons.Length; i++)
        {
            if (appButtons[i] == buttonTransform)
                return true;
        }

        return false;
    }

    private void ApplyBlueDoubleButton(Button button)
    {
        if (button == null)
            return;

        string objectName = button.name;
        Transform parent = button.transform.parent;
        if (parent != null && parent.name == "CatalogCategoryTabs")
            return;
        if (objectName.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Exit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Secondary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        RectTransform rect = button.transform as RectTransform;
        if (image == null || rect == null)
            return;

        bool square = rect.rect.width <= rect.rect.height * 1.35f ||
                      objectName.IndexOf("Plus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                      objectName.IndexOf("Minus", System.StringComparison.OrdinalIgnoreCase) >= 0;
        Sprite sprite = square ? squareButtonSprite : wideButtonSprite;
        if (sprite == null)
            return;

        image.sprite = sprite;
        image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
    }

    private void EnforceTouchTarget(RectTransform rect)
    {
        if (rect == null)
            return;

        float target = Mathf.Max(44f, minimumTouchTarget);
        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minWidth = Mathf.Max(layout.minWidth, target);
            layout.minHeight = Mathf.Max(layout.minHeight, target);
            if (layout.preferredHeight >= 0f && layout.preferredHeight < target)
                layout.preferredHeight = target;
        }

        Vector2 size = rect.sizeDelta;
        if (Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x) && size.x < target)
            size.x = target;
        if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y) && size.y < target)
            size.y = target;
        rect.sizeDelta = size;

        if (rect.parent is RectTransform parent &&
            parent.name.IndexOf("QuantityControls", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Vector2 parentSize = parent.sizeDelta;
            parentSize.x = Mathf.Max(parentSize.x, target * 2f + 24f);
            parentSize.y = Mathf.Max(parentSize.y, target);
            parent.sizeDelta = parentSize;
        }
    }

    private void EnforceScrollbarThickness(Scrollbar scrollbar)
    {
        if (scrollbar == null || scrollbar.transform is not RectTransform rect)
            return;

        Vector2 size = rect.sizeDelta;
        if (scrollbar.direction == Scrollbar.Direction.LeftToRight ||
            scrollbar.direction == Scrollbar.Direction.RightToLeft)
            size.y = Mathf.Max(size.y, minimumScrollbarThickness);
        else
            size.x = Mathf.Max(size.x, minimumScrollbarThickness);
        rect.sizeDelta = size;
    }

    private static void ConfigureText(TMP_Text text, float minimum)
    {
        if (text == null)
            return;

        minimum = Mathf.Max(8f, minimum);
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(text.fontSizeMin, minimum);
        text.fontSizeMax = Mathf.Max(text.fontSizeMax, minimum + 7f);
        text.fontSize = Mathf.Max(text.fontSize, minimum);
    }
}
