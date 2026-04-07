using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Positions a UI arrow to track a world-space or UI target each frame.
/// Call PointToWorld() or PointToUI() to redirect the arrow.
/// Call Hide() to remove it.
/// </summary>
public class KitchenTutorialArrowDriver : MonoBehaviour
{
    [Header("Arrow UI")]
    [SerializeField] private RectTransform arrowRect;
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, 80f);
    [SerializeField] private Vector2 uiOffset   = new Vector2(0f, 80f);

    private Camera cam;
    private Canvas rootCanvas;
    private Transform worldTarget;
    private RectTransform uiTarget;
    private bool trackingWorld;
    private bool trackingUI;

    private void Awake()
    {
        cam = Camera.main;
        rootCanvas = GetComponentInParent<Canvas>();
        while (rootCanvas != null && !rootCanvas.isRootCanvas)
            rootCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();

        SetArrowVisible(false);
    }

    private void LateUpdate()
    {
        if (arrowRect == null) return;

        if (trackingWorld && worldTarget != null)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(worldTarget.position);
            // Convert screen position to canvas space, accounting for canvas scale factor
            arrowRect.position = screenPos + new Vector3(worldOffset.x, worldOffset.y, 0f);

            // If the canvas is scaled, screenPos is in display pixels — match by setting
            // anchoredPosition via RectTransformUtility to handle ScaleWithScreenSize correctly.
            if (rootCanvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootCanvas.transform as RectTransform,
                    new Vector2(screenPos.x, screenPos.y),
                    rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                    out Vector2 localPoint))
            {
                arrowRect.anchoredPosition = localPoint + worldOffset;
            }
        }
        else if (trackingUI && uiTarget != null)
        {
            arrowRect.position = uiTarget.position + new Vector3(uiOffset.x, uiOffset.y, 0f);
        }
    }

    /// <summary>Points the arrow at a world-space Transform.</summary>
    public void PointToWorld(Transform target)
    {
        if (target == null) { Hide(); return; }
        worldTarget = target;
        uiTarget = null;
        trackingWorld = true;
        trackingUI = false;
        SetArrowVisible(true);
    }

    /// <summary>Points the arrow at a UI RectTransform (screen-space canvas).</summary>
    public void PointToUI(RectTransform target)
    {
        if (target == null) { Hide(); return; }
        uiTarget = target;
        worldTarget = null;
        trackingUI = true;
        trackingWorld = false;
        SetArrowVisible(true);
    }

    /// <summary>Hides the arrow and stops tracking.</summary>
    public void Hide()
    {
        trackingWorld = false;
        trackingUI = false;
        worldTarget = null;
        uiTarget = null;
        SetArrowVisible(false);
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowRect != null)
            arrowRect.gameObject.SetActive(visible);
    }
}
