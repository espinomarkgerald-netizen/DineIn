using System;
using UnityEngine;

public class WaiterHands : MonoBehaviour
{
    public static WaiterHands Instance { get; private set; }

    public static WaiterHands ActivePlayerHands
    {
        get
        {
            var session = MultiplayerSessionManager.Instance;
            if (session != null && session.IsMultiplayerSession)
                return session.LocalManager?.GetComponent<WaiterHands>();

            if (ManagerPlayer.Active != null)
            {
                WaiterHands managerHands = ManagerPlayer.Active.GetComponent<WaiterHands>();
                if (managerHands != null)
                    return managerHands;
            }

            return Instance;
        }
    }

    public static event Action<WaiterHands> OnHandsStateChanged;

    [Header("Holding")]
    public CustomerGroup holdingTicketFor;
    public CustomerGroup holdingBillFor;
    public FoodTray holdingTray;
    public CustomerGroup holdingMoneyFor;
    public int holdingMoneyAmount;

    [Header("Hold Points")]
    [SerializeField] private Transform trayHoldPoint;
    [SerializeField] private Transform billHoldPoint;

    [Header("Trolley")]
    [Tooltip("Editable point the trolley handle aligns to while this character pushes it. Falls back to Tray Hold Point.")]
    [SerializeField] private Transform trolleyGripPoint;

    [Header("Held Visuals")]
    [SerializeField] private GameObject billHeldVisualPrefab;
    [SerializeField] private Transform moneyHoldPoint;
    [SerializeField] private GameObject moneyHeldVisualPrefab;

    private GameObject moneyHeldVisualInstance;
    private MoneyPickup heldMoney;

    private GameObject billHeldVisualInstance;
    private BillPaper heldBillPaper;

    public bool HasTicket => holdingTicketFor != null;
    public bool HasBill => holdingBillFor != null;
    public bool HasTray => holdingTray != null;
    public bool HasMoney => heldMoney != null;

    public MoneyPickup HeldMoney => heldMoney;

    // Multiplayer snapshots and completion callbacks can move an item before
    // its old hand reference is cleared. Reconcile references only: never move
    // or destroy the real item, including one now held by a different actor.
    public static void ReconcileMultiplayerHands(GameObject actor)
    {
        if (actor == null) return;
        actor.GetComponent<WaiterHands>()?.ReconcileHeldItemReferences();
        actor.GetComponent<BusserHands>()?.ReconcileHeldTrayReference();
    }

    public void ReconcileHeldItemReferences()
    {
        bool changed = false;
        if (holdingTray != null && !holdingTray.transform.IsChildOf(TrayHoldPoint))
        { holdingTray = null; changed = true; }
        if (heldMoney != null)
        {
            var group = heldMoney.TargetGroup;
            if (group == null || !heldMoney.transform.IsChildOf(MoneyHoldPoint) || group.MultiplayerPaymentComplete)
                DetachMoneyPickup(heldMoney, reparent: false);
            else if (holdingMoneyFor != group || holdingMoneyAmount != heldMoney.Amount)
            { holdingMoneyFor = group; holdingMoneyAmount = heldMoney.Amount; changed = true; }
        }
        if (heldBillPaper != null)
        {
            var group = heldBillPaper.TargetGroup;
            if (group == null || !heldBillPaper.transform.IsChildOf(BillHoldPoint) || group.HasReceivedBill || group.MultiplayerPaymentComplete)
                DetachBillPaper(heldBillPaper);
            else if (holdingBillFor != group) { holdingBillFor = group; changed = true; }
        }
        else if (holdingBillFor != null && (holdingBillFor.HasReceivedBill || holdingBillFor.MultiplayerPaymentComplete))
        {
            holdingBillFor = null;
            if (billHeldVisualInstance != null) Destroy(billHeldVisualInstance);
            billHeldVisualInstance = null;
            changed = true;
        }
        if (holdingTicketFor != null && (MultiplayerWorldRegistry.Kitchen?.HasAcceptedOrder(holdingTicketFor.currentOrderNumber) == true
            || HasOtherTicketOwner(holdingTicketFor) || holdingTicketFor.MultiplayerPaymentComplete
            || holdingTicketFor.state == CustomerGroup.GroupState.Eating || holdingTicketFor.state == CustomerGroup.GroupState.NeedsBill
            || holdingTicketFor.state == CustomerGroup.GroupState.Leaving || holdingTicketFor.state == CustomerGroup.GroupState.AngryLeft
            || holdingTicketFor.state == CustomerGroup.GroupState.UnhappyLeft))
        { holdingTicketFor = null; changed = true; }
        if (changed) NotifyHandsChanged();
    }

