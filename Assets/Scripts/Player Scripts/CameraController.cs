using UnityEngine;
using UnityEngine.UI; // Required for Button
using UnityEngine.EventSystems;
using Photon.Pun;

public sealed class CameraController : MonoBehaviourPun
{
    [Header("UI Settings")]
    [SerializeField] private Button recenterButton; // Drag your UI Button here

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

    private void Awake()
    {
        if (!photonView.IsMine) { Destroy(gameObject); return; }
        
        if (transform.parent != null)
            cameraAnchor = transform.parent.Find("CameraAnchor");
            
        transform.SetParent(null); 
    }

    private void Start()
    {
        // Initialize Focus Position
        if (cameraAnchor != null)
            focusPosition = cameraAnchor.position;
        else
            focusPosition = transform.position - followOffset;

        // Setup the button listener manually if assigned
        if (recenterButton != null)
        {
            recenterButton.onClick.RemoveAllListeners();
            recenterButton.onClick.AddListener(EnterFollowMode);
        }
        else
        {
            // Fallback: Try to find it by name if you forgot to drag it
            GameObject btnObj = GameObject.Find("FollowButton");
            if (btnObj != null && btnObj.TryGetComponent(out Button btn))
            {
                recenterButton = btn;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(EnterFollowMode);
            }
        }
    }

    private void Update()
    {
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
        // 1. If following, the Focus Point is locked to the player
        if (followPlayer && cameraAnchor != null)
        {
            focusPosition = cameraAnchor.position;
        }

        // 2. Calculate the final Target Position based on Focus + Zoom Offset
        Vector3 finalTargetPosition = focusPosition + followOffset;

        // 3. Smooth Damp
        float currentSmoothTime = IsPanning ? 0.01f : followSmoothTime;
        transform.position = Vector3.SmoothDamp(transform.position, finalTargetPosition, ref smoothVelocity, currentSmoothTime);
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
        if(cameraAnchor != null) focusPosition = cameraAnchor.position;
        Debug.Log("Camera re-centered on player.");
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;
        if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return true;
        return false;
    }
}