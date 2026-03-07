using UnityEngine;
using TMPro;

public class OrderTicketUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text detailsText;

    private CustomerGroup group;

    public void Init(CustomerGroup g)
    {
        group = g;

        int num = g.currentOrderNumber;
        if (titleText != null) titleText.text = $"ORDER TICKET #{num}";
        if (detailsText != null) detailsText.text = $"{g.chosenFood} + {g.chosenDrink}\nDeliver to cashier.";

        // keep it until delivered
    }

    private void Update()
    {
        // auto-destroy if ticket no longer held (delivered/cancelled)
        if (WaiterHands.Instance == null) return;
        if (WaiterHands.Instance.holdingTicketFor != group)
            Destroy(gameObject);
    }
}