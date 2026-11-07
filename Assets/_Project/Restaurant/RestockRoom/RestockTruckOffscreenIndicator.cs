using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Prefab-backed screen-edge direction marker for a ready delivery truck.</summary>
public sealed class RestockTruckOffscreenIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform marker;
    [SerializeField, Min(0f)] private float edgePadding = 48f;
    [SerializeField] private Vector3 targetWorldOffset = new Vector3(0f, 1.5f, 0f);

    private RestockTruckInteractable truck;
    private RectTransform canvasRect;
    private void FocusTruck()
    {
        if(truck==null || !truck.HasReadyDelivery || GameplayUIBlocker.IsBlocked())return;
        var camera=Camera.main;var controller=camera!=null?camera.GetComponentInParent<MainCameraController>():null;
        if(controller==null)return;
        var renderers=truck.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds(truck.transform.position,Vector3.zero);bool first=true;
        foreach(var renderer in renderers)if(renderer.enabled){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
        controller.FrameWorldTarget(bounds.center);
    }

    public void Bind(RestockTruckInteractable target)
    {
        truck = target;
        Hide();
    }

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        if(GetComponent<GraphicRaycaster>()==null)gameObject.AddComponent<GraphicRaycaster>();
        if(marker!=null){var button=marker.GetComponent<Button>();if(button==null)button=marker.gameObject.AddComponent<Button>();button.targetGraphic=marker.GetComponent<Image>();button.onClick.AddListener(FocusTruck);UISubtlePressFeedback.EnsureButtons(marker);}
        Hide();
    }

    private void LateUpdate()
    {
        if (marker == null || truck == null ||
            !truck.isActiveAndEnabled || GameplayUIBlocker.IsBlocked() ||
            !TutorialSceneRuntimeMarker.AllowsHUDControl("TruckIndicator") ||
            !truck.HasReadyDelivery)
        {
            Hide();
            return;
        }

        Camera camera = Camera.main;
        if (camera == null || camera.GetComponentInParent<MainCameraController>()==null || truck.IsVisibleFrom(camera))
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

        var safe=Screen.safeArea;var canvas=GetComponent<Canvas>();float half=marker.rect.width*(canvas!=null?canvas.scaleFactor:1)*.5f;
        float pad=Mathf.Max(edgePadding,half+8);
        float lowY=Mathf.Max(safe.yMin+pad,Screen.height*.14f),highY=Mathf.Min(safe.yMax-pad,Screen.height*.72f);
        float screenY = Mathf.Clamp(screen.y, lowY, Mathf.Max(lowY,highY));
        Vector2 edgePoint = new Vector2(
            showRight ? safe.xMax-pad : safe.xMin+pad,
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
