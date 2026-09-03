using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialTargetIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Vector2 uiPadding = new Vector2(20f, 20f);
    [SerializeField] private Vector2 worldTargetSize = new Vector2(110f, 110f);
    [SerializeField] private float pulseAmount = 0.06f;
    [SerializeField] private float pulseSpeed = 5f;

    private Transform currentTarget;
    private bool initialized;

    public Transform CurrentTarget => currentTarget;
    public bool IsVisible => indicatorRect != null && indicatorRect.gameObject.activeSelf &&
                             (canvasGroup == null || canvasGroup.alpha > 0.001f);

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (indicatorRect == null)
            indicatorRect = transform as RectTransform;

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        Image image = indicatorRect != null ? indicatorRect.GetComponent<Image>() : null;
        if (image != null)
            image.raycastTarget = false;

        Hide();
    }

    private void LateUpdate()
    {
        if (indicatorRect == null || !indicatorRect.gameObject.activeSelf || currentTarget == null)
            return;

        UpdatePosition();

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        indicatorRect.localScale = Vector3.one * pulse;
    }

    public void Show(Transform target)
    {
        Initialize();
        currentTarget = target;

        if (indicatorRect == null)
            return;

        indicatorRect.gameObject.SetActive(target != null);
        if (canvasGroup != null)
            canvasGroup.alpha = target != null ? 1f : 0f;
        indicatorRect.SetAsLastSibling();
        indicatorRect.localScale = Vector3.one;
        UpdatePosition();
    }

    public void Hide()
    {
        currentTarget = null;

        if (indicatorRect == null)
            return;

        indicatorRect.localScale = Vector3.one;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        indicatorRect.gameObject.SetActive(false);
    }

    private void UpdatePosition()
    {
        if (indicatorRect == null || targetCanvas == null || currentTarget == null)
            return;

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        if (currentTarget is RectTransform targetRect)
        {
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);

            Camera eventCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            indicatorRect.anchoredPosition = (min + max) * 0.5f;
            indicatorRect.sizeDelta = max - min + uiPadding;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            return;
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
            return;

        Vector3 screenPoint = cameraToUse.WorldToScreenPoint(TutorialWorldTargetGeometry.Center(currentTarget));
        if (screenPoint.z < 0f)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            return;
        }


        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, canvasCamera, out Vector2 canvasPoint);
        indicatorRect.anchoredPosition = canvasPoint;
        indicatorRect.sizeDelta = worldTargetSize;
        if (TutorialWorldTargetGeometry.TryGetScreenRect(currentTarget, cameraToUse, out Rect screenRect))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRect.min, canvasCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRect.max, canvasCamera, out Vector2 max);
            indicatorRect.anchoredPosition = (min + max) * 0.5f;
            indicatorRect.sizeDelta = max - min + uiPadding;
        }
    }
}
