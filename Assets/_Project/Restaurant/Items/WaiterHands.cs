using System;
using UnityEngine;

public class WaiterHands : MonoBehaviour
{
    public static WaiterHands Instance { get; private set; }

    public static WaiterHands ActivePlayerHands
    {
        get
        {
            if (ManagerPlayer.Active != null)
            {
                WaiterHands managerHands = ManagerPlayer.Active.GetComponent<WaiterHands>();
                if (managerHands != null)
                    return managerHands;
            }

            return Instance;
        }
    }

    public static event Action OnHandsStateChanged;

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

    public Transform MoneyHoldPoint => moneyHoldPoint != null ? moneyHoldPoint : transform;
    public Transform TrayHoldPoint => trayHoldPoint != null ? trayHoldPoint : transform;
    public Transform BillHoldPoint => billHoldPoint != null ? billHoldPoint : transform;
    public Transform TrolleyGripPoint => trolleyGripPoint != null ? trolleyGripPoint : TrayHoldPoint;

    private void Awake()
    {
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
        OnHandsStateChanged?.Invoke();
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

        RestaurantTaskClaim.Complete(completedGroup);

        NotifyHandsChanged();
    }

    public void ClearTray()
    {
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
        if (group == null || holdingTray == null)
            return false;

        if (group.state != CustomerGroup.GroupState.OrderTaken ||
            !group.HasConfirmedOrder || group.IsPlayerReviewingOrder)
            return false;

        if (!holdingTray.Matches(group))
        {
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
        if (money == null) return;
        if (HasMoney) return;
        if (!money.IsAvailableForCollection) return;

        var tg = money.TargetGroup;
        var amt = money.Amount;

        if (tg == null) return;
        if (amt <= 0) return;

        Transform parent = MoneyHoldPoint;
        if (parent == null)
        {
            Debug.LogError("[WaiterHands] MoneyHoldPoint is NULL.");
            return;
        }

        holdingMoneyFor = tg;
        holdingMoneyAmount = amt;
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

        // Both manager-controlled and autonomous waiters use this method. The
        // pickup owns the one-time transition that disables its collider and
        // removes the world-space bubble.
        money.NotifyPickedUp();

        NotifyHandsChanged();
    }

    public void ClearMoney()
    {
        MoneyPickup completedMoney = heldMoney;
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;

        if (heldMoney != null)
        {
            Destroy(heldMoney.gameObject);
            heldMoney = null;
        }

        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }

        RestaurantTaskClaim.Complete(completedMoney);

        NotifyHandsChanged();
    }
}
