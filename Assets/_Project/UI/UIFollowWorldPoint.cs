using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Follow after camera LateUpdate so camera-front bubbles cannot straddle the near clip plane.
[DefaultExecutionOrder(12000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIFollowWorldPoint : MonoBehaviour
{
    private enum PresentationSpace
    {
        WorldSpace,
        ScreenSpace
    }

    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Presentation")]
    [SerializeField] private PresentationSpace presentationSpace = PresentationSpace.WorldSpace;
    [SerializeField] private bool preserveScreenSize = true;
    [SerializeField, Min(0.01f)] private float worldUnitsPerUiUnit = 0.01f;
    [SerializeField, Min(0.1f)] private float visualScale = 1f;
    [SerializeField, Min(1f)] private float mobileVisualScale = 1.8f;
    [SerializeField, Min(0f)] private float mobileSafeAreaPadding = 18f;
    [SerializeField, Min(0f)] private float cameraDepthOffset = 0.1f;
    [SerializeField, Min(1f)] private float dynamicPixelsPerUnit = 10f;
    [SerializeField] private int baseSortingOrder = 100;

    [Header("Screen Offset")]
    [SerializeField] private Vector2 screenOffset;
    [SerializeField] private float stackStepY = 40f;

    [Header("Above Target Layout")]
    [SerializeField] private bool placeAboveTarget;
    [SerializeField] private float edgeGapPixels = 8f;
    [SerializeField, Min(0f)] private float stackGapPixels = 6f;
    [SerializeField] private int stackPriority;

    [Tooltip("Keep the bubble attached to its world anchor; never move it to avoid HUD or other targets.")]
    [SerializeField] private bool stickToTarget = true;

    [Header("Screen Clearance")]
    [SerializeField] private bool avoidBubbleOverlaps = true;
    [SerializeField, Range(0f, .3f)] private float topHudClearance = .16f;
    [SerializeField, Range(0f, .3f)] private float bottomHudClearance = .08f;

    [Header("Customer Bubble Presentation")]
    [SerializeField] private bool preventSceneClipping = true;
    [SerializeField, Min(0f)] private float revealDuration = 0.22f;
    [SerializeField, Range(0.7f, 1f)] private float revealStartScale = 0.88f;
    private float revealElapsed;
    private float revealProgress;

    [Header("Block When UI Open")]
    [SerializeField] private bool hideWhenGameplayUIBlocked = true;

    private RectTransform rect;
    private Camera cam;
    private int stackIndex;
    private CanvasGroup canvasGroup;
    private Canvas worldCanvas;
    private Vector3 authoredScale = Vector3.one;
    private float sourceCanvasScaleFactor = 1f;
    private bool worldSpaceInitialized;
    private bool receivesPointerInput;
    private Graphic[] visualGraphics;
    private readonly Vector3[] graphicWorldCorners = new Vector3[4];

    public bool IsWorldSpace => worldSpaceInitialized;
    public void KeepAttachedToTarget()
    {
        stickToTarget = true;
        avoidBubbleOverlaps = false;
    }
    private float taskAlpha = 1f;
    private bool taskInteractable = true;
    public void SetTaskFocus(float alpha, bool interactable)
    { taskAlpha = Mathf.Clamp01(alpha); taskInteractable = interactable; }
    public void RefreshVisualBounds() => visualGraphics = GetComponentsInChildren<Graphic>(true);

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        authoredScale = rect.localScale;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Prefabs and scene-authored placeholders do not have a target during
        // Awake. Hide them immediately so they cannot render at their authored
        // canvas size for a frame before Init/LateUpdate positions them.
        SetVisible(false);
    }

    public void Init(Transform followTarget, Vector3 offset, Camera followCam)
    {
        target = followTarget;
        worldOffset = offset;
        cam = followCam != null ? followCam : Camera.main;
        revealElapsed = 0f;
        revealProgress = 0f;

        if (presentationSpace == PresentationSpace.WorldSpace)
            InitializeWorldSpace();
        WorldBubbleStackLayout.Register(this);
    }

    public void InitAboveTarget(
        Transform followTarget,
        Vector3 anchorOffset,
        Camera followCam,
        float gapPixels = 8f,
        int priority = 0,
        float gapBetweenBubblesPixels = 6f)
    {
        placeAboveTarget = true;
        edgeGapPixels = gapPixels;
        stackPriority = priority;
        stackGapPixels = Mathf.Max(0f, gapBetweenBubblesPixels);
        WorldBubbleStackLayout.Register(this);
        Init(followTarget, anchorOffset, followCam);
    }

    public void SetScreenOffset(Vector2 offset)
    {
        screenOffset = offset;
    }

    public void SetAboveTargetGap(float gapPixels)
    {
        edgeGapPixels = gapPixels;
    }

    public void SetStackIndex(int index)
    {
        stackIndex = Mathf.Max(0, index);
    }

    private void OnEnable()
    {
        revealElapsed = 0f;
        revealProgress = 0f;
        SetVisible(false);
        WorldBubbleStackLayout.Register(this);
    }

    private void OnDisable()
    {
        WorldBubbleStackLayout.Unregister(this);
    }

    private void OnDestroy()
    {
        WorldBubbleStackLayout.Unregister(this);
    }

    private void LateUpdate()
    {
        if (MultiplayerRestockView.Active) { SetVisible(false); return; }
        if (target == null)
        {
            SetVisible(false);
            return;
        }

        if (hideWhenGameplayUIBlocked && GameplayUIBlocker.IsBlocked())
        {
            SetVisible(false);
            return;
        }

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
        {
            SetVisible(false);
            return;
        }

        UpdateReveal();
        if (worldSpaceInitialized)
            UpdateWorldSpacePose();
        else
            UpdateScreenSpacePose();
    }

    private void UpdateReveal()
    {
        if (!placeAboveTarget || LevelOneUIAccessibility.ReducedMotion || revealDuration <= 0f)
        {
            revealProgress = 1f;
            return;
        }

        revealElapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
        revealProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(revealElapsed / revealDuration));
    }

    private float RevealScale => placeAboveTarget
        ? Mathf.Lerp(revealStartScale, 1f, revealProgress)
        : 1f;

    private void InitializeWorldSpace()
    {
        if (worldSpaceInitialized || rect == null)
            return;

        Canvas sourceCanvas = GetComponentInParent<Canvas>();
        authoredScale = rect.localScale;
        if (sourceCanvas == null)
            sourceCanvas = UIRoot.GameplayCanvasOrNull();

        if (sourceCanvas != null)
            sourceCanvasScaleFactor = Mathf.Max(0.01f, DineIn.NewMenu.SettingsManager.UnscaledCanvasFactor(sourceCanvas));

        Scene targetScene = target != null ? target.gameObject.scene : gameObject.scene;
        if (rect.parent != null)
            rect.SetParent(null, false);

        if (targetScene.IsValid() && targetScene.isLoaded && gameObject.scene != targetScene)
            SceneManager.MoveGameObjectToScene(gameObject, targetScene);

        Transform runtimeRoot = WorldBubbleRuntimeRoot.GetOrCreate(targetScene);
        rect.SetParent(runtimeRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = gameObject.AddComponent<Canvas>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = cam;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = WorldBubbleRuntimeRoot.NextSortingOrder(baseSortingOrder);

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = dynamicPixelsPerUnit;

        receivesPointerInput = GetComponentInChildren<Selectable>(true) != null;
        visualGraphics = GetComponentsInChildren<Graphic>(true);
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();

        if (receivesPointerInput && raycaster == null)
            raycaster = gameObject.AddComponent<GraphicRaycaster>();

        if (raycaster != null)
            raycaster.enabled = receivesPointerInput;

        worldSpaceInitialized = true;
        UpdateWorldSpacePose();
    }

    private void UpdateScreenSpacePose()
    {
        Vector3 screenPos = cam.WorldToScreenPoint(target.position + worldOffset);

        if (screenPos.z < 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        screenPos.x += screenOffset.x;
        screenPos.y += screenOffset.y + (stackIndex * stackStepY);
        rect.position = screenPos;
        rect.localScale = authoredScale * RevealScale;
    }

    private void UpdateWorldSpacePose()
    {
        // Init can run while a station has disabled the lobby's MainCamera.
        // LateUpdate acquires the camera and reveals the bubble on return.
        if (cam == null || target == null)
        {
            SetVisible(false);
            return;
        }
        Vector3 anchorPosition = target.position + worldOffset;
        Vector3 viewportPosition = cam.WorldToViewportPoint(anchorPosition);

        if (viewportPosition.z < 0f || (stickToTarget &&
            (viewportPosition.x < 0f || viewportPosition.x > 1f || viewportPosition.y < 0f || viewportPosition.y > 1f)))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        if (worldCanvas != null && worldCanvas.worldCamera != cam)
            worldCanvas.worldCamera = cam;

        float scale = ResolveWorldScale(anchorPosition);
        Vector3 cameraSpaceOffset = placeAboveTarget
            ? ResolveAboveTargetOffset(anchorPosition, scale)
            : ResolveStandardCameraOffset(scale);
        // Camera-front bubbles already have a safe depth. Moving the anchor toward
        // a perspective camera before projection would shift its screen position.
        Vector3 depthOffset = placeAboveTarget && preventSceneClipping
            ? Vector3.zero : -cam.transform.forward * cameraDepthOffset;

        Vector3 worldPosition = anchorPosition + cameraSpaceOffset + depthOffset;
        if (!stickToTarget && avoidBubbleOverlaps)
        {
            Vector2 correction = WorldBubbleStackLayout.GetScreenCorrection(this, cam);
            float pixelScale = ResolveWorldUnitsPerScreenPixel(anchorPosition);
            worldPosition += (cam.transform.right * correction.x + cam.transform.up * correction.y) * pixelScale;
        }
        else if (!stickToTarget && Application.isMobilePlatform)
            worldPosition = ClampWorldPositionToSafeArea(worldPosition, anchorPosition);

        // Canvas sorting cannot prevent walls from depth-clipping world UI.
        // Keep the screen position and pixel size on a camera-front plane.
        if (placeAboveTarget && preventSceneClipping)
        {
            Vector3 screen = cam.WorldToScreenPoint(worldPosition);
            float presentationDepth = cam.nearClipPlane + 0.1f;
            if (!cam.orthographic && screen.z > 0f)
                scale *= presentationDepth / screen.z;
            screen.z = presentationDepth;
            worldPosition = cam.ScreenToWorldPoint(screen);
        }

        rect.SetPositionAndRotation(worldPosition, cam.transform.rotation);
        rect.localScale = authoredScale * scale * RevealScale;
    }

    private Vector3 ClampWorldPositionToSafeArea(Vector3 worldPosition, Vector3 anchorPosition)
    {
        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
            return worldPosition;

        Rect safe = Screen.safeArea;
        float minX = safe.xMin + mobileSafeAreaPadding;
        float maxX = safe.xMax - mobileSafeAreaPadding;
        float minY = safe.yMin + mobileSafeAreaPadding;
        float maxY = safe.yMax - mobileSafeAreaPadding;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(screen.x, minX, maxX),
            Mathf.Clamp(screen.y, minY, maxY));
        Vector2 delta = clamped - new Vector2(screen.x, screen.y);
        float unitsPerPixel = ResolveWorldUnitsPerScreenPixel(anchorPosition);
        return worldPosition +
               cam.transform.right * (delta.x * unitsPerPixel) +
               cam.transform.up * (delta.y * unitsPerPixel);
    }

    private Vector3 ResolveStandardCameraOffset(float scale)
    {
        float verticalOffset = screenOffset.y + (stackIndex * stackStepY);
        return (cam.transform.right * screenOffset.x + cam.transform.up * verticalOffset) * scale;
    }

    private Vector3 ResolveAboveTargetOffset(Vector3 anchorPosition, float scale)
    {
        float unitsPerPixel = ResolveWorldUnitsPerScreenPixel(anchorPosition);
        Bounds visualBounds = ResolveLocalVisualBounds();
        float stackOffsetPixels = WorldBubbleStackLayout.GetOffsetPixels(this, anchorPosition);
        float lowerEdgeWorld = visualBounds.min.y * authoredScale.y * scale;
        float verticalPixels = edgeGapPixels + stackOffsetPixels + screenOffset.y;
        float verticalWorld = verticalPixels * unitsPerPixel - lowerEdgeWorld;
        float horizontalWorld = screenOffset.x * unitsPerPixel;

        return cam.transform.right * horizontalWorld + cam.transform.up * verticalWorld;
    }

    private float ResolveWorldScale(Vector3 anchorPosition)
    {
        float platformScale = (Application.isMobilePlatform ? mobileVisualScale : 1f) * DineIn.NewMenu.SettingsManager.UIScaleMultiplier;
        if (!preserveScreenSize)
            return worldUnitsPerUiUnit * visualScale * platformScale;

        return ResolveWorldUnitsPerScreenPixel(anchorPosition) * sourceCanvasScaleFactor *
               visualScale * platformScale;
    }

    private float ResolveWorldUnitsPerScreenPixel(Vector3 anchorPosition)
    {
        if (cam == null)
            return worldUnitsPerUiUnit;

        int pixelHeight = cam.pixelHeight > 0
            ? cam.pixelHeight
            : Mathf.Max(1, Screen.height);
        float unitsPerPixel;

        if (cam.orthographic)
        {
            unitsPerPixel = (cam.orthographicSize * 2f) / pixelHeight;
        }
        else
        {
            float distance = Mathf.Max(
                0.01f,
                Vector3.Dot(anchorPosition - cam.transform.position, cam.transform.forward));
            float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            unitsPerPixel = viewHeight / pixelHeight;
        }

        return unitsPerPixel;
    }

    private Bounds ResolveLocalVisualBounds()
    {
        bool foundVisibleGraphic = false;
        Bounds visibleBounds = default;

        if (visualGraphics != null)
        {
            for (int i = 0; i < visualGraphics.Length; i++)
            {
                Graphic graphic = visualGraphics[i];
                if (graphic == null ||
                    !graphic.enabled ||
                    !graphic.gameObject.activeInHierarchy ||
                    graphic.color.a <= 0.001f)
                {
                    continue;
                }

                RectTransform graphicRect = graphic.rectTransform;
                graphicRect.GetWorldCorners(graphicWorldCorners);
                for (int cornerIndex = 0; cornerIndex < graphicWorldCorners.Length; cornerIndex++)
                {
                    Vector3 localCorner = rect.InverseTransformPoint(graphicWorldCorners[cornerIndex]);
                    if (!foundVisibleGraphic)
                    {
                        visibleBounds = new Bounds(localCorner, Vector3.zero);
                        foundVisibleGraphic = true;
                    }
                    else
                    {
                        visibleBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        if (foundVisibleGraphic && visibleBounds.size.y > Mathf.Epsilon)
            return visibleBounds;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, rect);
        if (bounds.size.y > Mathf.Epsilon)
            return bounds;

        Rect localRect = rect.rect;
        return new Bounds(localRect.center, localRect.size);
    }

    internal bool TryGetScreenRect(Camera camera, out Rect bounds, out Rect safe)
    {
        bounds = safe = default;
        if (stickToTarget || !avoidBubbleOverlaps || !worldSpaceInitialized || cam != camera || target == null ||
            !isActiveAndEnabled || taskAlpha <= .001f) return false;
        Vector3 anchor = target.position + worldOffset;
        Vector3 projected = camera.WorldToViewportPoint(anchor);
        if (projected.z <= 0 || projected.x < 0 || projected.x > 1 || projected.y < 0 || projected.y > 1) return false;
        float scale = ResolveWorldScale(anchor);
        Vector3 offset = placeAboveTarget ? ResolveAboveTargetOffset(anchor, scale) : ResolveStandardCameraOffset(scale);
        Vector3 pixel = camera.WorldToScreenPoint(anchor + offset);
        Bounds visual = ResolveLocalVisualBounds();
        float ratio = scale / Mathf.Max(.000001f, ResolveWorldUnitsPerScreenPixel(anchor));
        Vector2 size = new Vector2(visual.size.x * Mathf.Abs(authoredScale.x), visual.size.y * Mathf.Abs(authoredScale.y)) * ratio;
        Vector2 min = new Vector2(pixel.x + visual.min.x * authoredScale.x * ratio, pixel.y + visual.min.y * authoredScale.y * ratio);
        bounds = new Rect(min, size);
        safe = camera.pixelRect;
        if (Application.isMobilePlatform)
            safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, Screen.safeArea.xMin), Mathf.Max(safe.yMin, Screen.safeArea.yMin),
                Mathf.Min(safe.xMax, Screen.safeArea.xMax), Mathf.Min(safe.yMax, Screen.safeArea.yMax));
        float height = safe.height;
        safe = Rect.MinMaxRect(safe.xMin + 8, safe.yMin + height * bottomHudClearance,
            safe.xMax - 8, safe.yMax - height * topHudClearance);
        return size.x > 0 && size.y > 0;
    }
    internal bool CanStackAbove(Transform followTarget)
    {
        return placeAboveTarget &&
            target == followTarget &&
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            taskAlpha > 0.001f;
    }

    internal bool SortsBefore(UIFollowWorldPoint other)
    {
        if (stackPriority != other.stackPriority)
            return stackPriority < other.stackPriority;

        return GetInstanceID() < other.GetInstanceID();
    }

    internal float GetVisualHeightPixels(Vector3 anchorPosition)
    {
        float unitsPerPixel = Mathf.Max(0.000001f, ResolveWorldUnitsPerScreenPixel(anchorPosition));
        float worldHeight = ResolveLocalVisualBounds().size.y *
            Mathf.Abs(authoredScale.y) * ResolveWorldScale(anchorPosition);
        return worldHeight / unitsPerPixel;
    }

    internal float StackGapPixels => stackGapPixels;

    internal Transform FollowTarget => target;

    private void SetVisible(bool value)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = value ? taskAlpha * (placeAboveTarget ? revealProgress : 1f) : 0f;
        canvasGroup.blocksRaycasts = value && taskInteractable && (!worldSpaceInitialized || receivesPointerInput);
        canvasGroup.interactable = value && taskInteractable && (!worldSpaceInitialized || receivesPointerInput);
    }
}

internal static class WorldBubbleStackLayout
{
    private static readonly List<UIFollowWorldPoint> Followers = new();

    private static int layoutFrame = -1;
    private static Camera layoutCamera;
    private static readonly Dictionary<UIFollowWorldPoint, Vector2> Corrections = new();
    private static readonly List<Rect> Placed = new();
    private static readonly List<UIFollowWorldPoint> Sorted = new();

    public static void Register(UIFollowWorldPoint follower)
    {
        if (follower != null && !Followers.Contains(follower))
        { Followers.Add(follower); layoutFrame = -1; }
    }

    public static void Unregister(UIFollowWorldPoint follower)
    {
        if (follower != null)
        { Followers.Remove(follower); layoutFrame = -1; }
    }

    // One deterministic placement pass per frame; no bubble is hidden to make space.
    public static Vector2 GetScreenCorrection(UIFollowWorldPoint follower, Camera camera)
    {
        if (layoutFrame != Time.frameCount || layoutCamera != camera)
        {
            layoutFrame = Time.frameCount; layoutCamera = camera;
            Corrections.Clear(); Placed.Clear(); Sorted.Clear();
            foreach (var item in Followers) if (item != null) Sorted.Add(item);
            Sorted.Sort((a, b) => a == b ? 0 : a.SortsBefore(b) ? -1 : 1);
            foreach (var item in Sorted)
            {
                if (!item.TryGetScreenRect(camera, out Rect desired, out Rect safe)) continue;
                Rect best = desired; float bestScore = float.PositiveInfinity;
                for (int row = 0; row < 4; row++)
                for (int column = -2; column <= 2; column++)
                {
                    Rect candidate = desired;
                    candidate.position += new Vector2(column * (desired.width + 8), row * (desired.height + 8));
                    candidate.x = Mathf.Clamp(candidate.x, safe.xMin, Mathf.Max(safe.xMin, safe.xMax - candidate.width));
                    candidate.y = Mathf.Clamp(candidate.y, safe.yMin, Mathf.Max(safe.yMin, safe.yMax - candidate.height));
                    float overlap = 0;
                    foreach (Rect placed in Placed)
                        overlap += Mathf.Max(0, Mathf.Min(candidate.xMax + 4, placed.xMax + 4) - Mathf.Max(candidate.xMin - 4, placed.xMin - 4)) *
                                   Mathf.Max(0, Mathf.Min(candidate.yMax + 4, placed.yMax + 4) - Mathf.Max(candidate.yMin - 4, placed.yMin - 4));
                    float score = overlap * 10000 + (candidate.position - desired.position).sqrMagnitude;
                    if (score < bestScore) { best = candidate; bestScore = score; }
                }
                Placed.Add(best); Corrections[item] = best.position - desired.position;
            }
        }
        return Corrections.TryGetValue(follower, out var correction) ? correction : Vector2.zero;
    }
    public static float GetOffsetPixels(UIFollowWorldPoint follower, Vector3 anchorPosition)
    {
        if (follower == null || follower.FollowTarget == null)
            return 0f;

        float offsetPixels = 0f;
        for (int i = Followers.Count - 1; i >= 0; i--)
        {
            UIFollowWorldPoint candidate = Followers[i];
            if (candidate == null)
            {
                Followers.RemoveAt(i);
                continue;
            }

            if (candidate == follower ||
                !candidate.CanStackAbove(follower.FollowTarget) ||
                !candidate.SortsBefore(follower))
            {
                continue;
            }

            offsetPixels += candidate.GetVisualHeightPixels(anchorPosition);
            offsetPixels += Mathf.Max(candidate.StackGapPixels, follower.StackGapPixels);
        }

        return offsetPixels;
    }
}

internal static class WorldBubbleRuntimeRoot
{
    private const string RootName = "[World Bubbles]";
    private static int sortingOffset;

    public static Transform GetOrCreate(Scene scene)
    {
        if (scene.IsValid() && scene.isLoaded)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == RootName)
                    return roots[i].transform;
            }
        }

        GameObject root = new GameObject(RootName);
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.MoveGameObjectToScene(root, scene);

        return root.transform;
    }

    public static int NextSortingOrder(int baseOrder)
    {
        int order = baseOrder + sortingOffset;
        sortingOffset = (sortingOffset + 1) % 1000;
        return order;
    }
}