    private bool HasOtherTicketOwner(CustomerGroup group)
    {
        if (GetComponent<AutonomousStaffBot>() != null) return false;
        // Legacy contaminated local references may coexist with the staff's real ticket.
        foreach (var staff in MultiplayerWorldRegistry.All<AutonomousStaffBot>())
        {
            var hands = staff != null ? staff.GetComponent<WaiterHands>() : null;
            if (hands != null && hands != this && hands.holdingTicketFor == group) return true;
        }
        return false;
    }

    public string MultiplayerHeldItemBlocker()
    {
        if (HasMoney) return "Take the cash you are carrying to the cashier and finish the payment first.";
        if (HasBill) return $"Deliver the bill for customer #{holdingBillFor.currentOrderNumber} first.";
        if (HasTicket) return $"Submit the order ticket for customer #{holdingTicketFor.currentOrderNumber} first.";
        if (HasTray) return $"Deliver the food for customer #{holdingTray.orderNumber} first.";
        return null;
    }

    public Transform MoneyHoldPoint => moneyHoldPoint != null ? moneyHoldPoint : transform;
    public Transform TrayHoldPoint => trayHoldPoint != null ? trayHoldPoint : transform;
    public Transform BillHoldPoint => billHoldPoint != null ? billHoldPoint : transform;
    public Transform TrolleyGripPoint => trolleyGripPoint != null ? trolleyGripPoint : TrayHoldPoint;
    public bool HasDedicatedTrolleyGrip => trolleyGripPoint != null;

    public bool TryGetTrolleyGripPoint(out Transform gripPoint)
    {
        gripPoint = trolleyGripPoint;
        return gripPoint != null;
    }

    private void Awake()
    {
        MultiplayerWorldRegistry.Track(this);
        Debug.Log($"[WaiterHands] Awake on {name} id={GetInstanceID()}");

        bool belongsToManager = GetComponent<ManagerPlayer>() != null;
        if (!belongsToManager && Instance != null && Instance != this)
        {
            Debug.LogWarning($"[WaiterHands] Duplicate staff instance ignored on {name}.", this);
            enabled = false;
            return;
        }

        if (!belongsToManager)
            Instance = this;

        holdingTray = null;
        holdingTicketFor = null;
        holdingBillFor = null;
        heldBillPaper = null;
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;
        heldMoney = null;

        if (billHeldVisualInstance != null)
        {
            Destroy(billHeldVisualInstance);
            billHeldVisualInstance = null;
        }

        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }

        NotifyHandsChanged();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static WaiterHands For(PlayerMovement mover)
    {
        if (mover != null)
        {
            WaiterHands ownedHands = mover.GetComponent<WaiterHands>();
            var session = MultiplayerSessionManager.Instance;
            if (session != null && session.IsMultiplayerSession)
                return ownedHands; // Never substitute another actor's hands.
            if (ownedHands != null)
                return ownedHands;
        }

        return ActivePlayerHands;
    }

