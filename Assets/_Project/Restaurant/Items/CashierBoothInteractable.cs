using UnityEngine;

public class CashierBoothInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    public KitchenManager kitchen;
    [SerializeField] private CashierRegisterUI registerUI;

    [Header("Stand Point")]
    [SerializeField] private Transform standPoint;

    [Header("Bill Pickup")]
    [SerializeField] private Transform billSearchRoot;
    [SerializeField] private float billPickupRadius = 2f;
    [SerializeField] private bool usePlanarDistance = true;

    [Header("Bill Settings")]
    public int saleAmountMin = 50;
    public int saleAmountMax = 150;

    [Header("Bill Behavior")]
    [SerializeField] private float findNeedsBillDistance = 8f;
    [SerializeField] private bool requestBillIfNonePrinted = true;
    [SerializeField] private bool preferBillForNearestNeedsBill = true;

    [Header("Auto Payment")]
    [SerializeField] private float autoPayRadius = 1.5f;
    [SerializeField] private bool debugAutoPay = true;

    private bool isOpeningRegister;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    private void Awake()
    {
        if (kitchen == null)
            kitchen = FindFirstObjectByType<KitchenManager>();

        if (registerUI == null)
            registerUI = FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        CashierRegisterUI.OnHidden += HandleRegisterHidden;
    }

    private void OnDisable()
    {
        CashierRegisterUI.OnHidden -= HandleRegisterHidden;
    }

    private void HandleRegisterHidden()
    {
        isOpeningRegister = false;
    }

    private void Update()
    {
        TryAutoOpenRegister();
    }

    public bool CanInteract()
    {
        var hands = WaiterHands.Instance;
        if (hands == null) return false;

        if (hands.HasMoney) return true;
        if (hands.HasTicket) return true;
        if (!hands.HasBill) return true;

        return false;
    }

    public void Interact(PlayerMovement player)
    {
        if (RoleManager.Instance == null) return;

        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
        {
            ShowWarning("Only the waiter can use this station.");
            return;
        }

        var hands = WaiterHands.Instance;
        if (hands == null) return;

        // Money must win over ticket here so payment auto-open never accidentally
        // re-submits a stale ticket when the waiter is carrying both.
        if (hands.HasMoney)
        {
            OpenRegisterForHeldMoney(hands);
            return;
        }

        if (TrySubmitHeldTicket(hands))
            return;

        if (!hands.HasBill)
        {
            var takeoutPhase = TakeoutFlowManager.Instance?.CurrentPhase;
            if (takeoutPhase == TakeoutFlowManager.TakeoutPhase.WaitingForKitchen ||
                takeoutPhase == TakeoutFlowManager.TakeoutPhase.WaitingForBagDelivery)
                return;

            if (TryPickupClosestBillPaper())
                return;

            var target = CustomerGroupFinder.FindClosestNeedsBill(transform.position, findNeedsBillDistance);
            if (target == null) return;

            if (requestBillIfNonePrinted && BillManager.Instance != null)
                BillManager.Instance.RequestBill(target);

            if (preferBillForNearestNeedsBill)
            {
                if (TryPickupBillForGroup(target))
                    return;
            }

            TryPickupClosestBillPaper();
        }
    }

    private bool TrySubmitHeldTicket(WaiterHands hands)
    {
        if (hands == null || !hands.HasTicket)
            return false;

        var group = hands.holdingTicketFor;

        if (group == null)
        {
            hands.ClearTicket();
            return true;
        }

        if (group.state != CustomerGroup.GroupState.OrderTaken)
        {
            Debug.LogWarning($"[Cashier] Ignored stale ticket for {group.name}. Current state: {group.state}");
            hands.ClearTicket();
            return true;
        }

        if (kitchen == null)
        {
            Debug.LogWarning("[Cashier] KitchenManager is missing.");
            return true;
        }

        hands.ClearTicket();
        kitchen.ProcessOrder(group);
        return true;
    }

    private void TryAutoOpenRegister()
    {
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

        var hands = WaiterHands.Instance;
        if (hands == null) return;

        if (!hands.HasMoney)
        {
            var ui = GetRegisterUI();
            if (ui == null || !ui.IsOpen)
                isOpeningRegister = false;
            return;
        }

        var uiRegister = GetRegisterUI();
        if (uiRegister == null)
        {
            if (debugAutoPay)
                Debug.LogWarning("[Cashier AutoOpen] CashierRegisterUI not found.");
            return;
        }

        if (uiRegister.IsOpen || isOpeningRegister)
            return;

        Vector3 a = mover.transform.position;
        Vector3 b = StandPoint.position;

        if (usePlanarDistance)
        {
            a.y = 0f;
            b.y = 0f;
        }

        float dist = Vector3.Distance(a, b);

        if (debugAutoPay)
            Debug.Log($"[Cashier AutoOpen] dist={dist:0.00} radius={autoPayRadius:0.00} hasMoney={hands.HasMoney} mover={mover.name}");

        if (dist > autoPayRadius)
            return;

        OpenRegisterForHeldMoney(hands);
    }

    private void OpenRegisterForHeldMoney(WaiterHands hands)
    {
        if (hands == null) return;
        if (!hands.HasMoney) return;

        var ui = GetRegisterUI();
        if (ui == null)
        {
            Debug.LogWarning("[Cashier] CashierRegisterUI is missing.");
            return;
        }

        if (ui.IsOpen)
        {
            Debug.Log($"[Cashier] OpenRegisterForHeldMoney SKIPPED — IsOpen={ui.IsOpen} isOpeningRegister={isOpeningRegister}");
            return;
        }

        if (isOpeningRegister)
            return;

        isOpeningRegister = true;

        var group = hands.holdingMoneyFor;
        int receivedAmount = hands.holdingMoneyAmount;
        int totalAmount = GetOrderTotal(group);

        Debug.Log($"[Cashier] Open register | received={receivedAmount} total={totalAmount} group={(group != null ? group.name : "NULL")}");
        ui.OpenForPayment(group, receivedAmount, totalAmount);
    }

    private CashierRegisterUI GetRegisterUI()
    {
        if (registerUI != null)
            return registerUI;

        if (CashierRegisterUI.Instance != null)
        {
            registerUI = CashierRegisterUI.Instance;
            return registerUI;
        }

        registerUI = FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);
        return registerUI;
    }

    private int GetOrderTotal(CustomerGroup group)
    {
        if (group == null)
            return 0;

        int groupSize = Mathf.Max(1, group.Size);

        if (OrderChecklistUI.Instance != null)
        {
            return OrderChecklistUI.Instance.GetOrderTotalFromContents(
                group.GetCurrentOrderContents()
            ) * groupSize;
        }

        if (group.currentOrder != null)
            return group.currentOrder.unitPrice * Mathf.Max(1, group.currentOrder.quantity);

        return 0;
    }

    private Vector3 PickupCenter => StandPoint.position;

    private float DistToPickupCenter(Vector3 billPos)
    {
        Vector3 a = PickupCenter;
        Vector3 b = billPos;

        if (usePlanarDistance)
        {
            a.y = 0f;
            b.y = 0f;
        }

        return Vector3.Distance(a, b);
    }

    private BillPaper[] GetAllBills()
    {
        if (billSearchRoot != null)
            return billSearchRoot.GetComponentsInChildren<BillPaper>(true);

        return FindObjectsByType<BillPaper>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private bool TryPickupClosestBillPaper()
    {
        var hands = WaiterHands.Instance;
        if (hands == null || hands.HasBill) return false;

        var bills = GetAllBills();
        if (bills == null || bills.Length == 0)
        {
            Debug.Log("[Cashier] No BillPaper found to pick up.");
            return false;
        }

        BillPaper best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < bills.Length; i++)
        {
            var bill = bills[i];
            if (bill == null) continue;
            if (!bill.gameObject.activeInHierarchy) continue;
            if (!bill.CanInteract()) continue;

            float dist = DistToPickupCenter(bill.transform.position);
            if (dist > billPickupRadius) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = bill;
            }
        }

        if (best == null)
        {
            Debug.Log($"[Cashier] No bill within radius {billPickupRadius} of pickup center.");
            return false;
        }

        bool ok = best.TryPickup();

        Debug.Log(ok
            ? $"[Cashier] Picked up bill (dist {bestDist:0.00})."
            : "[Cashier] Found bill but TryPickup failed.");

        return ok;
    }

    private bool TryPickupBillForGroup(CustomerGroup target)
    {
        var hands = WaiterHands.Instance;
        if (hands == null || hands.HasBill) return false;
        if (target == null) return false;

        var bills = GetAllBills();
        if (bills == null || bills.Length == 0) return false;

        BillPaper best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < bills.Length; i++)
        {
            var bill = bills[i];
            if (bill == null) continue;
            if (!bill.Matches(target)) continue;
            if (!bill.CanInteract()) continue;

            float dist = DistToPickupCenter(bill.transform.position);
            if (dist > billPickupRadius) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = bill;
            }
        }

        if (best == null) return false;

        return best.TryPickup();
    }

    public void ProcessTicket(CustomerGroup group)
    {
        if (group == null) return;
        if (kitchen == null) return;

        if (group.state != CustomerGroup.GroupState.OrderTaken)
        {
            Debug.LogWarning($"[Cashier] ProcessTicket ignored for {group.name}. Current state: {group.state}");
            return;
        }

        kitchen.ProcessOrder(group);
    }

    public int GenerateSaleAmount()
    {
        return Random.Range(saleAmountMin, saleAmountMax + 1);
    }

    private void ShowWarning(string message)
    {
        WarningSlideUI.Instance?.Show(message);
    }

    public float GetInteractRadius()
    {
        return 0.35f;
    }
}