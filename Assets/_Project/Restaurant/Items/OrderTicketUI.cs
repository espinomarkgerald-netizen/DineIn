using UnityEngine;
using TMPro;

public class OrderTicketUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text detailsText;

    private CustomerGroup group;
    private WaiterHands owner;

    public void Init(CustomerGroup g, WaiterHands ticketOwner)
    {
        group = g;
        owner = ticketOwner;

        UIFollowWorldPoint follow = GetComponent<UIFollowWorldPoint>();
        if (follow != null)
            follow.enabled = false;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        int num = g.currentOrderNumber;
        if (titleText != null) titleText.text = $"ORDER TICKET #{num}";
        if (detailsText != null) detailsText.text = $"{g.GetCurrentOrderSummary()}\nDeliver to cashier.";

        // keep it until delivered
    }

    private void Update()
    {
        // auto-destroy if ticket no longer held (delivered/cancelled)
        if (owner == null || owner != WaiterHands.ActivePlayerHands || owner.holdingTicketFor != group)
            Destroy(gameObject);
    }
}
