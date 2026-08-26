using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TakeoutBagInteractable : MonoBehaviour
{
    public static TakeoutBagInteractable HeldBag { get; private set; }

    [Header("Interaction")]
    [SerializeField] private Collider clickCollider;
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0f, 0.15f, 0.35f);
    [SerializeField] private Vector3 holdLocalEulerAngles = Vector3.zero;

    [Header("Pickup UI")]
    [SerializeField] private GameObject pickupUiPrefab;
    [SerializeField] private Transform uiAnchor;

    [Header("Order Data")]
    [SerializeField] private CustomerGroup targetGroup;
    [SerializeField] private int orderNumber = -1;
    [SerializeField] private List<string> deliveredContents = new();

    private bool isHeld;
    private bool heldByPlayer;
    private GameObject uiInstance;
    private bool claimedByStaff;

    public CustomerGroup TargetGroup => targetGroup;
    public int OrderNumber => orderNumber;
    public List<string> DeliveredContents => new(deliveredContents);
    public static bool HasHeldBag => HeldBag != null;
    public static bool PlayerHasHeldBag => HeldBag != null && HeldBag.heldByPlayer;

    private void Awake()
    {
        if (clickCollider == null)
            clickCollider = GetComponent<Collider>();

        HideUI();
    }

    private void Update()
    {
        RefreshUI();
    }

    private void OnDestroy()
    {
        RestaurantTaskClaim.Complete(this);
        HideUI();
    }

    /// <summary>
    /// Initialises the bag with the target group's order data.
    /// Called by KitchenManager immediately after spawning the bag.
    /// </summary>
    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;

        deliveredContents.Clear();

        if (group != null)
            deliveredContents.AddRange(group.GetCurrentOrderContents());
    }

    private void OnMouseDown()
    {
        TryPickup();
    }

    /// <summary>Picks up the bag and attaches it to the waiter's hold point.</summary>
    public void TryPickup()
    {
        WaiterHands hands = WaiterHands.ActivePlayerHands;
        if (hands == null)
            return;

        if (!TryPickupInternal(hands, true))
            return;
    }

    /// <summary>Autonomous waiter entry point; never redirects into Manager hands.</summary>
    public bool TryPickupForStaff(WaiterHands hands)
    {
        return TryPickupInternal(hands, false);
    }

    private bool TryPickupInternal(WaiterHands hands, bool playerInitiated)
    {
        if (isHeld || HeldBag != null)
            return false;

        if (hands == null)
            return false;

        if (hands.HasTray || hands.HasBill || hands.HasMoney || hands.HasTicket)
            return false;

        if (playerInitiated && !RestaurantTaskClaim.TryClaimPlayer(this))
        {
            WarningSlideUI.Instance?.Show(RestaurantTaskClaim.PlayerHasActiveTask
                ? "Finish your current task first."
                : "The waiter is already collecting this takeout order.");
            return false;
        }

        isHeld = true;
        heldByPlayer = playerInitiated;
        HeldBag = this;

        HideUI();

        Transform holdPoint = hands.TrayHoldPoint;
        WaiterHands.AttachKeepingWorldScale(
            transform,
            holdPoint,
            holdLocalPosition,
            Quaternion.Euler(holdLocalEulerAngles));

        WaiterHands.SetAllColliders(gameObject, false);

        // Highlight the target customer so the waiter knows where to deliver.
        ShowDeliveryHighlight();
        return true;
    }

    /// <summary>
    /// Delivers the bag to the given takeout customer group.
    /// Validates group match before calling ReceiveTakeoutBagFromWaiter.
    /// </summary>
    public bool TryDeliverTo(CustomerGroup group)
    {
        if (!isHeld || HeldBag != this)
            return false;

        if (group == null || !group.IsTakeout)
            return false;

        if (group != targetGroup)
        {
            WarningSlideUI.Instance?.Show("This order belongs to a different customer.");
            return false;
        }

        bool delivered = group.ReceiveTakeoutBagFromWaiter(deliveredContents);
        if (!delivered)
            return false;

        ClearHeldBag(true);
        return true;
    }

    public static void ClearHeldBag(bool destroyBag)
    {
        if (HeldBag == null)
            return;

        TakeoutBagInteractable bag = HeldBag;
        HeldBag = null;
        bag.isHeld = false;
        bag.heldByPlayer = false;
        RestaurantTaskClaim.Complete(bag);

        // Remove the delivery highlight from the target customer.
        bag.ClearDeliveryHighlight();

        if (destroyBag)
        {
            Destroy(bag.gameObject);
            return;
        }

        WaiterHands.SetAllColliders(bag.gameObject, true);
    }

    private void ShowDeliveryHighlight()
    {
        if (targetGroup == null)
            return;

        targetGroup.SetDeliveryHighlight(true);
    }

    private void ClearDeliveryHighlight()
    {
        if (targetGroup == null)
            return;

        targetGroup.SetDeliveryHighlight(false);
    }

    // -------------------------------------------------------------------------
    // UI — mirrors FoodTrayInteractable's ShowUI / HideUI / RefreshUI pattern
    // -------------------------------------------------------------------------

    private void RefreshUI()
    {
        if (claimedByStaff || RestaurantTaskClaim.IsClaimedByBot(this))
        {
            HideUI();
            return;
        }

        if (isHeld)
        {
            HideUI();
            return;
        }

        WaiterHands hands = WaiterHands.ActivePlayerHands;
        if (hands == null || hands.HasTray || hands.HasBill ||
            hands.HasMoney || hands.HasTicket || HasHeldBag)
        {
            HideUI();
            return;
        }

        if (RoleManager.Instance != null && !RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            HideUI();
            return;
        }

        ShowUI();
    }

    public void SetClaimedByStaff(bool claimed)
    {
        claimedByStaff = claimed;
        if (claimed)
            HideUI();
        else
            RefreshUI();
    }

    private void ShowUI()
    {
        if (pickupUiPrefab == null || uiAnchor == null)
            return;

        if (uiInstance != null)
            return;

        uiInstance = Instantiate(pickupUiPrefab);
        var follow = uiInstance.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(uiAnchor, Vector3.zero, Camera.main);

        // Try the bag-specific button first.
        var bagBtn = uiInstance.GetComponentInChildren<BagPickupUIButton>(true);
        if (bagBtn != null)
        {
            bagBtn.SetBag(this);
            bagBtn.SetOrderNumber(orderNumber);
        }
        else
        {
            // The PaperBag reuses TrayUi.prefab which carries TrayPickupUIButton, not
            // BagPickupUIButton. Set the order number through TrayPickupUIButton so the
            // TMP text is updated rather than showing the prefab's baked default "1".
            var trayBtn = uiInstance.GetComponentInChildren<TrayPickupUIButton>(true);
            if (trayBtn != null)
            {
                trayBtn.SetTableNumber(orderNumber);

                // Re-wire the button click to pick up the bag instead of a tray.
                var b = trayBtn.GetComponentInChildren<Button>(true);
                if (b == null) b = trayBtn.GetComponent<Button>();
                if (b != null)
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(TryPickup);
                }
            }
            else
            {
                // Last-resort: find any Button and wire it up.
                var b = uiInstance.GetComponentInChildren<Button>(true);
                if (b != null)
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(TryPickup);
                }
            }
        }

        Button actionButton = uiInstance.GetComponentInChildren<Button>(true);
        if (actionButton != null)
            PlayerTaskBubbleFocus.Bind(actionButton.gameObject, this);
    }

    private void HideUI()
    {
        if (uiInstance != null)
            Destroy(uiInstance);

        uiInstance = null;
    }

}
