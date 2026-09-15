using UnityEngine;

public class MoneyPickup : MonoBehaviour, IInteractable, ICancelableTaskTarget
{
    [Header("Runtime")]
    [SerializeField] private CustomerGroup targetGroup;
    [SerializeField] private int amount;
    [SerializeField] private bool isCardPayment;

    [Header("Interact")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private bool autoReturnHome = false;
    [SerializeField] private bool disableColliderWhileHeld = true;
    [SerializeField] private float interactRadius = 1.15f;

    private Collider cachedCol;
    private MoneyBubbleUI bubbleUI;
    private bool isPickedUp;
    private float paymentCreatedAt;
    private int orderNumber;

    public CustomerGroup TargetGroup => targetGroup;
    public int Amount => amount;
    public int OrderNumber => orderNumber;
    public int OrderTotal => isCardPayment
        ? Mathf.Max(0, amount)
        : targetGroup != null ? targetGroup.GetCurrentOrderTotal() : 0;
    public bool IsCardPayment => isCardPayment;
    public bool IsPickedUp => isPickedUp;
    public bool IsAvailableForCollection =>
        !isPickedUp && targetGroup != null && amount > 0 &&
        (!MultiplayerServiceActions.IsActive || orderNumber == targetGroup.currentOrderNumber && !targetGroup.MultiplayerPaymentComplete) &&
        targetGroup.state == CustomerGroup.GroupState.NeedsBill;
    public bool IsAvailableForBotCollection => IsAvailableForCollection &&
        (!isCardPayment || Time.time >= paymentCreatedAt + GetCardPlayerPrioritySeconds());

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        MultiplayerWorldRegistry.Track(this);
        cachedCol = GetComponentInChildren<Collider>(true);
    }

