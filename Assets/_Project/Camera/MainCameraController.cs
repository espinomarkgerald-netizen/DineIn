using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Pan (Drag)")]
    [SerializeField] private float panUnitsPerPixel = 0.02f;
    [SerializeField] private float panSmoothTime = 0.08f;
    [SerializeField] private bool requireRightMouseOnPC = true;
    [SerializeField] private bool blockPanOverUI = true;
    [SerializeField, Min(1f)] private float panGestureThresholdPixels = 32f;

    [Header("Zoom (Orthographic Size)")]
    [SerializeField] private float zoomSmoothTime = 0.10f;
    [SerializeField] private float minOrthoSize = 4f;
    [SerializeField] private float maxOrthoSize = 18f;
    [SerializeField] private float zoomSpeedMouseWheel = 18f;
    [SerializeField] private float zoomSpeedPinch = 0.01f;

    [Header("Optional: Keep Rig Flat")]
    [SerializeField] private bool lockRigY = true;
    [SerializeField] private float rigY = 0f;

    [Header("Bounds (Optional)")]
    [SerializeField] private bool useBounds = true;
    [Tooltip("World bounds for the CAMERA RIG center (x,z).")]
    [SerializeField] private Vector2 minXz = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxXz = new Vector2(50f, 50f);

    [Header("Viewport Bounds (Optional)")]
    [Tooltip("Constrain the visible view, rather than just the rig center. Disabled in existing scenes unless explicitly enabled.")]
    [SerializeField] private bool useViewportBounds;
    [Tooltip("World-space center of the usable restaurant framing area.")]
    [SerializeField] private Vector3 viewBoundsCenter;
    [Tooltip("Width and height in camera-plane world units. Include a small margin around usable furniture.")]
    [SerializeField] private Vector2 viewBoundsSize = new Vector2(100f, 60f);

    private float MaximumOrthoSize => useViewportBounds && cam != null
        ? Mathf.Max(minOrthoSize, Mathf.Min(maxOrthoSize, Mathf.Max(viewBoundsSize.x / (2f * Mathf.Max(.01f, cam.aspect)), viewBoundsSize.y * .5f)))
        : maxOrthoSize;

    private Vector3 ClampRigPosition(Vector3 position, float size)
    {
        if (useViewportBounds && cam != null)
        {
            // Solve a world XZ translation from camera-plane offsets, retaining rig height.
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float determinant = right.x * up.z - right.z * up.x;
            if (Mathf.Abs(determinant) > .0001f)
            {
                Vector3 relative = cam.transform.position + position - transform.position - viewBoundsCenter;
                float x = Vector3.Dot(relative, right), y = Vector3.Dot(relative, up);
                float limitX = Mathf.Max(0f, viewBoundsSize.x * .5f - size * cam.aspect);
                float limitY = Mathf.Max(0f, viewBoundsSize.y * .5f - size);
                float dx = Mathf.Clamp(x, -limitX, limitX) - x;
                float dy = Mathf.Clamp(y, -limitY, limitY) - y;
                position.x += (dx * up.z - right.z * dy) / determinant;
                position.z += (right.x * dy - dx * up.x) / determinant;
            }
        }
        else if (useBounds)
        {
            position.x = Mathf.Clamp(position.x, minXz.x, maxXz.x);
            position.z = Mathf.Clamp(position.z, minXz.y, maxXz.y);
        }
        if (lockRigY) position.y = rigY;
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useViewportBounds || cam == null) return;
        Gizmos.color = new Color(.2f, .85f, 1f, 1f);
        Vector3 r = cam.transform.right * viewBoundsSize.x * .5f;
        Vector3 u = cam.transform.up * viewBoundsSize.y * .5f;
        Gizmos.DrawLine(viewBoundsCenter-r-u, viewBoundsCenter+r-u);
        Gizmos.DrawLine(viewBoundsCenter+r-u, viewBoundsCenter+r+u);
        Gizmos.DrawLine(viewBoundsCenter+r+u, viewBoundsCenter-r+u);
        Gizmos.DrawLine(viewBoundsCenter-r+u, viewBoundsCenter-r-u);
    }

    private Vector3 targetRigPos;
    private Vector3 rigVel;

    private float targetOrtho;
    private float zoomVel;

    private Transform followTarget;
    private Vector3 followLocalOffset;
    private bool followSuspended;

    private bool isDragging;
    private Vector2 lastPointerPos;
    private Vector2 accumulatedPanPixels;
    private float accumulatedPanDistance;
    private bool panGestureReported;

    private static MainCameraController inputController;
    private int rightReleaseFrame = -1;
    private bool rightReleaseIsClick;

    // Shared release decision: camera input runs in LateUpdate, while task handlers
    // run in Update. Cache before clearing the gesture so either order gives the same result.
    public static bool RightClickCancelRequested
    {
        get
        {
            if (!Input.GetMouseButtonUp(1) || inputController == null) return false;
            inputController.CaptureRightClickRelease();
            return inputController.rightReleaseIsClick && !PanInputBlocked;
        }
    }

    private static bool PanInputBlocked => LobbyPauseMenu.IsAnyOpen
        || MultiplayerSocialUI.InputBlocked || GameplayUIBlocker.IsBlocked();

    private void OnEnable() { inputController = this; }
    private void OnDisable()
    {
        if (inputController == this) inputController = null;
        isDragging = false;
        rightReleaseFrame = -1;
        rightReleaseIsClick = false;
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) { isDragging = false; rightReleaseIsClick = false; }
    }
    private void CaptureRightClickRelease()
    {
        if (!Input.GetMouseButtonUp(1) || rightReleaseFrame == Time.frameCount) return;
        rightReleaseFrame = Time.frameCount;
        // Include movement on the release frame, even when no held frame saw it.
        float distance = accumulatedPanDistance + Vector2.Distance(lastPointerPos, Input.mousePosition);
        rightReleaseIsClick = requireRightMouseOnPC && isDragging && !panGestureReported
            && distance < panGestureThresholdPixels && !PanInputBlocked
            && !(blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
    }

    public event Action<Vector2> CameraPanned;

    public Camera Cam => cam;
    public Transform FollowTarget => followTarget;
    public bool FollowSuspended => followSuspended;
    public float NormalizedZoom => cam != null
        ? Mathf.InverseLerp(minOrthoSize, MaximumOrthoSize, cam.orthographicSize)
        : 0f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) cam.orthographic = true;

        targetRigPos = transform.position;
        if (lockRigY) targetRigPos.y = rigY;

        targetOrtho = cam != null
            ? cam.orthographicSize
            : Mathf.Clamp((minOrthoSize + maxOrthoSize) * 0.5f, minOrthoSize, MaximumOrthoSize);

        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, MaximumOrthoSize);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (followTarget != null && !followTarget.gameObject.activeInHierarchy)
            StopFollowing();

        HandlePanInput();
        HandleZoomInput();
        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, MaximumOrthoSize);
        if (followTarget != null && !followSuspended)
            CenterOnWorldPoint(followTarget.TransformPoint(followLocalOffset));

        Vector3 desired = targetRigPos;
        if (lockRigY) desired.y = rigY;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref rigVel, panSmoothTime);

        float z = Mathf.SmoothDamp(cam.orthographicSize, targetOrtho, ref zoomVel, zoomSmoothTime);
        cam.orthographicSize = Mathf.Clamp(z, minOrthoSize, MaximumOrthoSize);

        targetRigPos = ClampRigPosition(targetRigPos, Mathf.Max(targetOrtho, cam.orthographicSize));
        transform.position = ClampRigPosition(transform.position, cam.orthographicSize);
    }

    public void SetRigTargetPosition(Vector3 worldPos, bool snapInstant = false)
    {
        targetRigPos = worldPos;

        if (lockRigY)
            targetRigPos.y = rigY;

        targetRigPos = ClampRigPosition(targetRigPos, cam != null ? Mathf.Max(targetOrtho, cam.orthographicSize) : targetOrtho);

        if (snapInstant)
        {
            rigVel = Vector3.zero;
            transform.position = targetRigPos;
        }
    }

    public void CenterOnWorldPoint(Vector3 worldPos, bool snapInstant = false)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mathf.Abs(cam.transform.forward.y) < .0001f)
        {
            SetRigTargetPosition(worldPos, snapInstant);
            return;
        }

        // The isometric camera is offset from its rig. Move the rig by the
        // difference between the player's feet and the screen-center ray.
        float distanceToPlayerHeight = (worldPos.y - cam.transform.position.y) / cam.transform.forward.y;
        Vector3 centerAtPlayerHeight = cam.transform.position + cam.transform.forward * distanceToPlayerHeight;
        Vector3 centeredRig = transform.position + worldPos - centerAtPlayerHeight;
        SetRigTargetPosition(centeredRig, snapInstant);
    }

    public void SetFollowTarget(Transform target, Vector3 localFocusOffset)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            StopFollowing();
            return;
        }

        followTarget = target;
        followLocalOffset = localFocusOffset;
        isDragging = false;
        rigVel = Vector3.zero;
        if (!followSuspended)
            CenterOnWorldPoint(target.TransformPoint(localFocusOffset));
    }

    public void StopFollowing()
    {
        followTarget = null;
        followLocalOffset = Vector3.zero;
        targetRigPos = transform.position;
        rigVel = Vector3.zero;
    }

    // Temporary scripted framing keeps the selected follow target for resuming.
    public void SetFollowSuspended(bool suspended)
    {
        followSuspended = suspended;
    }

    public Vector3 GetRigTargetPosition()
    {
        return targetRigPos;
    }

    public float GetTargetOrthographicSize()
    {
        return targetOrtho;
    }

    public void SetTargetOrthographicSize(float orthographicSize, bool snapInstant = false)
    {
        targetOrtho = Mathf.Clamp(orthographicSize, minOrthoSize, MaximumOrthoSize);
        if (snapInstant && cam != null)
        {
            zoomVel = 0f;
            cam.orthographicSize = targetOrtho;
            targetRigPos = ClampRigPosition(targetRigPos, targetOrtho);
            transform.position = ClampRigPosition(transform.position, targetOrtho);
        }
    }

    private void HandlePanInput()
    {
        CaptureRightClickRelease();
        if (PanInputBlocked) { isDragging = false; return; }
        if (followTarget == null && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            PanByScreenDelta(-DineIn.NewMenu.SettingsManager.EdgePanDirection(edgePanMargin) * edgePanPixelsPerSecond * Time.deltaTime);
        if (Input.touchCount >= 2)
        {
            isDragging = false;
            accumulatedPanPixels = Vector2.zero;
            accumulatedPanDistance = 0f;
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                return;

            if (t.phase == TouchPhase.Began)
            {
                BeginPanGesture();
                lastPointerPos = t.position;
            }
            else if (t.phase == TouchPhase.Moved && isDragging)
            {
                Vector2 delta = t.position - lastPointerPos;
                lastPointerPos = t.position;
                RecordPanGesture(delta);
                PanByScreenDelta(delta);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.phase == TouchPhase.Ended && isDragging && followTarget != null)
                {
                    Vector2 delta = t.position - lastPointerPos;
                    RecordPanGesture(delta);
                    PanByScreenDelta(delta);
                }
                isDragging = false;
            }

            return;
        }

        bool dragHeld = requireRightMouseOnPC ? Input.GetMouseButton(1) : Input.GetMouseButton(0);
        bool dragDown = requireRightMouseOnPC ? Input.GetMouseButtonDown(1) : Input.GetMouseButtonDown(0);
        bool dragUp = requireRightMouseOnPC ? Input.GetMouseButtonUp(1) : Input.GetMouseButtonUp(0);

        if (dragDown)
        {
            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            BeginPanGesture();
            lastPointerPos = Input.mousePosition;
        }
        else if (dragHeld && isDragging)
        {
            Vector2 current = Input.mousePosition;
            Vector2 delta = current - lastPointerPos;
            lastPointerPos = current;
            RecordPanGesture(delta);
            PanByScreenDelta(delta);
        }
        else if (dragUp)
        {
            if (isDragging && followTarget != null)
            {
                Vector2 delta = (Vector2)Input.mousePosition - lastPointerPos;
                RecordPanGesture(delta);
                PanByScreenDelta(delta);
            }
            isDragging = false;
        }
    }

    private void BeginPanGesture()
    {
        isDragging = true;
        accumulatedPanPixels = Vector2.zero;
        accumulatedPanDistance = 0f;
        panGestureReported = false;
    }

    [Header("Desktop Edge Pan")]
    [SerializeField, Min(1)] private float edgePanMargin = 20f;
    [SerializeField, Min(1)] private float edgePanPixelsPerSecond = 450f;

    private void RecordPanGesture(Vector2 screenDelta)
    {
        if (!isDragging || panGestureReported)
            return;

        accumulatedPanPixels += screenDelta;
        accumulatedPanDistance += screenDelta.magnitude;
        if (accumulatedPanDistance < panGestureThresholdPixels)
            return;

        panGestureReported = true;
        if (followTarget != null && !followSuspended)
            StopFollowing();
        Vector2 reportedMovement = accumulatedPanPixels.sqrMagnitude > 0.001f
            ? accumulatedPanPixels
            : screenDelta;
        CameraPanned?.Invoke(reportedMovement);
    }

    private void PanByScreenDelta(Vector2 screenDelta)
    {
        // Ignore tap jitter and edge pan until an intentional drag releases follow.
        if (followTarget != null) return;

        float zoomScale = Mathf.InverseLerp(minOrthoSize, MaximumOrthoSize, targetOrtho);
        float scaledUnitsPerPixel = panUnitsPerPixel * DineIn.NewMenu.SettingsManager.PanMultiplier * Mathf.Lerp(0.7f, 2.0f, zoomScale);

        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 move = (-right * screenDelta.x + -forward * screenDelta.y) * scaledUnitsPerPixel;

        targetRigPos += move;

        if (lockRigY)
            targetRigPos.y = rigY;
    }

    private void HandleZoomInput()
    {
        if (LobbyPauseMenu.IsAnyOpen) return;
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

            targetOrtho = Mathf.Clamp(targetOrtho - delta * zoomSpeedPinch * DineIn.NewMenu.SettingsManager.ZoomMultiplier, minOrthoSize, MaximumOrthoSize);
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.0001f)
        {
            if (blockPanOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            targetOrtho = Mathf.Clamp(
                targetOrtho - wheel * zoomSpeedMouseWheel * DineIn.NewMenu.SettingsManager.ZoomMultiplier * Time.deltaTime,
                minOrthoSize,
                MaximumOrthoSize
            );
        }
    }
}
