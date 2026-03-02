using UnityEngine;

public class BillPaper : MonoBehaviour, IInteractable
{
    public int orderNumber;

    [SerializeField] private CustomerGroup targetGroup;
    public CustomerGroup TargetGroup => targetGroup;

    [Header("Interact")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private bool autoReturnHome = false;
    [SerializeField] private bool disableColliderWhileHeld = true;

    private Collider cachedCol;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        cachedCol = GetComponentInChildren<Collider>(true);
    }

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        var num = GetComponentInChildren<TableNumberUI>(true);
        if (num != null)
            num.SetNumber(orderNumber);
    }

    public bool CanInteract()
    {
        if (targetGroup == null) return false;
        if (WaiterHands.Instance == null) return false;
        return !WaiterHands.Instance.HasBill;
    }

    public void Interact(PlayerMovement mover)
    {
        TryPickup();
    }

    public bool TryPickup()
    {
        if (!CanInteract()) return false;

        var hands = WaiterHands.Instance;
        if (hands == null) return false;
        if (hands.HasBill) return false;

        hands.PickupBillPaper(this);

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

        return true;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null
            && targetGroup != null
            && targetGroup == group
            && group.currentOrderNumber == orderNumber;
    }
}