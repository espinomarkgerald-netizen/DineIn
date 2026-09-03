using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders animated hand demonstration hints inside the tutorial canvas.
/// Supports a looping swipe gesture (Hand → HandClick → Hand drag) and a
/// looping tap gesture at a world/UI target (Hand → HandClick → Hand).
/// All timing uses unscaled time so gameplay Time.timeScale never affects it.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialHandIndicator : MonoBehaviour
{
    public enum HintMode
    {
        Hidden,
        Swipe,
        Tap
    }

    // ── Serialised fields ───────────────────────────────────────────────────
    [Header("Hand Image")]
    [SerializeField] private RectTransform handRect;
    [SerializeField] private Image handImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Sprites")]
    [Tooltip("Open / resting hand — Hand.png")]
    [SerializeField] private Sprite handOpenSprite;
    [Tooltip("Pressed / clicking hand — Hand Click.png")]
    [SerializeField] private Sprite handClickSprite;

    [Header("Canvas Reference")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;

    [Header("Swipe Settings")]
    [SerializeField, Min(0.5f)] private float swipeCycleSeconds = 2.1f;
    [Tooltip("Hand travels this fraction of canvas width left to right")]
    [SerializeField, Range(0.05f, 0.4f)] private float swipeTravelCanvasFraction = 0.22f;
    [Tooltip("How long the initial press phase holds (fraction of cycle)")]
    [SerializeField, Range(0f, 0.3f)] private float swipePressHoldFraction = 0.12f;
    [Tooltip("Where the drag phase ends (fraction of cycle)")]
    [SerializeField, Range(0.3f, 0.9f)] private float swipeDragEndFraction = 0.76f;

    [Header("Tap Settings")]
    [SerializeField, Min(0.3f)] private float tapCycleSeconds = 1.15f;
    [SerializeField] private Vector2 tapOffset = new Vector2(30f, -30f);
    [Tooltip("How far the hand moves down on press (canvas units)")]
    [SerializeField] private float tapPressDownAmount = 14f;

    [Header("Visual")]
    [Tooltip("Base rendered size of the hand in canvas units (before scale)")]
    [SerializeField] private float handDisplaySize = 168f;

    // Legacy field names kept for backwards-compat with old scene YAML.
    // They are redirected into handOpenSprite / handClickSprite during
    // Awake so scenes saved before the rename still work without manual
    // Inspector tweaks.
    [SerializeField] private Sprite swipeSprite;
    [SerializeField] private Sprite tapSprite;

    // ── Runtime state ───────────────────────────────────────────────────────
    private HintMode mode = HintMode.Hidden;
    private Transform currentTarget;
    private float cycleStartedAt;
    private bool initialized;

    // ── Public API ──────────────────────────────────────────────────────────
    public HintMode  Mode           => mode;
    public Transform CurrentTarget  => currentTarget;
    public bool      IsVisible      => mode != HintMode.Hidden && gameObject.activeSelf;
    public Sprite    CurrentSprite  => handImage != null ? handImage.sprite : null;

    // ── Unity lifecycle ─────────────────────────────────────────────────────
    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        // Migrate legacy field values if new fields were left blank.
        if (handOpenSprite  == null && swipeSprite != null) handOpenSprite  = swipeSprite;
        if (handClickSprite == null && tapSprite   != null) handClickSprite = tapSprite;

        // Auto-resolve references if Inspector left them blank.
        if (handRect    == null) handRect    = transform as RectTransform;
        if (handImage   == null) handImage   = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>(true);
        if (worldCamera == null)
            worldCamera = Camera.main;

        // One reusable hand remains above the UI focus mask without intercepting input.
        Canvas hintLayer = GetComponent<Canvas>();
        if (hintLayer == null) hintLayer = gameObject.AddComponent<Canvas>();
        hintLayer.overrideSorting = true;
        hintLayer.sortingOrder = 32762;

        // Guarantee correct Image settings.
        if (handImage != null)
        {
            handImage.preserveAspect = true;
            handImage.raycastTarget  = false;
        }

        HideHint();
    }

    private void LateUpdate()
    {
        if (mode == HintMode.Hidden || !gameObject.activeSelf || handRect == null)
            return;

        switch (mode)
        {
            case HintMode.Swipe: AnimateSwipe(); break;
            case HintMode.Tap:   AnimateTap();   break;
        }
    }

    // ── Public methods ──────────────────────────────────────────────────────

    /// <summary>Starts the looping swipe (camera-pan) hand demonstration.</summary>
    public void ShowSwipeHint()
    {
        Initialize();
        ResetVisualState();
        mode           = HintMode.Swipe;
        currentTarget  = null;
        cycleStartedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        SetSprite(handOpenSprite);
        if (handRect != null)
        {
            handRect.sizeDelta       = new Vector2(handDisplaySize, handDisplaySize);
            handRect.anchoredPosition = SwipeStartPosition();
        }
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    /// <summary>Starts the looping tap (interaction) hand demonstration at a world/UI target.</summary>
    public void ShowTapHint(Transform target)
    {
        Initialize();
        if (target == null) { HideHint(); return; }

        ResetVisualState();
        mode           = HintMode.Tap;
        currentTarget  = target;
        cycleStartedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        SetSprite(handOpenSprite);
        if (handRect != null)
            handRect.sizeDelta = new Vector2(handDisplaySize, handDisplaySize);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    /// <summary>Immediately stops all animation and hides the hand.</summary>
    public void HideHint()
    {
        mode          = HintMode.Hidden;
        currentTarget = null;
        ResetVisualState();
        gameObject.SetActive(false);
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private void ResetVisualState()
    {
        if (handRect != null)
        {
            handRect.localScale    = Vector3.one;
            handRect.localRotation = Quaternion.identity;
            handRect.sizeDelta     = new Vector2(handDisplaySize, handDisplaySize);
        }
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        SetSprite(handOpenSprite);
    }

    private void SetSprite(Sprite sprite)
    {
        if (handImage == null || sprite == null) return;
        if (handImage.sprite == sprite) return;
        handImage.sprite  = sprite;
        handImage.enabled = true;
        handImage.SetAllDirty();   // force immediate mesh rebuild
    }

    // ── Swipe animation ─────────────────────────────────────────────────────

    private void AnimateSwipe()
    {
        float normalized = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, swipeCycleSeconds)
                           / swipeCycleSeconds;
        UpdateSwipePose(normalized);
    }

    private Vector2 SwipeStartPosition()
    {
        float travel = SwipeTravel();
        return new Vector2(-travel, 0f);
    }

    private float SwipeTravel()
    {
        RectTransform cr = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        float w = cr != null ? cr.rect.width : 1600f;
        return Mathf.Clamp(w * swipeTravelCanvasFraction, 130f, 320f);
    }

    private void UpdateSwipePose(float n)
    {
        // Phase 1  [0 .. swipePressHoldFraction)
        //   Show HandClick — finger touches the screen, hand stationary at left.
        // Phase 2  [swipePressHoldFraction .. swipeDragEndFraction)
        //   Show Hand (open) — drag smoothly to the right.
        // Phase 3  [swipeDragEndFraction .. 0.90)
        //   Hand open — slow stop at right edge.
        // Phase 4  [0.90 .. 1.00)
        //   Fade out — invisible snap back to left for seamless loop.

        float travel = SwipeTravel();
        float startX = -travel;
        float endX   =  travel;

        bool pressing = n < swipePressHoldFraction;
        SetSprite(pressing ? handClickSprite : handOpenSprite);

        // Horizontal position
        float moveT;
        if (n < swipePressHoldFraction)
            moveT = 0f;
        else if (n < swipeDragEndFraction)
            moveT = Mathf.SmoothStep(0f, 1f,
                (n - swipePressHoldFraction) / (swipeDragEndFraction - swipePressHoldFraction));
        else
            moveT = 1f;

        float xPos  = Mathf.Lerp(startX, endX, moveT);
        float yPos  = pressing ? -6f : 0f;   // slight downward shift when pressed
        if (handRect != null)
            handRect.anchoredPosition = new Vector2(xPos, yPos);

        // Scale: slight crush on press
        float scale = pressing ? Mathf.Lerp(1f, 0.90f, n / swipePressHoldFraction) : 1f;
        if (handRect != null)
            handRect.localScale = Vector3.one * scale;

        // Alpha: full until near end, then fade out for clean loop
        float alpha = n > 0.88f ? 1f - Mathf.InverseLerp(0.88f, 1f, n) : 1f;
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    // ── Tap animation ───────────────────────────────────────────────────────

    private void AnimateTap()
    {
        if (!TryGetTargetCanvasPosition(currentTarget, out Vector2 targetPos))
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            return;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        float normalized = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, tapCycleSeconds)
                           / tapCycleSeconds;
        UpdateTapPose(normalized, targetPos);
    }

    private void UpdateTapPose(float n, Vector2 targetPos)
    {
        // Phase 0  [0.00 – 0.20)   Hand open — hovering above target
        // Phase 1  [0.20 – 0.40)   Hand open → HandClick — press down
        // Phase 2  [0.40 – 0.60)   HandClick — held pressed
        // Phase 3  [0.60 – 0.80)   HandClick → Hand open — lift
        // Phase 4  [0.80 – 1.00)   Hand open — pause before next cycle

        bool isPressed = n >= 0.20f && n < 0.80f;
        SetSprite(isPressed ? handClickSprite : handOpenSprite);

        float pressT;
        if      (n < 0.20f) pressT = 0f;
        else if (n < 0.40f) pressT = Mathf.SmoothStep(0f, 1f, (n - 0.20f) / 0.20f);
        else if (n < 0.60f) pressT = 1f;
        else if (n < 0.80f) pressT = Mathf.SmoothStep(1f, 0f, (n - 0.60f) / 0.20f);
        else                pressT = 0f;

        if (handRect != null)
        {
            float scale = Mathf.Lerp(1f, 0.86f, pressT);
            // Anchor the visible fingertip, not the centre of the whole image.
            // These normalized points belong to the two Hand sprites, not a lesson.
            Vector2 fingertip = isPressed ? new Vector2(0.33f, 0.70f) : new Vector2(0.375f, 0.80f);
            Vector2 tipFromPivot = Vector2.Scale(fingertip - handRect.pivot, handRect.rect.size) * scale;
            handRect.anchoredPosition = targetPos - tipFromPivot + Vector2.up * (tapPressDownAmount * (1f - pressT));
            handRect.localScale = Vector3.one * scale;
        }
    }

    // ── World → Canvas coordinate conversion ────────────────────────────────

    private bool TryGetTargetCanvasPosition(Transform target, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        RectTransform canvasRect = targetCanvas != null
            ? targetCanvas.transform as RectTransform : null;
        if (target == null || canvasRect == null) return false;

        Vector3 screenPoint;
        if (target is RectTransform targetRect)
        {
            Canvas sourceCanvas = targetRect.GetComponentInParent<Canvas>();
            Camera eventCam = sourceCanvas == null || sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : sourceCanvas.worldCamera;
            screenPoint = RectTransformUtility.WorldToScreenPoint(eventCam, targetRect.TransformPoint(targetRect.rect.center));
        }
        else
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return false;
            screenPoint = cam.WorldToScreenPoint(TutorialWorldTargetGeometry.Center(target));
            if (screenPoint.z <= 0f) return false;
        }

        Camera canvasCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : targetCanvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, canvasCam, out canvasPosition);
    }
}
