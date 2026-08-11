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

    [Header("Failure Recovery")]
    [SerializeField, Min(5f)] private float waitingForOrderTimeoutSeconds = 30f;
    [SerializeField, Min(5f)] private float waitingForPaymentTimeoutSeconds = 20f;
    [SerializeField, Min(5f)] private float waitingForKitchenTimeoutSeconds = 45f;
    [SerializeField, Min(5f)] private float waitingForBagDeliveryTimeoutSeconds = 30f;

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
    private bool kitchenSubmitSent;
    private bool automatedService;
    private float phaseStartedAt = -1f;

    public CustomerGroup ActiveGroup => activeGroup;
    public TakeoutPhase CurrentPhase => currentPhase;

    public void SetAutomatedService(bool enabled)
    {
        automatedService = enabled;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (kitchenManager == null)
            kitchenManager = FindFirstObjectByType<KitchenManager>();

        if (kitchenManager != null)
            kitchenManager.OrderFinished += HandleKitchenOrderFinished;
    }

    private void OnDestroy()
    {
        if (kitchenManager != null)
            kitchenManager.OrderFinished -= HandleKitchenOrderFinished;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        SyncFrontCustomer();
        UpdatePhaseTimeout();
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
            kitchenSubmitSent = false;
            SetPhase(TakeoutPhase.WaitingForFront);
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

        if (currentPhase != TakeoutPhase.WaitingForFront && currentPhase != TakeoutPhase.None)
            return;

        orderFlowStarted = true;
        SetPhase(TakeoutPhase.WaitingForOrder);

        onFrontReady?.Invoke();
        activeGroup.BeginTakeoutOrderFlow(startOrderDelay);
    }

    public void NotifyOrderTaken(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        if (currentPhase != TakeoutPhase.WaitingForOrder)
        {
            Debug.LogWarning($"[TakeoutFlow] Ignored NotifyOrderTaken for {group.name} because phase is {currentPhase}.");
            return;
        }

        SetPhase(TakeoutPhase.WaitingForPayment);
        onPaymentRequested?.Invoke();

        if (!automatedService)
            OpenCashierForTakeout(group);

        Debug.Log(automatedService
            ? $"[TakeoutFlow] Order taken for {group.name}. Waiting for automated payment."
            : $"[TakeoutFlow] Order taken for {group.name}. Opening cashier for payment.");
    }

    private void OpenCashierForTakeout(CustomerGroup group)
    {
        CashierRegisterUI ui = CashierRegisterUI.Instance;
        if (ui == null)
        {
            Debug.LogWarning("[TakeoutFlow] CashierRegisterUI.Instance is null — cannot open payment.");
            return;
        }

        int total = 0;
        int groupSize = Mathf.Max(1, group.Size);

        if (OrderChecklistUI.Instance != null)
            total = OrderChecklistUI.Instance.GetOrderTotalFromContents(group.GetCurrentOrderContents()) * groupSize;
        else if (group.currentOrder != null)
            total = group.currentOrder.unitPrice * Mathf.Max(1, group.currentOrder.quantity);

        int received = GetPaymentDenomination(total);

        ui.OpenForPayment(group, received, total);
    }

    private static int GetPaymentDenomination(int total)
    {
        int[] denominations = { 1, 5, 10, 20, 50, 100, 200, 500, 1000 };

        for (int i = 0; i < denominations.Length; i++)
        {
            if (denominations[i] >= total)
                return denominations[i];
        }

        return Mathf.CeilToInt(total / 1000f) * 1000;
    }

    public void NotifyPaymentCompleted(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        if (currentPhase != TakeoutPhase.WaitingForPayment)
        {
            Debug.LogWarning($"[TakeoutFlow] Ignored NotifyPaymentCompleted for {group.name} because phase is {currentPhase}.");
            return;
        }

        SetPhase(TakeoutPhase.WaitingForKitchen);
        onPaymentCompleted?.Invoke();

        if (!autoSendToKitchenAfterPayment)
            return;

        if (kitchenSubmitSent)
        {
            Debug.LogWarning($"[TakeoutFlow] Kitchen submit already sent for {group.name}. Duplicate payment completion ignored.");
            return;
        }

        if (kitchenManager != null)
        {
            if (!kitchenManager.ProcessOrder(group))
            {
                FailActiveTakeout("Kitchen rejected the order before cooking started.");
                return;
            }

            kitchenSubmitSent = true;
            GameDayManager.Instance?.RegisterOrderProcessed();
            Debug.Log($"[TakeoutFlow] Payment completed for {group.name}. Sent to kitchen.");
        }
        else
        {
            Debug.LogError("[TakeoutFlow] kitchenManager is NOT assigned on TakeoutFlowManager — assign it in the Inspector! Bag will never spawn.");
            FailActiveTakeout("Kitchen manager is missing.");
        }
    }

    public bool NotifyBagReady(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return false;

        if (currentPhase != TakeoutPhase.WaitingForKitchen)
        {
            Debug.LogWarning($"[TakeoutFlow] Ignored NotifyBagReady for {group.name} because phase is {currentPhase}.");
            return false;
        }

        SetPhase(TakeoutPhase.WaitingForBagDelivery);
        onBagDeliveryRequested?.Invoke();

        Debug.Log($"[TakeoutFlow] Bag ready for {group.name}. Waiting for delivery.");
        return true;
    }

    private void HandleKitchenOrderFinished(CustomerGroup group, int _, bool succeeded)
    {
        if (succeeded || currentPhase != TakeoutPhase.WaitingForKitchen || !IsActiveFront(group))
            return;

        FailActiveTakeout("Kitchen could not create a deliverable takeout bag.");
    }

    private void FailActiveTakeout(string reason)
    {
        CustomerGroup failedGroup = activeGroup;
        if (failedGroup == null)
        {
            ClearRuntime();
            return;
        }

        Debug.LogError($"[TakeoutFlow] {reason} Releasing {failedGroup.name} so the queue can continue.", this);
        failedGroup.FailTakeoutService(reason);

        if (activeGroup == failedGroup)
            ClearRuntime();
    }

    private void UpdatePhaseTimeout()
    {
        if (activeGroup == null || phaseStartedAt < 0f)
            return;

        float timeoutSeconds = currentPhase switch
        {
            TakeoutPhase.WaitingForOrder => waitingForOrderTimeoutSeconds,
            TakeoutPhase.WaitingForPayment => waitingForPaymentTimeoutSeconds,
            TakeoutPhase.WaitingForKitchen => waitingForKitchenTimeoutSeconds,
            TakeoutPhase.WaitingForBagDelivery => waitingForBagDeliveryTimeoutSeconds,
            _ => 0f
        };

        if (timeoutSeconds <= 0f || Time.time - phaseStartedAt < timeoutSeconds)
            return;

        FailActiveTakeout(
            $"Timed out in phase {currentPhase} after {timeoutSeconds:0.#} seconds.");
    }

    private void SetPhase(TakeoutPhase phase)
    {
        currentPhase = phase;
        phaseStartedAt = phase == TakeoutPhase.None ? -1f : Time.time;

        if (activeGroup != null && phase != TakeoutPhase.None)
            Debug.Log($"[TakeoutFlow] {activeGroup.name} entered phase {phase}.", activeGroup);
    }

    public void NotifyBagDelivered(CustomerGroup group)
    {
        if (!IsActiveFront(group))
            return;

        if (currentPhase != TakeoutPhase.WaitingForBagDelivery)
        {
            Debug.LogWarning($"[TakeoutFlow] Ignored NotifyBagDelivered for {group.name} because phase is {currentPhase}.");
            return;
        }

        if (queueManager != null)
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
        SetPhase(TakeoutPhase.None);
        orderFlowStarted = false;
        kitchenSubmitSent = false;
    }
}
