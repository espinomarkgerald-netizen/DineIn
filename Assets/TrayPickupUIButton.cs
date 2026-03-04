using UnityEngine;
using UnityEngine.UI;

public class TrayPickupUIButton : MonoBehaviour
{
    [SerializeField] private Button button;
    private FoodTrayInteractable tray;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    public void SetTray(FoodTrayInteractable t)
    {
        tray = t;
    }

    private void OnClick()
    {
        if (tray != null)
            tray.UI_RequestPickup();
    }
}