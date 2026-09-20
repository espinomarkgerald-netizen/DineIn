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
    [SerializeField, Min(1f)] private float kioskOrderSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float kioskPayHereChance = .7f;
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
        if (CanInteract()) WarningSlideUI.Instance?.Show("Customers can order and pay here, or pay at a counter.");
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
            if (Random.value >= kioskPayHereChance)
            {
                group.FastFood.TransferToCounter(group);
                return;
            }
        }
        if (group.FastFood.CanSettle(group))
            CashierRegisterUI.ResolveInstance()?.CompleteAutomatedPayment(group);
    }
}
