using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pickup button used by the takeout bag UI. Mirrors TrayPickupUIButton but
/// targets TakeoutBagInteractable instead of FoodTrayInteractable.
/// </summary>
public class BagPickupUIButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text orderNumberText;

    private TakeoutBagInteractable bag;

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

    /// <summary>Sets the bag this button picks up when clicked.</summary>
    public void SetBag(TakeoutBagInteractable target)
    {
        bag = target;
    }

    /// <summary>Sets the label shown on the button, e.g. order number.</summary>
    public void SetOrderNumber(int number)
    {
        if (orderNumberText == null)
            return;

        orderNumberText.text = number >= 0 ? $"#{number}" : string.Empty;
    }

    private void OnClick()
    {
        if (bag == null)
            return;

        bag.TryPickup();
    }
}
