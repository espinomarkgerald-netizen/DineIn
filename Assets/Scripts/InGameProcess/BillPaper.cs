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

    [Header("Pickup UI (Tray-style)")]
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private GameObject pickupUiPrefab;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private bool spawnPickupUiOnInit = true;

    [Header("Canvas (Optional)")]
    [SerializeField] private Canvas gameplayCanvas;

    [Header("Auto")]
    [SerializeField] private AutoInteractRadius autoRadius;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Collider cachedCol;
    private GameObject pickupUiInstance;
    private bool pickupRequested;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        cachedCol = GetComponentInChildren<Collider>(true);
        if (autoRadius == null) autoRadius = GetComponent<AutoInteractRadius>();

        if (uiAnchor == null)
        {
            var t = transform.Find("ButtonAnchor");
            if (t != null) uiAnchor = t;
        }
    }

    private void Update()
    {
        if (pickupRequested)
        {
            ClearPickupUI();
            return;
        }

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

        if (spawnPickupUiOnInit)
            SpawnPickupUI();
    }

    public bool CanInteract()
    {
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
        ClearPickupUI();
        mover.UI_MoveTo(this);
    }

    public bool TryPickup()
    {
        if (!CanPickupWithWarning()) return false;

        var hands = WaiterHands.Instance;
        if (hands == null) return false;
        if (hands.HasBill) return false;

        hands.PickupBillPaper(this);

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

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

    private void SpawnPickupUI()
    {
        if (pickupRequested) return;
        if (!CanInteract()) return;

        if (pickupUiPrefab == null)
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] pickupUiPrefab is NULL", this);
            return;
        }

        if (uiAnchor == null)
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] uiAnchor is NULL", this);
            return;
        }

        var canvas = ResolveGameplayCanvas();
        if (canvas == null)
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] gameplay canvas not found", this);
            return;
        }

        ClearPickupUI();

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

    private void OnDisable() => ClearPickupUI();
    private void OnDestroy() => ClearPickupUI();
}