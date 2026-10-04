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

    [Tooltip("Additional authored service staff, such as Fast Food's second cashier.")]
    [SerializeField] private GameObject[] additionalServiceStaff = System.Array.Empty<GameObject>();
    public int StaffRootCount => 4 + (additionalServiceStaff?.Length ?? 0);
    public GameObject GetStaffRoot(int index) => index < 4 ? GetStaffRoot((ServiceRole)index)
        : additionalServiceStaff != null && index - 4 < additionalServiceStaff.Length ? additionalServiceStaff[index - 4] : null;
    public bool IsStaffAvailable(int index) => index < 4 ? IsRoleAvailableForAI((ServiceRole)index)
        : IsInitialized && GetStaffRoot(index) != null;
    public GameObject FindStaff(string name)
    { for (int i = 0; i < StaffRootCount; i++) { var root = GetStaffRoot(i); if (root != null && root.name == name) return root; } return null; }
    public bool Includes(AutonomousStaffBot bot)
    {
        if (bot == null) return false;
        for (int i = 0; i < StaffRootCount; i++)
        { var root = GetStaffRoot(i); if (root != null && IsStaffAvailable(i) && (bot.gameObject == root || bot.transform.IsChildOf(root.transform))) return true; }
        return false;
    }
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
        if (cashier == null || busser == null || session.RestaurantType != "FastFood" && (receptionist == null || waiter == null))
        {
            Debug.LogError("[MultiplayerStaffRoster] Assign the restaurant service staff scene roots.", this);
            return;
        }

        // Freeze the initial run selection: departures/reconnects do not rebalance staff.
        InitialPartySize = session.PartySize;
        IsInitialized = true;
        if (GetComponent<MultiplayerServiceStaffBridge>() == null)
            gameObject.AddComponent<MultiplayerServiceStaffBridge>();
        if (receptionist != null) receptionist.SetActive(IsRoleAvailableForAI(ServiceRole.Receptionist));
        if (waiter != null) waiter.SetActive(IsRoleAvailableForAI(ServiceRole.Waiter));
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
            case ServiceRole.Waiter:
            case ServiceRole.Cashier:
            case ServiceRole.Busser: return false; // Single-player hiring/assignment remains authoritative.
            default: return false;
        }
    }

    // Only service roles are represented here; kitchen staff are outside this controller.
    public bool IsRoleAvailableForAI(ServiceRole role) => IsInitialized
        && role >= ServiceRole.Receptionist && role <= ServiceRole.Busser
        && GetStaffRoot(role) != null && !IsRoleReplacedByHuman(role);
}
