using UnityEngine;
using UnityEngine.UI;

public class BillPaperPickupButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BillPaper bill;

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

    public void SetBill(BillPaper b)
    {
        bill = b;
    }

    private void OnClick()
    {
        if (bill == null)
            return;

        bill.UI_Pickup();
    }
}