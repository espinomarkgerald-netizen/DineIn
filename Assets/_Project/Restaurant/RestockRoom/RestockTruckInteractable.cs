using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RestockTruckInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;
    [SerializeField] private TMP_Text statusText;
    [SerializeField, Min(0.5f)] private float interactRadius = 2f;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;

    public void Configure(Transform configuredStandPoint, TMP_Text configuredStatus)
    {
        standPoint = configuredStandPoint;
        statusText = configuredStatus;
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
        RestockFlowCoordinator.EnsureInstance().OpenTruckCollection();
    }

    public float GetInteractRadius() => Mathf.Max(0.5f, interactRadius);

    private void RefreshStatus()
    {
        if (statusText == null)
            return;

        RestockOrderManager manager = RestockOrderManager.Instance;
        if (manager != null && manager.DeliveredContainerCount > 0)
            statusText.text = "DELIVERY TRUCK\n" + manager.DeliveredContainerCount + " BOXES READY";
        else
            statusText.text = "DELIVERY TRUCK\nNO ORDER READY";
    }
}
