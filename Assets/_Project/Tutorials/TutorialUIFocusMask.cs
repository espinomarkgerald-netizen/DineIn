using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One tutorial-owned overlay that follows live UI and moves between targets.</summary>
[DisallowMultipleComponent]
public sealed class TutorialUIFocusMask : MaskableGraphic
{
    [SerializeField] private Vector2 padding = new Vector2(8f, 8f);
    [SerializeField] private Color focusColor = new Color(1f, 0.8f, 0.15f, 1f);
    [SerializeField] private float borderWidth = 3f;
    [SerializeField, Min(0f)] private float transitionDuration = 0.24f;
    [SerializeField, Range(0f, .5f)] private float pulseAmount = .22f;
    [SerializeField, Min(.1f)] private float pulseSpeed = 2.4f;
    [Tooltip("Outward border growth as a fraction of the target's shorter side. The input opening stays fixed.")]
    [SerializeField, Range(0f, .08f)] private float pulseExpansion = .03f;

    public void ConfigurePulse(float amount, float speed, float expansion)
    {
        pulseAmount = Mathf.Clamp(amount, 0f, .5f);
        pulseSpeed = Mathf.Max(.1f, speed);
        pulseExpansion = Mathf.Clamp(expansion, 0f, .08f);
    }

    public void ConfigureTransition(float duration) => transitionDuration = Mathf.Max(0f, duration);

    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform target;
    private RectTransform worldProxy;
    private Transform projectedWorldTarget;

    public void SetWorldProjection(RectTransform proxy, Transform worldTarget)
    {
        worldProxy = proxy;
        projectedWorldTarget = worldTarget;
    }
    private Rect focusRect;
    private bool allowTargetInput;
    private bool hasFocus;
    private bool transitioning;
    public bool GesturePassThrough { get; set; }
    private Coroutine transitionRoutine;
    private bool gestureBlocked;
    private bool dialogueInput;

    public void SetDialogueInput(bool enabled)
    {
        dialogueInput = enabled;
        if (enabled) gameObject.SetActive(true);
        raycastTarget = enabled || hasFocus;
        SetVerticesDirty();
    }
    private RectTransform[] clipParents = Array.Empty<RectTransform>();
    private CanvasGroup[] targetGroups = Array.Empty<CanvasGroup>();

    public RectTransform CurrentTarget => target;
    public Rect FocusRect => focusRect;
    public bool IsVisible => isActiveAndEnabled && hasFocus;

