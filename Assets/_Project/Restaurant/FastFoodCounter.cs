using UnityEngine;

/// <summary>Scene-authored cashier interaction, including reopening a cancelled payment.</summary>
public sealed class FastFoodCounter : MonoBehaviour, IInteractable
{
    [SerializeField] private FastFoodRestaurant restaurant;
    [SerializeField] private Transform standPoint;
    [SerializeField] private FastFoodServiceStation station;
    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;
    public float GetInteractRadius() => 1.5f;
    public bool CanInteract()
    {
        var group = station?.Flow?.ActiveGroup;
        var hands = WaiterHands.ActivePlayerHands;
        return restaurant != null && group != null &&
            (restaurant.CanSettle(group) || group.state == CustomerGroup.GroupState.ReadyToOrder &&
                group.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.AtOrderPoint) && hands != null
            && !RestaurantTaskClaim.IsClaimedByBot(group)
            && !hands.HasTray && !hands.HasBill && !hands.HasMoney && !hands.HasTicket;
    }
    public void Interact(PlayerMovement mover)
    {
        if (!CanInteract()) return;
        var group = station.Flow.ActiveGroup;
        if (restaurant.CanSettle(group)) { restaurant.OpenCounterPayment(group); return; }
        var review = OrderChecklistUI.Instance;
        if (review == null || !RestaurantTaskClaim.TryClaimPlayer(group)) return;
        if (!group.BeginPlayerOrderReview()) { RestaurantTaskClaim.ReleasePlayer(group); return; }
        group.SetOrderTaskClaimedByStaff(true);
        review.Open(group);
    }
}
