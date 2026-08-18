using UnityEngine;

public class MoneyPickup : MonoBehaviour, IInteractable, ICancelableTaskTarget
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
    private bool isPickedUp;

    public CustomerGroup TargetGroup => targetGroup;
    public int Amount => amount;
    public bool IsPickedUp => isPickedUp;
    public bool IsAvailableForCollection =>
        !isPickedUp && targetGroup != null && amount > 0 &&
        targetGroup.state == CustomerGroup.GroupState.NeedsBill;

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
        isPickedUp = false;

        if (cachedCol != null)
            cachedCol.enabled = true;
    }

    public void SetBubbleUI(MoneyBubbleUI ui)
    {
        bubbleUI = ui;
        bubbleUI?.SetClaimedByStaff(RestaurantTaskClaim.IsClaimedByBot(this));
    }

    public void SetClaimedByStaff(bool claimed)
    {
        bubbleUI?.SetClaimedByStaff(claimed);
    }

    public void NotifyPickedUp()
    {
        if (isPickedUp)
            return;

        isPickedUp = true;

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

        if (bubbleUI != null)
            bubbleUI.RemoveBubble();

        bubbleUI = null;
    }

    public float GetInteractRadius()
    {
        return interactRadius;
    }

    public bool CanInteract()
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (!IsAvailableForCollection) return false;
        if (RestaurantTaskClaim.IsClaimedByBot(this)) return false;
        if (WaiterHands.ActivePlayerHands == null) return false;

        return !WaiterHands.ActivePlayerHands.HasMoney;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!TryPickup(mover))
            RecoverFailedPickup();
    }

    public void UI_RequestPickup()
    {
        if (RoleManager.Instance == null) return;

        PlayerMovement mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

        if (mover.IsTaskLocked)
        {
            WarningSlideUI.Instance?.Show("Finish your current task first.");
            return;
        }

        if (RestaurantTaskClaim.IsClaimedByBot(this))
        {
            WarningSlideUI.Instance?.Show("The waiter is already collecting this payment.");
            return;
        }

        if (!CanInteract())
            return;

        if (!RestaurantTaskClaim.TryClaimPlayer(this))
        {
            WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already collecting this payment.");
            return;
        }

        SetClaimedByStaff(true);
        mover.LockTask(this);
        mover.UI_MoveTo(this);
    }

    public bool TryPickup(PlayerMovement mover = null)
    {
        if (RestaurantTaskClaim.IsClaimedByBot(this))
        {
            WarningSlideUI.Instance?.Show("The waiter is already collecting this payment.");
            return false;
        }

        if (!CanInteract())
        {
            RestaurantTaskClaim.ReleasePlayer(this);
            return false;
        }

        if (!RestaurantTaskClaim.TryClaimPlayer(this))
        {
            WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already collecting this payment.");
            return false;
        }

        var hands = WaiterHands.For(mover);
        if (hands == null || hands.HasMoney)
        {
            RestaurantTaskClaim.ReleasePlayer(this);
            return false;
        }

        hands.PickupMoney(this);

        if (!hands.HasMoney || hands.HeldMoney != this)
        {
            RestaurantTaskClaim.ReleasePlayer(this);
            return false;
        }

        return true;
    }

    public void OnTaskCancelled()
    {
        RecoverFailedPickup();
    }

    private void RecoverFailedPickup()
    {
        RestaurantTaskClaim.ReleasePlayer(this);
        SetClaimedByStaff(RestaurantTaskClaim.IsClaimedByBot(this));
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null && targetGroup == group;
    }

    private void OnDestroy()
    {
        RestaurantTaskClaim.Complete(this);
        if (bubbleUI != null)
            bubbleUI.RemoveBubble();
    }
}
