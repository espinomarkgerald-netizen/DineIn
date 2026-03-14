using UnityEngine;

public class CashierBoothInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    public KitchenManager kitchen;

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

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    private void Awake()
    {
        if (kitchen == null)
            kitchen = FindFirstObjectByType<KitchenManager>();
    }

    private void Update()
    {
        TryAutoOpenRegister();
    }

    public bool CanInteract()
    {
        var hands = WaiterHands.Instance;
        if (hands == null) return false;

        if (hands.HasTicket) return true;
        if (hands.HasMoney) return true;
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

        if (hands.HasTicket)
        {
            var group = hands.holdingTicketFor;
            if (group == null) return;

            hands.ClearTicket();

            if (kitchen != null)
                kitchen.ProcessOrder(group);

            return;
        }

        if (hands.HasMoney)
        {
            OpenRegisterForHeldMoney(hands);
            return;
        }

        if (!hands.HasBill)
        {
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

    private void TryAutoOpenRegister()
    {
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return;

        var hands = WaiterHands.Instance;
        if (hands == null) return;
        if (!hands.HasMoney) return;

        if (CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen)
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

        if (dist > autoPayRadius) return;

        OpenRegisterForHeldMoney(hands);
    }

    private void OpenRegisterForHeldMoney(WaiterHands hands)
    {
        if (hands == null) return;
        if (!hands.HasMoney) return;
        if (CashierRegisterUI.Instance == null) return;

        var group = hands.holdingMoneyFor;
        int receivedAmount = hands.holdingMoneyAmount;
        int totalAmount = GetOrderTotal(group);

        Debug.Log($"[Cashier] Open register | received={receivedAmount} total={totalAmount} group={(group != null ? group.name : "NULL")}");

        CashierRegisterUI.Instance.OpenForPayment(group, receivedAmount, totalAmount);
    }

    private int GetOrderTotal(CustomerGroup group)
    {
        if (group == null) return 0;
        return GetFoodPrice(group.confirmedFood) + GetDrinkPrice(group.confirmedDrink);
    }

    private int GetFoodPrice(CustomerGroup.FoodType food)
    {
        switch (food)
        {
            case CustomerGroup.FoodType.Chicken: return 99;
            case CustomerGroup.FoodType.Fries: return 79;
            case CustomerGroup.FoodType.Burger: return 79;
            default: return 0;
        }
    }

    private int GetDrinkPrice(CustomerGroup.DrinkType drink)
    {
        switch (drink)
        {
            case CustomerGroup.DrinkType.Coke: return 39;
            case CustomerGroup.DrinkType.Pineapple: return 39;
            case CustomerGroup.DrinkType.IceTea: return 39;
            default: return 0;
        }
    }

    private Vector3 PickupCenter
    {
        get { return StandPoint.position; }
    }

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
}