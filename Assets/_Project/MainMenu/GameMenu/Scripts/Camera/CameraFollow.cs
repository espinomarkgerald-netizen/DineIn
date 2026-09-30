using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    [Tooltip("The authored starting destination, independent of the saved selection.")]
    [SerializeField] private Transform compositionReference;
    [Header("Restaurant district overview (optional)")]
    [SerializeField] private bool districtOverview;
    [SerializeField] private Bounds districtBounds = new Bounds(new Vector3(-11, 2, 2), new Vector3(24, 8, 38));
    [SerializeField, Range(0, .2f)] private float selectionPan = .07f;
    [SerializeField, Min(.1f)] private float overviewSmoothTime = .65f;
    [SerializeField, Range(1.05f, 1.5f)] private float framePadding = 1.2f;
    private Vector3 offset, followVelocity;
    private bool isInitialized;
    private Camera viewCamera;

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        if (compositionReference == null) return;
        offset = transform.position - compositionReference.position;
        isInitialized = true;
    }

    public void SnapToAuthoredComposition()
    {
        if (districtOverview)
        {
            transform.position = OverviewPosition();
            FitDistrict();
            followVelocity = Vector3.zero;
            return;
        }
        if (compositionReference != null && isInitialized && target != null)
            transform.position = target.position + offset;
    }

    private void Start()
    {
        if (target != null && !isInitialized)
        {
            offset = transform.position - target.position;
            isInitialized = true;
        }
        SnapToAuthoredComposition();
    }

    private void LateUpdate()
    {
        if (districtOverview)
        {
            transform.position = Vector3.SmoothDamp(transform.position, OverviewPosition(), ref followVelocity,
                overviewSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            FitDistrict();
            return;
        }
        if (target == null || !isInitialized) return;
        transform.position = Vector3.Lerp(transform.position, target.position + offset, smoothSpeed * Time.deltaTime);
    }

    private Vector3 OverviewPosition()
    {
        var focus = districtBounds.center - transform.up * 1.2f;
        if (target != null && !LevelOneUIAccessibility.ReducedMotion)
        {
            var displacement = target.position - districtBounds.center;
            displacement.y = 0;
            focus += Vector3.ClampMagnitude(displacement, districtBounds.extents.magnitude) * selectionPan;
        }
        return focus - transform.forward * 60f;
    }

    private void FitDistrict()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (viewCamera == null || !viewCamera.orthographic) return;
        float horizontal = 0, vertical = 0;
        var extents = districtBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y, (i & 4) == 0 ? -extents.z : extents.z);
            horizontal = Mathf.Max(horizontal, Mathf.Abs(Vector3.Dot(corner, transform.right)));
            vertical = Mathf.Max(vertical, Mathf.Abs(Vector3.Dot(corner, transform.up)));
        }
        float panMargin = extents.magnitude * selectionPan;
        viewCamera.orthographicSize = Mathf.Max(vertical + 1.2f + panMargin,
            (horizontal + panMargin) / Mathf.Max(.25f, viewCamera.aspect)) * framePadding;
    }
}
