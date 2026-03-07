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

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Collider cachedCol;
    private GameObject pickupUiInstance;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => autoReturnHome;

    private void Awake()
    {
        cachedCol = GetComponentInChildren<Collider>(true);

        if (uiAnchor == null)
        {
            var t = transform.Find("ButtonAnchor");
            if (t != null) uiAnchor = t;
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
        if (WaiterHands.Instance == null) return false;
        return !WaiterHands.Instance.HasBill;
    }

    public void Interact(PlayerMovement mover)
    {
        TryPickup();
    }

    public void UI_Pickup()
    {
        if (!CanInteract()) return;

        var mover = FindFirstObjectByType<PlayerMovement>();
        if (mover == null) return;

        mover.UI_MoveTo(this);
    }

    public bool TryPickup()
    {
        if (!CanInteract()) return false;

        var hands = WaiterHands.Instance;
        if (hands == null) return false;
        if (hands.HasBill) return false;

        hands.PickupBillPaper(this);

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

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

    private void SpawnPickupUI()
    {
        if (pickupUiPrefab == null)
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] pickupUiPrefab is NULL", this);
            return;
        }

        if (uiAnchor == null)
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] uiAnchor is NULL (ButtonAnchor not found/assigned)", this);
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

        if (debugLogs) Debug.Log($"[BillPaper] Spawned pickup UI under canvas: {canvas.name}", this);

        var follow = pickupUiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
        {
            follow.Init(uiAnchor, uiOffset, Camera.main);
            if (debugLogs) Debug.Log("[BillPaper] UIFollowWorldPoint.Init() called", this);
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] UIFollowWorldPoint missing on pickupUiPrefab", this);
        }

        var pickBtn = pickupUiInstance.GetComponentInChildren<BillPaperPickupButton>(true);
        if (pickBtn != null)
        {
            pickBtn.SetBill(this);
            if (debugLogs) Debug.Log("[BillPaper] Assigned bill to BillPaperPickupButton", this);
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[BillPaper] BillPaperPickupButton missing on pickupUiPrefab", this);
        }
    }

    private Canvas ResolveGameplayCanvas()
    {
        if (gameplayCanvas != null) return gameplayCanvas;

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (!c.isActiveAndEnabled) continue;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                return c;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c != null && c.isActiveAndEnabled)
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

    private void OnDisable()
    {
        ClearPickupUI();
    }

    private void OnDestroy()
    {
        ClearPickupUI();
    }
}