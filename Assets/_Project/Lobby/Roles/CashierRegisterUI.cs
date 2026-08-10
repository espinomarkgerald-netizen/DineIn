using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CashierRegisterUI : MonoBehaviour
{
    public static CashierRegisterUI Instance { get; private set; }

    public static event System.Action OnHidden;

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
    private bool buttonsBound;

    // Tracks whether this register session ended with a successful Confirm().
    // Set to true by Confirm(), reset to false by OpenForPayment() and CloseRegister().
    // Used to detect abandoned sessions for cash error tracking.
    private bool sessionConfirmed;

    public bool IsOpen
    {
        get
        {
            ResolveRoot();

            if (root == null)
                return false;

            return isOpen && root.activeInHierarchy;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveRoot();
        BindButtons();
        ResetDisplay();
        HideImmediate();
    }

    private void LateUpdate()
    {
        ResolveRoot();

        if (root == null || !isOpen)
            return;

        // If the root went inactive while the register is open, something external
        // disabled it without going through Hide(). Log it and reset state cleanly.
        if (!root.activeInHierarchy)
        {
            Debug.LogError($"[CashierRegisterUI] Root '{root.name}' was deactivated externally while open. Closing register.", this);
            ForceClosedState(true);
        }
    }

    private void OnDisable()
    {
        // Fired when this GameObject itself is disabled externally while the register is open.
        // Log the full stack trace so the caller can be identified and fixed.
        if (isOpen)
        {
            Debug.LogError(
                $"[CashierRegisterUI] OnDisable — this GameObject was disabled while isOpen=true! Caller:\n{new System.Diagnostics.StackTrace(true)}",
                this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveRoot()
    {
        if (root == null || root == gameObject)
        {
            Transform pos = transform.Find("POS");
            if (pos != null)
            {
                root = pos.gameObject;
                return;
            }

            if (transform.childCount > 0)
            {
                root = transform.GetChild(0).gameObject;
                return;
            }

            root = gameObject;
        }
    }

    private void BindButtons()
    {
        if (buttonsBound)
            return;

        buttonsBound = true;

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
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => AddChangeInput(value));
    }

    public void Show()
    {
        ResolveRoot();

        if (root != null)
        {
            root.SetActive(true);

            SetParentsActive(root.transform);
            SetHierarchyActive(root.transform);

            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 1f;
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }
        }

        if (transform.parent != null)
            transform.SetAsLastSibling();

        isOpen = true;
        Debug.Log($"[CashierRegisterUI] Show() called — root={root?.name ?? "NULL"} isOpen={isOpen}", this);
    }

    public void Hide()
    {
        ResolveRoot();

        if (root != null)
        {
            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 0f;
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
            }

            root.SetActive(false);
        }

        ForceClosedState(true);

        Debug.Log($"[CashierRegisterUI] Hide() called — root={root?.name ?? "NULL"} caller={new System.Diagnostics.StackTrace().ToString().Split('\n')[1].Trim()}", this);
    }

    private void HideImmediate()
    {
        ResolveRoot();

        if (root != null)
        {
            CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                    continue;

                groups[i].alpha = 0f;
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
            }

            root.SetActive(false);
        }

        ForceClosedState(false);
    }

    private void ForceClosedState(bool invokeEvent)
    {
        bool wasOpen = isOpen;
        isOpen = false;

        if (invokeEvent && wasOpen)
            OnHidden?.Invoke();
    }

    private void SetParentsActive(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private void SetHierarchyActive(Transform target)
    {
        if (target == null)
            return;

        target.gameObject.SetActive(true);

        for (int i = 0; i < target.childCount; i++)
            SetHierarchyActive(target.GetChild(i));
    }

    public void OpenForPayment(CustomerGroup group, int received, int total)
    {
        activeGroup = group;
        receivedAmount = Mathf.Max(0, received);
        totalAmount = Mathf.Max(0, total);
        expectedChange = Mathf.Max(0, receivedAmount - totalAmount);
        inputChangeAmount = 0;
        sessionConfirmed = false;

        Show();

        // Refresh AFTER Show() so any OnEnable or tutorial callbacks
        // that run during Show() cannot overwrite the display values.
        RefreshOrderDisplay();
        RefreshTotalsDisplay();
        RefreshInputDisplay();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnCashierOpened(activeGroup, expectedChange);

        if (TutorialManager.Instance != null && tutorialHint != null)
            tutorialHint.Show($"Give the exact change: {expectedChange:0.00}");
    }

    public void CloseRegister()
    {
        // If this session was opened (a group was present) but never successfully confirmed,
        // it counts as a cash-handling error — the waiter abandoned the transaction.
        if (activeGroup != null && !sessionConfirmed)
            GameDayManager.Instance?.RegisterCashError();

        activeGroup = null;
        receivedAmount = 0;
        totalAmount = 0;
        expectedChange = 0;
        inputChangeAmount = 0;
        sessionConfirmed = false;

        ResetDisplay();
        Hide();
    }

    private void AddChangeInput(int value)
    {
        if (!IsOpen)
            return;

        inputChangeAmount += value;
        RefreshInputDisplay();
    }

    private void UndoLastInput()
    {
        if (!IsOpen)
            return;

        inputChangeAmount = 0;
        RefreshInputDisplay();
    }

    private void Confirm()
    {
        if (!IsOpen)
            return;

        if (inputChangeAmount != expectedChange)
            return;

        // Mark session as completed before CloseRegister() so the abandonment check is skipped.
        sessionConfirmed = true;
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

            if (paidGroup.IsTakeout)
            {
                TakeoutFlowManager.Instance?.NotifyPaymentCompleted(paidGroup);
            }
            else
            {
                paidGroup.PayAndLeave();
            }

            OrderFlowManager.Instance?.ShowPayment(amountEarned, paidGroup.currentOrderNumber);
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
        Debug.Log($"[CashierRegisterUI] RefreshTotalsDisplay — received={receivedAmount} total={totalAmount} change={expectedChange}", this);
        SetText(receivedText, FormatMoney(receivedAmount));
        SetText(totalText, FormatMoney(totalAmount));
        SetText(changeText, FormatMoney(expectedChange));
    }

    private void RefreshInputDisplay()
    {
        SetText(cashierChangeText, FormatMoney(inputChangeAmount));

        if (cashierChangeText == null)
            return;

        if (inputChangeAmount == 0)
            cashierChangeText.color = normalInputColor;
        else if (inputChangeAmount == expectedChange)
            cashierChangeText.color = correctInputColor;
        else
            cashierChangeText.color = wrongInputColor;
    }

    private void ResetDisplay()
    {
        Debug.Log($"[CashierRegisterUI] ResetDisplay called", this);
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
        return value.ToString("N2");
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