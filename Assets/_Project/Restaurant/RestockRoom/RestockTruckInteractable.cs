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

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;
    public bool IsParked => isParked;
    public bool ParkingConfigured => parkingConfigured;
    public bool HasReadyDelivery => RestockOrderManager.Instance != null &&
                                    RestockOrderManager.Instance.DeliveredContainerCount > 0;

    private Vector3 parkingPosition;
    private Quaternion parkingRotation;
    private Collider[] interactionColliders;
    private Coroutine arrivalRoutine;
    private Coroutine hornRoutine;
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
        bool alreadyArrived = manager != null &&
                              (manager.DeliveredContainerCount > 0 ||
                               manager.HotbarContainerCount > 0);
        if (alreadyArrived)
            PlaceAtParkingPosition();
        else
            PlaceAtApproachPosition();

        RefreshStatus();
    }

    private void Awake()
    {
        parkingPosition = transform.position;
        parkingRotation = transform.rotation;
        interactionColliders = GetComponentsInChildren<Collider>(true);
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
        AudioSource.PlayClipAtPoint(arrivalHornClip, transform.position, hornVolume);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, secondBeepDelay));
        AudioSource.PlayClipAtPoint(arrivalHornClip, transform.position, hornVolume);
        hornRoutine = null;
    }
}
