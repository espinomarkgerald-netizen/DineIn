using UnityEngine;
using UnityEngine.Events;

public class TakeoutFlowManager : MonoBehaviour
{
    public static TakeoutFlowManager Instance { get; private set; }

    public enum TakeoutPhase
    {
        None,
        WaitingForFront,
        WaitingForOrder,
        WaitingForPayment,
        WaitingForKitchen,
        WaitingForBagDelivery
    }

    [Header("References")]
    [SerializeField] private TakeoutQueueManager queueManager;
    [SerializeField] private KitchenManager kitchenManager;

    [Header("Timing")]
    [SerializeField] private float startOrderDelay = 0.15f;

    [Header("Flow")]
    [SerializeField] private bool autoStartOrderWhenFrontArrives = true;
    [SerializeField] private bool autoSendToKitchenAfterPayment = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onFrontReady;
    [SerializeField] private UnityEvent onPaymentRequested;
    [SerializeField] private UnityEvent onPaymentCompleted;
    [SerializeField] private UnityEvent onBagDeliveryRequested;
    [SerializeField] private UnityEvent onTakeoutCompleted;

    private CustomerGroup activeGroup;
    private TakeoutPhase currentPhase = TakeoutPhase.None;
    private bool orderFlowStarted;

    public CustomerGroup ActiveGroup => activeGroup;
    public TakeoutPhase CurrentPhase => currentPhase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        SyncFrontCustomer();
    }

    private void SyncFrontCustomer()
    {
        if (queueManager == null)
            return;

        CustomerGroup front = queueManager.CurrentFront;

        if (front == null)
        {
            ClearRuntime();
            return;
        }

        if (activeGroup != front)
        {
            activeGroup = front;
            orderFlowStarted = false;
            currentPhase = TakeoutPhase.WaitingForFront;
        }

        if (activeGroup.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint)
            return;

        if (autoStartOrderWhenFrontArrives && !orderFlowStarted)
            StartFrontOrderFlow();
    }

    public void StartFrontOrderFlow()
    {
        if (activeGroup == null)
            return;

        if (activeGroup.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint)
            return;

        if (orderFlowStarted)
            return;

        orderFlowStarted = true;
        currentPhase = TakeoutPhase.WaitingForOrder;

        onFrontReady?.Invoke();
        activeGroup.BeginTakeoutOrderFlow(startOrderDelay);
    }

    public void NotifyOrderTaken(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        currentPhase = TakeoutPhase.WaitingForPayment;
        onPaymentRequested?.Invoke();

        OpenCashierForTakeout(group);

        Debug.Log($"[TakeoutFlow] Order taken for {group.name}. Opening cashier for payment.");
    }

    /// <summary>
    /// Opens the cashier register directly for takeout payment, bypassing the
    /// dine-in bill-paper → booth → ticket path entirely.
    /// received is the smallest denomination that covers the total, matching
    /// the same logic CustomerGroup.GetCustomerPaymentAmount uses for dine-in.
    /// </summary>
    private void OpenCashierForTakeout(CustomerGroup group)
    {
        CashierRegisterUI ui = CashierRegisterUI.Instance;
        if (ui == null)
        {
            Debug.LogWarning("[TakeoutFlow] CashierRegisterUI.Instance is null — cannot open payment.");
            return;
        }

        int total = 0;

        if (OrderChecklistUI.Instance != null)
            total = OrderChecklistUI.Instance.GetOrderTotalFromContents(group.GetCurrentOrderContents());
        else if (group.currentOrder != null)
            total = group.currentOrder.unitPrice * Mathf.Max(1, group.currentOrder.quantity);

        int received = GetPaymentDenomination(total);

        ui.OpenForPayment(group, received, total);
    }

    /// <summary>
    /// Returns the smallest standard denomination that covers the total.
    /// Mirrors CustomerGroup.GetCustomerPaymentAmount to keep payment amounts consistent.
    /// </summary>
    private static int GetPaymentDenomination(int total)
    {
        int[] denominations = { 1, 5, 10, 20, 50, 100, 200, 500, 1000 };

        for (int i = 0; i < denominations.Length; i++)
        {
            if (denominations[i] >= total)
                return denominations[i];
        }

        // Total exceeds 1000 — round up to the nearest 1000.
        return Mathf.CeilToInt(total / 1000f) * 1000;
    }

    public void NotifyPaymentCompleted(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        currentPhase = TakeoutPhase.WaitingForKitchen;
        onPaymentCompleted?.Invoke();

        if (autoSendToKitchenAfterPayment)
        {
            if (kitchenManager != null)
            {
                kitchenManager.ProcessOrder(group);
                Debug.Log($"[TakeoutFlow] Payment completed for {group.name}. Sent to kitchen.");
            }
            else
            {
                Debug.LogError("[TakeoutFlow] kitchenManager is NOT assigned on TakeoutFlowManager — assign it in the Inspector! Bag will never spawn.");
            }
        }
    }

    public void NotifyBagReady(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        currentPhase = TakeoutPhase.WaitingForBagDelivery;
        onBagDeliveryRequested?.Invoke();

        Debug.Log($"[TakeoutFlow] Bag ready for {group.name}. Waiting for delivery.");
    }

    public void NotifyBagDelivered(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        queueManager.ReleaseFrontFromOrderPoint();
        onTakeoutCompleted?.Invoke();

        Debug.Log($"[TakeoutFlow] Takeout completed for {group.name}. Releasing customer.");

        ClearRuntime();
    }

    [ContextMenu("Debug Complete Payment")]
    public void DebugCompletePayment()
    {
        if (activeGroup == null)
            return;

        if (currentPhase != TakeoutPhase.WaitingForPayment)
            return;

        NotifyPaymentCompleted(activeGroup);
    }

    [ContextMenu("Debug Mark Bag Ready")]
    public void DebugMarkBagReady()
    {
        if (activeGroup == null)
            return;

        if (currentPhase != TakeoutPhase.WaitingForKitchen)
            return;

        NotifyBagReady(activeGroup);
    }

    /// <summary>
    /// Forces the flow manager to release its reference to the given group and
    /// reset to idle. Called when a takeout group times out and leaves without
    /// completing the normal bag-delivery path.
    /// Only clears state when the group matches the currently tracked active group
    /// so that an unrelated group timing out cannot corrupt an ongoing flow.
    /// </summary>
    public void ForceRelease(CustomerGroup group)
    {
        if (group == null || group != activeGroup)
            return;

        Debug.Log($"[TakeoutFlow] ForceRelease called for {group.name}. Clearing active flow.");
        ClearRuntime();
    }

    private bool IsActiveFront(CustomerGroup group)
    {
        if (group == null || queueManager == null)
            return false;

        return group == activeGroup && group == queueManager.CurrentFront;
    }

    private void ClearRuntime()
    {
        activeGroup = null;
        currentPhase = TakeoutPhase.None;
        orderFlowStarted = false;
    }
}