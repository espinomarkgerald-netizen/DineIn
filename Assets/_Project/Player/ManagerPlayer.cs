using UnityEngine;

/// <summary>
/// Permanent identity for the player-controlled restaurant manager.
/// The manager can assist every staff workflow without changing StaffRole.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class ManagerPlayer : MonoBehaviour
{
    public enum Capability
    {
        Host,
        Waiter,
        Cashier,
        Busser,
        Chef,
        Barista
    }

    public static ManagerPlayer Active { get; private set; }

    [Header("Restaurant Capabilities")]
    [SerializeField] private bool canHost = true;
    [SerializeField] private bool canWaiter = true;
    [SerializeField] private bool canCashier = true;
    [SerializeField] private bool canBusser = true;
    [SerializeField] private bool canChef = true;
    [SerializeField] private bool canBarista = true;

    private bool externalInputSuppressed;

    public PlayerMovement Movement { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();

        if (Active != null && Active != this)
        {
            Debug.LogError("[ManagerPlayer] More than one active Manager exists.", this);
            enabled = false;
            return;
        }

        Active = this;
        Movement.SetPlayerControlled(true);
        Movement.CancelAutoFinish();
    }

    private void Start()
    {
        Movement.RefreshSceneCamera();
        ConfigureLobbyInputFromHost();
    }

    private void OnEnable()
    {
        if (Movement != null)
            Movement.SetPlayerControlled(!externalInputSuppressed);
    }

    private void LateUpdate()
    {
        // Autonomous service disables the legacy role players at runtime. Keep
        // the independent Manager input path alive regardless of script order.
        if (Movement != null)
        {
            Movement.enabled = true;
            Movement.SetPlayerControlled(!externalInputSuppressed);
        }

        RoleBasedAssignController seating = GetComponent<RoleBasedAssignController>();
        if (seating != null)
            seating.enabled = !externalInputSuppressed;
    }

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    public bool Can(Capability capability)
    {
        switch (capability)
        {
            case Capability.Host: return canHost;
            case Capability.Waiter: return canWaiter;
            case Capability.Cashier: return canCashier;
            case Capability.Busser: return canBusser;
            case Capability.Chef: return canChef;
            case Capability.Barista: return canBarista;
            default: return false;
        }
    }

    public bool CanPerform(StaffRole.Role role)
    {
        switch (role)
        {
            case StaffRole.Role.Host: return canHost;
            case StaffRole.Role.Waiter: return canWaiter;
            case StaffRole.Role.Cashier: return canCashier;
            case StaffRole.Role.Busser: return canBusser;
            default: return false;
        }
    }

    /// <summary>
    /// Temporarily releases gameplay input while a full-screen manager tool is open.
    /// </summary>
    public void SetExternalInputSuppressed(bool suppressed)
    {
        externalInputSuppressed = suppressed;

        if (Movement != null)
        {
            Movement.SetPlayerControlled(!suppressed);
            if (suppressed)
                Movement.StopForRoleSwitch();
        }

        RoleBasedAssignController seating = GetComponent<RoleBasedAssignController>();
        if (seating != null)
            seating.enabled = !suppressed;
    }

    private void ConfigureLobbyInputFromHost()
    {
        RoleBasedAssignController managerController =
            GetComponent<RoleBasedAssignController>();
        if (managerController == null)
            return;

        RoleBasedAssignController[] controllers =
            FindObjectsByType<RoleBasedAssignController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            RoleBasedAssignController candidate = controllers[i];
            if (candidate == null || candidate == managerController)
                continue;

            StaffRole role = candidate.GetComponent<StaffRole>();
            if (role == null || role.role != StaffRole.Role.Host)
                continue;

            managerController.customerLayer = candidate.customerLayer;
            managerController.boothLayer = candidate.boothLayer;
            managerController.cleanableLayer = candidate.cleanableLayer;
            managerController.maxRayDistance = candidate.maxRayDistance;
            managerController.ignoreWhenPointerOverUI = candidate.ignoreWhenPointerOverUI;
            break;
        }
    }
}
