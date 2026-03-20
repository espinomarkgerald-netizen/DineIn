using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CashierRegisterUI : MonoBehaviour
{
    public static CashierRegisterUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Current Order")]
    [SerializeField] private TMP_Text tableNumberText;
    [SerializeField] private Image foodImage;
    [SerializeField] private TMP_Text foodPriceText;
    [SerializeField] private Image drinkImage;
    [SerializeField] private TMP_Text drinkPriceText;

    [Header("Totals")]
    [SerializeField] private TMP_Text receivedText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text changeText;

    [Header("Change Pad")]
    [SerializeField] private TMP_Text cashierChangeText;
    [SerializeField] private Button undoButton;

    [Header("Peso Buttons - Bills")]
    [SerializeField] private Button bill1000Button;
    [SerializeField] private Button bill500Button;
    [SerializeField] private Button bill200Button;
    [SerializeField] private Button bill100Button;
    [SerializeField] private Button bill50Button;

    [Header("Peso Buttons - Coins")]
    [SerializeField] private Button coin20Button;
    [SerializeField] private Button coin10Button;
    [SerializeField] private Button coin5Button;
    [SerializeField] private Button coin1Button;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;

    [Header("Food Sprites")]
    [SerializeField] private Sprite chickenSprite;
    [SerializeField] private Sprite friesSprite;
    [SerializeField] private Sprite burgerSprite;

    [Header("Drink Sprites")]
    [SerializeField] private Sprite cokeSprite;
    [SerializeField] private Sprite pineappleSprite;
    [SerializeField] private Sprite icedTeaSprite;

    [Header("Input Colors")]
    [SerializeField] private Color normalInputColor = Color.white;
    [SerializeField] private Color wrongInputColor = Color.red;
    [SerializeField] private Color correctInputColor = Color.green;

    private int receivedAmount;
    private int totalAmount;
    private int expectedChange;
    private int inputChangeAmount;

    private CustomerGroup activeGroup;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (root == null)
            root = gameObject;

        BindButtons();
        ResetDisplay();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BindButtons()
    {
        BindMoneyButton(bill1000Button, 1000);
        BindMoneyButton(bill500Button, 500);
        BindMoneyButton(bill200Button, 200);
        BindMoneyButton(bill100Button, 100);
        BindMoneyButton(bill50Button, 50);

        BindMoneyButton(coin20Button, 20);
        BindMoneyButton(coin10Button, 10);
        BindMoneyButton(coin5Button, 5);
        BindMoneyButton(coin1Button, 1);

        if (undoButton != null)
        {
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(UndoLastInput);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }
    }

    private void BindMoneyButton(Button button, int value)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => AddChangeInput(value));
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        isOpen = true;
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        isOpen = false;
    }

    public void OpenForPayment(CustomerGroup group, int received, int total)
    {
        activeGroup = group;
        receivedAmount = Mathf.Max(0, received);
        totalAmount = Mathf.Max(0, total);
        expectedChange = Mathf.Max(0, receivedAmount - totalAmount);
        inputChangeAmount = 0;

        RefreshOrderDisplay();
        RefreshTotalsDisplay();
        RefreshInputDisplay();
        Show();
    }

    public void CloseRegister()
    {
        activeGroup = null;
        receivedAmount = 0;
        totalAmount = 0;
        expectedChange = 0;
        inputChangeAmount = 0;

        ResetDisplay();
        Hide();
    }

    private void AddChangeInput(int value)
    {
        if (!isOpen) return;
        inputChangeAmount += value;
        RefreshInputDisplay();
    }

    private void UndoLastInput()
    {
        if (!isOpen) return;

        inputChangeAmount = 0;
        RefreshInputDisplay();
    }

    private void Confirm()
    {
        if (!isOpen) return;
        if (inputChangeAmount != expectedChange) return;

        var paidGroup = activeGroup;

        var hands = WaiterHands.Instance;
        if (hands != null)
            hands.ClearMoney();

        if (paidGroup != null)
        {
            GameDayManager.Instance?.RegisterPaymentCompleted();
            paidGroup.PayAndLeave();
        }

        CloseRegister();
    }

    private void RefreshOrderDisplay()
    {
        if (activeGroup == null)
        {
            SetText(tableNumberText, "-");
            SetOrderImage(foodImage, null);
            SetOrderImage(drinkImage, null);
            SetText(foodPriceText, "0.00");
            SetText(drinkPriceText, "0.00");
            return;
        }

        SetText(tableNumberText, activeGroup.currentOrderNumber.ToString());

        int foodPrice = GetFoodPrice(activeGroup.confirmedFood);
        int drinkPrice = GetDrinkPrice(activeGroup.confirmedDrink);

        SetOrderImage(foodImage, GetFoodSprite(activeGroup.confirmedFood));
        SetOrderImage(drinkImage, GetDrinkSprite(activeGroup.confirmedDrink));

        SetText(foodPriceText, FormatMoney(foodPrice));
        SetText(drinkPriceText, FormatMoney(drinkPrice));
    }

    private void RefreshTotalsDisplay()
    {
        SetText(receivedText, FormatMoney(receivedAmount));
        SetText(totalText, FormatMoney(totalAmount));
        SetText(changeText, FormatMoney(expectedChange));
    }

    private void RefreshInputDisplay()
    {
        SetText(cashierChangeText, FormatMoney(inputChangeAmount));

        if (cashierChangeText == null) return;

        if (inputChangeAmount == 0)
            cashierChangeText.color = normalInputColor;
        else if (inputChangeAmount == expectedChange)
            cashierChangeText.color = correctInputColor;
        else
            cashierChangeText.color = wrongInputColor;
    }

    private void ResetDisplay()
    {
        SetText(tableNumberText, "-");
        SetOrderImage(foodImage, null);
        SetOrderImage(drinkImage, null);

        SetText(foodPriceText, "0.00");
        SetText(drinkPriceText, "0.00");

        SetText(receivedText, "0.00");
        SetText(totalText, "0.00");
        SetText(changeText, "0.00");
        SetText(cashierChangeText, "0.00");

        if (cashierChangeText != null)
            cashierChangeText.color = normalInputColor;
    }

    private void SetText(TMP_Text textComp, string value)
    {
        if (textComp != null)
            textComp.text = value;
    }

    private void SetOrderImage(Image img, Sprite sprite)
    {
        if (img == null) return;

        img.sprite = sprite;
        img.enabled = sprite != null;
    }

    private string FormatMoney(int value)
    {
        return value.ToString("0.00");
    }

    private int GetFoodPrice(CustomerGroup.FoodType food)
    {
        switch (food)
        {
            case CustomerGroup.FoodType.Chicken: return 99;
            case CustomerGroup.FoodType.Fries: return 79;
            case CustomerGroup.FoodType.Burger: return 79;
            default: return 0;
        }
    }

    private int GetDrinkPrice(CustomerGroup.DrinkType drink)
    {
        switch (drink)
        {
            case CustomerGroup.DrinkType.Coke: return 39;
            case CustomerGroup.DrinkType.Pineapple: return 39;
            case CustomerGroup.DrinkType.IceTea: return 39;
            default: return 0;
        }
    }

    private Sprite GetFoodSprite(CustomerGroup.FoodType food)
    {
        switch (food)
        {
            case CustomerGroup.FoodType.Chicken: return chickenSprite;
            case CustomerGroup.FoodType.Fries: return friesSprite;
            case CustomerGroup.FoodType.Burger: return burgerSprite;
            default: return null;
        }
    }

    private Sprite GetDrinkSprite(CustomerGroup.DrinkType drink)
    {
        switch (drink)
        {
            case CustomerGroup.DrinkType.Coke: return cokeSprite;
            case CustomerGroup.DrinkType.Pineapple: return pineappleSprite;
            case CustomerGroup.DrinkType.IceTea: return icedTeaSprite;
            default: return null;
        }
    }
}