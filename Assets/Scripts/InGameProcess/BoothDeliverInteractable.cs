using UnityEngine;

public class BoothDeliverInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Booth booth;
    [SerializeField] private Transform tableFoodSpawn;
    [SerializeField] private AutoInteractRadius autoRadius;

    public Transform StandPoint => booth != null && booth.approachPoint != null ? booth.approachPoint : transform;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        if (booth == null) booth = GetComponent<Booth>();
        if (autoRadius == null) autoRadius = GetComponent<AutoInteractRadius>();

        if (tableFoodSpawn == null && booth != null)
        {
            foreach (var t in booth.GetComponentsInChildren<Transform>(true))
                if (t.name == "TableFoodSpawn") { tableFoodSpawn = t; break; }
        }
    }

    private void Update()
    {
        if (autoRadius == null) return;
        if (!autoRadius.IsActiveRoleInRange(StaffRole.Role.Waiter)) return;

        var mover = RoleManager.Instance != null ? RoleManager.Instance.GetActivePlayerMovement() : null;
        if (mover == null) return;

        if (CanInteract())
            Interact(mover);
    }

    public bool CanInteract()
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (WaiterHands.Instance == null || !WaiterHands.Instance.HasTray) return false;
        if (booth == null) return false;

        var group = booth.CurrentGroup;
        if (group == null) return false;
        if (group.state != CustomerGroup.GroupState.OrderTaken) return false;

        var tray = WaiterHands.Instance.holdingTray;
        return tray != null && tray.Matches(group);
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanInteract()) return;

        var hands = WaiterHands.Instance;
        var group = booth.CurrentGroup;
        var tray = hands.holdingTray;

        if (tableFoodSpawn == null)
        {
            Debug.LogWarning("[BoothDeliver] No TableFoodSpawn found.");
            return;
        }

        bool ok = hands.TryDeliverTrayTo(group, destroyTrayObject: false);
        if (!ok) return;

        if (tray != null)
        {
            tray.transform.SetParent(tableFoodSpawn, false);
            tray.transform.localPosition = Vector3.zero;
            tray.transform.localRotation = Quaternion.identity;

            var col = tray.GetComponentInChildren<Collider>(true);
            if (col != null) col.enabled = true;
        }

        var trayInteractable = tray != null ? tray.GetComponent<FoodTrayInteractable>() : null;
        if (trayInteractable != null)
            trayInteractable.NotifyDeliveredToTable();

        group.ReceiveFoodFromWaiter();
        Debug.Log($"[BoothDeliver] Delivered tray #{group.currentOrderNumber} to {booth.name}");
    }
}