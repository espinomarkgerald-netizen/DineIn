using UnityEngine;
using UnityEngine.UI;

public class OrderChecklistUI : MonoBehaviour
{
    public static OrderChecklistUI Instance { get; private set; }

    [Header("Food Toggles")]
    public Toggle chickenToggle;
    public Toggle friesToggle;
    public Toggle burgerToggle;

    [Header("Drink Toggles")]
    public Toggle cokeToggle;
    public Toggle pineappleToggle;
    public Toggle iceTeaToggle;

    [Header("Buttons")]
    public Button confirmButton;
    public Button exitButton;

    [Header("Optional: block clicks behind notepad")]
    public GameObject inputBlockerPanel;

    [Header("Optional: hide other UI while open")]
    public GameObject[] uiToHideWhileOpen;

    private CustomerGroup currentGroup;

    private void Awake()
    {
        Instance = this;

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (exitButton != null) exitButton.onClick.AddListener(Close);

        if (inputBlockerPanel != null) inputBlockerPanel.SetActive(false);

        gameObject.SetActive(false);
    }

    public void Open(CustomerGroup group)
    {
        if (group == null) return;

        currentGroup = group;
        currentGroup.SetOrderPause(true);

        // waiter checks manually -> start blank
        SetAll(false);

        // ensure toggles are interactable
        SetTogglesInteractable(true);

        // bring to top
        transform.SetAsLastSibling();

        // block input behind
        if (inputBlockerPanel != null) inputBlockerPanel.SetActive(true);

        // hide other ui if set
        SetOtherUIHidden(true);

        gameObject.SetActive(true);
    }

    public void Confirm()
    {
        if (currentGroup == null) { Close(); return; }

        // Must pick exactly 1 food + 1 drink
        int foodCount = (chickenToggle.isOn ? 1 : 0) + (friesToggle.isOn ? 1 : 0) + (burgerToggle.isOn ? 1 : 0);
        int drinkCount = (cokeToggle.isOn ? 1 : 0) + (pineappleToggle.isOn ? 1 : 0) + (iceTeaToggle.isOn ? 1 : 0);

        if (foodCount != 1 || drinkCount != 1)
        {
            Debug.Log("Pick exactly 1 food and 1 drink before confirming.");
            return;
        }

        currentGroup.TakeOrderFromWaiter();
        Close();
    }

    public void Close()
    {
        if (currentGroup != null)
            currentGroup.SetOrderPause(false);

        currentGroup = null;

        if (inputBlockerPanel != null) inputBlockerPanel.SetActive(false);
        SetOtherUIHidden(false);

        gameObject.SetActive(false);
    }

    private void SetAll(bool value)
    {
        if (chickenToggle != null) chickenToggle.isOn = value;
        if (friesToggle != null) friesToggle.isOn = value;
        if (burgerToggle != null) burgerToggle.isOn = value;

        if (cokeToggle != null) cokeToggle.isOn = value;
        if (pineappleToggle != null) pineappleToggle.isOn = value; // ✅ fixed (was a common typo)
        if (iceTeaToggle != null) iceTeaToggle.isOn = value;
    }

    private void SetTogglesInteractable(bool interactable)
    {
        if (chickenToggle != null) chickenToggle.interactable = interactable;
        if (friesToggle != null) friesToggle.interactable = interactable;
        if (burgerToggle != null) burgerToggle.interactable = interactable;

        if (cokeToggle != null) cokeToggle.interactable = interactable;
        if (pineappleToggle != null) pineappleToggle.interactable = interactable; // ✅ fixed
        if (iceTeaToggle != null) iceTeaToggle.interactable = interactable;
    }

    private void SetOtherUIHidden(bool hide)
    {
        if (uiToHideWhileOpen == null) return;

        for (int i = 0; i < uiToHideWhileOpen.Length; i++)
        {
            if (uiToHideWhileOpen[i] != null)
                uiToHideWhileOpen[i].SetActive(!hide);
        }
    }
}