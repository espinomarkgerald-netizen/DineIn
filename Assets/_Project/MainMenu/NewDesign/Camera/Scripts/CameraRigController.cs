using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CameraRigController : MonoBehaviour
{
    [Header("Handheld Effect")]
    [SerializeField] private float intensity = 0.05f;
    [SerializeField] private float frequency = 1.5f;

    [Header("Mouse Pan Settings")]
    [SerializeField] private float mouseLookSensitivity = 0.5f;
    [SerializeField] private float mouseReturnSpeed = 2.0f;

    [Header("Movement Settings")]
    [SerializeField] private float smoothSpeed = 3.0f;
    [SerializeField] private float stopDistance = 0.01f;
    [SerializeField] private Transform[] cameraPoints;

    [Header("Zoom Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float[] cameraSizes;
    [SerializeField] private float zoomSpeed = 2.0f;
    [SerializeField] private float stopSizeThreshold = 0.01f;

    [Header("Input Configurations")]
    [SerializeField] private CameraSwitchConfig[] switchConfigs;

    public enum InputType { ScreenTap, ButtonClick }

    [System.Serializable]
    public struct CameraSwitchConfig
    {
        public string description;
        public InputType inputType;
        public Button triggerButton;
        public Button exitButton;
        public int targetIndex;
    }

    /// <summary>
    /// Snapshot of the "pointer" for this frame, regardless of whether it came
    /// from a mouse or a touch. This is the single abstraction the rest of the
    /// class talks to instead of branching on platform/Input APIs directly.
    /// </summary>
    private struct PointerState
    {
        public Vector3 screenPosition;
        public bool isActive;      // Is there a live pointer we should pan toward?
        public bool wasPressedThisFrame;
        public int fingerId;       // -1 for mouse, actual finger id for touch
    }

    // Decided once, not evaluated every frame inside Update.
    // On touch-only platforms, a lack of touches means "no input", so we
    // return to center instead of panning based on a stale/last-known mouse position.
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private const bool IsTouchOnlyPlatform = true;
#else
    private const bool IsTouchOnlyPlatform = false;
#endif

    private int currentIndex = 0;
    private float targetSize;
    private Vector3 mouseOffset;
    private bool isMoving = false;

    // Reused every frame to avoid GC allocations from repeated raycasts.
    private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Start()
    {
        if (mainCamera != null) targetSize = mainCamera.orthographicSize;

        foreach (var config in switchConfigs)
        {
            if (config.inputType == InputType.ButtonClick && config.triggerButton != null)
            {
                config.triggerButton.onClick.AddListener(() => SwitchToCameraPoint(config.targetIndex));
            }

            if (config.exitButton != null)
            {
                config.exitButton.onClick.AddListener(() => SwitchToCameraPoint(0));
            }
        }
    }

    void Update()
    {
        // 1. Array and Null Safety
        if (cameraPoints == null || cameraPoints.Length == 0 || currentIndex >= cameraPoints.Length) return;

        // 2. Handheld Noise
        float x = Mathf.PerlinNoise(Time.time * frequency, 0f) - 0.5f;
        float y = Mathf.PerlinNoise(0f, Time.time * frequency) - 0.5f;
        Vector3 noise = new Vector3(x, y, 0) * intensity;

        // 3. Unified pointer read (mouse or touch, resolved in one place)
        PointerState pointer = GetPointerState();

        float panX = 0f;
        float panY = 0f;

        if (pointer.isActive)
        {
            panX = (pointer.screenPosition.x / (Screen.width > 0 ? Screen.width : 1)) - 0.5f;
            panY = (pointer.screenPosition.y / (Screen.height > 0 ? Screen.height : 1)) - 0.5f;
        }

        if (transform != null)
        {
            // If there's active input, pan toward the offset; otherwise ease back to center.
            Vector3 targetOffset = pointer.isActive
                ? (transform.right * panX + transform.up * panY) * (mouseLookSensitivity * 2f)
                : Vector3.zero;

            mouseOffset = Vector3.Lerp(mouseOffset, targetOffset, Time.deltaTime * mouseReturnSpeed);
        }

        // 4. Movement Logic
        Transform target = cameraPoints[currentIndex];
        if (target != null)
        {
            Vector3 desiredPosition = target.position + noise + mouseOffset;

            // NaN Firewall
            if (float.IsNaN(desiredPosition.x) || float.IsNaN(desiredPosition.y) || float.IsNaN(desiredPosition.z)) return;

            if (isMoving)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, Time.deltaTime * smoothSpeed);

                if (mainCamera != null)
                    mainCamera.orthographicSize = Mathf.MoveTowards(mainCamera.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);

                if (Vector3.Distance(transform.position, target.position) < stopDistance &&
                    (mainCamera == null || Mathf.Abs(mainCamera.orthographicSize - targetSize) < stopSizeThreshold))
                {
                    isMoving = false;
                }
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            }
        }

        // 5. Tap / Click -> Camera Switch
        if (pointer.wasPressedThisFrame && !IsPointerOverUI(pointer))
        {
            foreach (var config in switchConfigs)
            {
                if (config.inputType == InputType.ScreenTap)
                {
                    SwitchToCameraPoint(config.targetIndex);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Resolves this frame's input into a single platform-agnostic PointerState.
    /// Touch is always preferred when present (covers PC devices with touchscreens too).
    /// When no touch is present, falls back to mouse on non-touch-only platforms,
    /// or to an inactive state on touch-only platforms so the camera recenters
    /// instead of panning toward a stale position.
    /// </summary>
    private PointerState GetPointerState()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return new PointerState
            {
                screenPosition = touch.position,
                isActive = true,
                wasPressedThisFrame = touch.phase == TouchPhase.Began,
                fingerId = touch.fingerId
            };
        }

        if (IsTouchOnlyPlatform)
        {
            // No touches on a touch-only device: nothing to pan toward.
            return new PointerState
            {
                screenPosition = Vector3.zero,
                isActive = false,
                wasPressedThisFrame = false,
                fingerId = -1
            };
        }

        // Mouse-driven platforms (PC / Editor / desktop builds)
        return new PointerState
        {
            screenPosition = Input.mousePosition,
            isActive = true,
            wasPressedThisFrame = Input.GetMouseButtonDown(0),
            fingerId = -1
        };
    }

    /// <summary>
    /// Robust UI hit-test for both mouse and touch, across every Graphic Raycaster
    /// in the scene - Screen Space and World Space canvases alike.
    ///
    /// We intentionally do NOT use EventSystem.IsPointerOverGameObject() /
    /// IsPointerOverGameObject(fingerId) here. Those methods read *cached* pointer
    /// state that the active input module (StandaloneInputModule /
    /// InputSystemUIInputModule) resolves during its own Update(). Depending on
    /// script execution order, and on how many World Space canvases the module has
    /// to resolve that frame, that cache can be stale or simply not yet populated
    /// for a given canvas when this script's Update() runs - which is exactly the
    /// kind of inconsistent tap-through you'd see on some World Space canvases
    /// (Options/Account) but not others (Main Menu, Screen Space).
    ///
    /// Instead, we build a PointerEventData for this frame's actual screen
    /// position and ask EventSystem to RaycastAll against every registered
    /// raycaster directly. This is a fresh, synchronous raycast - it doesn't
    /// depend on any module's internal bookkeeping, so it's consistent for
    /// mouse and touch, and for every canvas whose Graphic Raycaster has its
    /// Event Camera assigned (which yours already do).
    /// </summary>
    private bool IsPointerOverUI(PointerState pointer)
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = pointer.screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        return uiRaycastResults.Count > 0;
    }

    public void SwitchToCameraPoint(int index)
    {
        if (index < 0 || index >= cameraPoints.Length || cameraPoints[index] == null) return;

        currentIndex = index;
        if (currentIndex < cameraSizes.Length) targetSize = cameraSizes[currentIndex];
        isMoving = true;
    }
}