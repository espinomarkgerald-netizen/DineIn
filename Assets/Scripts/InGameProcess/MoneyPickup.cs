using UnityEngine;

public class MoneyPickup : MonoBehaviour, IInteractable
{
    [Header("Runtime")]
    [SerializeField] private CustomerGroup targetGroup;
    [SerializeField] private int amount;

    [Header("Interact")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private bool autoReturnHome = false;
    [SerializeField] private bool disableColliderWhileHeld = true;
    [SerializeField] private float interactRadius = 1.15f;

    private Collider cachedCol;
    private MoneyBubbleUI bubbleUI;

    public CustomerGroup TargetGroup => targetGroup;
    public int Amount => amount;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        cachedCol = GetComponentInChildren<Collider>(true);
    }

    public void Init(CustomerGroup group, int moneyAmount, Transform useStandPoint, MoneyBubbleUI ui = null)
    {
        targetGroup = group;
        amount = moneyAmount;
        standPoint = useStandPoint;
        bubbleUI = ui;

        if (cachedCol != null)
            cachedCol.enabled = true;
    }

    public void SetBubbleUI(MoneyBubbleUI ui)
    {
        bubbleUI = ui;
    }

    public float GetInteractRadius()
    {
        return interactRadius;
    }

    public bool CanInteract()
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (targetGroup == null) return false;
        if (WaiterHands.Instance == null) return false;

        return !WaiterHands.Instance.HasMoney;
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
        if (hands.HasMoney) return false;

        hands.PickupMoney(this);

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

        if (bubbleUI != null)
            bubbleUI.RemoveBubble();

        bubbleUI = null;
        return true;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null && targetGroup == group;
    }

    private void OnDestroy()
    {
        if (bubbleUI != null)
            bubbleUI.RemoveBubble();
    }
}