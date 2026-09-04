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

    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform target;
    private Rect focusRect;
    private bool allowTargetInput;
    private bool hasFocus;
    private bool transitioning;
    private Coroutine transitionRoutine;

    public RectTransform CurrentTarget => target;
    public Rect FocusRect => focusRect;
    public bool IsVisible => isActiveAndEnabled && hasFocus;

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
        Canvas.willRenderCanvases += RefreshFocus;
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= RefreshFocus;
        target = null;
        transitioning = false;
        hasFocus = false;
        raycastTarget = false;
        base.OnDisable();
    }

    public void Show(RectTransform focusTarget, bool targetMayBeClicked)
    {
        StopTransition();
        if (focusTarget == null) { Hide(); return; }
        gameObject.SetActive(true);
        target = focusTarget;
        allowTargetInput = targetMayBeClicked;
        TutorialUIAutoScroller.ForceLayout(target);
        if (TryCalculateRect(target, out Rect next))
        {
            focusRect = next;
            hasFocus = true;
            raycastTarget = true;
            SetVerticesDirty();
        }
        else Hide();
    }

    /// <summary>Keeps the last valid hole while a runtime page rebuilds or scrolls.</summary>
    public void Hold()
    {
        StopTransition();
        target = null;
        allowTargetInput = false;
        if (hasFocus)
        {
            gameObject.SetActive(true);
            raycastTarget = true;
        }
    }

    public void TransitionTo(RectTransform focusTarget, bool targetMayBeClicked, Action onReady)
    {
        StopTransition();
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
            Hide();
            onReady?.Invoke();
            return;
        }

        if (!hasFocus || !Application.isPlaying || LevelOneUIAccessibility.ReducedMotion ||
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
            focusRect, destination, focusTarget, targetMayBeClicked, onReady));
    }

    public void Hide()
    {
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
        RectTransform nextTarget,
        bool targetMayBeClicked,
        Action onReady)
    {
        for (float elapsed = 0f; elapsed < transitionDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, transitionDuration));
            focusRect = LerpRect(start, destination, t);
            SetVerticesDirty();
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
        if (transitioning) return;
        if (target == null)
        {
            raycastTarget = hasFocus;
            return;
        }
        if (!TryCalculateRect(target, out Rect next))
        {
            // Runtime pages can replace cards between frames. Preserve the last stable
            // hole until TutorialSystem resolves the next live target.
            target = null;
            allowTargetInput = false;
            raycastTarget = hasFocus;
            return;
        }
        if (focusRect != next || !hasFocus)
        {
            focusRect = next;
            hasFocus = true;
            SetVerticesDirty();
        }
        raycastTarget = true;
    }

    private bool TryCalculateRect(RectTransform source, out Rect result)
    {
        result = default;
        if (source == null || !source.gameObject.activeInHierarchy) return false;
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : sourceCanvas.worldCamera;
        Camera overlayCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvas.worldCamera;
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
        result = Rect.MinMaxRect(
            Mathf.Clamp(min.x - padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(min.y - padding.y, bounds.yMin, bounds.yMax),
            Mathf.Clamp(max.x + padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(max.y + padding.y, bounds.yMin, bounds.yMax));
        return result.width > 0f && result.height > 0f;
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsVisible || !raycastTarget || !base.Raycast(screenPoint, eventCamera)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPoint, eventCamera, out Vector2 local);
        return !allowTargetInput || !focusRect.Contains(local);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!hasFocus) return;
        Rect r = rectTransform.rect;
        Rect h = focusRect;
        Quad(vh, r.xMin, r.yMin, h.xMin, r.yMax, color);
        Quad(vh, h.xMax, r.yMin, r.xMax, r.yMax, color);
        Quad(vh, h.xMin, r.yMin, h.xMax, h.yMin, color);
        Quad(vh, h.xMin, h.yMax, h.xMax, r.yMax, color);
        float b = borderWidth;
        Quad(vh, h.xMin - b, h.yMin - b, h.xMax + b, h.yMin, focusColor);
        Quad(vh, h.xMin - b, h.yMax, h.xMax + b, h.yMax + b, focusColor);
        Quad(vh, h.xMin - b, h.yMin, h.xMin, h.yMax, focusColor);
        Quad(vh, h.xMax, h.yMin, h.xMax + b, h.yMax, focusColor);
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
