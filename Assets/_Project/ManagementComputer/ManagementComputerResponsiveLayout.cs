using UnityEngine;

/// <summary>
/// Keeps the management desktop inside the device safe area and switches the
/// app window between a right-hand desktop layout and a full compact layout.
/// The containing CanvasScaler supplies uniform resolution scaling.
/// </summary>
public sealed class ManagementComputerResponsiveLayout : MonoBehaviour
{
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private RectTransform appWindow;
    [SerializeField] private RectTransform[] appButtons;
    [SerializeField, Min(0.5f)] private float compactAspectThreshold = 1.25f;
    [SerializeField] private Vector2 landscapeWindowMin = new Vector2(0.30f, 0.12f);
    [SerializeField] private Vector2 landscapeWindowMax = new Vector2(0.97f, 0.92f);
    [SerializeField] private Vector2 compactWindowMin = new Vector2(0.035f, 0.12f);
    [SerializeField] private Vector2 compactWindowMax = new Vector2(0.965f, 0.92f);

    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;

    public RectTransform SafeAreaRoot => safeAreaRoot;
    public RectTransform AppWindow => appWindow;

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

        RectTransform rootRect = transform as RectTransform;
        Vector2 rootSize = rootRect != null ? rootRect.rect.size : Vector2.zero;
        Vector2 logicalSafeSize = new Vector2(
            rootSize.x * safe.width / Screen.width,
            rootSize.y * safe.height / Screen.height);
        ApplyAppButtonGrid(compact, logicalSafeSize);
    }

    private void ApplyWindowAnchors(bool compact)
    {
        if (appWindow == null)
            return;

        appWindow.anchorMin = compact ? compactWindowMin : landscapeWindowMin;
        appWindow.anchorMax = compact ? compactWindowMax : landscapeWindowMax;
        appWindow.pivot = new Vector2(0.5f, 0.5f);
        appWindow.offsetMin = new Vector2(10f, 10f);
        appWindow.offsetMax = new Vector2(-10f, -10f);
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

        const float gap = 16f;
        float availableWidth = compact ? safeWidth * 0.92f : safeWidth * 0.27f;
        float buttonWidth = Mathf.Clamp((availableWidth - gap * 3f) * 0.5f, 150f, compact ? 300f : 220f);
        float buttonHeight = Mathf.Clamp(safeHeight * 0.08f, 68f, 88f);
        float left = compact ? safeWidth * 0.04f : 24f;
        float top = compact ? 92f : 82f;

        for (int i = 0; i < appButtons.Length; i++)
        {
            RectTransform button = appButtons[i];
            if (button == null)
                continue;

            int column = i % 2;
            int row = i / 2;
            button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
            button.pivot = new Vector2(0.5f, 0.5f);
            button.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            button.anchoredPosition = new Vector2(
                left + buttonWidth * 0.5f + column * (buttonWidth + gap),
                -(top + buttonHeight * 0.5f + row * (buttonHeight + gap)));
        }
    }
}
