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

    public Transform StandPoint => pickupPoint != null ? pickupPoint : transform;
    public bool AutoReturnHome => false;
    public bool IsCleanupPickable => mode == TrayMode.Cleanup;

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
        if (queueOwner != null)
            queueOwner.Unregister(this);

        HideUI();
    }

    public float GetInteractRadius()
    {
        return interactRadius;
    }

    public void OnTaskCancelled()
    {
        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    public void SetDeliveryPickable(TrayPickupQueue queue)
    {
        mode = TrayMode.Delivery;
        queueOwner = queue;

        if (queueOwner != null)
            queueOwner.Register(this);

        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    public void NotifyDeliveredToTable()
    {
        mode = TrayMode.None;
        queueOwner = null;
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
        pickupRequested = false;
        uiHiddenUntilStateChange = false;
        RefreshUI();
    }

    public void SetQueuePickable(bool allowed)
    {
        RefreshUI();
    }

    public bool CanInteract()
    {
        if (mode == TrayMode.None) return false;
        if (tray == null) return false;
        if (RoleManager.Instance == null) return false;

        if (mode == TrayMode.Delivery)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
                return false;

            if (WaiterHands.Instance == null) return false;
            if (WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill) return false;

            if (queueOwner != null && !queueOwner.IsNext(this))
                return false;
        }
        else if (mode == TrayMode.Cleanup)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Busser))
                return false;

            if (BusserHands.Instance == null) return false;
            if (BusserHands.Instance.HasTray) return false;
        }

        return true;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanInteractWithWarning())
        {
            pickupRequested = false;
            uiHiddenUntilStateChange = false;
            RefreshUI();
            return;
        }

        bool wasCleanup = (mode == TrayMode.Cleanup);

        if (mode == TrayMode.Delivery)
        {
            if (WaiterHands.Instance == null)
            {
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            if (!WaiterHands.Instance.PickupTray(tray))
            {
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
            if (BusserHands.Instance == null)
            {
                pickupRequested = false;
                uiHiddenUntilStateChange = false;
                RefreshUI();
                return;
            }

            if (!BusserHands.Instance.PickupTray(tray))
            {
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

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

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

        if (mode == TrayMode.Delivery)
        {
            if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
            {
                ShowWarning("Only the waiter can deliver food.");
                return false;
            }

            if (WaiterHands.Instance == null) return false;

            if (WaiterHands.Instance.HasBill)
            {
                ShowWarning("You are already carrying a bill.");
                return false;
            }

            if (WaiterHands.Instance.HasTray)
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

            if (BusserHands.Instance == null) return false;

            if (BusserHands.Instance.HasTray)
            {
                ShowWarning("You are already carrying a tray.");
                return false;
            }
        }

        return CanInteract();
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

            if (WaiterHands.Instance == null || WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill)
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

            if (BusserHands.Instance == null || BusserHands.Instance.HasTray)
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
