using UnityEngine;

public class CashierMoneyAutoReceiver : MonoBehaviour
{
    [SerializeField] private CashierBoothInteractable cashier;
    [SerializeField] private float detectRadius = 1.25f;
    [SerializeField] private bool useXZOnly = true;
    [SerializeField] private float cooldown = 0.2f;

    private PlayerMovement player;
    private float nextAllowedTime;

    private void Awake()
    {
        if (cashier == null)
            cashier = GetComponent<CashierBoothInteractable>();

        player = FindFirstObjectByType<PlayerMovement>();
    }

    private void Update()
    {
        if (Time.time < nextAllowedTime) return;
        if (cashier == null) return;
        if (player == null) return;

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasMoney) return;

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