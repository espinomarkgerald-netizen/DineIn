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

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    private void Awake()
    {
        if (kitchen == null)
            kitchen = FindFirstObjectByType<KitchenManager>();
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
        var hands = WaiterHands.Instance;
        if (hands == null) return;

        // Ticket flow
        if (hands.HasTicket)
        {
            var group = hands.holdingTicketFor;
            if (group == null) return;

            hands.ClearTicket();

            if (kitchen != null)
                kitchen.ProcessOrder(group);

            return;
        }

        // Money flow
        if (hands.HasMoney)
        {
            var group = hands.holdingMoneyFor;
            int amount = hands.holdingMoneyAmount;

            Debug.Log($"[Cashier] Received payment {amount} for {(group != null ? group.name : "NULL")}");

            hands.ClearMoney();

            if (group != null)
                group.PayAndLeave();

            return;
        }

        // Bill pickup flow
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

    private Vector3 PickupCenter
    {
        get
        {
            return StandPoint.position;
        }
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
}