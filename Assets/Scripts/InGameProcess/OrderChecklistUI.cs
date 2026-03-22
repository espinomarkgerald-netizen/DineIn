using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderChecklistUI : MonoBehaviour
{
    public static OrderChecklistUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text tableNumberText;
    [SerializeField] private TMP_Text customerMessageText;

    [Header("Icons")]
    [SerializeField] private Image food1;
    [SerializeField] private Image food2;
    [SerializeField] private Image drink;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button exitButton;

    [Header("Food Toggles")]
    [SerializeField] private Toggle chickenToggle;
    [SerializeField] private Toggle friesToggle;
    [SerializeField] private Toggle burgerToggle;

    [Header("Drink Toggles")]
    [SerializeField] private Toggle cokeToggle;
    [SerializeField] private Toggle pineappleToggle;
    [SerializeField] private Toggle iceTeaToggle;

    [Header("Bundle Toggles")]
    [SerializeField] private Toggle chickenFriesToggle;
    [SerializeField] private Toggle chickenBurgerToggle;
    [SerializeField] private Toggle burgerFriesToggle;

    [Header("Sprites")]
    [SerializeField] private Sprite chickenSprite;
    [SerializeField] private Sprite friesSprite;
    [SerializeField] private Sprite burgerSprite;
    [SerializeField] private Sprite cokeSprite;
    [SerializeField] private Sprite pineappleSprite;
    [SerializeField] private Sprite iceTeaSprite;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typeSpeed = 0.02f;

    private CustomerGroup group;
    private Coroutine typingRoutine;

    // This stays as the CUSTOMER'S real generated order.
    private readonly List<string> requestedContents = new List<string>();

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
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(Close);
            exitButton.onClick.AddListener(Close);
        }

        BindToggleLogic();
        ResetToggles();

        gameObject.SetActive(false);
    }

    public void Open(CustomerGroup g)
    {
        if (g == null) return;

        group = g;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        group.SetOrderPause(true);

        ResetToggles();
        LoadRequestedOrder();
        RefreshRequestedOrderUI();
    }

    public void Close()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (group != null)
            group.SetOrderPause(false);

        group = null;
        requestedContents.Clear();

        gameObject.SetActive(false);
    }

    private void LoadRequestedOrder()
    {
        requestedContents.Clear();

        if (group != null && group.currentOrder != null && group.currentOrder.contents != null)
            requestedContents.AddRange(group.currentOrder.contents);
    }

    private void RefreshRequestedOrderUI()
    {
        RefreshTableText();
        RefreshMessageFromRequestedOrder();
        RefreshIconsFromRequestedOrder();
    }

    private void RefreshTableText()
    {
        if (tableNumberText == null) return;

        if (group == null)
        {
            tableNumberText.text = "Table -";
            return;
        }

        int num = group.currentOrderNumber;
        tableNumberText.text = num > 0 ? $"Table {num}" : "Table -";
    }

    private void RefreshMessageFromRequestedOrder()
    {
        if (customerMessageText == null) return;

        string sentence = GenerateSentence(requestedContents);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (useTypewriter && gameObject.activeInHierarchy)
            typingRoutine = StartCoroutine(TypeSentence(sentence));
        else
            customerMessageText.text = sentence;
    }

    private IEnumerator TypeSentence(string sentence)
    {
        customerMessageText.text = string.Empty;

        if (string.IsNullOrEmpty(sentence))
            yield break;

        for (int i = 0; i < sentence.Length; i++)
        {
            customerMessageText.text += sentence[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        typingRoutine = null;
    }

    private void RefreshIconsFromRequestedOrder()
    {
        List<string> foods = new List<string>();
        string drinkItem = string.Empty;

        for (int i = 0; i < requestedContents.Count; i++)
        {
            string item = requestedContents[i];
            if (string.IsNullOrWhiteSpace(item)) continue;

            if (IsDrink(item))
                drinkItem = item;
            else
                foods.Add(item);
        }

        SetIcon(food1, foods.Count > 0 ? GetSprite(foods[0]) : null);
        SetIcon(food2, foods.Count > 1 ? GetSprite(foods[1]) : null);
        SetIcon(drink, !string.IsNullOrEmpty(drinkItem) ? GetSprite(drinkItem) : null);
    }

    private string GenerateSentence(List<string> contents)
    {
        List<string> foods = new List<string>();
        string drinkItem = string.Empty;

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];
            if (string.IsNullOrWhiteSpace(item)) continue;

            if (IsDrink(item))
                drinkItem = item;
            else
                foods.Add(item);
        }

        if (foods.Count == 1 && !string.IsNullOrEmpty(drinkItem))
            return $"I'd like a {foods[0]} with a {drinkItem}.";

        if (foods.Count == 1)
            return $"I'd like a {foods[0]}.";

        if (foods.Count >= 2 && !string.IsNullOrEmpty(drinkItem))
            return $"I'd like a {foods[0]} and {foods[1]} bundle with a {drinkItem}.";

        if (foods.Count >= 2)
            return $"I'd like a {foods[0]} and {foods[1]} bundle.";

        if (!string.IsNullOrEmpty(drinkItem))
            return $"I'd like a {drinkItem}.";

        return "Order not found.";
    }

    private void SetIcon(Image img, Sprite sprite)
    {
        if (img == null) return;

        img.sprite = sprite;
        img.enabled = sprite != null;
        img.gameObject.SetActive(sprite != null);
    }

    private bool IsDrink(string item)
    {
        return item == "Coke" || item == "Pineapple" || item == "Ice Tea";
    }

    private Sprite GetSprite(string item)
    {
        switch (item)
        {
            case "Chicken": return chickenSprite;
            case "Fries": return friesSprite;
            case "Burger": return burgerSprite;
            case "Coke": return cokeSprite;
            case "Pineapple": return pineappleSprite;
            case "Ice Tea": return iceTeaSprite;
            default: return null;
        }
    }

    private void Confirm()
    {
        if (group == null) return;

        if (!TryBuildSelection(
            out List<string> selectedContents,
            out string orderName,
            out int unitPrice,
            out CustomerGroup.FoodType mainFood,
            out CustomerGroup.DrinkType selectedDrink))
            return;

        if (group.submittedOrder == null)
            group.submittedOrder = new CustomerGroup.SimpleOrder();

        group.submittedOrder.Clear();
        group.submittedOrder.name = orderName;
        group.submittedOrder.unitPrice = unitPrice;
        group.submittedOrder.quantity = 1;
        group.submittedOrder.contents.AddRange(selectedContents);

        group.TakeOrderFromWaiter(mainFood, selectedDrink);

        KitchenManager kitchen = FindFirstObjectByType<KitchenManager>();
        if (kitchen != null)
            kitchen.ProcessOrder(group);

        Close();
    }

    private bool TryBuildSelection(
        out List<string> selectedContents,
        out string orderName,
        out int unitPrice,
        out CustomerGroup.FoodType mainFood,
        out CustomerGroup.DrinkType selectedDrink)
    {
        selectedContents = new List<string>();
        orderName = string.Empty;
        unitPrice = 0;
        mainFood = CustomerGroup.FoodType.Chicken;
        selectedDrink = CustomerGroup.DrinkType.Coke;

        bool hasSolo =
            IsOn(chickenToggle) ||
            IsOn(friesToggle) ||
            IsOn(burgerToggle);

        bool hasBundle =
            IsOn(chickenFriesToggle) ||
            IsOn(chickenBurgerToggle) ||
            IsOn(burgerFriesToggle);

        if (hasSolo && hasBundle)
        {
            ShowWarning("You can't check a solo food and a bundle at the same time.");
            return false;
        }

        if (!hasSolo && !hasBundle)
        {
            ShowWarning("Please select a food or a bundle first.");
            return false;
        }

        if (IsOn(chickenToggle))
        {
            orderName = "Chicken";
            unitPrice = 299;
            selectedContents.Add("Chicken");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(friesToggle))
        {
            orderName = "Fries";
            unitPrice = 79;
            selectedContents.Add("Fries");
            mainFood = CustomerGroup.FoodType.Fries;
        }
        else if (IsOn(burgerToggle))
        {
            orderName = "Burger";
            unitPrice = 119;
            selectedContents.Add("Burger");
            mainFood = CustomerGroup.FoodType.Burger;
        }
        else if (IsOn(chickenFriesToggle))
        {
            orderName = "Chicken + Fries";
            unitPrice = 375;
            selectedContents.Add("Chicken");
            selectedContents.Add("Fries");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(chickenBurgerToggle))
        {
            orderName = "Chicken + Burger";
            unitPrice = 415;
            selectedContents.Add("Chicken");
            selectedContents.Add("Burger");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(burgerFriesToggle))
        {
            orderName = "Burger + Fries";
            unitPrice = 195;
            selectedContents.Add("Burger");
            selectedContents.Add("Fries");
            mainFood = CustomerGroup.FoodType.Burger;
        }

        if (IsOn(cokeToggle))
        {
            selectedContents.Add("Coke");
            unitPrice += 50;
            selectedDrink = CustomerGroup.DrinkType.Coke;
        }
        else if (IsOn(pineappleToggle))
        {
            selectedContents.Add("Pineapple");
            unitPrice += 50;
            selectedDrink = CustomerGroup.DrinkType.Pineapple;
        }
        else if (IsOn(iceTeaToggle))
        {
            selectedContents.Add("Ice Tea");
            unitPrice += 50;
            selectedDrink = CustomerGroup.DrinkType.IceTea;
        }
        else if (group != null)
        {
            selectedDrink = group.chosenDrink;
        }

        return true;
    }

    private void BindToggleLogic()
    {
        BindFoodToggle(chickenToggle);
        BindFoodToggle(friesToggle);
        BindFoodToggle(burgerToggle);

        BindFoodToggle(chickenFriesToggle);
        BindFoodToggle(chickenBurgerToggle);
        BindFoodToggle(burgerFriesToggle);

        BindDrinkToggle(cokeToggle);
        BindDrinkToggle(pineappleToggle);
        BindDrinkToggle(iceTeaToggle);
    }

    private void BindFoodToggle(Toggle toggle)
    {
        if (toggle == null) return;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(isOn =>
        {
            if (!isOn) return;

            if (IsAnotherFoodToggleOn(toggle))
            {
                toggle.SetIsOnWithoutNotify(false);
                ShowWarning("You can't check multiple foods or bundles at the same time.");
            }
        });
    }

    private void BindDrinkToggle(Toggle toggle)
    {
        if (toggle == null) return;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(isOn =>
        {
            if (!isOn) return;

            if (IsAnotherDrinkToggleOn(toggle))
            {
                toggle.SetIsOnWithoutNotify(false);
                ShowWarning("You can't check multiple drinks at the same time.");
            }
        });
    }

    private bool IsAnotherFoodToggleOn(Toggle current)
    {
        Toggle[] all =
        {
            chickenToggle, friesToggle, burgerToggle,
            chickenFriesToggle, chickenBurgerToggle, burgerFriesToggle
        };

        for (int i = 0; i < all.Length; i++)
        {
            Toggle t = all[i];
            if (t == null || t == current) continue;
            if (t.isOn) return true;
        }

        return false;
    }

    private bool IsAnotherDrinkToggleOn(Toggle current)
    {
        Toggle[] all =
        {
            cokeToggle, pineappleToggle, iceTeaToggle
        };

        for (int i = 0; i < all.Length; i++)
        {
            Toggle t = all[i];
            if (t == null || t == current) continue;
            if (t.isOn) return true;
        }

        return false;
    }

    private void ResetToggles()
    {
        SetToggle(chickenToggle, false);
        SetToggle(friesToggle, false);
        SetToggle(burgerToggle, false);

        SetToggle(chickenFriesToggle, false);
        SetToggle(chickenBurgerToggle, false);
        SetToggle(burgerFriesToggle, false);

        SetToggle(cokeToggle, false);
        SetToggle(pineappleToggle, false);
        SetToggle(iceTeaToggle, false);
    }

    private void SetToggle(Toggle t, bool value)
    {
        if (t == null) return;
        t.SetIsOnWithoutNotify(value);
    }

    private bool IsOn(Toggle t)
    {
        return t != null && t.isOn;
    }

    private void ShowWarning(string msg)
    {
        Debug.Log("[WARNING] " + msg);

        WarningSlideUI popup = FindFirstObjectByType<WarningSlideUI>();
        if (popup != null)
            popup.Show(msg);
    }
}