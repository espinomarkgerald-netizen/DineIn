using UnityEngine;

public class BillPaper : MonoBehaviour, IInteractable, ICancelableTaskTarget
{
    public int orderNumber;

    [SerializeField] private CustomerGroup targetGroup;
    public CustomerGroup TargetGroup => targetGroup;

    [Header("Interact")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private bool autoReturnHome = false;
    [SerializeField] private bool disableColliderWhileHeld = true;

    [Header("Pickup UI (Tray-style)")]
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private GameObject pickupUiPrefab;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private bool spawnPickupUiOnInit = true;

    [Header("Auto")]
    [SerializeField] private AutoInteractRadius autoRadius;

    private Collider cachedCol;
    private GameObject pickupUiInstance;
    private bool pickupRequested;
    private bool isPickedUp;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        cachedCol = GetComponentInChildren<Collider>(true);
        if (autoRadius == null)
            autoRadius = GetComponent<AutoInteractRadius>();

        if (uiAnchor == null)
        {
            var t = transform.Find("ButtonAnchor");
            if (t != null)
                uiAnchor = t;
        }
    }

    private void Start()
    {
        RefreshPickupUI();
    }

    private void Update()
    {
        if (IsHeldByAnyWaiter())
        {
            if (!isPickedUp)
                isPickedUp = true;

            pickupRequested = false;
            ClearPickupUI();
            return;
        }

        RefreshPickupUI();

        if (autoRadius != null && autoRadius.IsActiveRoleInRange(StaffRole.Role.Waiter))
        {
            var mover = RoleManager.Instance != null ? RoleManager.Instance.GetActivePlayerMovement() : null;
            if (mover != null && CanInteract())
                Interact(mover);
        }
    }

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        var num = GetComponentInChildren<TableNumberUI>(true);
        if (num != null)
            num.SetNumber(orderNumber);

        RefreshPickupUI();
    }

    public bool CanInteract()
    {
        if (isPickedUp) return false;
        if (IsHeldByAnyWaiter()) return false;
        if (targetGroup == null) return false;
        if (RestaurantTaskClaim.IsClaimedByBot(targetGroup)) return false;
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (WaiterHands.ActivePlayerHands == null) return false;

        return !WaiterHands.ActivePlayerHands.HasBill;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!TryPickup(mover))
            RecoverFailedPickup();
    }

    public void UI_Pickup()
    {
        if (!CanPickupWithWarning()) return;
        if (RoleManager.Instance == null) return;

        if (!RestaurantTaskClaim.TryClaimPlayer(targetGroup))
        {
            ShowWarning(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already handling this bill.");
            return;
        }

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null)
        {
            RestaurantTaskClaim.ReleasePlayer(targetGroup);
            return;
        }

        pickupRequested = true;
        RefreshPickupUI();
        mover.LockTask(this);
        mover.UI_MoveTo(this);
    }

    public bool TryPickup(PlayerMovement mover = null)
    {
        if (isPickedUp) return false;
        if (IsHeldByAnyWaiter()) return false;
        if (!CanPickupWithWarning()) return false;

        if (!RestaurantTaskClaim.TryClaimPlayer(targetGroup))
        {
            ShowWarning(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already handling this bill.");
            return false;
        }

        var hands = WaiterHands.For(mover);
        if (hands == null || hands.HasBill)
        {
            RestaurantTaskClaim.ReleasePlayer(targetGroup);
            return false;
        }

        hands.PickupBillPaper(this);

        if (!hands.HasBill || hands.holdingBillFor != targetGroup || !IsHeldBy(hands))
        {
            RecoverFailedPickup();
            return false;
        }

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

        isPickedUp = true;
        pickupRequested = false;
        ClearPickupUI();
        return true;
    }

    public bool Matches(CustomerGroup group)
    {
        return group != null
            && targetGroup != null
            && targetGroup == group
            && group.currentOrderNumber == orderNumber;
    }

    private bool CanPickupWithWarning()
    {
        if (isPickedUp) return false;
        if (IsHeldByAnyWaiter()) return false;
        if (targetGroup == null) return false;
        if (RoleManager.Instance == null) return false;

        if (RestaurantTaskClaim.IsClaimedByBot(targetGroup))
        {
            ShowWarning("The waiter is already handling this bill.");
            return false;
        }

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            ShowWarning("Only the waiter can pick up bills.");
            return false;
        }

        if (WaiterHands.ActivePlayerHands == null) return false;

        if (WaiterHands.ActivePlayerHands.HasBill)
        {
            int tableNo = WaiterHands.ActivePlayerHands.holdingBillFor != null
                ? WaiterHands.ActivePlayerHands.holdingBillFor.currentOrderNumber
                : -1;

            ShowWarning(tableNo >= 0
                ? $"You are already holding the bill for table {tableNo}."
                : "You are already holding a bill.");

            return false;
        }

        return true;
    }

    private void RefreshPickupUI()
    {
        bool shouldShow =
            spawnPickupUiOnInit &&
            !pickupRequested &&
            !isPickedUp &&
            !IsHeldByAnyWaiter() &&
            !RestaurantTaskClaim.IsClaimedByBot(targetGroup);

        if (shouldShow)
        {
            if (pickupUiInstance == null)
                SpawnPickupUI();
        }
        else
        {
            if (pickupUiInstance != null)
                ClearPickupUI();
        }
    }

    private bool IsHeldByAnyWaiter()
    {
        return IsHeldBy(WaiterHands.ActivePlayerHands) ||
               IsHeldBy(WaiterHands.Instance);
    }

    private bool IsHeldBy(WaiterHands hands)
    {
        if (hands == null)
            return false;

        Transform holdPoint = hands.BillHoldPoint;
        return holdPoint != null &&
               (transform == holdPoint || transform.IsChildOf(holdPoint));
    }

    private void SpawnPickupUI()
    {
        if (pickupUiPrefab == null || uiAnchor == null) return;
        if (pickupUiInstance != null) return;

        pickupUiInstance = Instantiate(pickupUiPrefab);
        pickupUiInstance.SetActive(true);

        var follow = pickupUiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(uiAnchor, uiOffset, Camera.main);

        var pickBtn = pickupUiInstance.GetComponentInChildren<BillPaperPickupButton>(true);
        if (pickBtn != null)
            pickBtn.SetBill(this);
    }

    private void ClearPickupUI()
    {
        if (pickupUiInstance != null)
            Destroy(pickupUiInstance);

        pickupUiInstance = null;
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }

    public float GetInteractRadius()
    {
        return 1.1f;
    }

    public void OnTaskCancelled()
    {
        RecoverFailedPickup();
    }

    private void RecoverFailedPickup()
    {
        if (isPickedUp || IsHeldByAnyWaiter())
            return;

        RestaurantTaskClaim.ReleasePlayer(targetGroup);
        pickupRequested = false;
        RefreshPickupUI();
    }

    private void OnDisable() => ClearPickupUI();
    private void OnDestroy()
    {
        RestaurantTaskClaim.ReleasePlayer(targetGroup);
        ClearPickupUI();
    }
}