    public void Init(
        CustomerGroup group,
        int moneyAmount,
        Transform useStandPoint,
        MoneyBubbleUI ui = null,
        bool cardPayment = false)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : 0;
        amount = moneyAmount;
        standPoint = useStandPoint;
        bubbleUI = ui;
        isCardPayment = cardPayment;
        isPickedUp = false;
        paymentCreatedAt = Time.time;

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
        PresentPickedUp(true);
    }

    public void PresentPickedUp(bool pickedUp)
    {
        isPickedUp = pickedUp;
        WaiterHands.SetAllColliders(gameObject, !pickedUp);
        if (cachedCol != null) cachedCol.enabled = !pickedUp || !disableColliderWhileHeld;
        if (pickedUp)
        {
            bubbleUI?.RemoveBubble();
            bubbleUI = null;
        }
    }

    // Used by authority recovery and guest projection. Keep the same payment
    // object, amount and order identity when its holder releases it.
    public bool PresentAtPickup()
    {
        var anchor = targetGroup != null ? targetGroup.assignedBooth?.GetComponent<BoothMoneySpawner>()?.MoneySpawnPoint : null;
        if (anchor == null) return false;
        GetComponentInParent<WaiterHands>(true)?.DetachMoneyPickup(this);
        transform.SetParent(anchor, true);
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        PresentPickedUp(false);
        return true;
    }

    public float GetInteractRadius()
    {
        return interactRadius;
    }

    public bool CanInteract()
    {
        if (MultiplayerServiceActions.IsActive) return MultiplayerServiceActions.CanCollect(this);
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (!IsAvailableForCollection) return false;
        if (RestaurantTaskClaim.IsClaimedByBot(this)) return false;
        if (WaiterHands.ActivePlayerHands == null) return false;

        return !WaiterHands.ActivePlayerHands.HasMoney;
    }

    public void Interact(PlayerMovement mover)
    {
        if (MultiplayerServiceActions.IsActive) { MultiplayerServiceActions.Approach(isCardPayment ? "card_open" : "money_pickup", targetGroup, StandPoint, interactRadius); return; }
        if (isCardPayment)
        {
            UI_RequestCardPayment();
            return;
        }

        if (!TryPickup(mover))
            RecoverFailedPickup();
    }

    public void UI_RequestPickup()
    {
        if (MultiplayerServiceActions.IsActive) { MultiplayerServiceActions.Approach("money_pickup", targetGroup, StandPoint, interactRadius); return; }
        if (!TutorialCustomerFlowBridge.AllowsServiceUI("PaymentPickupButton")) return;
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

    public void UI_RequestCardPayment()
    {
        if (MultiplayerServiceActions.IsActive) { MultiplayerServiceActions.Approach("card_open", targetGroup, StandPoint, interactRadius); return; }
        if (!TutorialCustomerFlowBridge.AllowsServiceUI("PaymentPickupButton")) return;
        if (!isCardPayment || !IsAvailableForCollection)
            return;

        if (RestaurantTaskClaim.PlayerHasActiveTask &&
            !RestaurantTaskClaim.IsClaimedByPlayer(this))
        {
            WarningSlideUI.Instance?.Show("Finish your current task first.");
            return;
        }

        if (!RestaurantTaskClaim.TryClaimPlayer(this))
        {
            WarningSlideUI.Instance?.Show("A staff member is already handling this payment.");
            return;
        }

        SetClaimedByStaff(true);
        CardPaymentUI ui = CardPaymentUI.Instance != null
            ? CardPaymentUI.Instance
            : FindFirstObjectByType<CardPaymentUI>(FindObjectsInactive.Include);
        if (ui == null || !ui.Open(this))
        {
            RestaurantTaskClaim.ReleasePlayer(this);
            SetClaimedByStaff(false);
            WarningSlideUI.Instance?.Show("Card terminal is unavailable.");
        }
    }

    public void CancelCardPaymentUI()
    {
        if (MultiplayerServiceActions.IsActive) { MultiplayerServiceActions.Send("payment_cancel", targetGroup); return; }
        if (!isCardPayment || isPickedUp)
            return;

        RestaurantTaskClaim.ReleasePlayer(this);
        SetClaimedByStaff(RestaurantTaskClaim.IsClaimedByBot(this));
    }

    public bool CompleteCardPayment()
    {
        if (MultiplayerServiceActions.IsActive) return MultiplayerServiceActions.Send("card_confirm", targetGroup);
        if (!isCardPayment || isPickedUp || !IsAvailableForCollection)
            return false;

        CashierRegisterUI register = CashierRegisterUI.Instance != null
            ? CashierRegisterUI.Instance
            : FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
        if (register == null || !register.CompleteAutomatedPayment(targetGroup))
            return false;

        isPickedUp = true;
        if (cachedCol != null)
            cachedCol.enabled = false;
        bubbleUI?.RemoveBubble();
        bubbleUI = null;
        RestaurantTaskClaim.Complete(this);
        Destroy(gameObject);
        return true;
    }

    public bool TryPickup(PlayerMovement mover = null)
    {
        if (MultiplayerServiceActions.IsActive) return MultiplayerServiceActions.Send("money_pickup", targetGroup);
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

    private static float GetCardPlayerPrioritySeconds()
    {
        EquipmentUpgrade upgrade = EquipmentUpgradeService.Find(EquipmentUpgradeEffect.CardPayment);
        return upgrade != null ? Mathf.Max(0f, upgrade.playerPrioritySeconds) : 5f;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null && targetGroup == group;
    }

    private void OnDestroy()
    {
        // Unity is already tearing down this transform. Clear the holder's
        // reference without trying to move the dying object out of its parent.
        GetComponentInParent<WaiterHands>(true)?.DetachMoneyPickup(this, reparent: false);
        if (!MultiplayerServiceActions.IsActive || MultiplayerSessionManager.Instance.IsAuthority)
            RestaurantTaskClaim.Complete(this);
        if (bubbleUI != null)
            bubbleUI.RemoveBubble();
    }
}
