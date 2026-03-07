using UnityEngine;
using UnityEngine.EventSystems;

public class MainCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Pan (Drag)")]
    [SerializeField] private float panUnitsPerPixel = 0.02f;     // base drag-to-world conversion
    [SerializeField] private float panSmoothTime = 0.08f;
    [SerializeField] private bool requireRightMouseOnPC = true;  // true = RMB drag, false = LMB drag
    [SerializeField] private bool blockPanOverUI = true;

    [Header("Zoom (Orthographic Size)")]
    [SerializeField] private float zoomSmoothTime = 0.10f;
    [SerializeField] private float minOrthoSize = 4f;
    [SerializeField] private float maxOrthoSize = 18f;
    [SerializeField] private float zoomSpeedMouseWheel = 18f;    // bigger = faster wheel zoom
    [SerializeField] private float zoomSpeedPinch = 0.01f;       // bigger = faster pinch zoom

    [Header("Optional: Keep Rig Flat")]
    [SerializeField] private bool lockRigY = true;
    [SerializeField] private float rigY = 0f;

    [Header("Bounds (Optional)")]
    [SerializeField] private bool useBounds = true;
    [Tooltip("World bounds for the CAMERA RIG center (x,z).")]
    [SerializeField] private Vector2 minXz = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxXz = new Vector2(50f, 50f);

    // smoothing state
    private Vector3 targetRigPos;
    private Vector3 rigVel;

    private float targetOrtho;
    private float zoomVel;

    // input state
    private bool isDragging;
    private Vector2 lastPointerPos;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) cam.orthographic = true;

        targetRigPos = transform.position;
        if (lockRigY) targetRigPos.y = rigY;

        targetOrtho = cam != null ? cam.orthographicSize : Mathf.Clamp((minOrthoSize + maxOrthoSize) * 0.5f, minOrthoSize, maxOrthoSize);
        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, maxOrthoSize);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        HandlePanInput();
        HandleZoomInput();

        // Smooth pan
        Vector3 desired = targetRigPos;
        if (lockRigY) desired.y = rigY;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref rigVel, panSmoothTime);

        // Smooth zoom
        float z = Mathf.SmoothDamp(cam.orthographicSize, targetOrtho, ref zoomVel, zoomSmoothTime);
        cam.orthographicSize = Mathf.Clamp(z, minOrthoSize, maxOrthoSize);

        // Clamp bounds (apply to both current + target to prevent fighting)
        if (useBounds)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minXz.x, maxXz.x);
            p.z = Mathf.Clamp(p.z, minXz.y, maxXz.y);
            if (lockRigY) p.y = rigY;
            transform.position = p;

            Vector3 tp = targetRigPos;
            tp.x = Mathf.Clamp(tp.x, minXz.x, maxXz.x);
            tp.z = Mathf.Clamp(tp.z, minXz.y, maxXz.y);
            if (lockRigY) tp.y = rigY;
            targetRigPos = tp;
        }
    }

    // -------------------- PAN --------------------
    private void HandlePanInput()
    {
        // If pinching, don't pan
        if (Input.touchCount >= 2)
        {
            isDragging = false;
            return;
        }

        // Mobile: 1 finger drag
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                return;

            if (t.phase == TouchPhase.Began)
            {
                isDragging = true;
                lastPointerPos = t.position;
            }
            else if (t.phase == TouchPhase.Moved && isDragging)
            {
                Vector2 delta = t.position - lastPointerPos;
                lastPointerPos = t.position;

                PanByScreenDelta(delta);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }

            return;
        }

        // PC: mouse drag
        bool dragHeld = requireRightMouseOnPC ? Input.GetMouseButton(1) : Input.GetMouseButton(0);
        bool dragDown = requireRightMouseOnPC ? Input.GetMouseButtonDown(1) : Input.GetMouseButtonDown(0);
        bool dragUp   = requireRightMouseOnPC ? Input.GetMouseButtonUp(1) : Input.GetMouseButtonUp(0);

        if (dragDown)
        {
            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            isDragging = true;
            lastPointerPos = Input.mousePosition;
        }
        else if (dragHeld && isDragging)
        {
            Vector2 current = Input.mousePosition;
            Vector2 delta = current - lastPointerPos;
            lastPointerPos = current;

            PanByScreenDelta(delta);
        }
        else if (dragUp)
        {
            isDragging = false;
        }
    }

    private void PanByScreenDelta(Vector2 screenDelta)
    {
        // Orthographic: pan feels best when drag speed scales with ortho size (zoom level)
        float zoomScale = Mathf.InverseLerp(minOrthoSize, maxOrthoSize, targetOrtho);
        float scaledUnitsPerPixel = panUnitsPerPixel * Mathf.Lerp(0.7f, 2.0f, zoomScale);

        // Use camera's right/forward projected onto XZ plane
        Vector3 right = cam.transform.right;  right.y = 0f; right.Normalize();
        Vector3 forward = cam.transform.forward; forward.y = 0f; forward.Normalize();

        // Drag right -> move camera left (so world follows finger)
        Vector3 move = (-right * screenDelta.x + -forward * screenDelta.y) * scaledUnitsPerPixel;

        targetRigPos += move;
        if (lockRigY) targetRigPos.y = rigY;
    }

    // -------------------- ZOOM --------------------
    private void HandleZoomInput()
    {
        // Mobile pinch
        if (Input.touchCount >= 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            if (blockPanOverUI && EventSystem.current != null)
            {
                if (EventSystem.current.IsPointerOverGameObject(a.fingerId) ||
                    EventSystem.current.IsPointerOverGameObject(b.fingerId))
                    return;
            }

            Vector2 aPrev = a.position - a.deltaPosition;
            Vector2 bPrev = b.position - b.deltaPosition;

            float prevDist = Vector2.Distance(aPrev, bPrev);
            float currDist = Vector2.Distance(a.position, b.position);
            float delta = currDist - prevDist;

            // Pinch out (delta>0) -> zoom in (smaller orthoSize)
            targetOrtho = Mathf.Clamp(targetOrtho - delta * zoomSpeedPinch, minOrthoSize, maxOrthoSize);
            return;
        }

        // PC mouse wheel
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.0001f)
        {
            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // wheel up -> zoom in -> smaller orthoSize
            targetOrtho = Mathf.Clamp(targetOrtho - wheel * zoomSpeedMouseWheel * Time.deltaTime, minOrthoSize, maxOrthoSize);
        }
    }
}
