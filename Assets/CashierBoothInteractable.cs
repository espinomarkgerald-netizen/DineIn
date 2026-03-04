using UnityEngine;

public class CashierBoothInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    public KitchenManager kitchen;

    [Header("Stand Point")]
    [SerializeField] private Transform standPoint;

    [Header("Bill Pickup")]
    [SerializeField] private Transform billSearchRoot; // optional: BillsRoot
    [SerializeField] private float billPickupRadius = 2f;
    [SerializeField] private bool usePlanarDistance = true; // ignores Y differences

    [Header("Bill Settings")]
    public int saleAmountMin = 50;
    public int saleAmountMax = 150;

    [Header("Bill Behavior")]
    [SerializeField] private float findNeedsBillDistance = 8f;
    [SerializeField] private bool requestBillIfNonePrinted = true;
    [SerializeField] private bool preferBillForNearestNeedsBill = true;

    public Transform StandPoint => standPoint;
    public bool AutoReturnHome => true;

    private void Awake()
    {
        if (kitchen == null) kitchen = FindFirstObjectByType<KitchenManager>();
    }

    public bool CanInteract() => true;

    public void Interact(PlayerMovement player)
    {
        if (WaiterHands.Instance == null) return;

        // Ticket flow
        if (WaiterHands.Instance.HasTicket)
        {
            var g = WaiterHands.Instance.holdingTicketFor;
            if (g == null) return;

            WaiterHands.Instance.ClearTicket();
            if (kitchen != null) kitchen.ProcessOrder(g);
            return;
        }

        // Money flow
        if (WaiterHands.Instance.HasMoney)
        {
            var g = WaiterHands.Instance.holdingMoneyFor;
            int amt = WaiterHands.Instance.holdingMoneyAmount;

            WaiterHands.Instance.ClearMoney();

            if (g != null)
                g.PayAndLeave();

            return;
        }

        // Bill pickup flow
        if (!WaiterHands.Instance.HasBill)
        {
            if (TryPickupClosestBillPaper())
                return;
        }

        var target = CustomerGroupFinder.FindClosestNeedsBill(transform.position, maxDistance: findNeedsBillDistance);
        if (target != null)
        {
            if (requestBillIfNonePrinted && BillManager.Instance != null)
                BillManager.Instance.RequestBill(target);

            if (!WaiterHands.Instance.HasBill)
            {
                if (preferBillForNearestNeedsBill)
                    TryPickupBillForGroup(target);
                else
                    TryPickupClosestBillPaper();
            }
        }
    }

    private Vector3 PickupCenter
    {
        get
        {
            var p = (standPoint != null) ? standPoint.position : transform.position;
            return p;
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
            var b = bills[i];
            if (b == null) continue;

            if (!b.gameObject.activeInHierarchy) continue;
            if (!b.CanInteract()) continue;

            float d = DistToPickupCenter(b.transform.position);
            if (d > billPickupRadius) continue;

            if (d < bestDist)
            {
                bestDist = d;
                best = b;
            }
        }

        if (best == null)
        {
            Debug.Log($"[Cashier] No bill within radius {billPickupRadius} of pickup center. (Check radius / BillsRoot / spawn positions)");
            return false;
        }

        bool ok = best.TryPickup();
        Debug.Log(ok
            ? $"[Cashier] Picked up bill (dist {bestDist:0.00})."
            : "[Cashier] Found bill but TryPickup() failed (targetGroup null or already holding bill).");

        return ok;
    }

    private bool TryPickupBillForGroup(CustomerGroup target)
    {
        if (target == null) return false;

        var bills = GetAllBills();
        if (bills == null || bills.Length == 0) return false;

        BillPaper best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < bills.Length; i++)
        {
            var b = bills[i];
            if (b == null) continue;
            if (!b.Matches(target)) continue;
            if (!b.CanInteract()) continue;

            float d = DistToPickupCenter(b.transform.position);
            if (d > billPickupRadius) continue;

            if (d < bestDist)
            {
                bestDist = d;
                best = b;
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

    public int GenerateSaleAmount() => Random.Range(saleAmountMin, saleAmountMax + 1);
}