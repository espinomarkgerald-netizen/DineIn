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

    public Transform StandPoint => pickupPoint != null ? pickupPoint : transform;
    public bool AutoReturnHome => false;

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
        RefreshUI();
    }

    public void SetDeliveryPickable(TrayPickupQueue queue)
    {
        mode = TrayMode.Delivery;
        queueOwner = queue;

        if (queueOwner != null)
            queueOwner.Register(this);

        pickupRequested = false;
        RefreshUI();
    }

    public void NotifyDeliveredToTable()
    {
        mode = TrayMode.None;
        queueOwner = null;
        pickupRequested = false;
        HideUI();
    }

    public void SetCleanupPickable(bool value)
    {
        if (queueOwner != null)
            queueOwner.Unregister(this);

        queueOwner = null;
        mode = value ? TrayMode.Cleanup : TrayMode.None;
        pickupRequested = false;

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
            RefreshUI();
            return;
        }

        bool wasCleanup = (mode == TrayMode.Cleanup);

        if (mode == TrayMode.Delivery)
        {
            if (WaiterHands.Instance == null)
            {
                pickupRequested = false;
                RefreshUI();
                return;
            }

            if (!WaiterHands.Instance.PickupTray(tray))
            {
                pickupRequested = false;
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
                RefreshUI();
                return;
            }

            if (!BusserHands.Instance.PickupTray(tray))
            {
                pickupRequested = false;
                RefreshUI();
                return;
            }
        }

        pickupRequested = false;
        mode = TrayMode.None;
        queueOwner = null;
        HideUI();

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
        HideUI();

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

        var group = tray.TargetGroup;
        if (group == null) return;

        if (group.state == CustomerGroup.GroupState.Leaving ||
            group.state == CustomerGroup.GroupState.AngryLeft)
        {
            SetCleanupPickable(true);
        }
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

        if (!CanInteract())
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
        }

        ShowUI();
    }

    private void ShowUI()
    {
        if (pickupUiPrefab == null || uiAnchor == null) return;
        if (uiInstance != null) return;

        Canvas canvas = null;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (!c.isActiveAndEnabled) continue;

            var ray = c.GetComponent<GraphicRaycaster>();
            if (ray == null || !ray.enabled) continue;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas = c;
                break;
            }
        }

        if (canvas == null) return;

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;

        uiInstance = Instantiate(pickupUiPrefab, canvas.transform);

        var follow = uiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(uiAnchor, Vector3.zero, Camera.main);

        var btn = uiInstance.GetComponentInChildren<TrayPickupUIButton>(true);
        if (btn != null)
        {
            btn.SetTray(this);
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
}