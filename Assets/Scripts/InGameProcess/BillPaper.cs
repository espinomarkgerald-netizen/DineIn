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

    [Header("Canvas (Optional)")]
    [SerializeField] private Canvas gameplayCanvas;

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
        if (IsHeldByWaiter())
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
        if (IsHeldByWaiter()) return false;
        if (targetGroup == null) return false;
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return false;
        if (WaiterHands.Instance == null) return false;

        return !WaiterHands.Instance.HasBill;
    }

    public void Interact(PlayerMovement mover)
    {
        TryPickup();
    }

    public void UI_Pickup()
    {
        if (!CanPickupWithWarning()) return;
        if (RoleManager.Instance == null) return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

        pickupRequested = true;
        RefreshPickupUI();
        mover.UI_MoveTo(this);
    }

    public bool TryPickup()
    {
        if (isPickedUp) return false;
        if (IsHeldByWaiter()) return false;
        if (!CanPickupWithWarning()) return false;

        var hands = WaiterHands.Instance;
        if (hands == null) return false;
        if (hands.HasBill) return false;

        hands.PickupBillPaper(this);

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
        if (IsHeldByWaiter()) return false;
        if (targetGroup == null) return false;
        if (RoleManager.Instance == null) return false;

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            ShowWarning("Only the waiter can pick up bills.");
            return false;
        }

        if (WaiterHands.Instance == null) return false;

        if (WaiterHands.Instance.HasBill)
        {
            int tableNo = WaiterHands.Instance.holdingBillFor != null
                ? WaiterHands.Instance.holdingBillFor.currentOrderNumber
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
            !IsHeldByWaiter();

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

    private bool IsHeldByWaiter()
    {
        if (WaiterHands.Instance == null)
            return false;

        var holdPoint = WaiterHands.Instance.BillHoldPoint;
        if (holdPoint == null)
            return false;

        return transform == holdPoint || transform.IsChildOf(holdPoint);
    }

    private void SpawnPickupUI()
    {
        if (pickupUiPrefab == null || uiAnchor == null) return;
        if (pickupUiInstance != null) return;

        var canvas = ResolveGameplayCanvas();
        if (canvas == null) return;

        pickupUiInstance = Instantiate(pickupUiPrefab);
        pickupUiInstance.transform.SetParent(canvas.transform, false);
        pickupUiInstance.transform.localScale = Vector3.one;
        pickupUiInstance.SetActive(true);

        var follow = pickupUiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(uiAnchor, uiOffset, Camera.main);

        var pickBtn = pickupUiInstance.GetComponentInChildren<BillPaperPickupButton>(true);
        if (pickBtn != null)
            pickBtn.SetBill(this);
    }

    private Canvas ResolveGameplayCanvas()
    {
        if (gameplayCanvas != null) return gameplayCanvas;

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null || !c.isActiveAndEnabled) continue;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                return c;
        }

        return null;
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
        if (isPickedUp || IsHeldByWaiter())
            return;

        pickupRequested = false;
        RefreshPickupUI();
    }

    private void OnDisable() => ClearPickupUI();
    private void OnDestroy() => ClearPickupUI();
}