using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrayPickupUIButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text tableNumberText;
    private FoodTrayInteractable tray;

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

    public void SetTray(FoodTrayInteractable t)
    {
        tray = t;
    }

    /// <summary>Sets the table number displayed on the pickup button. Pass -1 to hide it.</summary>
    public void SetTableNumber(int number)
    {
        if (tableNumberText == null) return;
        tableNumberText.text = number >= 0 ? $"#{number}" : string.Empty;
    }

    private void OnClick()
    {
        if (tray != null)
            tray.UI_RequestPickup();
    }
}