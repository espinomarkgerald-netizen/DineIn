using UnityEngine;

public class OrderFlowManager : MonoBehaviour
{
    public static OrderFlowManager Instance { get; private set; }

    [Header("UI Prefabs (under Canvas_Gameplay)")]
    public OrderTicketUI ticketUiPrefab;

    private Canvas gameplayCanvas;

    private void Awake()
    {
        Instance = this;
        gameplayCanvas = UIRoot.GameplayCanvasOrNull();
        if (gameplayCanvas == null)
            gameplayCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    // Called by CustomerGroup when order is confirmed and number assigned
    public void SpawnTicket(CustomerGroup group, WaiterHands owner)
    {
        if (group == null || owner == null) return;
        owner.holdingTicketFor = group;
        // Staff owns its ticket; a simulation callback never changes the local human's hands.
        if (owner != WaiterHands.ActivePlayerHands) return;
        if (ticketUiPrefab == null || gameplayCanvas == null)
        {
            Debug.LogError("[OrderFlowManager] Missing ticketUiPrefab or gameplayCanvas.");
            return;
        }

        // waiter "holds" the ticket
        // spawn ticket UI
        var ui = Instantiate(ticketUiPrefab, gameplayCanvas.transform);
        ui.Init(group, owner);
    }

}
