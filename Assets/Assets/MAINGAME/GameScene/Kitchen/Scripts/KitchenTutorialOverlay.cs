using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the dark overlay used during the kitchen tour.
/// Call Show() with a world-space target to dim everything except a spotlight circle around that target.
/// Call Hide() to fade it away.
/// </summary>
public class KitchenTutorialOverlay : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private float fadeSpeed = 3f;

    [Header("Spotlight")]
    [SerializeField] private RectTransform spotlightRect;
    [SerializeField] private Vector2 spotlightSize = new Vector2(500f, 500f);

    private Camera cam;
    private Canvas rootCanvas;
    private Transform trackedTarget;
    private bool isVisible;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        cam = Camera.main;
        rootCanvas = GetComponentInParent<Canvas>();
        while (rootCanvas != null && !rootCanvas.isRootCanvas)
            rootCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();

        if (overlayGroup != null)
        {
            overlayGroup.alpha = 0f;
            overlayGroup.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!isVisible || trackedTarget == null || spotlightRect == null || cam == null)
            return;

        // Convert world position to screen space, then to root canvas local position
        // using RectTransformUtility to correctly handle ScaleWithScreenSize canvases.
        Vector3 screenPos = cam.WorldToScreenPoint(trackedTarget.position);
        if (rootCanvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                new Vector2(screenPos.x, screenPos.y),
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                out Vector2 localPoint))
        {
            spotlightRect.anchoredPosition = localPoint;
        }
    }

    /// <summary>Fades in the overlay and tracks a world-space target with the spotlight.</summary>
    public void Show(Transform target, Vector2 size)
    {
        trackedTarget = target;
        if (spotlightRect != null)
            spotlightRect.sizeDelta = size;

        isVisible = true;

        if (overlayGroup != null)
            overlayGroup.gameObject.SetActive(true);

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeTo(1f));
    }

    /// <summary>Fades in the overlay and tracks a world-space target with the default spotlight size.</summary>
    public void Show(Transform target)
    {
        Show(target, spotlightSize);
    }

    /// <summary>Fades the overlay out and disables it.</summary>
    public void Hide()
    {
        isVisible = false;
        trackedTarget = null;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOutAndDisable());
    }

    private IEnumerator FadeTo(float target)
    {
        while (!Mathf.Approximately(overlayGroup.alpha, target))
        {
            overlayGroup.alpha = Mathf.MoveTowards(overlayGroup.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        overlayGroup.alpha = target;
    }

    private IEnumerator FadeOutAndDisable()
    {
        yield return FadeTo(0f);
        if (overlayGroup != null)
            overlayGroup.gameObject.SetActive(false);
    }
}
