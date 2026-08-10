using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using System.Collections;

public sealed class CameraController : MonoBehaviourPun
{
    [Header("UI Settings")]
    [SerializeField] private Button recenterButton;           // Drag your UI Button here
    [SerializeField] private string recenterButtonName = "FollowButton"; // Fallback find name
    [SerializeField] private float uiFindRetrySeconds = 0.25f;

    [Header("Target Settings")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -10f);
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Pan Settings")]
    [SerializeField] private float pcPanSpeed = 25f;
    [SerializeField] private float mobilePanSpeed = 15f;
    [SerializeField] private float dragThreshold = 10f;

    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 20f;
    [SerializeField] private float pcZoomSpeed = 5f;
    [SerializeField] private float mobileZoomSpeed = 0.5f;

    public static bool IsPanning { get; private set; }

    private Transform cameraAnchor;
    private Vector3 focusPosition;
    private Vector3 smoothVelocity;
    private bool followPlayer = true;

    // Input Tracking
    private Vector2 inputStartPos;
    private bool hasStartedDragging;

    private Coroutine bindRoutine;
    private bool isButtonBound;

    private void Awake()
    {
        if (!photonView.IsMine) { Destroy(gameObject); return; }

        if (transform.parent != null)
            cameraAnchor = transform.parent.Find("CameraAnchor");

        transform.SetParent(null);
    }

    private void OnEnable()
    {
        // Bind whenever this object becomes active (handles scene/UI rebuilds)
        TryBindRecenterButton();

        if (!isButtonBound)
        {
            // UI might spawn later -> keep trying
            if (bindRoutine != null) StopCoroutine(bindRoutine);
            bindRoutine = StartCoroutine(BindWhenAvailable());
        }
    }

    private void OnDisable()
    {
        UnbindRecenterButton();

        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }
    }

    private void Start()
    {
        // Initialize Focus Position
        if (cameraAnchor != null)
            focusPosition = cameraAnchor.position;
        else
            focusPosition = transform.position - followOffset;
    }

    private void Update()
    {
        // NOTE: Don't worry about this blocking the UI click.
        // The Button's onClick is handled by Unity's UI system, not by this Update loop.
        if (IsPointerOverUI()) return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        HandlePCInput();
#endif

        if (Input.touchCount > 0)
            HandleMobileInput();

        if (Input.GetKeyDown(KeyCode.Space)) EnterFollowMode();
    }

    private void LateUpdate()
    {
        if (followPlayer && cameraAnchor != null)
            focusPosition = cameraAnchor.position;

        Vector3 finalTargetPosition = focusPosition + followOffset;

        float currentSmoothTime = IsPanning ? 0.01f : followSmoothTime;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            finalTargetPosition,
            ref smoothVelocity,
            currentSmoothTime
        );
    }

    // ---------------------- PC LOGIC ----------------------
    private void HandlePCInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Zoom(scroll * pcZoomSpeed);
            followPlayer = false;
        }

        if (Input.GetMouseButtonDown(0))
        {
            inputStartPos = Input.mousePosition;
            hasStartedDragging = false;
            IsPanning = false;
        }
        else if (Input.GetMouseButton(0))
        {
            if (!hasStartedDragging && Vector2.Distance((Vector2)Input.mousePosition, inputStartPos) > dragThreshold)
            {
                hasStartedDragging = true;
                IsPanning = true;
                followPlayer = false;
            }

            if (hasStartedDragging)
            {
                float x = Input.GetAxis("Mouse X") * pcPanSpeed * Time.deltaTime;
                float z = Input.GetAxis("Mouse Y") * pcPanSpeed * Time.deltaTime;
                focusPosition -= new Vector3(x, 0, z);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            IsPanning = false;
            hasStartedDragging = false;
        }
    }

    // ---------------------- MOBILE LOGIC ----------------------
    private void HandleMobileInput()
    {
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            Zoom(-deltaMagnitudeDiff * mobileZoomSpeed * 0.1f);

            IsPanning = true;
            followPlayer = false;
        }
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    inputStartPos = touch.position;
                    hasStartedDragging = false;
                    IsPanning = false;
                    break;

                case TouchPhase.Moved:
                    if (!hasStartedDragging && Vector2.Distance(touch.position, inputStartPos) > dragThreshold)
                    {
                        hasStartedDragging = true;
                        IsPanning = true;
                        followPlayer = false;
                    }

                    if (hasStartedDragging)
                    {
                        float x = touch.deltaPosition.x * mobilePanSpeed * Time.deltaTime;
                        float z = touch.deltaPosition.y * mobilePanSpeed * Time.deltaTime;
                        focusPosition -= new Vector3(x, 0, z);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    IsPanning = false;
                    hasStartedDragging = false;
                    break;
            }
        }
    }

    private void Zoom(float increment)
    {
        Vector3 currentDirection = followOffset.normalized;
        float currentDistance = followOffset.magnitude;
        currentDistance -= increment;
        currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
        followOffset = currentDirection * currentDistance;
    }

    public void EnterFollowMode()
    {
        followPlayer = true;
        smoothVelocity = Vector3.zero;
        if (cameraAnchor != null) focusPosition = cameraAnchor.position;
        Debug.Log("Camera re-centered on player.");
    }

    // ---------------------- UI BINDING (FIX) ----------------------
    private IEnumerator BindWhenAvailable()
    {
        while (!isButtonBound)
        {
            TryBindRecenterButton();
            if (isButtonBound) yield break;
            yield return new WaitForSeconds(uiFindRetrySeconds);
        }
    }

    private void TryBindRecenterButton()
    {
        if (recenterButton == null)
        {
            GameObject btnObj = GameObject.Find(recenterButtonName);
            if (btnObj != null) btnObj.TryGetComponent(out recenterButton);
        }

        if (recenterButton != null && !isButtonBound)
        {
            recenterButton.onClick.AddListener(EnterFollowMode);
            isButtonBound = true;
        }
    }

    private void UnbindRecenterButton()
    {
        if (recenterButton != null && isButtonBound)
        {
            recenterButton.onClick.RemoveListener(EnterFollowMode);
        }
        isButtonBound = false;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        // Mouse / Editor / Standalone
        if (EventSystem.current.IsPointerOverGameObject()) return true;

        // Touch
        if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            return true;

        return false;
    }
}
