using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestockTruckInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;
    [SerializeField] private TMP_Text statusText;
    [SerializeField, Min(0.5f)] private float interactRadius = 2f;

    [Header("Arrival Animation")]
    [Tooltip("Signed world-Z distance behind the authored parking position.")]
    [SerializeField] private float approachZOffset = -24f;
    [SerializeField, Min(0.1f)] private float arrivalDuration = 2.8f;

    [Header("Arrival Horn")]
    [Tooltip("Assign the delivery-truck horn clip here.")]
    [SerializeField] private AudioClip arrivalHornClip;
    [SerializeField, Range(0f, 1f)] private float hornVolume = 1f;
    [SerializeField, Min(0.05f)] private float secondBeepDelay = 0.32f;

    [Header("Departure")]
    [SerializeField, Min(0f)] private float departureDelaySeconds = 2f;
    [SerializeField, Min(0.1f)] private float departureDuration = 2.4f;
    [SerializeField, Min(1f)] private float departureDistance = 24f;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;
    public bool IsParked => isParked;
    public bool ParkingConfigured => parkingConfigured;
    public bool HasReadyDelivery => RestockOrderManager.Instance != null &&
                                    RestockOrderManager.Instance.DeliveredContainerCount > 0;

    private Vector3 parkingPosition;
    private Quaternion parkingRotation;
    private Collider[] interactionColliders;
    private Renderer[] presentationRenderers;
    private Canvas[] presentationCanvases;
    private AudioSource hornSource;
    private Coroutine arrivalRoutine;
    private Coroutine hornRoutine;
    private Coroutine departureRoutine;
    private bool parkingConfigured;
    private bool isParked;

    public void Configure(Transform configuredStandPoint, TMP_Text configuredStatus)
    {
        standPoint = configuredStandPoint;
        statusText = configuredStatus;
    }

    public void ConfigureParkingPose(Vector3 position, Quaternion rotation)
    {
        parkingPosition = position;
        parkingRotation = rotation;
        parkingConfigured = true;

        RestockOrderManager manager = RestockOrderManager.Instance;
        bool hasReadyDelivery = manager != null && manager.DeliveredContainerCount > 0;
        bool wasAlreadyCollected = manager != null && manager.HotbarContainerCount > 0;
        if (hasReadyDelivery)
        {
            SetPresentationVisible(true);
            PlaceAtParkingPosition();
            PlayArrivalHorn();
        }
        else if (wasAlreadyCollected)
        {
            PlacePastDeparturePoint();
            SetPresentationVisible(false);
        }
        else
        {
            SetPresentationVisible(true);
            PlaceAtApproachPosition();
        }

        RefreshStatus();
    }

    private void Awake()
    {
        parkingPosition = transform.position;
        parkingRotation = transform.rotation;
        interactionColliders = GetComponentsInChildren<Collider>(true);
        presentationRenderers = GetComponentsInChildren<Renderer>(true);
        presentationCanvases = GetComponentsInChildren<Canvas>(true);
        hornSource = GetComponent<AudioSource>();
        if (hornSource == null)
            hornSource = gameObject.AddComponent<AudioSource>();
        hornSource.playOnAwake = false;
        hornSource.loop = false;
        hornSource.spatialBlend = 0f;
        hornSource.volume = hornVolume;
    }

    private void OnEnable()
    {
        RefreshStatus();
        if (RestockOrderManager.Instance != null)
        {
            RestockOrderManager.Instance.OrdersChanged += RefreshStatus;
            RestockOrderManager.Instance.OrderDelivered += HandleOrderDelivered;
        }
    }

    private void Start()
    {
        if (!parkingConfigured)
            ConfigureParkingPose(transform.position, transform.rotation);
    }

    private void OnDisable()
    {
        if (RestockOrderManager.Instance != null)
        {
            RestockOrderManager.Instance.OrdersChanged -= RefreshStatus;
            RestockOrderManager.Instance.OrderDelivered -= HandleOrderDelivered;
        }

        if (arrivalRoutine != null)
            StopCoroutine(arrivalRoutine);
        if (hornRoutine != null)
            StopCoroutine(hornRoutine);
        if (departureRoutine != null)
            StopCoroutine(departureRoutine);
    }

    public bool CanInteract() => isActiveAndEnabled && isParked && HasReadyDelivery;

    public void Interact(PlayerMovement mover)
    {
        RestockFlowCoordinator.EnsureInstance().OpenTruckCollection();
    }

    public float GetInteractRadius() => Mathf.Max(0.5f, interactRadius);

    private void RefreshStatus()
    {
        RestockOrderManager manager = RestockOrderManager.Instance;
        int ready = manager != null ? manager.DeliveredContainerCount : 0;
        SetInteractionReady(isParked && ready > 0);

        if (statusText != null)
        {
            if (!isParked && ready > 0)
                statusText.text = "DELIVERY TRUCK\nARRIVING";
            else if (ready > 0)
                statusText.text = "DELIVERY TRUCK\n" + ready + " BOXES READY";
            else
                statusText.text = "DELIVERY TRUCK\nNO ORDER READY";
        }
    }

    public bool IsVisibleFrom(Camera camera)
    {
        if (camera == null)
            return false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds && GeometryUtility.TestPlanesAABB(
            GeometryUtility.CalculateFrustumPlanes(camera),
            combined);
    }

    private void HandleOrderDelivered(RestockOrderSaveData _)
    {
        if (departureRoutine != null)
        {
            StopCoroutine(departureRoutine);
            departureRoutine = null;
        }
        SetPresentationVisible(true);

        if (!parkingConfigured)
        {
            parkingPosition = transform.position;
            parkingRotation = transform.rotation;
            parkingConfigured = true;
        }

        if (isParked)
        {
            PlayArrivalHorn();
            RefreshStatus();
            return;
        }

        if (arrivalRoutine != null)
            StopCoroutine(arrivalRoutine);
        arrivalRoutine = StartCoroutine(ArrivalRoutine());
    }

    public void BeginDepartureAfterCollection()
    {
        if (!parkingConfigured || !isActiveAndEnabled)
            return;

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }
        if (departureRoutine != null)
            StopCoroutine(departureRoutine);
        departureRoutine = StartCoroutine(DepartureRoutine());
    }

    private IEnumerator ArrivalRoutine()
    {
        isParked = false;
        SetInteractionReady(false);
        Vector3 start = transform.position;
        start.x = parkingPosition.x;
        start.y = parkingPosition.y;
        float duration = Mathf.Max(0.1f, arrivalDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            Vector3 position = parkingPosition;
            position.z = Mathf.Lerp(start.z, parkingPosition.z, t);
            transform.SetPositionAndRotation(position, parkingRotation);
            yield return null;
        }

        PlaceAtParkingPosition();
        arrivalRoutine = null;
        PlayArrivalHorn();
        RefreshStatus();
    }

    private void PlaceAtApproachPosition()
    {
        Vector3 position = parkingPosition;
        position.z += approachZOffset;
        transform.SetPositionAndRotation(position, parkingRotation);
        isParked = false;
        Physics.SyncTransforms();
        SetInteractionReady(false);
    }

    private void PlaceAtParkingPosition()
    {
        transform.SetPositionAndRotation(parkingPosition, parkingRotation);
        isParked = true;
        Physics.SyncTransforms();
        SetInteractionReady(HasReadyDelivery);
    }

    private void PlacePastDeparturePoint()
    {
        Vector3 position = parkingPosition + parkingRotation * Vector3.forward * departureDistance;
        transform.SetPositionAndRotation(position, parkingRotation);
        isParked = false;
        Physics.SyncTransforms();
        SetInteractionReady(false);
    }

    private void SetInteractionReady(bool ready)
    {
        if (interactionColliders == null || interactionColliders.Length == 0)
            interactionColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < interactionColliders.Length; i++)
        {
            if (interactionColliders[i] != null)
                interactionColliders[i].enabled = ready;
        }

        Outline outline = GetComponent<Outline>();
        if (outline != null && !ready)
            outline.enabled = false;
    }

    private void PlayArrivalHorn()
    {
        if (arrivalHornClip == null || !isActiveAndEnabled)
            return;

        if (hornRoutine != null)
            StopCoroutine(hornRoutine);
        hornRoutine = StartCoroutine(HornRoutine());
    }

    private IEnumerator HornRoutine()
    {
        hornSource.volume = hornVolume;
        hornSource.PlayOneShot(arrivalHornClip, hornVolume);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, secondBeepDelay));
        hornSource.PlayOneShot(arrivalHornClip, hornVolume);
        hornRoutine = null;
    }

    private IEnumerator DepartureRoutine()
    {
        isParked = false;
        SetInteractionReady(false);
        if (statusText != null)
            statusText.text = "✓ ORDER COLLECTED";

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, departureDelaySeconds));

        Vector3 start = transform.position;
        Vector3 end = parkingPosition + parkingRotation * Vector3.forward * departureDistance;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, departureDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            transform.SetPositionAndRotation(Vector3.Lerp(start, end, t), parkingRotation);
            yield return null;
        }

        transform.SetPositionAndRotation(end, parkingRotation);
        SetPresentationVisible(false);
        departureRoutine = null;
    }

    private void SetPresentationVisible(bool visible)
    {
        if (presentationRenderers == null || presentationRenderers.Length == 0)
            presentationRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < presentationRenderers.Length; i++)
            if (presentationRenderers[i] != null)
                presentationRenderers[i].enabled = visible;

        if (presentationCanvases == null || presentationCanvases.Length == 0)
            presentationCanvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < presentationCanvases.Length; i++)
            if (presentationCanvases[i] != null)
                presentationCanvases[i].enabled = visible;
    }
}
