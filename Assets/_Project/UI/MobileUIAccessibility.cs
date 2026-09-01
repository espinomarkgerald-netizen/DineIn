using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mobile-only accessibility pass. It leaves authored reference resolutions intact,
/// makes screen-space canvases fit inside unusual mobile aspect ratios, and gives
/// small controls a larger invisible pointer target without stretching their artwork.
///
/// Reference resolution is part of the coordinate system used by every child
/// RectTransform. Changing it without converting the complete hierarchy enlarges or
/// shrinks fixed-size UI. In particular, converting a 1920 x 1080 canvas to
/// 600 x 337.5 made the Level 1 UI roughly 3.2 times larger on Android.
/// </summary>
public sealed class MobileUIAccessibility : MonoBehaviour
{
    private const string RuntimeName = "[Mobile UI Accessibility]";
    private const string TouchAreaName = "[Mobile Touch Area]";
    private const float ScanInterval = 2f;
    public const float MinimumPhysicalTouchPixels = 72f;
    public const float MinimumPersistentHudPixels = 70f;
    public const float MinimumWorkspaceControlPixels = 58f;
    private static MobileUIAccessibility instance;
    private readonly Dictionary<RectTransform, Vector3> authoredScales =
        new Dictionary<RectTransform, Vector3>();
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isMobilePlatform || instance != null)
            return;

        GameObject root = new GameObject(RuntimeName);
        instance = root.AddComponent<MobileUIAccessibility>();
        DontDestroyOnLoad(root);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + ScanInterval;
        ApplyToLoadedUI();
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        nextScan = 0f;
        ApplyToLoadedUI();
    }

    private void ApplyToLoadedUI()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
            ConfigureCanvasForMobile(scalers[i]);

        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            // Keep authored visuals identical between the Editor/Device Simulator
            // and a player build. Only persistent HUD controls opt into a larger
            // visible size; modal/workspace controls receive an invisible hit area
            // below without changing their RectTransforms or layout elements.
            EnsurePersistentHudVisualSize(buttons[i]);
            EnsureTouchArea(buttons[i]);
        }

        // NewGameMenu and the loading prefab already have authored responsive
        // animation/layout behavior. Do not rewrite their transforms here: doing so
        // can race their open animation and leave the Play or burger art invisible.
    }

    /// <summary>Runs the same pass used by Android for editor scene validation.</summary>
    public void ApplyNowForValidation()
    {
        ApplyToLoadedUI();
    }

    /// <summary>
    /// Applies the mobile aspect-ratio policy without changing the canvas coordinate
    /// system. Public so the editor regression validator can exercise the exact
    /// runtime code used by Android builds.
    /// </summary>
    public static void ConfigureCanvasForMobile(CanvasScaler scaler)
    {
        Canvas canvas = scaler != null ? scaler.GetComponent<Canvas>() : null;
        if (scaler == null || canvas == null || canvas.renderMode == RenderMode.WorldSpace ||
            scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return;

        string canvasName = scaler.gameObject.name;

        // These two screens are already authored and tested for landscape phones.
        // Preserve their CanvasScaler policy exactly so Android matches the Editor
        // device simulator instead of moving animated content outside the viewport.
        Transform canvasRoot = scaler.transform.root;
        bool isLoadingCanvas = canvasRoot != null &&
                               (NameMatches(canvasRoot.name, "LoadingScreen") ||
                                canvasRoot.GetComponentInChildren<BurgerLoadingAnimation>(true) != null);
        if (NameMatches(canvasName, "GameCanvas") || isLoadingCanvas)
            return;

        // These are compact, persistent HUD canvases rather than modal layouts. On a
        // 20:9 phone, width scaling makes their authored 76-82 unit buttons render at
        // approximately 51-55 physical pixels instead of 40-44 pixels. Modal panels
        // remain on the non-cropping policy below.
        if (IsWidthScaledPersistentHud(canvasName))
        {
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            return;
        }

        // The management computer is authored and previewed against this scaler.
        // A second Android-only scaler policy made the final build use a different
        // coordinate system than the Editor and Device Simulator.
        if (NameMatches(canvasName, "ManagementComputerCanvas"))
            return;

        // Expand selects the smaller of the width/height scale factors. The complete
        // authored reference rectangle therefore remains visible on extra-wide phones
        // such as 20:9 devices instead of losing its top or bottom edges.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private static void EnsureTouchArea(Button button)
    {
        if (button == null)
            return;

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null)
            return;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas != null)
            canvas = canvas.rootCanvas;

        // RectTransform dimensions are canvas units, not physical screen pixels.
        // Dividing by scaleFactor keeps the actual Android tap area consistent even
        // on a 1920 x 1080 HUD rendered onto a short 576-pixel-tall phone display.
        float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        float minimumCanvasUnits = MinimumCanvasTouchSizeForScale(canvasScale);

        Rect rect = buttonRect.rect;
        float extraX = Mathf.Max(0f, minimumCanvasUnits - rect.width) * 0.5f;
        float extraY = Mathf.Max(0f, minimumCanvasUnits - rect.height) * 0.5f;

        Transform existing = button.transform.Find(TouchAreaName);
        if (extraX <= 0f && extraY <= 0f && existing == null)
            return;

        GameObject area;
        RectTransform areaRect;
        Image image;
        if (existing == null)
        {
            area = new GameObject(TouchAreaName, typeof(RectTransform), typeof(Image));
            areaRect = area.GetComponent<RectTransform>();
            areaRect.SetParent(button.transform, false);
            areaRect.SetAsFirstSibling();
            image = area.GetComponent<Image>();
        }
        else
        {
            area = existing.gameObject;
            areaRect = existing as RectTransform;
            image = area.GetComponent<Image>();
        }

        if (areaRect == null || image == null)
            return;

        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(-extraX, -extraY);
        areaRect.offsetMax = new Vector2(extraX, extraY);

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }

    private static bool IsWidthScaledPersistentHud(string canvasName)
    {
        return NameMatches(canvasName, "PlayerTaskHUD") ||
               NameMatches(canvasName, "CasualDiningProgressHUD") ||
               NameMatches(canvasName, "LobbyPauseMenu");
    }

    private static bool NameMatches(string runtimeName, string authoredName)
    {
        return runtimeName == authoredName || runtimeName.StartsWith(authoredName + "(");
    }

    /// <summary>Converts the mobile physical-pixel target into the active canvas units.</summary>
    public static float MinimumCanvasTouchSizeForScale(float canvasScale)
    {
        return MinimumPhysicalTouchPixels / Mathf.Max(0.01f, canvasScale);
    }

    private static void EnsurePersistentHudVisualSize(Button button)
    {
        if (button == null || button.transform is not RectTransform rect)
            return;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas != null)
            canvas = canvas.rootCanvas;
        if (canvas == null)
            return;

        float minimumPixels = IsWidthScaledPersistentHud(canvas.name)
            ? MinimumPersistentHudPixels
            : 0f;
        if (minimumPixels <= 0f)
            return;

        Vector2 physicalSize = GetPhysicalSize(rect, canvas);
        Vector2 targetSize = rect.rect.size;
        if (physicalSize.x > 0f && physicalSize.x < minimumPixels)
            targetSize.x *= minimumPixels / physicalSize.x;
        if (physicalSize.y > 0f && physicalSize.y < minimumPixels)
            targetSize.y *= minimumPixels / physicalSize.y;

        if (targetSize == rect.rect.size)
            return;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetSize.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetSize.y);

        // Layout groups otherwise overwrite SetSizeWithCurrentAnchors on their next
        // rebuild. Preserve the mobile minimum in the element they consult.
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minWidth = Mathf.Max(layout.minWidth, targetSize.x);
            layout.minHeight = Mathf.Max(layout.minHeight, targetSize.y);
            if (layout.preferredWidth >= 0f)
                layout.preferredWidth = Mathf.Max(layout.preferredWidth, targetSize.x);
            if (layout.preferredHeight >= 0f)
                layout.preferredHeight = Mathf.Max(layout.preferredHeight, targetSize.y);
        }
    }

    private static Vector2 GetPhysicalSize(RectTransform rect, Canvas canvas)
    {
        float scaleX = Mathf.Max(0.01f, canvas.scaleFactor);
        float scaleY = scaleX;
        Transform current = rect;
        while (current != null && current != canvas.transform)
        {
            scaleX *= Mathf.Abs(current.localScale.x);
            scaleY *= Mathf.Abs(current.localScale.y);
            current = current.parent;
        }

        return new Vector2(rect.rect.width * scaleX, rect.rect.height * scaleY);
    }

    private static bool HasAncestorComponent<T>(Transform current) where T : Component
    {
        while (current != null)
        {
            if (current.GetComponent<T>() != null)
                return true;
            current = current.parent;
        }

        return false;
    }

    private void ApplyFullScreenPanelPolicy(RectTransform[] rects)
    {
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform panel = rects[i];
            if (panel == null ||
                (panel.name != "GamemodePopUpUI" && panel.name != "ShopPanelUI"))
                continue;

            ConfigureFullScreenPanelForMobile(panel);

            // An open full-screen panel must cover the base GameMenu controls too.
            if (panel.gameObject.activeInHierarchy)
                panel.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Normalizes the popup root, preserves the artwork's authored motif scale, and
    /// enlarges only the interactive content frame for a landscape phone.
    /// </summary>
    public static void ConfigureFullScreenPanelForMobile(RectTransform panel)
    {
        if (panel == null)
            return;

        StretchToParent(panel);
        for (int childIndex = 0; childIndex < panel.childCount; childIndex++)
        {
            if (panel.GetChild(childIndex) is not RectTransform child)
                continue;

            if (child.name == "Background")
                CenterAuthoredBackdrop(child);
            else if (child.name == "Foreground")
            {
                if (child.sizeDelta.x > 1000f || child.sizeDelta.y > 700f)
                    CenterAuthoredBackdrop(child);
                else
                    StretchToParent(child);
            }
        }

        if (panel.name == "GamemodePopUpUI")
            ConfigureGameModeContent(panel);
        else if (panel.name == "ShopPanelUI")
            ConfigureShopContent(panel);
    }

    private static void ConfigureGameModeContent(RectTransform panel)
    {
        RectTransform title = FindDescendant(panel, "TitleFrame");
        RectTransform campaign = FindDescendant(panel, "CampaignButton");
        RectTransform multiplayer = FindDescendant(panel, "MultiplayerButton");
        RectTransform close = FindDescendant(panel, "CancelButton ");

        ConfigureScaledCenteredControl(title, new Vector2(0f, 80f), 2.1f);
        ConfigureScaledCenteredControl(campaign, new Vector2(-300f, -128f), 2.1f);
        ConfigureScaledCenteredControl(multiplayer, new Vector2(318f, -128f), 2.1f);
        ConfigureCornerButton(close, new Vector2(54f, -48f));
    }

    private static void ConfigureShopContent(RectTransform panel)
    {
        RectTransform scroll = FindDescendant(panel, "Vertical Scroll");
        if (scroll != null)
            scroll.localScale = new Vector3(1.42f, 1.42f, 1f);

        ConfigureCornerButton(FindDescendant(panel, "CloseButton"), new Vector2(54f, -48f));
    }

    private static void ConfigureScaledCenteredControl(
        RectTransform rect,
        Vector2 position,
        float scale)
    {
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = new Vector3(scale, scale, 1f);
    }

    private static void ConfigureCornerButton(RectTransform rect, Vector2 position)
    {
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(72f, 72f);
    }

    private static RectTransform FindDescendant(RectTransform root, string objectName)
    {
        if (root == null)
            return null;

        RectTransform[] descendants = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
                return descendants[i];
        }

        return null;
    }

    private void ApplyNewGameMenuPolicy(RectTransform[] rects)
    {
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || !IsUnderCanvas(rect, "GameCanvas"))
                continue;

            if (rect.name == "RestaurantSelectorButtonsUI")
            {
                SetAuthoredScaleMultiplier(rect, new Vector3(2.1f, 2.1f, 1f));
            }
            else if (rect.name == "MoneyUI")
            {
                SetAuthoredScaleMultiplier(rect, new Vector3(1.2f, 1.2f, 1f));
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -105f);
            }
            else if (rect.name == "ShopButton")
            {
                EnsureVisibleSize(rect, 72f, 72f);
            }
            else if (rect.name == "BackButton" &&
                     rect.parent != null && rect.parent.name == "GameCanvas")
            {
                // This control was authored as center-minus-353, which becomes x=300
                // when Expand widens the logical canvas on a 20:9 phone. Preserve the
                // intended 47-unit left inset instead.
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(52f, -44f);
                EnsureVisibleSize(rect, 72f, 72f);
            }
        }
    }

    private void ApplyLoadingScreenPolicy()
    {
        BurgerLoadingAnimation[] animations = FindObjectsByType<BurgerLoadingAnimation>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < animations.Length; i++)
        {
            BurgerLoadingAnimation animation = animations[i];
            if (animation == null || animation.transform is not RectTransform burger)
                continue;

            SetAuthoredScaleMultiplier(burger, new Vector3(1.8f, 1.8f, 1f));

            Canvas canvas = animation.GetComponentInParent<Canvas>();
            Slider slider = canvas != null ? canvas.GetComponentInChildren<Slider>(true) : null;
            if (slider != null && slider.transform is RectTransform sliderRect)
                SetAuthoredScaleMultiplier(sliderRect, new Vector3(1.3f, 2f, 1f));
        }
    }

    private void SetAuthoredScaleMultiplier(RectTransform rect, Vector3 multiplier)
    {
        if (!authoredScales.TryGetValue(rect, out Vector3 authoredScale))
        {
            authoredScale = rect.localScale;
            authoredScales.Add(rect, authoredScale);
        }

        rect.localScale = Vector3.Scale(authoredScale, multiplier);
    }

    private static bool IsUnderCanvas(RectTransform rect, string canvasName)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        return canvas != null && canvas.name == canvasName;
    }

    private static void EnsureVisibleSize(RectTransform rect, float minimumWidth, float minimumHeight)
    {
        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Max(size.x, minimumWidth);
        size.y = Mathf.Max(size.y, minimumHeight);
        rect.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void CenterAuthoredBackdrop(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one;
    }
}
