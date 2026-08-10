using UnityEngine;

public class BoothDeliverInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Booth booth;
    [SerializeField] private Transform tableFoodSpawn;
    [SerializeField] private AutoInteractRadius autoRadius;

    [Header("Warning")]
    [SerializeField] private float wrongOrderWarningCooldown = 1f;

    private float lastWrongOrderWarningTime = -999f;

    public Transform StandPoint => booth != null && booth.approachPoint != null ? booth.approachPoint : transform;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        if (booth == null) booth = GetComponent<Booth>();
        if (autoRadius == null) autoRadius = GetComponent<AutoInteractRadius>();

        if (tableFoodSpawn == null && booth != null)
        {
            foreach (var t in booth.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "TableFoodSpawn")
                {
                    tableFoodSpawn = t;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (autoRadius == null) return;
        if (!autoRadius.IsActiveRoleInRange(StaffRole.Role.Waiter)) return;

        var mover = RoleManager.Instance != null ? RoleManager.Instance.GetActivePlayerMovement() : null;
        if (mover == null) return;

        if (CanAttemptInteract())
            Interact(mover);
    }

    public bool CanInteract()
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasTray) return false;

        if (booth == null) return false;

        var group = booth.CurrentGroup;
        if (group == null) return false;
        if (group.state != CustomerGroup.GroupState.OrderTaken) return false;

        var tray = hands.holdingTray;
        return tray != null && tray.Matches(group);
    }

    private bool CanAttemptInteract()
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasTray) return false;

        if (booth == null) return false;

        var group = booth.CurrentGroup;
        if (group == null) return false;

        var tray = hands.holdingTray;
        return tray != null;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanAttemptInteract()) return;

        var hands = WaiterHands.Instance;
        var group = booth.CurrentGroup;
        var tray = hands.holdingTray;

        if (group == null || tray == null)
            return;

        if (!tray.Matches(group))
        {
            if (Time.time - lastWrongOrderWarningTime >= wrongOrderWarningCooldown)
            {
                ShowWarning($"This order is for table {tray.orderNumber}, not table {group.currentOrderNumber}.");
                lastWrongOrderWarningTime = Time.time;
            }
            return;
        }

        if (group.state != CustomerGroup.GroupState.OrderTaken)
            return;

        if (tableFoodSpawn == null)
        {
            Debug.LogWarning("[BoothDeliver] No TableFoodSpawn found.");
            return;
        }

        bool ok = hands.TryDeliverTrayTo(group, destroyTrayObject: false);
        if (!ok) return;

        tray.transform.SetParent(tableFoodSpawn, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;

        var col = tray.GetComponentInChildren<Collider>(true);
        if (col != null) col.enabled = true;

        var trayInteractable = tray.GetComponent<FoodTrayInteractable>();
        if (trayInteractable != null)
            trayInteractable.NotifyDeliveredToTable();

        group.ReceiveFoodFromWaiter(tray.DeliveredContents);

        Debug.Log($"[BoothDeliver] Delivered tray #{group.currentOrderNumber} to {booth.name}");
    }

    private void ShowWarning(string message)
    {
        if (WarningSlideUI.Instance != null)
        {
            WarningSlideUI.Instance.Show(message);
            return;
        }

        Debug.LogWarning(message);
    }

    public float GetInteractRadius()
    {
        return 0.5f;
    }
}