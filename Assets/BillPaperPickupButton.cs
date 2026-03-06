using UnityEngine;
using UnityEngine.UI;

public class BillPaperPickupButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BillPaper bill;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Click);
        }
    }

    public void SetBill(BillPaper b)
    {
        bill = b;
    }

    private void Click()
    {
        if (bill == null) return;

        var hands = WaiterHands.Instance;
        if (hands != null && hands.HasBill) return;

        bill.UI_Pickup();
    }
}