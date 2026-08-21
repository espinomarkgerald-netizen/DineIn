using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prefab-backed screen-edge direction marker for a ready delivery truck.</summary>
public sealed class RestockTruckOffscreenIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform marker;
    [SerializeField, Min(0f)] private float edgePadding = 48f;
    [SerializeField] private Vector3 targetWorldOffset = new Vector3(0f, 1.5f, 0f);

    private RestockTruckInteractable truck;
    private RectTransform canvasRect;

    public void Bind(RestockTruckInteractable target)
    {
        truck = target;
        Hide();
    }

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        Hide();
    }

    private void LateUpdate()
    {
        if (marker == null || truck == null ||
            SceneManager.GetActiveScene().name != "Lobby1" ||
            !truck.HasReadyDelivery)
        {
            Hide();
            return;
        }

        Camera camera = Camera.main;
        if (camera == null || truck.IsVisibleFrom(camera))
        {
            Hide();
            return;
        }

        if (canvasRect == null)
            canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            Hide();
            return;
        }

        Vector3 target = truck.transform.position + targetWorldOffset;
        Vector3 cameraLocal = camera.transform.InverseTransformPoint(target);
        bool showRight = cameraLocal.x >= 0f;
        Vector3 screen = camera.WorldToScreenPoint(target);
        if (screen.z < 0f)
            screen.y = Screen.height - screen.y;

        float screenY = Mathf.Clamp(screen.y, edgePadding, Screen.height - edgePadding);
        Vector2 edgePoint = new Vector2(
            showRight ? Screen.width - edgePadding : edgePadding,
            screenY);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            edgePoint,
            null,
            out Vector2 anchored);

        marker.anchoredPosition = anchored;
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (marker != null && marker.gameObject.activeSelf)
            marker.gameObject.SetActive(false);
    }
}
