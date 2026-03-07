using UnityEngine;

public class OrderFlowManager : MonoBehaviour
{
    public static OrderFlowManager Instance { get; private set; }

    [Header("UI Prefabs (under Canvas_Gameplay)")]
    public OrderTicketUI ticketUiPrefab;
    public PaymentPopupUI paymentPopupPrefab;

    private Canvas gameplayCanvas;

    private void Awake()
    {
        Instance = this;
        gameplayCanvas = UIRoot.GameplayCanvasOrNull();
        if (gameplayCanvas == null)
            gameplayCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    // Called by CustomerGroup when order is confirmed and number assigned
    public void SpawnTicket(CustomerGroup group)
    {
        if (group == null) return;
        if (ticketUiPrefab == null || gameplayCanvas == null)
        {
            Debug.LogError("[OrderFlowManager] Missing ticketUiPrefab or gameplayCanvas.");
            return;
        }

        // waiter "holds" the ticket
        WaiterHands.Instance.holdingTicketFor = group;

        // spawn ticket UI
        var ui = Instantiate(ticketUiPrefab, gameplayCanvas.transform);
        ui.Init(group);
    }

    public void ShowPayment(int amount, int orderNumber)
    {
        if (paymentPopupPrefab == null || gameplayCanvas == null) return;
        var ui = Instantiate(paymentPopupPrefab, gameplayCanvas.transform);
        ui.Show(amount, orderNumber);
    }
}