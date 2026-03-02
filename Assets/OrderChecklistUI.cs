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
    [Tooltip("This button will act as SEND TO CASHIER (it will: Take Order -> Send to Cashier -> Close).")]
    public Button confirmButton;
    public Button exitButton;

    [Header("Cashier (REQUIRED for Send)")]
    [Tooltip("Drag your CashierBoothInteractable from the scene here.")]
    public CashierBoothInteractable cashier;

    [Header("Optional: block clicks behind notepad")]
    public GameObject inputBlockerPanel;

    [Header("Optional: hide other UI while open")]
    public GameObject[] uiToHideWhileOpen;

    private CustomerGroup currentGroup;

    private void Awake()
    {
        // Safe singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Avoid stacking listeners if this object persists across scene loads or gets re-enabled
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmAndSendToCashier);
            confirmButton.onClick.AddListener(ConfirmAndSendToCashier);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
            exitButton.onClick.AddListener(Close);
        }

        if (inputBlockerPanel != null) inputBlockerPanel.SetActive(false);

        // Start hidden
        gameObject.SetActive(false);
    }

    public void Open(CustomerGroup group)
    {
        if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
        {
            Debug.Log("[OrderChecklistUI] Can't take orders while holding a tray.");
            return;
        }

        if (group == null) return;

        currentGroup = group;
        currentGroup.SetOrderPause(true);
        
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

    // CONFIRM BUTTON BEHAVIOR:
    // 1) Validate 1 food + 1 drink
    // 2) TakeOrderFromWaiter() (assigns order number, table number, etc.)
    // 3) Send to cashier (works on mobile because it's a UI button)
    // 4) Close notepad
    public void ConfirmAndSendToCashier()
    {
        if (currentGroup == null) { Close(); return; }

        // Must pick exactly 1 food + 1 drink
        int foodCount = (chickenToggle != null && chickenToggle.isOn ? 1 : 0)
                      + (friesToggle != null && friesToggle.isOn ? 1 : 0)
                      + (burgerToggle != null && burgerToggle.isOn ? 1 : 0);

        int drinkCount = (cokeToggle != null && cokeToggle.isOn ? 1 : 0)
                       + (pineappleToggle != null && pineappleToggle.isOn ? 1 : 0)
                       + (iceTeaToggle != null && iceTeaToggle.isOn ? 1 : 0);

        if (foodCount != 1 || drinkCount != 1)
        {
            Debug.Log("Pick exactly 1 food and 1 drink before confirming.");
            return;
        }

        // 1) Take the order (your existing logic)
        currentGroup.TakeOrderFromWaiter();

        // 2) Send to cashier immediately (no tapping required)
        TrySendToCashier(currentGroup);

        // 3) Close notepad
        Close();
    }

    private void TrySendToCashier(CustomerGroup group)
    {
        if (group == null) return;

        if (cashier == null)
            cashier = FindFirstObjectByType<CashierBoothInteractable>();

        if (cashier == null)
        {
            Debug.LogWarning("[OrderChecklistUI] No CashierBoothInteractable found.");
            return;
        }

        cashier.ProcessTicket(group);
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
        if (pineappleToggle != null) pineappleToggle.isOn = value;
        if (iceTeaToggle != null) iceTeaToggle.isOn = value;
    }

    private void SetTogglesInteractable(bool interactable)
    {
        if (chickenToggle != null) chickenToggle.interactable = interactable;
        if (friesToggle != null) friesToggle.interactable = interactable;
        if (burgerToggle != null) burgerToggle.interactable = interactable;

        if (cokeToggle != null) cokeToggle.interactable = interactable;
        if (pineappleToggle != null) pineappleToggle.interactable = interactable;
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