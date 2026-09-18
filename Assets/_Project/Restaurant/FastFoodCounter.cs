using UnityEngine;

/// <summary>Scene-authored cashier interaction, including reopening a cancelled payment.</summary>
public sealed class FastFoodCounter : MonoBehaviour, IInteractable
{
    [SerializeField] private FastFoodRestaurant restaurant;
    [SerializeField] private Transform standPoint;
    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;
    public float GetInteractRadius() => 1.5f;
    public bool CanInteract()
    {
        var group = TakeoutFlowManager.Instance?.ActiveGroup;
        var hands = WaiterHands.ActivePlayerHands;
        return restaurant != null && restaurant.CanSettle(group) && hands != null
            && !hands.HasTray && !hands.HasBill && !hands.HasMoney && !hands.HasTicket;
    }
    public void Interact(PlayerMovement mover)
    {
        if (CanInteract()) restaurant.OpenCounterPayment(TakeoutFlowManager.Instance.ActiveGroup);
    }
}
