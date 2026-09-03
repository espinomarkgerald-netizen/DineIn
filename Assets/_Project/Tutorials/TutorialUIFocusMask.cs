using UnityEngine;
using UnityEngine.UI;

/// <summary>One tutorial-owned overlay. Draws around a live target without altering it.</summary>
[DisallowMultipleComponent]
public sealed class TutorialUIFocusMask : MaskableGraphic
{
    [SerializeField] private Vector2 padding = new Vector2(8f, 8f);
    [SerializeField] private Color focusColor = new Color(1f, 0.8f, 0.15f, 1f);
    [SerializeField] private float borderWidth = 3f;
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform target;
    private Rect focusRect;
    private bool allowTargetInput;
    private bool hasFocus;

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
        overlayCanvas.sortingOrder = 32760; // Above the real Newspaper canvas (31000).
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
        hasFocus = false;
        raycastTarget = false;
        base.OnDisable();
    }

    public void Show(RectTransform focusTarget, bool targetMayBeClicked)
    {
        if (focusTarget == null) { Hide(); return; }
        gameObject.SetActive(true);
        target = focusTarget;
        allowTargetInput = targetMayBeClicked;
        RefreshFocus();
    }

    public void Hide()
    {
        target = null;
        hasFocus = false;
        allowTargetInput = false;
        raycastTarget = false;
        canvasRenderer.Clear();
        gameObject.SetActive(false);
    }

    private void LateUpdate() => RefreshFocus();

    private void RefreshFocus()
    {
        bool valid = target != null && target.gameObject.activeInHierarchy;
        if (!valid)
        {
            if (hasFocus) { hasFocus = false; SetVerticesDirty(); }
            raycastTarget = false;
            return;
        }
        Canvas sourceCanvas = target.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : sourceCanvas.worldCamera;
        Camera overlayCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvas.worldCamera;
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, overlayCamera, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Rect bounds = rectTransform.rect;
        Rect next = Rect.MinMaxRect(
            Mathf.Clamp(min.x - padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(min.y - padding.y, bounds.yMin, bounds.yMax),
            Mathf.Clamp(max.x + padding.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(max.y + padding.y, bounds.yMin, bounds.yMax));
        valid = next.width > 0f && next.height > 0f;
        if (focusRect != next || hasFocus != valid) { focusRect = next; hasFocus = valid; SetVerticesDirty(); }
        raycastTarget = valid;
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsVisible || !raycastTarget || !base.Raycast(screenPoint, eventCamera)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local);
        // In action mode the hole passes input through to the real button beneath it.
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
