using UnityEngine;

public class CustomerDeliverInteractable : MonoBehaviour, IInteractable
{
    [Header("Drop Point")]
    [SerializeField] private Transform tableFoodSpawn;

    [Header("Behavior")]
    [SerializeField] private bool destroyTrayOnDeliver = false;

    private Booth booth;

    public Transform StandPoint => ResolveStandPoint();
    public bool AutoReturnHome => false;

    private void Awake()
    {
        booth = GetComponent<Booth>();
        if (booth == null) booth = GetComponentInParent<Booth>();
    }

    public bool CanInteract()
    {
        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasTray) return false;

        if (booth == null || booth.CurrentGroup == null) return false;

        var group = booth.CurrentGroup;

        if (group.state != CustomerGroup.GroupState.OrderTaken) return false;

        var tray = hands.holdingTray;
        return tray != null && tray.Matches(group);
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanInteract()) return;

        var hands = WaiterHands.Instance;
        var tray = hands.holdingTray;
        var group = booth.CurrentGroup;

        Transform drop = ResolveDropPoint();
        if (drop == null)
        {
            Debug.LogWarning("[Deliver] No TableFoodSpawn found under this booth.");
            return;
        }

        hands.ClearTray();

        if (destroyTrayOnDeliver)
        {
            Destroy(tray.gameObject);
        }
        else
        {
            tray.transform.SetParent(drop, false);
            tray.transform.localPosition = Vector3.zero;
            tray.transform.localRotation = Quaternion.identity;

            var col = tray.GetComponentInChildren<Collider>(true);
            if (col != null) col.enabled = true;
        }

        if (booth != null)
            booth.ClearMenuBook();

        group.ReceiveFoodFromWaiter();

        Debug.Log($"[Deliver] Placed tray #{group.currentOrderNumber} on {booth.name}/{drop.name}");
    }

    private Transform ResolveDropPoint()
    {
        if (tableFoodSpawn != null) return tableFoodSpawn;
        if (booth == null) return null;

        foreach (var t in booth.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "TableFoodSpawn")
            {
                tableFoodSpawn = t;
                return tableFoodSpawn;
            }
        }

        return null;
    }

    private Transform ResolveStandPoint()
    {
        // Stand at a floor point, not the tabletop
        if (booth != null && booth.approachPoint != null)
            return booth.approachPoint;

        return transform;
    }
}