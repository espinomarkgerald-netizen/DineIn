using UnityEngine;

/// <summary>Authored, independent Lobby2 counter or self-service kiosk.</summary>
[DisallowMultipleComponent]
public sealed class FastFoodServiceStation : MonoBehaviour, IInteractable
{
    public enum StationKind { Counter, Kiosk }
    [SerializeField] private StationKind kind;
    [SerializeField] private TakeoutQueueManager queue;
    [SerializeField] private TakeoutFlowManager flow;
    [SerializeField] private Transform staffApproach;
    [SerializeField, Min(1f)] private float kioskOrderSeconds = 2f;
    [SerializeField, Min(.1f)] private float cashierOrderSeconds = 2f;
    [SerializeField, Min(.1f)] private float cashierPaymentSeconds = 2f;
    [Tooltip("Companion positions while the representative uses this station; authored in scene space.")]
    [SerializeField] private Transform[] companionPoints;
    public float CashierOrderSeconds => cashierOrderSeconds;
    public float CashierPaymentSeconds => cashierPaymentSeconds;
    public Transform[] CompanionPoints => companionPoints;
    public float ServiceProgress => timedCustomer != null ? Mathf.Clamp01(elapsed / kioskOrderSeconds) : 0f;
    private CustomerGroup timedCustomer;
    private float elapsed;
    public bool IsKiosk => kind == StationKind.Kiosk;
    public TakeoutQueueManager Queue => queue;
    public TakeoutFlowManager Flow => flow;
    public Transform StaffApproach => staffApproach;
    public Transform StandPoint => queue != null ? queue.OrderPoint : transform;
    public bool AutoReturnHome => false;
    public float GetInteractRadius() => 1.5f;
    public bool CanInteract() => IsKiosk && gameObject.scene.name == "Lobby2" &&
        HygieneManager.HandsEmpty(RoleManager.Instance?.GetActivePlayerMovement());
    public void Interact(PlayerMovement mover)
    {
        if (CanInteract()) WarningSlideUI.Instance?.Show("Customers order and pay automatically here.");
    }

    private void Update()
    {
        if (!IsKiosk || gameObject.scene.name != "Lobby2" || MultiplayerDayBridge.IsActive) return;
        var group = queue != null ? queue.CurrentFront : null;
        if (group != timedCustomer) { timedCustomer = group; elapsed = 0f; }
        if (group == null || group.FastFood == null || group.FastFoodPaid ||
            group.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint) return;
        if (group.state == CustomerGroup.GroupState.ReadyToOrder && !group.IsPlayerReviewingOrder)
        {
            elapsed += Time.deltaTime;
            if (elapsed < kioskOrderSeconds) return;
            // The existing stock reservation and confirmed-order operation runs once.
            if (!group.TakeOrderFromWaiter(group.chosenFood, group.chosenDrink, null)) return;
        }
        if (group.FastFood.CanSettle(group))
            CashierRegisterUI.ResolveInstance()?.CompleteAutomatedPayment(group);
    }
}
