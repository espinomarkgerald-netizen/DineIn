using UnityEngine;

public class BoothDeliverInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Booth booth;
    [SerializeField] private Transform tableFoodSpawn; // auto-find by name "TableFoodSpawn" if empty

    public Transform StandPoint => booth != null && booth.approachPoint != null ? booth.approachPoint : transform;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        if (booth == null) booth = GetComponent<Booth>();

        if (tableFoodSpawn == null && booth != null)
        {
            foreach (var t in booth.GetComponentsInChildren<Transform>(true))
                if (t.name == "TableFoodSpawn") { tableFoodSpawn = t; break; }
        }
    }

    public bool CanInteract()
    {
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

        // Clear from hands but keep tray object so we can place it
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

        group.ReceiveFoodFromWaiter();
        Debug.Log($"[BoothDeliver] Delivered tray #{group.currentOrderNumber} to {booth.name}");
    }

    
}