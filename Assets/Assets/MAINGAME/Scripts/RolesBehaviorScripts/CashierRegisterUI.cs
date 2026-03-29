using System.Collections.Generic;
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

    [Header("Food")]
    [SerializeField] private Image foodImage;
    [SerializeField] private Image foodImage2;
    [SerializeField] private TMP_Text foodPriceText;

    [Header("Drink")]
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

    [SerializeField] private TutorialHintTextUI tutorialHint;

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

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnCashierOpened(activeGroup, expectedChange);

        if (TutorialManager.Instance != null && tutorialHint != null)
            tutorialHint.Show($"Give the exact change: {expectedChange:0.00}");
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
            int amountEarned = totalAmount;

            if (DailyFinanceBridge.Instance != null)
            {
                DailyFinanceBridge.Instance.AddEarnings(amountEarned);
                if (GameDayManager.Instance != null)
                GameDayManager.Instance.RefreshRevenueUI();
                
                Debug.Log("[Finance] Earned ₱" + amountEarned +
                          " | Total = ₱" + DailyFinanceBridge.Instance.EarnedToday);
            }

            GameDayManager.Instance?.RegisterPaymentCompleted();
            paidGroup.PayAndLeave();
        }

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnCashierConfirmed(paidGroup);

        CloseRegister();
    }

    private void RefreshOrderDisplay()
    {
        if (activeGroup == null || activeGroup.currentOrder == null)
        {
            SetText(tableNumberText, "-");
            SetFoodDisplay(null, null, 0);
            SetDrinkDisplay(null, 0);
            return;
        }

        SetText(tableNumberText, activeGroup.currentOrderNumber.ToString());

        List<string> contents = activeGroup.GetCurrentOrderContents();

        string firstFood = null;
        string secondFood = null;
        string drink = null;

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];

            if (IsDrink(item))
            {
                if (string.IsNullOrEmpty(drink))
                    drink = item;
            }
            else
            {
                if (string.IsNullOrEmpty(firstFood))
                    firstFood = item;
                else if (string.IsNullOrEmpty(secondFood))
                    secondFood = item;
            }
        }

        int foodPrice = 0;
        int drinkPrice = 0;

        if (OrderChecklistUI.Instance != null)
        {
            foodPrice = OrderChecklistUI.Instance.GetFoodTotalFromContents(contents);
            drinkPrice = OrderChecklistUI.Instance.GetDrinkTotalFromContents(contents);
        }
        else
        {
            foodPrice = GetFallbackFoodTotal(contents);
            drinkPrice = GetFallbackDrinkTotal(contents);
        }

        SetFoodDisplay(GetItemSprite(firstFood), GetItemSprite(secondFood), foodPrice);
        SetDrinkDisplay(GetItemSprite(drink), drinkPrice);
    }

    private int GetFallbackFoodTotal(List<string> contents)
    {
        if (contents == null)
            return 0;

        List<string> foods = new List<string>();

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];
            if (item == "Chicken" || item == "Fries" || item == "Burger")
                foods.Add(item);
        }

        if (foods.Count == 2)
        {
            bool hasChicken = foods.Contains("Chicken");
            bool hasFries = foods.Contains("Fries");
            bool hasBurger = foods.Contains("Burger");

            if (hasChicken && hasFries)
                return 349;

            if (hasChicken && hasBurger)
                return 399;

            if (hasBurger && hasFries)
                return 179;
        }

        int total = 0;

        for (int i = 0; i < foods.Count; i++)
        {
            switch (foods[i])
            {
                case "Chicken":
                    total += 299;
                    break;

                case "Fries":
                    total += 79;
                    break;

                case "Burger":
                    total += 119;
                    break;
            }
        }

        return total;
    }

    private int GetFallbackDrinkTotal(List<string> contents)
    {
        if (contents == null)
            return 0;

        int total = 0;

        for (int i = 0; i < contents.Count; i++)
        {
            switch (contents[i])
            {
                case "Coke":
                    total += 50;
                    break;

                case "Pineapple":
                    total += 50;
                    break;

                case "Ice Tea":
                    total += 50;
                    break;
            }
        }

        return total;
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
        SetFoodDisplay(null, null, 0);
        SetDrinkDisplay(null, 0);

        SetText(receivedText, "0.00");
        SetText(totalText, "0.00");
        SetText(changeText, "0.00");
        SetText(cashierChangeText, "0.00");

        if (cashierChangeText != null)
            cashierChangeText.color = normalInputColor;
    }

    private void SetFoodDisplay(Sprite firstSprite, Sprite secondSprite, int sharedPrice)
    {
        if (foodImage != null)
        {
            foodImage.sprite = firstSprite;
            foodImage.enabled = firstSprite != null;
        }

        if (foodImage2 != null)
        {
            foodImage2.sprite = secondSprite;
            foodImage2.enabled = secondSprite != null;
        }

        if (foodPriceText != null)
        {
            if (firstSprite == null)
                foodPriceText.text = "";
            else
                foodPriceText.text = FormatMoney(sharedPrice);
        }
    }

    private void SetDrinkDisplay(Sprite sprite, int price)
    {
        if (drinkImage != null)
        {
            drinkImage.sprite = sprite;
            drinkImage.enabled = sprite != null;
        }

        if (drinkPriceText != null)
        {
            if (sprite == null)
                drinkPriceText.text = "";
            else
                drinkPriceText.text = FormatMoney(price);
        }
    }

    private void SetText(TMP_Text textComp, string value)
    {
        if (textComp != null)
            textComp.text = value;
    }

    private string FormatMoney(int value)
    {
        return value.ToString("0.00");
    }

    private bool IsDrink(string itemName)
    {
        return itemName == "Coke" || itemName == "Pineapple" || itemName == "Ice Tea";
    }

    private Sprite GetItemSprite(string itemName)
    {
        switch (itemName)
        {
            case "Chicken": return chickenSprite;
            case "Fries": return friesSprite;
            case "Burger": return burgerSprite;
            case "Coke": return cokeSprite;
            case "Pineapple": return pineappleSprite;
            case "Ice Tea": return icedTeaSprite;
            default: return null;
        }
    }
}