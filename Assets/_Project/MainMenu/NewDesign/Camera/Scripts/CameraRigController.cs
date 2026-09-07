using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class CameraRigController : MonoBehaviour
{
    public enum MenuState { Opening, Transition, Menu, Options, Profile }
    public MenuState State { get; private set; } = MenuState.Opening;
    private bool ownsTitleFlow;
    private bool titleReady;
    [Header("Opening Screen UI")]
    [Tooltip("Edit title wording, font, color and layout on this scene TMP object.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Edit the shared Click/Tap appearance on this scene TMP object.")]
    [SerializeField] private TMP_Text titlePrompt;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private string pcPromptText = "CLICK THE SCREEN";
    [SerializeField] private string touchPromptText = "TAP THE SCREEN";
    [SerializeField, Range(0.01f, .65f)] private float titleFadeOutDuration = .2f;

    [Header("Opening Prompt Animation")]
    [SerializeField] private bool enablePulse;
    [SerializeField, Range(0f, 1f)] private float minAlpha = .65f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [Tooltip("Pulse cycles per second; multiplies the TMP text color alpha.")]
    [SerializeField, Min(0f)] private float pulseSpeed = .5f;
    private readonly Dictionary<Canvas, int> boardOwners = new();
    private readonly List<(Button button, UnityEngine.Events.UnityAction action)> menuListeners = new();
    private bool touchPresentation;
    private Vector3 lastMousePosition;

    [Header("Opening Screen Idle")]
    [SerializeField] private bool openingIdleEnabled = true;
    [SerializeField, Min(0f), InspectorName("Horizontal Position Amount")] private float openingPositionDrift = 0.018f;
    [SerializeField, Min(0f)] private float verticalPositionAmount = .0099f;
    [SerializeField, Min(0f), InspectorName("Yaw Amount")] private float openingRotationDrift = 0.18f;
    [SerializeField, Min(0f)] private float pitchAmount = .108f;
    [SerializeField, Min(0.01f), InspectorName("Position Speed")] private float openingIdleSpeed = 0.22f;
    [SerializeField, Min(0.01f)] private float rotationSpeed = .22f;
    [Tooltip("Seconds to remove idle offset during the existing 0.65 second camera move.")]
    [SerializeField, Range(.01f, .65f)] private float blendOutDuration = .3f;

    private void Awake()
    {
        ownsTitleFlow = gameObject.scene.name == "NewMainMenu";
        if (!ownsTitleFlow) return;
        if (titleGroup != null)
        {
            titleGroup.alpha = 0f;
            titleGroup.interactable = titleGroup.blocksRaycasts = false;
        }
        touchPresentation = Application.isMobilePlatform || (Input.touchSupported && !Input.mousePresent);
        lastMousePosition = Input.mousePosition;
        // Disable real raycasters before the first EventSystem update, not just a visual cover.
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.gameObject.scene == gameObject.scene && canvas.GetComponentInChildren<Selectable>(true) != null)
                SetBoardInput(canvas, false);
        if (PlayFabAuthManager.Instance != null)
            SetBoardInput(PlayFabAuthManager.Instance.GetComponent<Canvas>(), false);
    }

    private void BindMenuButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
        menuListeners.Add((button, action));
    }

    private IEnumerator PrepareTitle()
    {
        // Auth may have retained its original canvas across a menu round trip.
        yield return null;
        foreach (var config in switchConfigs)
        {
            if (config.inputType != InputType.ButtonClick || config.triggerButton == null) continue;
            Canvas main = config.triggerButton.GetComponentInParent<Canvas>();
            if (main != null) boardOwners[main] = 1;
            Button close = config.exitButton;
            Canvas popup = close != null ? close.GetComponentInParent<Canvas>() : null;
            if (config.targetIndex == 3 && PlayFabAuthManager.Instance != null)
            {
                popup = PlayFabAuthManager.Instance.GetComponent<Canvas>();
                if (popup != null && (close == null || close.GetComponentInParent<Canvas>() != popup))
                    close = FindCanvasLevelButton(popup, "Account ExitButton");
            }
            if (popup != null)
            {
                popup.worldCamera = mainCamera;
                boardOwners[popup] = config.targetIndex;
            }
            PrepareCloseButton(close);
            int index = config.targetIndex;
            BindMenuButton(config.triggerButton, () => SwitchToCameraPoint(index));
            BindMenuButton(close, () => SwitchToCameraPoint(1));
        }
        ApplyMenuOwnership();
        if (cameraPoints.Length > 0 && cameraPoints[0] != null)
        {
            transform.SetPositionAndRotation(cameraPoints[0].position, cameraPoints[0].rotation);
            if (mainCamera != null && cameraSizes.Length > 0) mainCamera.orthographicSize = cameraSizes[0];
        }
        IrisScaleToggle iris = IrisScaleToggle.Ensure();
        while (!iris.IsOpen || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)) yield return null;
        if (titleGroup != null) titleGroup.alpha = 1f;
        while (Input.GetMouseButton(0) || Input.touchCount > 0) yield return null;
        yield return null; // Do not reuse a release from the loading screen.
        titleReady = true;
    }

    private static Button FindCanvasLevelButton(Canvas canvas, string buttonName)
    {
        if (canvas == null) return null;
        foreach (Button candidate in canvas.GetComponentsInChildren<Button>(true))
        {
            if (candidate.name != buttonName) continue;
            if (candidate.transform.parent == canvas.transform && candidate.gameObject.activeInHierarchy)
                return candidate;
        }
        return null;
    }

    public static void PrepareCloseButton(Button close)
    {
        if (close == null) return;
        close.transform.SetAsLastSibling();
        if (close.targetGraphic != null)
        {
            close.targetGraphic.raycastTarget = true;
            close.targetGraphic.raycastPadding = new Vector4(-14, -14, -14, -14);
        }
        foreach (TMP_Text text in close.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
    }

    private static void SetBoardInput(Canvas canvas, bool allowed)
    {
        if (canvas == null) return;
        CanvasGroup group = canvas.GetComponent<CanvasGroup>();
        if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.interactable = allowed;
        group.blocksRaycasts = allowed;
        foreach (CanvasGroup child in canvas.GetComponentsInChildren<CanvasGroup>(true))
            if (child != group) child.ignoreParentGroups = false;
        foreach (GraphicRaycaster raycaster in canvas.GetComponentsInChildren<GraphicRaycaster>(true))
            raycaster.enabled = allowed;
    }

    private void ApplyMenuOwnership()
    {
        int active = State == MenuState.Menu ? 1 : State == MenuState.Options ? 2 : State == MenuState.Profile ? 3 : -1;
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) active = -1;
        foreach (var board in boardOwners) SetBoardInput(board.Key, board.Value == active);
    }

    private IEnumerator MoveToBoard(int index)
    {
        bool leavingOpening = State == MenuState.Opening;
        State = MenuState.Transition;
        ApplyMenuOwnership();
        EventSystem.current?.SetSelectedGameObject(null);
        Vector3 from = leavingOpening ? cameraPoints[0].position : transform.position;
        Quaternion rotation = leavingOpening ? cameraPoints[0].rotation : transform.rotation;
        Vector3 idlePositionOffset = leavingOpening ? transform.position - from : Vector3.zero;
        Quaternion idleRotationOffset = leavingOpening
            ? Quaternion.Inverse(rotation) * transform.rotation
            : Quaternion.identity;
        float fromSize = mainCamera != null ? mainCamera.orthographicSize : 1f;
        float toSize = index < cameraSizes.Length ? cameraSizes[index] : cameraSizes.Length > 1 ? cameraSizes[1] : fromSize;
        for (float elapsed = 0; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / .65f);
            float idleBlend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendOutDuration));
            Vector3 position = Vector3.Lerp(from, cameraPoints[index].position, t) + idlePositionOffset * idleBlend;
            Quaternion boardRotation = Quaternion.Slerp(rotation, cameraPoints[index].rotation, t);
            Quaternion idleRotation = Quaternion.Slerp(Quaternion.identity, idleRotationOffset, idleBlend);
            transform.SetPositionAndRotation(position, boardRotation * idleRotation);
            if (mainCamera != null) mainCamera.orthographicSize = Mathf.Lerp(fromSize, toSize, t);
            if (titleGroup != null) titleGroup.alpha = 1f - Mathf.Clamp01(elapsed / titleFadeOutDuration);
            yield return null;
        }
        transform.SetPositionAndRotation(cameraPoints[index].position, cameraPoints[index].rotation);
        if (mainCamera != null) mainCamera.orthographicSize = toSize;
        if (titleGroup != null) titleGroup.gameObject.SetActive(false);
        while (Input.GetMouseButton(0) || Input.touchCount > 0) yield return null;
        yield return null; // First press/release belongs only to the title/previous board.
        currentIndex = index;
        State = index == 2 ? MenuState.Options : index == 3 ? MenuState.Profile : MenuState.Menu;
        ApplyMenuOwnership();
    }

    private void OnDestroy()
    {
        foreach (var listener in menuListeners)
            if (listener.button != null) listener.button.onClick.RemoveListener(listener.action);
        foreach (var board in boardOwners) SetBoardInput(board.Key, false);
    }
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
        if (ownsTitleFlow) { StartCoroutine(PrepareTitle()); return; }
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
        if (ownsTitleFlow)
        {
            ApplyMenuOwnership();
            if (State == MenuState.Opening && openingIdleEnabled && cameraPoints.Length > 0 && cameraPoints[0] != null)
                ApplyOpeningIdle();
            if (Input.touchCount > 0) touchPresentation = true;
            else if (Input.mousePresent && (Input.GetMouseButtonDown(0) || Input.mousePosition != lastMousePosition))
                touchPresentation = false;
            lastMousePosition = Input.mousePosition;
            if (titlePrompt != null)
                titlePrompt.text = touchPresentation
                    ? touchPromptText : pcPromptText;
            if (titlePrompt != null && State == MenuState.Opening)
                titlePrompt.canvasRenderer.SetAlpha(enablePulse
                    ? Mathf.Lerp(Mathf.Min(minAlpha, maxAlpha), Mathf.Max(minAlpha, maxAlpha),
                        .5f + .5f * Mathf.Cos(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f))
                    : 1f);
            if (State == MenuState.Opening && titleReady &&
                (Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                StartCoroutine(MoveToBoard(1));
            return;
        }
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

    private void ApplyOpeningIdle()
    {
        Transform opening = cameraPoints[0];
        float time = Time.unscaledTime * openingIdleSpeed * Mathf.PI * 2f;
        float horizontal = Mathf.Sin(time * .73f) * openingPositionDrift;
        float vertical = Mathf.Sin(time * 1.11f + 1.7f) * verticalPositionAmount;
        float rotationTime = Time.unscaledTime * rotationSpeed * Mathf.PI * 2f;
        float yaw = Mathf.Sin(rotationTime * .61f + .8f) * openingRotationDrift;
        float pitch = Mathf.Sin(rotationTime * .89f + 2.2f) * pitchAmount;

        Vector3 positionOffset = opening.right * horizontal + opening.up * vertical;
        Quaternion rotationOffset = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(opening.position + positionOffset, opening.rotation * rotationOffset);
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
        if (ownsTitleFlow)
        {
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;
            if (State == MenuState.Menu && (index == 2 || index == 3) ||
                (State == MenuState.Options || State == MenuState.Profile) && index == 1)
                StartCoroutine(MoveToBoard(index));
            return;
        }

        currentIndex = index;
        if (currentIndex < cameraSizes.Length) targetSize = cameraSizes[currentIndex];
        isMoving = true;
    }
}
