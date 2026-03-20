using System;
using UnityEngine;

public class WaiterHands : MonoBehaviour
{
    public static WaiterHands Instance { get; private set; }

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

    [Header("Held Visuals")]
    [SerializeField] private GameObject billHeldVisualPrefab;
    [SerializeField] private Transform moneyHoldPoint;
    [SerializeField] private GameObject moneyHeldVisualPrefab;

    private GameObject moneyHeldVisualInstance;
    private MoneyPickup heldMoneyPickup;

    private GameObject billHeldVisualInstance;
    private BillPaper heldBillPaper;

    public bool HasTicket => holdingTicketFor != null;
    public bool HasBill => holdingBillFor != null;
    public bool HasTray => holdingTray != null;
    public bool HasMoney => holdingMoneyFor != null && holdingMoneyAmount > 0;

    public Transform MoneyHoldPoint => moneyHoldPoint != null ? moneyHoldPoint : transform;
    public Transform TrayHoldPoint => trayHoldPoint != null ? trayHoldPoint : transform;
    public Transform BillHoldPoint => billHoldPoint != null ? billHoldPoint : transform;

    private void Awake()
    {
        Debug.Log($"[WaiterHands] Awake on {name} id={GetInstanceID()}");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        holdingTray = null;
        holdingTicketFor = null;
        holdingBillFor = null;
        heldBillPaper = null;
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;
        heldMoneyPickup = null;

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

        NotifyHandsChanged();
    }

    public void ClearTray()
    {
        holdingTray = null;
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

        tray.transform.SetParent(parent, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;

        var col = tray.GetComponentInChildren<Collider>(true);
        if (col != null) col.enabled = false;

        NotifyHandsChanged();
        return true;
    }

    public void DisposeTray(bool destroyObject = true)
    {
        var tray = holdingTray;
        holdingTray = null;

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

        paper.transform.SetParent(parent, false);
        paper.transform.localPosition = Vector3.zero;
        paper.transform.localRotation = Quaternion.identity;

        var col = paper.GetComponentInChildren<Collider>(true);
        if (col != null) col.enabled = false;

        Debug.Log($"[WaiterHands] Bill now child of hand? {paper.transform.IsChildOf(parent)} worldPos={paper.transform.position}");

        RefreshBillHeldVisual();
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
        billHeldVisualInstance = Instantiate(billHeldVisualPrefab, parent);
        billHeldVisualInstance.transform.localPosition = Vector3.zero;
        billHeldVisualInstance.transform.localRotation = Quaternion.identity;
    }

    public bool TryDeliverTrayTo(CustomerGroup group, bool destroyTrayObject = true)
    {
        if (group == null || holdingTray == null)
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

        if (destroyTrayObject && deliveredTray != null)
            Destroy(deliveredTray.gameObject);

        NotifyHandsChanged();
        return true;
    }

    public void PickupMoney(MoneyPickup money)
    {
        if (money == null) return;

        if (holdingMoneyFor != null && holdingMoneyAmount <= 0)
            ClearMoney();

        if (HasMoney) return;

        var tg = money.TargetGroup;
        var amt = money.Amount;

        if (tg == null) return;
        if (amt <= 0) return;

        holdingMoneyFor = tg;
        holdingMoneyAmount = amt;
        heldMoneyPickup = money;

        Transform parent = MoneyHoldPoint;
        money.transform.SetParent(parent, false);
        money.transform.localPosition = Vector3.zero;
        money.transform.localRotation = Quaternion.identity;

        var col = money.GetComponentInChildren<Collider>(true);
        if (col != null) col.enabled = false;

        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }

        if (moneyHeldVisualPrefab != null)
        {
            moneyHeldVisualInstance = Instantiate(moneyHeldVisualPrefab, parent);
            moneyHeldVisualInstance.transform.localPosition = Vector3.zero;
            moneyHeldVisualInstance.transform.localRotation = Quaternion.identity;
        }

        NotifyHandsChanged();
    }

    public void ClearMoney()
    {
        holdingMoneyFor = null;
        holdingMoneyAmount = 0;

        if (heldMoneyPickup != null)
        {
            Destroy(heldMoneyPickup.gameObject);
            heldMoneyPickup = null;
        }

        if (moneyHeldVisualInstance != null)
        {
            Destroy(moneyHeldVisualInstance);
            moneyHeldVisualInstance = null;
        }

        NotifyHandsChanged();
    }
}