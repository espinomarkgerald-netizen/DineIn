using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestockStockRoomEntrance : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RestockStorageType storageType = RestockStorageType.Dry;
    [SerializeField, Min(0.5f)] private float interactRadius = 1.5f;

    [Header("Destination Guidance")]
    [SerializeField] private Color guidanceColor = new Color(0.2f, 0.95f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float guidanceSeconds = 1.4f;
    [SerializeField, Min(1f)] private float guidanceScale = 1.08f;

    private Coroutine guidanceRoutine;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public RestockStorageType StorageType => storageType;
    public bool AutoReturnHome => false;

    public void Configure(Transform configuredStandPoint, TMP_Text configuredStatus)
    {
        standPoint = configuredStandPoint;
        statusText = configuredStatus;
    }

    public void ConfigureRoom(RestockStorageType configuredStorage)
    {
        storageType = configuredStorage;
        RefreshStatus();
    }

    private void OnEnable()
    {
        RefreshStatus();
        if (RestockOrderManager.Instance != null)
            RestockOrderManager.Instance.OrdersChanged += RefreshStatus;
    }

    private void OnDisable()
    {
        if (RestockOrderManager.Instance != null)
            RestockOrderManager.Instance.OrdersChanged -= RefreshStatus;
    }

    public bool CanInteract() => isActiveAndEnabled;

    public void Interact(PlayerMovement mover)
    {
        RestockFlowCoordinator.EnsureInstance().EnterRestockRoom(storageType);
    }

    public float GetInteractRadius() => Mathf.Max(0.5f, interactRadius);

    private void RefreshStatus()
    {
        if (statusText == null)
            return;

        int boxes = RestockOrderManager.Instance != null
            ? RestockOrderManager.Instance.GetHotbarContainerCount(storageType)
            : 0;
        string roomName = storageType == RestockStorageType.Frozen
            ? "WALK-IN FREEZER"
            : "DRY STORAGE";
        statusText.text = boxes > 0
            ? roomName + "\n" + boxes + " BOX" + (boxes == 1 ? string.Empty : "ES") + " REMAINING"
            : roomName + "\nOPEN ANYTIME";
    }

    public void PulseGuidance()
    {
        if (!isActiveAndEnabled)
            return;
        if (guidanceRoutine != null)
            StopCoroutine(guidanceRoutine);
        guidanceRoutine = StartCoroutine(GuidanceRoutine());
    }

    private IEnumerator GuidanceRoutine()
    {
        Outline outline = GetComponent<Outline>();
        Vector3 originalScale = transform.localScale;
        bool originalEnabled = outline != null && outline.enabled;
        Color originalColor = outline != null ? outline.OutlineColor : Color.white;
        float elapsed = 0f;

        if (outline != null)
        {
            outline.OutlineColor = guidanceColor;
            outline.enabled = true;
        }

        while (elapsed < guidanceSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 12f);
            transform.localScale = originalScale * Mathf.Lerp(1f, guidanceScale, pulse);
            yield return null;
        }

        transform.localScale = originalScale;
        if (outline != null)
        {
            outline.OutlineColor = originalColor;
            outline.enabled = originalEnabled;
        }
        guidanceRoutine = null;
    }
}
