using UnityEngine;

public class FoodTrayInteractable : MonoBehaviour, IInteractable
{
    [Header("Refs")]
    [SerializeField] private FoodTray tray;
    [SerializeField] private Transform pickupPoint;

    [Header("Hold")]
    [SerializeField] private bool disableColliderWhileHeld = true;

    private Collider cachedCol;

    public Transform StandPoint => pickupPoint != null ? pickupPoint : transform;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        if (tray == null) tray = GetComponent<FoodTray>();
        cachedCol = GetComponentInChildren<Collider>(true);
    }

    public bool CanInteract()
    {
        if (tray == null) return false;
        if (WaiterHands.Instance == null) return false;
        return !WaiterHands.Instance.HasTray;
    }

    public void Interact(PlayerMovement mover)
    {
        if (!CanInteract()) return;

        var hands = WaiterHands.Instance;

        Transform hand = hands != null ? hands.TrayHoldPoint : null;
        if (hand == null)
        {
            Debug.LogWarning("[FoodTrayInteractable] TrayHoldPoint not assigned on WaiterHands.");
            return;
        }

        hands.holdingTray = tray;

        tray.transform.SetParent(hand, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;

        if (disableColliderWhileHeld && cachedCol != null)
            cachedCol.enabled = false;

        Debug.Log($"[Tray] Picked up tray #{tray.orderNumber}");
    }
}