    /// <summary>
    /// Parents a carried object without inheriting the actor prefab's import
    /// scale. Manager and bot-held items therefore keep identical world size.
    /// </summary>
    public static void AttachKeepingWorldScale(
        Transform item,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        if (item == null || parent == null) return;

        Vector3 worldScale = item.lossyScale;
        item.SetParent(parent, false);
        item.localPosition = localPosition;
        item.localRotation = localRotation;

        Vector3 parentScale = parent.lossyScale;
        item.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    public static void SetAllColliders(GameObject target, bool enabled)
    {
        if (target == null) return;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private void NotifyHandsChanged()
    {
        OnHandsStateChanged?.Invoke(this);
    }

    public void ClearTicket()
    {
        holdingTicketFor = null;
        NotifyHandsChanged();
    }

    public void ClearBill()
    {
        CustomerGroup completedGroup = holdingBillFor;
        holdingBillFor = null;

        if (heldBillPaper != null)
        {
            Destroy(heldBillPaper.gameObject);
            heldBillPaper = null;
        }

        if (billHeldVisualInstance != null)
        {
            Destroy(billHeldVisualInstance);
            billHeldVisualInstance = null;
        }

        if (!MultiplayerDayBridge.IsActive) RestaurantTaskClaim.Complete(completedGroup);

        NotifyHandsChanged();
    }

    // Presentation/recovery must not destroy the paper or complete a gameplay claim.
    public void DetachBillPaper(BillPaper paper)
    {
        if (paper == null || (heldBillPaper != paper && holdingBillFor != paper.TargetGroup)) return;
        heldBillPaper = null;
        holdingBillFor = null;
        if (billHeldVisualInstance != null) Destroy(billHeldVisualInstance);
        billHeldVisualInstance = null;
        NotifyHandsChanged();
    }

    public void PresentBillPaper(BillPaper paper)
    {
        if (paper == null || (heldBillPaper == paper && paper.transform.IsChildOf(BillHoldPoint))) return;
        if (heldBillPaper != null && heldBillPaper != paper) DetachBillPaper(heldBillPaper);
        paper.GetComponentInParent<WaiterHands>(true)?.DetachBillPaper(paper);
        holdingBillFor = paper.TargetGroup;
        heldBillPaper = paper;
        AttachKeepingWorldScale(paper.transform, BillHoldPoint, Vector3.zero, Quaternion.identity);
        SetAllColliders(paper.gameObject, false);
        RefreshBillHeldVisual();
        NotifyHandsChanged();
    }

    public void ClearTray()
    {
        if (holdingTray != null && holdingTray.NetworkCarryLocked) return;
        FoodTray completedTray = holdingTray;
        holdingTray = null;
        RestaurantTaskClaim.Complete(completedTray);
        NotifyHandsChanged();
    }

    public bool PickupTray(FoodTray tray)
    {
        if (tray == null) return false;
        if (HasTray) return false;

        Transform parent = TrayHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[WaiterHands] TrayHoldPoint is NULL.");
            return false;
        }

        holdingTray = tray;

        AttachKeepingWorldScale(
            tray.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        SetAllColliders(tray.gameObject, false);

        NotifyHandsChanged();
        return true;
    }

    public void DisposeTray(bool destroyObject = true)
    {
        if (holdingTray != null && holdingTray.NetworkCarryLocked) return;
        var tray = holdingTray;
        holdingTray = null;

        RestaurantTaskClaim.Complete(tray);

        if (destroyObject && tray != null)
            Destroy(tray.gameObject);

        NotifyHandsChanged();
    }

    public void PickupBill(CustomerGroup group)
    {
        if (group == null) return;
        if (HasBill) return;

        holdingBillFor = group;
        RefreshBillHeldVisual();
        NotifyHandsChanged();
    }

    public void PickupBillPaper(BillPaper paper)
    {
        if (paper == null)
        {
            Debug.LogWarning("[WaiterHands] PickupBillPaper: paper null");
            return;
        }

        if (heldBillPaper != null)
        {
            Debug.LogWarning("[WaiterHands] PickupBillPaper: already holding bill paper");
            return;
        }

        Transform parent = BillHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[WaiterHands] BillHoldPoint is NULL.");
            return;
        }

        holdingBillFor = paper.TargetGroup;
        heldBillPaper = paper;

        Debug.Log($"[WaiterHands] Picking bill #{paper.orderNumber}. Parent={parent.name} (path: {GetPath(parent)})");

        AttachKeepingWorldScale(
            paper.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        SetAllColliders(paper.gameObject, false);

        Debug.Log($"[WaiterHands] Bill now child of hand? {paper.transform.IsChildOf(parent)} worldPos={paper.transform.position}");

        RefreshBillHeldVisual();
        if (GetComponent<ManagerPlayer>() != null && holdingBillFor != null)
            holdingBillFor.SetBillTaskClaimedByStaff(false);
        NotifyHandsChanged();
    }

    private string GetPath(Transform t)
    {
        if (t == null) return "null";

        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    private void RefreshBillHeldVisual()
    {
        if (billHeldVisualInstance != null)
        {
            Destroy(billHeldVisualInstance);
            billHeldVisualInstance = null;
        }

        if (billHeldVisualPrefab == null) return;

        Transform parent = BillHoldPoint;
        billHeldVisualInstance = Instantiate(billHeldVisualPrefab);
        AttachKeepingWorldScale(
            billHeldVisualInstance.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        SetAllColliders(billHeldVisualInstance, false);
    }

    public bool TryDeliverTrayTo(CustomerGroup group, bool destroyTrayObject = true)
    {
        if (holdingTray != null && holdingTray.NetworkCarryLocked) return false;
        if (group == null || holdingTray == null)
            return false;

        if (group.state != CustomerGroup.GroupState.OrderTaken ||
            !group.HasConfirmedOrder || group.IsPlayerReviewingOrder)
            return false;

        if (!holdingTray.Matches(group))
        {
            if (!MultiplayerServiceActions.IsActive || MultiplayerSessionManager.Instance.LocalManager == gameObject)
                WarningSlideUI.Instance?.Show($"This order is for table {holdingTray.orderNumber}.");
            return false;
        }

        if (group.assignedBooth != null)
            group.assignedBooth.ClearMenuBook();

        var deliveredTray = holdingTray;
        holdingTray = null;

        RestaurantTaskClaim.Complete(deliveredTray);

        if (destroyTrayObject && deliveredTray != null)
            Destroy(deliveredTray.gameObject);

        NotifyHandsChanged();
        return true;
    }

    public void PickupMoney(MoneyPickup money)
    {
        if (money == null || HasMoney || !money.IsAvailableForCollection) return;
        PresentMoneyPickup(money);
    }

    // Replication only attaches the existing payment. It cannot claim, collect,
    // settle, or dispatch an interaction for either the local or a remote actor.
    public void PresentMoneyPickup(MoneyPickup money)
    {
        if (money == null || money.TargetGroup == null || money.Amount <= 0) return;
        Transform parent = MoneyHoldPoint;
        if (parent == null) return;
        if (heldMoney == money && money.transform.IsChildOf(parent)) return;
        var previousHolder = money.GetComponentInParent<WaiterHands>(true);
        if (previousHolder != null && previousHolder != this) previousHolder.DetachMoneyPickup(money);
        if (heldMoney != null && heldMoney != money)
        {
            var previous = heldMoney;
            DetachMoneyPickup(previous);
            previous.PresentAtPickup();
        }

        holdingMoneyFor = money.TargetGroup;
        holdingMoneyAmount = money.Amount;
        heldMoney = money;

        AttachKeepingWorldScale(
            money.transform,
            parent,
            Vector3.zero,
            Quaternion.identity);
        SetAllColliders(money.gameObject, false);

        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }

        if (moneyHeldVisualPrefab != null)
        {
            moneyHeldVisualInstance = Instantiate(moneyHeldVisualPrefab);
            AttachKeepingWorldScale(
                moneyHeldVisualInstance.transform,
                parent,
                Vector3.zero,
                Quaternion.identity);
            SetAllColliders(moneyHeldVisualInstance, false);
        }

        money.PresentPickedUp(true);

        NotifyHandsChanged();
    }

    public void DetachMoneyPickup(MoneyPickup money, bool reparent = true)
    {
        if (money == null || heldMoney != money) return;
        heldMoney = null;
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;
        if (reparent && money.transform.IsChildOf(transform)) money.transform.SetParent(null, true);
        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }
        NotifyHandsChanged();
    }

    public bool ReleaseTrayForRetry(Vector3 worldPosition)
    {
        FoodTray tray = holdingTray;
        if (tray == null)
            return false;

        holdingTray = null;
        tray.transform.SetParent(null, true);
        tray.transform.position = worldPosition;
        SetAllColliders(tray.gameObject, true);
        tray.GetComponent<FoodTrayInteractable>()?.RestoreAfterStaffPickup();
        NotifyHandsChanged();
        return true;
    }

    public void ClearMoney()
    {
        MoneyPickup completedMoney = heldMoney;
        bool hadObject = completedMoney != null;
        if (completedMoney != null)
        {
            DetachMoneyPickup(completedMoney);
            completedMoney.gameObject.SetActive(false);
            Destroy(completedMoney.gameObject);
        }
        // Legacy payments can set only the group and amount, and an external
        // despawn can invalidate heldMoney before settlement reaches this actor.
        heldMoney = null;
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;
        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }
        if (!MultiplayerServiceActions.IsActive || MultiplayerSessionManager.Instance.IsAuthority)
            RestaurantTaskClaim.Complete(completedMoney);
        if (!hadObject) NotifyHandsChanged();
    }
}
