using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialHandIndicator : MonoBehaviour
{
    public enum HintMode
    {
        Hidden,
        Swipe,
        Tap
    }

    [SerializeField] private RectTransform handRect;
    [SerializeField] private Image handImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Sprite swipeSprite;
    [SerializeField] private Sprite tapSprite;
    [SerializeField, Min(0.5f)] private float swipeCycleSeconds = 1.45f;
    [SerializeField, Min(0.25f)] private float tapCycleSeconds = 0.9f;
    [SerializeField] private float swipeTravelCanvasFraction = 0.13f;
    [SerializeField] private Vector2 tapOffset = new Vector2(30f, -24f);

    private HintMode mode;
    private Transform currentTarget;
    private float cycleStartedAt;

    public HintMode Mode => mode;
    public Transform CurrentTarget => currentTarget;
    public bool IsVisible => mode != HintMode.Hidden && gameObject.activeSelf;
    public Sprite CurrentSprite => handImage != null ? handImage.sprite : null;

    private void Awake()
    {
        if (handRect == null)
            handRect = transform as RectTransform;
        if (handImage == null)
            handImage = GetComponent<Image>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (handImage != null)
        {
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
        }

        HideHint();
    }

    private void LateUpdate()
    {
        if (mode == HintMode.Hidden || !gameObject.activeSelf || handRect == null)
            return;

        if (mode == HintMode.Swipe)
            AnimateSwipe();
        else if (mode == HintMode.Tap)
            AnimateTap();
    }

    public void ShowSwipeHint()
    {
        Show(HintMode.Swipe, swipeSprite, null);
        UpdateSwipePose(0f);
    }

    public void ShowTapHint(Transform target)
    {
        if (target == null)
        {
            HideHint();
            return;
        }

        Show(HintMode.Tap, tapSprite, target);
        UpdateTapPose(0f);
    }

    public void HideHint()
    {
        mode = HintMode.Hidden;
        currentTarget = null;
        if (handRect != null)
        {
            handRect.localScale = Vector3.one;
            handRect.localRotation = Quaternion.identity;
        }
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void Show(HintMode nextMode, Sprite sprite, Transform target)
    {
        mode = nextMode;
        currentTarget = target;
        cycleStartedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (handImage != null)
            handImage.sprite = sprite;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void AnimateSwipe()
    {
        float normalized = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, swipeCycleSeconds) /
                           swipeCycleSeconds;
        UpdateSwipePose(normalized);
    }

    private void UpdateSwipePose(float normalized)
    {
        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 1600f;
        float travel = Mathf.Clamp(canvasWidth * swipeTravelCanvasFraction, 120f, 260f);
        float moveT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.74f, normalized));
        handRect.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, moveT), 0f);

        float press = normalized < 0.16f ? Mathf.Lerp(1f, 0.88f, normalized / 0.16f) :
            normalized < 0.74f ? 0.88f : Mathf.Lerp(0.88f, 1f, (normalized - 0.74f) / 0.12f);
        handRect.localScale = Vector3.one * press;
        if (canvasGroup != null)
            canvasGroup.alpha = normalized > 0.86f ? 1f - Mathf.InverseLerp(0.86f, 1f, normalized) : 1f;
    }

    private void AnimateTap()
    {
        float normalized = Mathf.Repeat(Time.unscaledTime - cycleStartedAt, tapCycleSeconds) /
                           tapCycleSeconds;
        UpdateTapPose(normalized);
    }

    private void UpdateTapPose(float normalized)
    {
        if (!TryGetTargetCanvasPosition(currentTarget, out Vector2 targetPosition))
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            return;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        float pressT = normalized < 0.28f
            ? Mathf.SmoothStep(0f, 1f, normalized / 0.28f)
            : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.28f, 0.58f, normalized));
        handRect.anchoredPosition = targetPosition + tapOffset + Vector2.down * (12f * pressT);
        handRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.84f, pressT);
    }

    private bool TryGetTargetCanvasPosition(Transform target, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;
        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        if (target == null || canvasRect == null)
            return false;

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        Vector3 screenPoint;
        if (target is RectTransform targetRect)
        {
            Camera eventCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
            screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, targetRect.position);
        }
        else
        {
            if (cameraToUse == null)
                return false;
            screenPoint = cameraToUse.WorldToScreenPoint(target.position);
            if (screenPoint.z <= 0f)
                return false;
        }

        Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, canvasCamera, out canvasPosition);
    }
}
