using UnityEngine;
using UnityEngine.UI;

public class RoleManager : MonoBehaviour
{
    public static RoleManager Instance;

    [Header("Staff")]
    public GameObject host;
    public GameObject waiter;
    public GameObject cashier;
    public GameObject busser;

    [Header("Default Role")]
    [SerializeField] private GameObject defaultRole;

    [Header("UI")]
    [SerializeField] private RoleSwitchWarningUI warningUI;

    [Header("Role Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button waiterButton;
    [SerializeField] private Button cashierButton;
    [SerializeField] private Button busserButton;

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
        InitializeDefaultRole();
    }

    private void Start()
    {
        RefreshButtonVisuals();

        if (cameraController != null && activeRole != null)
            cameraController.PanToTarget(activeRole.transform);
    }

    private void InitializeDefaultRole()
    {
        if (defaultRole == null)
            defaultRole = waiter != null ? waiter : host;

        InitializeRole(host, false);
        InitializeRole(waiter, false);
        InitializeRole(cashier, false);
        InitializeRole(busser, false);

        activeRole = defaultRole;

        SetPlayerControlled(activeRole, true);
        SetIndicator(activeRole, true);
    }

    private void InitializeRole(GameObject obj, bool playerControlled)
    {
        if (obj == null) return;

        var move = obj.GetComponent<PlayerMovement>();
        if (move != null)
        {
            move.enabled = true;
            move.SetPlayerControlled(playerControlled);
            move.CancelAutoFinish();
        }

        SetIndicator(obj, playerControlled);
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
        var nextMove = nextRole.GetComponent<PlayerMovement>();

        if (nextMove == null) return;

        if (currentMove != null)
        {
            currentMove.SetPlayerControlled(false);
            currentMove.BeginAutoFinish();
        }

        SetIndicator(host, false);
        SetIndicator(waiter, false);
        SetIndicator(cashier, false);
        SetIndicator(busser, false);

        activeRole = nextRole;

        nextMove.CancelAutoFinish();
        nextMove.SetPlayerControlled(true);

        SetIndicator(activeRole, true);

        RefreshButtonVisuals();

        if (cameraController != null)
            cameraController.PanToTarget(activeRole.transform);
    }

    private void RefreshButtonVisuals()
    {
        ApplyButtonVisual(hostButton, activeRole == host);
        ApplyButtonVisual(waiterButton, activeRole == waiter);
        ApplyButtonVisual(cashierButton, activeRole == cashier);
        ApplyButtonVisual(busserButton, activeRole == busser);
    }

    private void ApplyButtonVisual(Button button, bool selected)
    {
        if (button == null || button.image == null) return;

        if (selected)
        {
            var selectedSprite = button.spriteState.selectedSprite;
            button.image.overrideSprite = selectedSprite != null ? selectedSprite : null;
        }
        else
        {
            button.image.overrideSprite = null;
        }
    }

    private void SetPlayerControlled(GameObject obj, bool value)
    {
        if (obj == null) return;

        var move = obj.GetComponent<PlayerMovement>();
        if (move != null)
            move.SetPlayerControlled(value);
    }

    private void SetIndicator(GameObject obj, bool value)
    {
        if (obj == null) return;

        var indicator = obj.GetComponent<RoleIndicator>();
        if (indicator != null)
            indicator.SetSelected(value);
    }

    public bool IsActiveRole(GameObject obj)
    {
        return activeRole == obj;
    }

    public string ActiveRoleName()
    {
        return activeRole != null ? activeRole.name : "NULL";
    }

    public StaffRole.Role ActiveRoleType()
    {
        if (activeRole == null) return StaffRole.Role.Waiter;

        var sr = activeRole.GetComponent<StaffRole>();
        return sr != null ? sr.role : StaffRole.Role.Waiter;
    }

    public bool IsActiveRoleType(StaffRole.Role role)
    {
        return ActiveRoleType() == role;
    }

    public PlayerMovement GetActivePlayerMovement()
    {
        if (activeRole == null) return null;
        return activeRole.GetComponent<PlayerMovement>();
    }
}