using UnityEngine;
using UnityEngine.UI;

public class FoodTrayInteractable : MonoBehaviour, IInteractable, ICancelableTaskTarget
{
    public enum TrayMode { None, Delivery, Cleanup }

    [Header("Refs")]
    [SerializeField] private FoodTray tray;
    [SerializeField] private Transform pickupPoint;

    [Header("UI")]
    [SerializeField] private GameObject pickupUiPrefab;
    [SerializeField] private Transform uiAnchor;

    [Header("Cleanup")]
    [SerializeField] private SinkInteractable sink;
    [SerializeField] private bool autoGoSinkOnCleanupPickup = true;

    [Header("Interact")]
    [SerializeField] private float interactRadius = 1.2f;

    private GameObject uiInstance;
    private TrayPickupQueue queueOwner;
    private TrayMode mode = TrayMode.None;
    private bool pickupRequested;
    private bool pendingCleanup;
    private bool uiHiddenUntilStateChange;
    private bool claimedByStaff;
    private bool staffCarried;
    private bool complaintRemoval;
    private TrayMode modeBeforeStaffPickup = TrayMode.None;
    private TrayPickupQueue queueBeforeStaffPickup;
    private float readySince;
    private float readySinceBeforeStaffPickup;

    public Transform StandPoint => ResolveStandPoint();
    public bool AutoReturnHome => false;
    public bool IsDeliveryPickable => mode == TrayMode.Delivery;
    public bool IsCleanupPickable => mode == TrayMode.Cleanup;
    public bool IsComplaintRemoval => complaintRemoval;
    public bool IsStaffCarried => staffCarried;
    public TrayMode CurrentMode => mode;
    public float ReadySince => readySince;

    private void Awake()
    {
        if (tray == null) tray = GetComponent<FoodTray>();
        if (sink == null) sink = FindFirstObjectByType<SinkInteractable>();
        HideUI();
    }

    private void Update()
    {
        CheckCleanupState();
        RefreshUI();
    }

    private void OnDestroy()
    {
        RestaurantTaskClaim.Complete(tray);
        if (queueOwner != null)
            queueOwner.Unregister(this);
        if (queueBeforeStaffPickup != null && queueBeforeStaffPickup != queueOwner)
            queueBeforeStaffPickup.Unregister(this);

        HideUI();
    }

    public float GetInteractRadius()
    {
        return interactRadius;
    }

    private Transform ResolveStandPoint()
    {
        if (mode == TrayMode.Cleanup)
        {
            // A used tray is sitting on a non-walkable tabletop. Use the same
            // booth approach point as the autonomous busser instead of trying
            // to path the Manager to the tray's elevated world position.
            Booth sourceBooth = GetComponentInParent<Booth>();
            if (sourceBooth == null && tray != null && tray.TargetGroup != null)
                sourceBooth = tray.TargetGroup.assignedBooth;

            if (sourceBooth != null)
            {
                if (sourceBooth.approachPoint != null)
                    return sourceBooth.approachPoint;

                return sourceBooth.transform;
            }
        }

        return pickupPoint != null ? pickupPoint : transform;
    }

