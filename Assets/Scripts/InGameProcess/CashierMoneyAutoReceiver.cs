using UnityEngine;

public class CashierMoneyAutoReceiver : MonoBehaviour
{
    [SerializeField] private CashierBoothInteractable cashier;
    [SerializeField] private float detectRadius = 1.25f;
    [SerializeField] private bool useXZOnly = true;
    [SerializeField] private float cooldown = 0.2f;

    private float nextAllowedTime;

    private void Awake()
    {
        if (cashier == null)
            cashier = GetComponent<CashierBoothInteractable>();
    }

    private void Update()
    {
        if (Time.time < nextAllowedTime) return;
        if (cashier == null) return;

        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter)) return;

        var player = RoleManager.Instance.GetActivePlayerMovement();
        if (player == null) return;

        var hands = WaiterHands.Instance;
        if (hands == null) return;
        if (!hands.HasMoney) return;

        // Prevent the money auto-receiver from accidentally triggering ticket submission
        // when the waiter is somehow still carrying both.
        if (hands.HasTicket) return;

        Vector3 playerPos = player.transform.position;
        Vector3 standPos = cashier.StandPoint != null ? cashier.StandPoint.position : cashier.transform.position;

        if (useXZOnly)
        {
            playerPos.y = 0f;
            standPos.y = 0f;
        }

        if (Vector3.Distance(playerPos, standPos) > detectRadius) return;

        nextAllowedTime = Time.time + cooldown;
        cashier.Interact(player);
    }
}