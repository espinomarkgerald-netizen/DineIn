using UnityEngine;

public class RoleManager : MonoBehaviour
{
    public static RoleManager Instance;

    [Header("Staff")]
    public GameObject host;
    public GameObject waiter;
    public GameObject cashier;
    public GameObject busser;

    [Header("UI")]
    [SerializeField] private RoleSwitchWarningUI warningUI;

    [Header("Camera Anchors")]
    [SerializeField] private Transform hostCameraAnchor;
    [SerializeField] private Transform waiterCameraAnchor;
    [SerializeField] private Transform cashierCameraAnchor;
    [SerializeField] private Transform busserCameraAnchor;

    private GameObject activeRole;
    private RoleCameraController cameraController;

    private void Awake()
    {
        Instance = this;
        cameraController = GetComponent<RoleCameraController>();
    }

    private void Start()
    {
        activeRole = waiter;

        SetMovementEnabled(host, false);
        SetMovementEnabled(waiter, true);
        SetMovementEnabled(cashier, false);
        SetMovementEnabled(busser, false);

        SetIndicator(host, false);
        SetIndicator(waiter, true);
        SetIndicator(cashier, false);
        SetIndicator(busser, false);
    }

    public void SwitchToHost()
    {
        TrySwitch(host, hostCameraAnchor);
    }

    public void SwitchToWaiter()
    {
        TrySwitch(waiter, waiterCameraAnchor);
    }

    public void SwitchToCashier()
    {
        TrySwitch(cashier, cashierCameraAnchor);
    }

    public void SwitchToBusser()
    {
        TrySwitch(busser, busserCameraAnchor);
    }

    private void TrySwitch(GameObject nextRole, Transform nextCameraAnchor)
    {
        if (nextRole == null) return;
        if (activeRole == nextRole) return;

        var currentMove = activeRole != null ? activeRole.GetComponent<PlayerMovement>() : null;
        if (currentMove != null && !currentMove.CanSwitchRole())
        {
            if (warningUI != null)
                warningUI.ShowWarning();
            return;
        }

        SetMovementEnabled(host, false);
        SetMovementEnabled(waiter, false);
        SetMovementEnabled(cashier, false);
        SetMovementEnabled(busser, false);

        SetIndicator(host, false);
        SetIndicator(waiter, false);
        SetIndicator(cashier, false);
        SetIndicator(busser, false);

        SetMovementEnabled(nextRole, true);
        SetIndicator(nextRole, true);

        activeRole = nextRole;

        if (cameraController != null && nextCameraAnchor != null)
            cameraController.PanToTarget(nextRole.transform);
    }

    private void SetMovementEnabled(GameObject obj, bool value)
    {
        if (obj == null) return;

        var move = obj.GetComponent<PlayerMovement>();
        if (move != null)
            move.enabled = value;
    }

    private void SetIndicator(GameObject obj, bool value)
    {
        if (obj == null) return;

        var indicator = obj.GetComponent<RoleIndicator>();
        if (indicator != null)
            indicator.SetSelected(value);
    }
}