    public void OnTaskCancelled()
    {
        RestaurantTaskClaim.ReleasePlayer(tray);
        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    public void SetClaimedByStaff(bool claimed)
    {
        claimedByStaff = claimed;
        if (claimed)
            HideUI();
        else if (!staffCarried)
            RefreshUI();
    }

    public void SetDeliveryPickable(TrayPickupQueue queue)
    {
        if (queueOwner != null && queueOwner != queue)
            queueOwner.Unregister(this);

        ClearStaffPickupSnapshot();
        complaintRemoval = false;
        mode = TrayMode.Delivery;
        queueOwner = queue;
        readySince = Time.time;

        if (queueOwner != null)
            queueOwner.Register(this);

        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    public void NotifyDeliveredToTable()
    {
        if (queueOwner != null)
            queueOwner.Unregister(this);
        if (queueBeforeStaffPickup != null && queueBeforeStaffPickup != queueOwner)
            queueBeforeStaffPickup.Unregister(this);

        mode = TrayMode.None;
        complaintRemoval = false;
        queueOwner = null;
        readySince = 0f;
        claimedByStaff = false;
        staffCarried = false;
        ClearStaffPickupSnapshot();
        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        HideUI();
    }

    public void SetCleanupPickable(bool value)
    {
        if (queueOwner != null)
            queueOwner.Unregister(this);

        queueOwner = null;
        mode = value ? TrayMode.Cleanup : TrayMode.None;
        readySince = value ? Time.time : 0f;
        staffCarried = false;
        ClearStaffPickupSnapshot();
        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    /// <summary>
    /// Makes food rejected during a Manager complaint available to the existing
    /// busser cleanup workflow even though the customer is still seated.
    /// </summary>
    public void MarkForComplaintRemoval()
    {
        complaintRemoval = true;
        SetCleanupPickable(true);
    }

    /// <summary>
    /// Performs the logical half of an autonomous pickup before the tray is
    /// reparented to hands or a trolley. Queue membership, task UI and tray mode
    /// change atomically, and RestoreAfterStaffPickup can reverse the transition.
    /// </summary>
    public bool TryBeginStaffPickup(AutonomousStaffBot owner, TrayMode expectedMode)
    {
        if (owner == null || tray == null || staffCarried || mode != expectedMode ||
            !RestaurantTaskClaim.IsClaimedByBot(tray, owner))
        {
            return false;
        }

        modeBeforeStaffPickup = mode;
        queueBeforeStaffPickup = queueOwner;
        readySinceBeforeStaffPickup = readySince;
        if (queueOwner != null)
            queueOwner.Unregister(this);

        queueOwner = null;
        mode = TrayMode.None;
        readySince = 0f;
        staffCarried = true;
        claimedByStaff = true;
        pickupRequested = false;
        uiHiddenUntilStateChange = true;
        HideUI();
        return true;
    }

    public void RestoreAfterStaffPickup()
    {
        if (!staffCarried)
            return;

        mode = modeBeforeStaffPickup;
        readySince = readySinceBeforeStaffPickup > 0f
            ? readySinceBeforeStaffPickup
            : Time.time;
        queueOwner = mode == TrayMode.Delivery ? queueBeforeStaffPickup : null;
        staffCarried = false;
        claimedByStaff = false;
        pickupRequested = false;
        uiHiddenUntilStateChange = false;

        if (queueOwner != null)
            queueOwner.Register(this);

        ClearStaffPickupSnapshot();
        RefreshUI();
    }

    private void ClearStaffPickupSnapshot()
    {
        modeBeforeStaffPickup = TrayMode.None;
        queueBeforeStaffPickup = null;
        readySinceBeforeStaffPickup = 0f;
    }

    public void SetQueuePickable(bool allowed)
    {
        RefreshUI();
    }

    public bool CanInteract()
    {
        if (mode == TrayMode.None) return false;
        if (tray == null) return false;
        if (RestaurantTaskClaim.IsClaimedByBot(tray)) return false;
        if (RoleManager.Instance == null) return false;

        if (mode == TrayMode.Delivery)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
                return false;

            if (WaiterHands.ActivePlayerHands == null) return false;
            if (WaiterHands.ActivePlayerHands.HasTray || WaiterHands.ActivePlayerHands.HasBill) return false;

            if (queueOwner != null && !queueOwner.IsNext(this))
                return false;
        }
        else if (mode == TrayMode.Cleanup)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Busser))
                return false;

            if (BusserHands.ActivePlayerHands == null) return false;
            if (BusserHands.ActivePlayerHands.HasTray) return false;
        }

