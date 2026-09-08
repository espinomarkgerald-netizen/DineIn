using UnityEngine;

// Initial multiplayer balance only; does not assign human roles or change staff behavior.
[DefaultExecutionOrder(-150)]
[RequireComponent(typeof(MultiplayerSessionManager))]
public class MultiplayerStaffRosterController : MonoBehaviour
{
    public enum ServiceRole { Receptionist, Waiter, Cashier, Busser }

    [Header("Multiplayer Scene Service Staff")]
    [SerializeField] private GameObject receptionist;
    [SerializeField] private GameObject waiter;
    [SerializeField] private GameObject cashier;
    [SerializeField] private GameObject busser;

    public bool IsInitialized { get; private set; }
    public int InitialPartySize { get; private set; }

    // Session Awake runs first; apply before ordinary staff Awake/Start behavior.
    private void Awake() => InitializeRoster();
    private void Start() => InitializeRoster();

    private void InitializeRoster()
    {
        if (IsInitialized) return;
        var session = GetComponent<MultiplayerSessionManager>();
        if (!session.IsMultiplayerSession || session.PartySize < 2 || session.PartySize > 4) return;
        if (receptionist == null || waiter == null || cashier == null || busser == null)
        {
            Debug.LogError("[MultiplayerStaffRoster] Assign all four service staff scene roots.", this);
            return;
        }

        // Freeze the initial run selection: departures/reconnects do not rebalance staff.
        InitialPartySize = session.PartySize;
        IsInitialized = true;
        if (GetComponent<MultiplayerServiceStaffBridge>() == null)
            gameObject.AddComponent<MultiplayerServiceStaffBridge>();
        receptionist.SetActive(IsRoleAvailableForAI(ServiceRole.Receptionist));
        waiter.SetActive(IsRoleAvailableForAI(ServiceRole.Waiter));
        cashier.SetActive(IsRoleAvailableForAI(ServiceRole.Cashier));
        busser.SetActive(IsRoleAvailableForAI(ServiceRole.Busser));
    }

    public GameObject GetStaffRoot(ServiceRole role) => role switch
    {
        ServiceRole.Receptionist => receptionist,
        ServiceRole.Waiter => waiter,
        ServiceRole.Cashier => cashier,
        ServiceRole.Busser => busser,
        _ => null
    };

    public bool IsRoleReplacedByHuman(ServiceRole role)
    {
        if (!IsInitialized) return false;
        switch (role)
        {
            case ServiceRole.Receptionist:
            case ServiceRole.Waiter: return true;
            case ServiceRole.Cashier: return InitialPartySize >= 3;
            case ServiceRole.Busser: return InitialPartySize >= 4;
            default: return false;
        }
    }

    // Only service roles are represented here; kitchen staff are outside this controller.
    public bool IsRoleAvailableForAI(ServiceRole role) => IsInitialized
        && role >= ServiceRole.Receptionist && role <= ServiceRole.Busser
        && !IsRoleReplacedByHuman(role);
}
