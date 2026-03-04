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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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

        SetAll(false);
        SetTogglesInteractable(true);

        transform.SetAsLastSibling();

        if (inputBlockerPanel != null) inputBlockerPanel.SetActive(true);

        SetOtherUIHidden(true);

        gameObject.SetActive(true);
    }

    public void ConfirmAndSendToCashier()
    {
        if (currentGroup == null) { Close(); return; }

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

        if (!TryGetSelectedFood(out var food) || !TryGetSelectedDrink(out var drink))
        {
            Debug.Log("Missing food/drink selection.");
            return;
        }

        // IMPORTANT: CustomerGroup must have:
        // public void TakeOrderFromWaiter(FoodType food, DrinkType drink)
        currentGroup.TakeOrderFromWaiter(food, drink);

        TrySendToCashier(currentGroup);

        Close();
    }

    private bool TryGetSelectedFood(out CustomerGroup.FoodType food)
    {
        food = default;

        if (chickenToggle != null && chickenToggle.isOn) { food = CustomerGroup.FoodType.Chicken; return true; }
        if (friesToggle != null && friesToggle.isOn) { food = CustomerGroup.FoodType.Fries; return true; }
        if (burgerToggle != null && burgerToggle.isOn) { food = CustomerGroup.FoodType.Burger; return true; }

        return false;
    }

    private bool TryGetSelectedDrink(out CustomerGroup.DrinkType drink)
    {
        drink = default;

        if (cokeToggle != null && cokeToggle.isOn) { drink = CustomerGroup.DrinkType.Coke; return true; }
        if (pineappleToggle != null && pineappleToggle.isOn) { drink = CustomerGroup.DrinkType.Pineapple; return true; }
        if (iceTeaToggle != null && iceTeaToggle.isOn) { drink = CustomerGroup.DrinkType.IceTea; return true; }

        return false;
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