using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BillPaperPickupButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BillPaper bill;
    [SerializeField] private TMP_Text tableNumberText;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            button = GetComponentInChildren<Button>(true);

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    /// <summary>Assigns the bill paper this button controls and updates the table number label.</summary>
    public void SetBill(BillPaper b)
    {
        bill = b;
        SetTableNumber(b != null ? b.orderNumber : -1);
    }

    /// <summary>Sets the table number displayed on the pickup button. Pass -1 to hide it.</summary>
    public void SetTableNumber(int number)
    {
        if (tableNumberText == null) return;
        tableNumberText.text = number >= 0 ? $"#{number}" : string.Empty;
    }

    private void OnClick()
    {
        if (bill == null)
            return;

        bill.UI_Pickup();
    }
}