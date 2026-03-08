using UnityEngine;
using UnityEngine.UI;

public class FoodTrayInteractable : MonoBehaviour, IInteractable
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

    private GameObject uiInstance;
    private TrayPickupQueue queueOwner;
    private TrayMode mode = TrayMode.None;

    public Transform StandPoint => pickupPoint != null ? pickupPoint : transform;
    public bool AutoReturnHome => false;
    private bool pickupRequested;

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

    public void SetDeliveryPickable(TrayPickupQueue queue)
    {
        mode = TrayMode.Delivery;
        queueOwner = queue;

        if (queueOwner != null)
            queueOwner.Register(this);

        RefreshUI();
    }

    public void NotifyDeliveredToTable()
    {
        mode = TrayMode.None;
        queueOwner = null;
        HideUI();
    }

    public void SetCleanupPickable(bool value)
    {
        Debug.Log("[Tray] SetCleanupPickable called: " + value);

        if (queueOwner != null)
            queueOwner.Unregister(this);

        queueOwner = null;
        mode = value ? TrayMode.Cleanup : TrayMode.None;

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
        if (!CanInteract()) return;

        bool wasCleanup = (mode == TrayMode.Cleanup);

        if (mode == TrayMode.Delivery)
        {
            if (WaiterHands.Instance == null) return;
            if (!WaiterHands.Instance.PickupTray(tray)) return;

            if (queueOwner != null)
                queueOwner.OnPicked(this);
        }
        else if (mode == TrayMode.Cleanup)
        {
            if (BusserHands.Instance == null) return;
            if (!BusserHands.Instance.PickupTray(tray)) return;
        }

        pickupRequested = false;
        mode = TrayMode.None;
        queueOwner = null;
        HideUI();

        if (wasCleanup && autoGoSinkOnCleanupPickup && sink != null)
            mover.UI_MoveTo(sink);
    }

    public void UI_RequestPickup()
    {
        if (!CanInteract()) return;
        if (RoleManager.Instance == null) return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

        HideUI();
        mover.UI_MoveTo(this);
    }

    private void OnMouseDown()
    {
        if (!CanInteract()) return;
        UI_RequestPickup();
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
}