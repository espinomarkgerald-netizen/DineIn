using UnityEngine;

public class PaymentPickupInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Booth booth;
    [SerializeField] private Transform standPointOverride;

    private CustomerGroup group;
    private int amount;
    private bool paymentReady;

    public Transform StandPoint
    {
        get
        {
            if (standPointOverride != null) return standPointOverride;
            if (booth != null && booth.approachPoint != null) return booth.approachPoint;
            return transform;
        }
    }

    public bool AutoReturnHome => true;

    private void Awake()
    {
        if (booth == null) booth = GetComponent<Booth>();
    }

    public void SetPayment(CustomerGroup g, int amt)
    {
        group = g;
        amount = amt;
        paymentReady = (group != null && amount > 0);
    }

    public void ClearPayment()
    {
        group = null;
        amount = 0;
        paymentReady = false;
    }

    public bool CanInteract()
    {
        if (!paymentReady) return false;
        if (WaiterHands.Instance == null) return false;
        if (WaiterHands.Instance.HasMoney) return false;
        return true;
    }

    public void Interact(PlayerMovement player)
    {
        if (!CanInteract()) return;

        WaiterHands.Instance.holdingMoneyFor = group;
        WaiterHands.Instance.holdingMoneyAmount = amount;

        // optional: if you have a money held visual system, call your PickupMoney() method instead
        // WaiterHands.Instance.PickupMoneyVirtual(group, amount);

        paymentReady = false; // picked up
    }

    public float GetInteractRadius()
    {
        return 1.1f;
    }

    public CustomerGroup CurrentGroup => group;
    public int CurrentAmount => amount;
    public bool PaymentReady => paymentReady;
}