    public bool TryGetFocusPoint(RectTransform source, out Vector2 screen)
    {
        screen = default;
        if (source != target) return false;
        RefreshFocus();
        if (!hasFocus) return false;
        Canvas owner = canvas != null ? canvas.rootCanvas : null;
        Camera camera = owner == null || owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
        screen = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(focusRect.center));
        return true;
    }

    public static TutorialUIFocusMask Create(Transform parent)
    {
        var go = new GameObject("TutorialUIFocusMask", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var overlayCanvas = go.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 32760;
        var mask = go.AddComponent<TutorialUIFocusMask>();
        mask.color = new Color(0f, 0f, 0f, 0.68f);
        mask.Hide();
        return mask;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.preWillRenderCanvases += RefreshFocus;
        Canvas.willRenderCanvases += RefreshFocus;
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= RefreshFocus;
        Canvas.preWillRenderCanvases -= RefreshFocus;
        target = null;
        transitioning = false;
        hasFocus = false;
        raycastTarget = false;
        base.OnDisable();
    }

    public void ApplyDebugPadding(float value)
    {
        padding = Vector2.one * Mathf.Clamp(value, 0f, 40f);
    }

    public void Show(RectTransform focusTarget, bool targetMayBeClicked)
    {
        gestureBlocked = false;
        GesturePassThrough = false;
        StopTransition();
        if (focusTarget == null) { Hide(); return; }
        gameObject.SetActive(true);
        target = focusTarget;
        CacheClipping(target);
        allowTargetInput = targetMayBeClicked;
        TutorialUIAutoScroller.ForceLayout(target);
        if (TryCalculateRect(target, out Rect next))
        {
            focusRect = next;
            hasFocus = true;
            raycastTarget = true;
            SetVerticesDirty();
        }
        else
        {
            // HUD alpha/layout can settle after step entry. Retain the actual
            // target and retry at render time, while showing no stale opening.
            hasFocus = false;
            raycastTarget = dialogueInput;
            SetVerticesDirty();
        }
    }

    /// <summary>Retains a live visual source for the next transition, with input blocked.</summary>
    public void Hold()
    {
        StopTransition();
        gestureBlocked = false;
        allowTargetInput = false;
        if (target == null) { Hide(); return; }
        // Step bindings can temporarily restore/reveal HUD ancestors in this
        // same frame. Validate at render time and TransitionTo, after rebinding,
        // rather than discarding the source during that synchronous cleanup.
        raycastTarget = true;
        SetVerticesDirty();
    }

    public void BlockGesture()
    {
        StopTransition();
        gameObject.SetActive(true);
        gestureBlocked = true;
        // Keep valid geometry visible through dismissal, while Raycast consumes
        // the entire gesture. Hold/TransitionTo will validate it again afterward.
        if (target == null || !TryCalculateRect(target, out focusRect)) hasFocus = false;
        raycastTarget = true;
        SetVerticesDirty();
    }

    public void TransitionTo(RectTransform focusTarget, bool targetMayBeClicked, Action onReady)
    {
        gestureBlocked = false;
        StopTransition();
        RectTransform sourceTarget = target;
        CanvasGroup[] sourceGroups = targetGroups;
        RectTransform[] sourceClips = clipParents;
        bool sourceValid = hasFocus && sourceTarget != null && TryCalculateRect(sourceTarget, out _);
        CacheClipping(focusTarget);
        if (focusTarget == null)
        {
            Hide();
            onReady?.Invoke();
            return;
        }

        gameObject.SetActive(true);
        TutorialUIAutoScroller.ForceLayout(focusTarget);
        if (!TryCalculateRect(focusTarget, out Rect destination))
        {
            target = focusTarget;
            allowTargetInput = targetMayBeClicked;
            hasFocus = false;
            raycastTarget = dialogueInput;
            SetVerticesDirty();
            onReady?.Invoke();
            return;
        }

        if (!sourceValid || !Application.isPlaying || LevelOneUIAccessibility.ReducedMotion ||
            transitionDuration <= 0f)
        {
            focusRect = destination;
            hasFocus = true;
            target = focusTarget;
            allowTargetInput = targetMayBeClicked;
            raycastTarget = true;
            SetVerticesDirty();
            onReady?.Invoke();
            return;
        }

        target = null;
        allowTargetInput = false;
        transitioning = true;
        raycastTarget = true;
        transitionRoutine = StartCoroutine(TransitionRoutine(
            focusRect, destination, sourceTarget, sourceGroups, sourceClips, focusTarget, targetMayBeClicked, onReady));
    }

    public void Hide()
    {
        dialogueInput = false;
        gestureBlocked = false;
        StopTransition();
        target = null;
        hasFocus = false;
        allowTargetInput = false;
        raycastTarget = false;
        canvasRenderer.Clear();
        gameObject.SetActive(false);
    }

    private IEnumerator TransitionRoutine(
        Rect start,
        Rect destination,
        RectTransform sourceTarget,
        CanvasGroup[] sourceGroups,
        RectTransform[] sourceClips,
        RectTransform nextTarget,
        bool targetMayBeClicked,
        Action onReady)
    {
        for (float elapsed = 0f; elapsed < transitionDuration; elapsed += Time.unscaledDeltaTime)
        {
            // Layout/scrolling can change either target during the short move.
            // Never animate toward a hidden/clipped control or from a lost source.
            if (!TryCalculateRect(nextTarget, out destination))
            {
                hasFocus = false;
                break;
            }
            if (!TryCalculateRect(sourceTarget, out _, sourceGroups, sourceClips)) break;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, transitionDuration));
            focusRect = LerpRect(start, destination, t);
            SetVerticesDirty();
            yield return null;
        }
        // A press begun on the moving mask must finish on the blocker, even if
        // the following presentation enables a real action immediately.
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButtonUp(0) ||
            Input.GetMouseButtonUp(1) || Input.touchCount > 0)
        {
            while (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.touchCount > 0)
            {
                hasFocus = TryCalculateRect(nextTarget, out focusRect);
                SetVerticesDirty();
                yield return null;
            }
            yield return null;
        }
        focusRect = destination;
        target = nextTarget;
        allowTargetInput = targetMayBeClicked;
        transitioning = false;
        transitionRoutine = null;
        RefreshFocus();
        onReady?.Invoke();
    }

    private void LateUpdate() => RefreshFocus();

    private void RefreshFocus()
    {
        if (gestureBlocked)
        {
            raycastTarget = true;
            hasFocus = target != null && TryCalculateRect(target, out focusRect);
            SetVerticesDirty();
            return;
        }
        if (GesturePassThrough && !dialogueInput) raycastTarget = false;
        if (transitioning) return;
        if (target == null)
        {
            hasFocus = false;
            raycastTarget = dialogueInput;
            SetVerticesDirty();
            return;
        }
        if (!TryCalculateRect(target, out Rect next))
        {
            if (target == worldProxy && projectedWorldTarget != null)
            {
                // Off-screen is not a new logical target. Resume projection when visible.
                hasFocus = false;
                raycastTarget = dialogueInput;
                SetVerticesDirty();
                return;
            }
            hasFocus = false;
            raycastTarget = dialogueInput;
            SetVerticesDirty();
            return;
        }
        if (focusRect != next || !hasFocus)
        {
            focusRect = next;
            hasFocus = true;
            SetVerticesDirty();
        }
        raycastTarget = dialogueInput || !GesturePassThrough;
        if (!LevelOneUIAccessibility.ReducedMotion) SetVerticesDirty();
    }

    private bool TryCalculateRect(RectTransform source, out Rect result,
        CanvasGroup[] visibilityGroups = null, RectTransform[] clipping = null)
    {
        result = default;
        if (source == null || !source.gameObject.activeInHierarchy) return false;
        foreach (CanvasGroup group in visibilityGroups ?? targetGroups)
        {
            if (group == null || !group.enabled) continue;
            if (group.alpha <= .01f) return false;
            if (group.ignoreParentGroups) break;
        }
        // A screen-space proxy represents a cached world object, not a cached screen rect.
        // Reproject at render time, after camera LateUpdate, without interpolation.
        if (source == worldProxy)
        {
            if (projectedWorldTarget == null) return false;
            Camera worldCamera = TutorialWorldTargetGeometry.ResolveCamera(projectedWorldTarget, Camera.main);
            if (worldCamera == null || !TutorialWorldTargetGeometry.TryGetScreenRect(projectedWorldTarget, worldCamera, out Rect projected))
                return false;
            source.position = projected.center;
            source.sizeDelta = projected.size;
        }
        // Read live geometry: a layout rebuild or scrolling can move a control
        // without changing its identity, lesson, scene, or screen resolution.
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas != null && !sourceCanvas.isActiveAndEnabled) return false;
        if (sourceCanvas != null) sourceCanvas = sourceCanvas.rootCanvas;
        if (sourceCanvas != null && !sourceCanvas.isActiveAndEnabled) return false;
        Camera sourceCamera = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : sourceCanvas.worldCamera;
        if (sourceCanvas != null && sourceCanvas.renderMode == RenderMode.WorldSpace && sourceCamera == null)
            sourceCamera = Camera.main;
        Canvas overlay = canvas != null ? canvas.rootCanvas : null;
        Camera overlayCamera = overlay == null || overlay.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : overlay.worldCamera;
        source.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screen, overlayCamera, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Rect bounds = rectTransform.rect;
        foreach (RectTransform clip in clipping ?? clipParents)
        {
            if (clip == null || !clip.gameObject.activeInHierarchy) return false;
            clip.GetWorldCorners(corners);
            Vector2 clipMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 clipMax = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, overlayCamera, out Vector2 local);
                clipMin = Vector2.Min(clipMin, local); clipMax = Vector2.Max(clipMax, local);
            }
            min = Vector2.Max(min, clipMin); max = Vector2.Min(max, clipMax);
        }
        if (max.x <= min.x || max.y <= min.y) return false;
        result = Rect.MinMaxRect(
            Mathf.Clamp(min.x - padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(min.y - padding.y, bounds.yMin, bounds.yMax),
            Mathf.Clamp(max.x + padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(max.y + padding.y, bounds.yMin, bounds.yMax));
        return result.width > 0f && result.height > 0f;
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (gestureBlocked || dialogueInput || transitioning) return base.Raycast(screenPoint, eventCamera);
        RefreshFocus(); // Input and rendering use exactly the same live rectangle.
        if (!IsVisible || !raycastTarget || !base.Raycast(screenPoint, eventCamera)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPoint, eventCamera, out Vector2 local);
        return !allowTargetInput || !focusRect.Contains(local);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if ((gestureBlocked || dialogueInput) && !hasFocus)
        {
            Rect surface = rectTransform.rect;
            // Nonzero alpha avoids CanvasRenderer's transparent-mesh culling,
            // which would otherwise also remove this surface from UI raycasts.
            Quad(vh, surface.xMin, surface.yMin, surface.xMax, surface.yMax, new Color(0f, 0f, 0f, .001f));
            return;
        }
        if (!hasFocus) return;
        Rect r = rectTransform.rect;
        Rect h = focusRect;
        Quad(vh, r.xMin, r.yMin, h.xMin, r.yMax, color);
        Quad(vh, h.xMax, r.yMin, r.xMax, r.yMax, color);
        Quad(vh, h.xMin, r.yMin, h.xMax, h.yMin, color);
        Quad(vh, h.xMin, h.yMax, h.xMax, r.yMax, color);
        float b = borderWidth;
        Color border = focusColor;
        if (!LevelOneUIAccessibility.ReducedMotion)
        {
            float breath = .5f + .5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            border.a *= 1f - pulseAmount * (1f - breath);
            // Scaling only the old 3-unit stroke by .22 moved its edge by less
            // than one canvas unit. Use target-relative expansion so it remains
            // visible after landscape canvas scaling. Never change focusRect.
            b += Mathf.Min(h.width, h.height) * pulseExpansion * breath;
        }
        Quad(vh, h.xMin - b, h.yMin - b, h.xMax + b, h.yMin, border);
        Quad(vh, h.xMin - b, h.yMax, h.xMax + b, h.yMax + b, border);
        Quad(vh, h.xMin - b, h.yMin, h.xMin, h.yMax, border);
        Quad(vh, h.xMax, h.yMin, h.xMax + b, h.yMax, border);
    }

    private void CacheClipping(RectTransform source)
    {
        if (source == null) { clipParents = Array.Empty<RectTransform>(); return; }
        targetGroups = source.GetComponentsInParent<CanvasGroup>(false);
        var clips = new System.Collections.Generic.List<RectTransform>();
        foreach (RectMask2D mask in source.GetComponentsInParent<RectMask2D>(false))
            if (mask.isActiveAndEnabled) clips.Add(mask.rectTransform);
        foreach (Mask mask in source.GetComponentsInParent<Mask>(false))
            if (mask.isActiveAndEnabled) clips.Add((RectTransform)mask.transform);
        clipParents = clips.ToArray();
    }

    private static Rect LerpRect(Rect a, Rect b, float t) => Rect.MinMaxRect(
        Mathf.LerpUnclamped(a.xMin, b.xMin, t),
        Mathf.LerpUnclamped(a.yMin, b.yMin, t),
        Mathf.LerpUnclamped(a.xMax, b.xMax, t),
        Mathf.LerpUnclamped(a.yMax, b.yMax, t));

    private void StopTransition()
    {
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        transitioning = false;
    }

    private static void Quad(VertexHelper vh, float left, float bottom, float right, float top, Color tint)
    {
        if (right <= left || top <= bottom) return;
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
        vh.AddVert(new Vector3(left, top), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, top), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