        return true;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanInteractWithWarning())
        {
            RestaurantTaskClaim.ReleasePlayer(tray);
            pickupRequested = false;
            uiHiddenUntilStateChange = false;
            RefreshUI();
            return;
        }

        if (!TryClaimForPlayer())
        {
            pickupRequested = false;
            uiHiddenUntilStateChange = false;
            RefreshUI();
            return;
        }

        bool wasCleanup = (mode == TrayMode.Cleanup);

        if (mode == TrayMode.Delivery)
        {
            WaiterHands waiterHands = WaiterHands.For(mover);
            if (waiterHands == null)
            {
                RestaurantTaskClaim.ReleasePlayer(tray);
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            if (!waiterHands.PickupTray(tray))
            {
                RestaurantTaskClaim.ReleasePlayer(tray);
                Debug.Log("[FoodTrayInteractable] Waiter pickup failed: " + name);
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            if (queueOwner != null)
                queueOwner.OnPicked(this);
        }
        else if (mode == TrayMode.Cleanup)
        {
            BusserHands busserHands = BusserHands.For(mover);
            if (busserHands == null)
            {
                RestaurantTaskClaim.ReleasePlayer(tray);
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            if (!busserHands.PickupTray(tray))
            {
                RestaurantTaskClaim.ReleasePlayer(tray);
                Debug.Log("[FoodTrayInteractable] Busser pickup failed: " + name);
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            NotifyTutorialBusserPickup(tray);
        }

        pickupRequested = false;
        uiHiddenUntilStateChange = true;
        mode = TrayMode.None;
        readySince = 0f;
        staffCarried = false;
        ClearStaffPickupSnapshot();
        queueOwner = null;
        HideUI();

        Debug.Log("[FoodTrayInteractable] Pickup success, hiding UI: " + name);

        if (wasCleanup && autoGoSinkOnCleanupPickup && sink != null && mover != null)
        {
            mover.LockTask(sink);
            mover.UI_MoveTo(sink);
        }
    }

    public void UI_RequestPickup()
    {
        if (!CanInteractWithWarning()) return;
        if (RoleManager.Instance == null) return;

        if (!TryClaimForPlayer())
            return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null)
        {
            RestaurantTaskClaim.ReleasePlayer(tray);
            return;
        }

        pickupRequested = true;
        uiHiddenUntilStateChange = true;
        HideUI();

        Debug.Log("[FoodTrayInteractable] UI pickup requested: " + name);

        mover.LockTask(this);
        mover.UI_MoveTo(this);
    }

    private void OnMouseDown()
    {
        UI_RequestPickup();
    }

    private bool CanInteractWithWarning()
    {
        if (mode == TrayMode.None) return false;
        if (tray == null) return false;
        if (RoleManager.Instance == null) return false;

        if (RestaurantTaskClaim.IsClaimedByBot(tray))
        {
            ShowWarning(mode == TrayMode.Cleanup
                ? "The busser is already collecting this tray."
                : "The waiter is already collecting this order.");
            return false;
        }

        if (mode == TrayMode.Delivery)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
            {
                ShowWarning("Only the waiter can deliver food.");
                return false;
            }

            if (WaiterHands.ActivePlayerHands == null) return false;

            if (WaiterHands.ActivePlayerHands.HasBill)
            {
                ShowWarning("You are already carrying a bill.");
                return false;
            }

            if (WaiterHands.ActivePlayerHands.HasTray)
            {
                ShowWarning("You are already carrying a tray.");
                return false;
            }

            if (queueOwner != null && !queueOwner.IsNext(this))
            {
                ShowWarning("Pick up the next ready tray first.");
                return false;
            }
        }
        else if (mode == TrayMode.Cleanup)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Busser))
            {
                ShowWarning("Only the busser can clean used trays.");
                return false;
            }

            if (BusserHands.ActivePlayerHands == null) return false;

            if (BusserHands.ActivePlayerHands.HasTray)
            {
                ShowWarning("You are already carrying a tray.");
                return false;
            }
        }

        return CanInteract();
    }

    private bool TryClaimForPlayer()
    {
        if (RestaurantTaskClaim.TryClaimPlayer(tray))
            return true;

        ShowWarning(RestaurantTaskClaim.PlayerHasActiveTask
            ? "Finish your current task first."
            : mode == TrayMode.Cleanup
                ? "The busser is already collecting this tray."
                : "The waiter is already collecting this order.");
        return false;
    }

    private void CheckCleanupState()
    {
        if (mode != TrayMode.None) return;
        if (tray == null) return;

        if (pendingCleanup)
        {
            pendingCleanup = false;
            SetCleanupPickable(true);
            return;
        }

        var group = tray.TargetGroup;
        if (group == null) return;

        if (group.state == CustomerGroup.GroupState.Leaving ||
            group.state == CustomerGroup.GroupState.AngryLeft ||
            group.state == CustomerGroup.GroupState.UnhappyLeft)
        {
            SetCleanupPickable(true);
        }
    }

    public void NotifyGroupLeaving()
    {
        if (mode != TrayMode.None) return;
        pendingCleanup = true;
    }

    private void RefreshUI()
    {
        if (claimedByStaff || (tray != null && RestaurantTaskClaim.IsClaimedByBot(tray)))
        {
            HideUI();
            return;
        }

        if (pickupRequested)
        {
            HideUI();
            return;
        }

        if (mode == TrayMode.None)
        {
            HideUI();
            return;
        }

        if (RoleManager.Instance == null)
        {
            HideUI();
            return;
        }

        if (mode == TrayMode.Delivery)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
            {
                HideUI();
                return;
            }

            if (WaiterHands.ActivePlayerHands == null || WaiterHands.ActivePlayerHands.HasTray || WaiterHands.ActivePlayerHands.HasBill)
            {
                HideUI();
                return;
            }

            if (queueOwner != null && !queueOwner.IsNext(this))
            {
                HideUI();
                return;
            }
        }
        else if (mode == TrayMode.Cleanup)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Busser))
            {
                HideUI();
                return;
            }

            if (BusserHands.ActivePlayerHands == null || BusserHands.ActivePlayerHands.HasTray)
            {
                HideUI();
                return;
            }
        }

        ShowUI();
    }

    private void ShowUI()
    {
        if (pickupUiPrefab == null || uiAnchor == null) return;
        if (uiInstance != null) return;

        uiInstance = Instantiate(pickupUiPrefab);

        var follow = uiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(uiAnchor, Vector3.zero, Camera.main);

        var btn = uiInstance.GetComponentInChildren<TrayPickupUIButton>(true);
        if (btn != null)
        {
            btn.SetTray(this);

            int tableNumber = tray != null ? tray.orderNumber : -1;
            btn.SetTableNumber(tableNumber);
        }
        else
        {
            var b = uiInstance.GetComponentInChildren<Button>(true);
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(UI_RequestPickup);
            }
        }

        Button actionButton = uiInstance.GetComponentInChildren<Button>(true);
        if (actionButton != null)
            PlayerTaskBubbleFocus.Bind(actionButton.gameObject, tray);
    }

    private void HideUI()
    {
        if (uiInstance != null)
            Destroy(uiInstance);

        uiInstance = null;
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }

    private void NotifyTutorialBusserPickup(FoodTray pickedTray)
    {
        if (TutorialManager.Instance == null || !TutorialManager.Instance.TutorialStarted)
            return;

        TutorialManager.Instance.RegisterDirtyTrayPickedUp(pickedTray);
    }
}
