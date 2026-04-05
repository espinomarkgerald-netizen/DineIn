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

    [Header("Single Item Prices")]
    [SerializeField] private int chickenPrice = 299;
    [SerializeField] private int friesPrice = 79;
    [SerializeField] private int burgerPrice = 119;
    [SerializeField] private int cokePrice = 50;
    [SerializeField] private int pineapplePrice = 50;
    [SerializeField] private int iceTeaPrice = 50;

    [Header("Bundle Prices")]
    [SerializeField] private bool useCustomBundlePrices = true;
    [SerializeField] private int chickenFriesBundlePrice = 349;
    [SerializeField] private int chickenBurgerBundlePrice = 399;
    [SerializeField] private int burgerFriesBundlePrice = 179;

    [Header("Price Text UI")]
    [SerializeField] private TMP_Text chickenPriceText;
    [SerializeField] private TMP_Text friesPriceText;
    [SerializeField] private TMP_Text burgerPriceText;
    [SerializeField] private TMP_Text cokePriceText;
    [SerializeField] private TMP_Text pineapplePriceText;
    [SerializeField] private TMP_Text iceTeaPriceText;
    [SerializeField] private TMP_Text chickenFriesBundlePriceText;
    [SerializeField] private TMP_Text chickenBurgerBundlePriceText;
    [SerializeField] private TMP_Text burgerFriesBundlePriceText;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typeSpeed = 0.02f;

    [SerializeField] private TutorialHintTextUI tutorialHint;

    [Header("Available Stock UI")]
    [SerializeField] private TMP_Text chickenAvailableText;
    [SerializeField] private TMP_Text friesAvailableText;
    [SerializeField] private TMP_Text burgerAvailableText;

    private CustomerGroup group;
    private Coroutine typingRoutine;

    private readonly List<string> requestedContents = new List<string>();

    private const string RecipeIDChicken = "01";
    private const string RecipeIDBurger = "02";
    private const string RecipeIDFries = "03";

    private void OnEnable()
    {
        UnlockManager.OnRecipeUnlocked += HandleRecipeUnlocked;
        RefreshPriceTexts();
    }

    private void OnDisable()
    {
        UnlockManager.OnRecipeUnlocked -= HandleRecipeUnlocked;
    }

    private void HandleRecipeUnlocked(string recipeID)
    {
        RefreshUnlockUI();
    }

    /// <summary>
    /// Gates food toggles based on recipe unlock state in UnlockManager.
    /// Runs on top of the existing stock availability gate.
    /// </summary>
    private void RefreshUnlockUI()
    {
        if (UnlockManager.Instance == null) return;

        bool chickenUnlocked = UnlockManager.Instance.IsRecipeUnlocked(RecipeIDChicken);
        bool burgerUnlocked  = UnlockManager.Instance.IsRecipeUnlocked(RecipeIDBurger);
        bool friesUnlocked   = UnlockManager.Instance.IsRecipeUnlocked(RecipeIDFries);

        SetToggleUnlocked(chickenToggle, chickenUnlocked);
        SetToggleUnlocked(burgerToggle,  burgerUnlocked);
        SetToggleUnlocked(friesToggle,   friesUnlocked);

        SetToggleUnlocked(chickenFriesToggle,  chickenUnlocked && friesUnlocked);
        SetToggleUnlocked(chickenBurgerToggle, chickenUnlocked && burgerUnlocked);
        SetToggleUnlocked(burgerFriesToggle,   burgerUnlocked  && friesUnlocked);
    }

    private void SetToggleUnlocked(Toggle toggle, bool unlocked)
    {
        if (toggle == null) return;

        if (!unlocked)
        {
            toggle.interactable = false;
            toggle.SetIsOnWithoutNotify(false);
        }
        // When unlocked, leave interactable as-is — stock gate in RefreshFoodAvailabilityUI owns that state.
    }

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
        RefreshPriceTexts();

        gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        RefreshPriceTexts();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Open(CustomerGroup g)
    {
        if (g == null) return;

        group = g;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        group.SetOrderPause(true);

        ResetToggles();
        RefreshPriceTexts();
        RefreshUnlockUI();
        RefreshFoodAvailabilityUI();
        RefreshAvailableStockUI();
        LoadRequestedOrder();
        RefreshRequestedOrderUI();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnNotepadOpened(g);

        if (TutorialManager.Instance != null && tutorialHint != null)
            tutorialHint.Show("Read the order above. Match the same food and drink below.");
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

    private void RefreshPriceTexts()
    {
        SetPriceText(chickenPriceText, chickenPrice);
        SetPriceText(friesPriceText, friesPrice);
        SetPriceText(burgerPriceText, burgerPrice);

        SetPriceText(cokePriceText, cokePrice);
        SetPriceText(pineapplePriceText, pineapplePrice);
        SetPriceText(iceTeaPriceText, iceTeaPrice);

        if (useCustomBundlePrices)
        {
            SetPriceText(chickenFriesBundlePriceText, chickenFriesBundlePrice);
            SetPriceText(chickenBurgerBundlePriceText, chickenBurgerBundlePrice);
            SetPriceText(burgerFriesBundlePriceText, burgerFriesBundlePrice);
        }
        else
        {
            SetPriceText(chickenFriesBundlePriceText, chickenPrice + friesPrice);
            SetPriceText(chickenBurgerBundlePriceText, chickenPrice + burgerPrice);
            SetPriceText(burgerFriesBundlePriceText, burgerPrice + friesPrice);
        }
    }

    private void SetPriceText(TMP_Text target, int value)
    {
        if (target == null) return;
        target.text = value.ToString("0.00");
    }

    private void RefreshAvailableStockUI()
    {
        if (LobbyStockBridge.Instance == null)
        {
            SetAvailableStockText(chickenAvailableText, 0);
            SetAvailableStockText(friesAvailableText, 0);
            SetAvailableStockText(burgerAvailableText, 0);
            return;
        }

        SetAvailableStockText(
            chickenAvailableText,
            LobbyStockBridge.Instance.GetFoodStock(CustomerGroup.FoodType.Chicken));

        SetAvailableStockText(
            friesAvailableText,
            LobbyStockBridge.Instance.GetFoodStock(CustomerGroup.FoodType.Fries));

        SetAvailableStockText(
            burgerAvailableText,
            LobbyStockBridge.Instance.GetFoodStock(CustomerGroup.FoodType.Burger));
    }

    private void SetAvailableStockText(TMP_Text textUI, int amount)
    {
        if (textUI == null) return;
        textUI.text = "x" + amount;
    }

    private void RefreshFoodAvailabilityUI()
    {
        bool chickenAvailable = LobbyStockBridge.Instance == null || LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Chicken);
        bool friesAvailable = LobbyStockBridge.Instance == null || LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Fries);
        bool burgerAvailable = LobbyStockBridge.Instance == null || LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Burger);

        SetToggleInteractable(chickenToggle, chickenAvailable);
        SetToggleInteractable(friesToggle, friesAvailable);
        SetToggleInteractable(burgerToggle, burgerAvailable);

        SetToggleInteractable(chickenFriesToggle, chickenAvailable && friesAvailable);
        SetToggleInteractable(chickenBurgerToggle, chickenAvailable && burgerAvailable);
        SetToggleInteractable(burgerFriesToggle, burgerAvailable && friesAvailable);
    }

    private void SetToggleInteractable(Toggle toggle, bool interactable)
    {
        if (toggle == null) return;

        toggle.interactable = interactable;

        if (!interactable)
            toggle.SetIsOnWithoutNotify(false);
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

    private bool IsFoodItem(string item)
    {
        return item == "Chicken" || item == "Fries" || item == "Burger";
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

    public int GetPriceForItem(string item)
    {
        switch (item)
        {
            case "Chicken": return chickenPrice;
            case "Fries": return friesPrice;
            case "Burger": return burgerPrice;
            case "Coke": return cokePrice;
            case "Pineapple": return pineapplePrice;
            case "Ice Tea": return iceTeaPrice;
            default: return 0;
        }
    }

    public bool TryGetBundleFoodPrice(List<string> contents, out int price)
    {
        price = 0;

        if (contents == null)
            return false;

        List<string> foods = new List<string>();

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];
            if (IsFoodItem(item))
                foods.Add(item);
        }

        if (foods.Count != 2)
            return false;

        bool hasChicken = foods.Contains("Chicken");
        bool hasFries = foods.Contains("Fries");
        bool hasBurger = foods.Contains("Burger");

        if (hasChicken && hasFries)
        {
            price = useCustomBundlePrices ? chickenFriesBundlePrice : chickenPrice + friesPrice;
            return true;
        }

        if (hasChicken && hasBurger)
        {
            price = useCustomBundlePrices ? chickenBurgerBundlePrice : chickenPrice + burgerPrice;
            return true;
        }

        if (hasBurger && hasFries)
        {
            price = useCustomBundlePrices ? burgerFriesBundlePrice : burgerPrice + friesPrice;
            return true;
        }

        return false;
    }

    public int GetFoodTotalFromContents(List<string> contents)
    {
        if (contents == null) return 0;

        if (TryGetBundleFoodPrice(contents, out int bundlePrice))
            return bundlePrice;

        int total = 0;

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];

            if (item == "Chicken" || item == "Fries" || item == "Burger")
                total += GetPriceForItem(item);
        }

        return total;
    }

    public int GetDrinkTotalFromContents(List<string> contents)
    {
        if (contents == null) return 0;

        int total = 0;

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];

            if (item == "Coke" || item == "Pineapple" || item == "Ice Tea")
                total += GetPriceForItem(item);
        }

        return total;
    }

    public int GetOrderTotalFromContents(List<string> contents)
    {
        return GetFoodTotalFromContents(contents) + GetDrinkTotalFromContents(contents);
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

        if (LobbyStockBridge.Instance != null)
        {
            for (int i = 0; i < selectedContents.Count; i++)
            {
                string item = selectedContents[i];

                if (item == "Chicken" &&
                    !LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Chicken))
                {
                    ShowWarning("Chicken is no longer available.");
                    return;
                }

                if (item == "Fries" &&
                    !LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Fries))
                {
                    ShowWarning("Fries are no longer available.");
                    return;
                }

                if (item == "Burger" &&
                    !LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Burger))
                {
                    ShowWarning("Burger is no longer available.");
                    return;
                }
            }

            int stockMultiplier = Mathf.Max(1, group.Size);

            for (int repeat = 0; repeat < stockMultiplier; repeat++)
            {
                for (int i = 0; i < selectedContents.Count; i++)
                {
                    string item = selectedContents[i];

                    if (item == "Chicken")
                        LobbyStockBridge.Instance.TryUseFoodStock(CustomerGroup.FoodType.Chicken);

                    if (item == "Fries")
                        LobbyStockBridge.Instance.TryUseFoodStock(CustomerGroup.FoodType.Fries);

                    if (item == "Burger")
                        LobbyStockBridge.Instance.TryUseFoodStock(CustomerGroup.FoodType.Burger);
                }

                LobbyStockBridge.Instance.TryUseDrinkStock(selectedDrink);
            }
        }

        if (group.submittedOrder == null)
            group.submittedOrder = new CustomerGroup.SimpleOrder();

        group.submittedOrder.Clear();
        group.submittedOrder.name = orderName;
        group.submittedOrder.unitPrice = unitPrice;
        group.submittedOrder.quantity = Mathf.Max(1, group.Size);
        group.submittedOrder.contents.AddRange(selectedContents);

        group.currentOrder.quantity = group.submittedOrder.quantity;

        group.TakeOrderFromWaiter(mainFood, selectedDrink);

        if (group.IsTakeout)
        {
            // Takeout: payment must happen before kitchen. TakeOrderFromWaiter already
            // called TakeoutFlowManager.NotifyOrderTaken, which opens the payment step.
            if (ProcessingBillIndicatorUI.Instance != null)
                ProcessingBillIndicatorUI.Instance.ShowForSeconds("Order confirmed — awaiting payment", 2f);
        }
        else
        {
            if (ProcessingBillIndicatorUI.Instance != null)
                ProcessingBillIndicatorUI.Instance.ShowForSeconds("Order Sent to Kitchen", 2f);

            KitchenManager kitchen = FindFirstObjectByType<KitchenManager>();
            if (kitchen != null)
                kitchen.ProcessOrder(group);
        }

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnOrderConfirmed(group);

        Close();
    }

    private bool AutoReplaceUnavailableFoods(List<string> selectedContents, out bool replacedAny)
    {
        replacedAny = false;

        if (selectedContents == null || selectedContents.Count == 0)
            return false;

        if (LobbyStockBridge.Instance == null)
            return true;

        for (int i = 0; i < selectedContents.Count; i++)
        {
            string item = selectedContents[i];

            if (!IsFoodItem(item))
                continue;

            if (HasStockForFoodName(item))
                continue;

            string replacement = GetReplacementFoodName(item);

            if (string.IsNullOrEmpty(replacement))
            {
                ShowWarning("No food ingredients are available for this order.");
                return false;
            }

            selectedContents[i] = replacement;
            replacedAny = true;
        }

        return true;
    }

    private void ApplyReplacementToCustomerOrder(List<string> selectedContents, string orderName, int unitPrice)
    {
        requestedContents.Clear();
        requestedContents.AddRange(selectedContents);

        if (group != null && group.currentOrder != null)
        {
            group.currentOrder.contents.Clear();
            group.currentOrder.contents.AddRange(selectedContents);
            group.currentOrder.name = orderName;
            group.currentOrder.unitPrice = unitPrice;
            group.currentOrder.quantity = 1;
        }

        RefreshRequestedOrderUI();
    }

    private bool HasStockForFoodName(string item)
    {
        if (LobbyStockBridge.Instance == null)
            return false;

        switch (item)
        {
            case "Chicken":
                return LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Chicken);

            case "Fries":
                return LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Fries);

            case "Burger":
                return LobbyStockBridge.Instance.HasFoodStock(CustomerGroup.FoodType.Burger);
        }

        return false;
    }

    private string GetReplacementFoodName(string originalItem)
    {
        string[] candidates = { "Chicken", "Fries", "Burger" };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];

            if (candidate == originalItem)
                continue;

            if (HasStockForFoodName(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private void RebuildOrderDataFromContents(
        List<string> contents,
        CustomerGroup.DrinkType selectedDrink,
        out string orderName,
        out int unitPrice,
        out CustomerGroup.FoodType mainFood)
    {
        List<string> foods = new List<string>();

        unitPrice = 0;
        orderName = string.Empty;
        mainFood = CustomerGroup.FoodType.Chicken;

        for (int i = 0; i < contents.Count; i++)
        {
            string item = contents[i];

            if (IsFoodItem(item))
                foods.Add(item);
        }

        unitPrice = GetOrderTotalFromContents(contents);

        if (foods.Count == 1)
            orderName = foods[0];
        else if (foods.Count >= 2)
            orderName = foods[0] + " + " + foods[1];
        else
            orderName = "Order";

        if (foods.Count > 0)
            mainFood = GetFoodTypeFromName(foods[0]);
    }

    private CustomerGroup.FoodType GetFoodTypeFromName(string item)
    {
        switch (item)
        {
            case "Chicken":
                return CustomerGroup.FoodType.Chicken;
            case "Fries":
                return CustomerGroup.FoodType.Fries;
            case "Burger":
                return CustomerGroup.FoodType.Burger;
        }

        return CustomerGroup.FoodType.Chicken;
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
            selectedContents.Add("Chicken");
            unitPrice += GetPriceForItem("Chicken");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(friesToggle))
        {
            orderName = "Fries";
            selectedContents.Add("Fries");
            unitPrice += GetPriceForItem("Fries");
            mainFood = CustomerGroup.FoodType.Fries;
        }
        else if (IsOn(burgerToggle))
        {
            orderName = "Burger";
            selectedContents.Add("Burger");
            unitPrice += GetPriceForItem("Burger");
            mainFood = CustomerGroup.FoodType.Burger;
        }
        else if (IsOn(chickenFriesToggle))
        {
            orderName = "Chicken + Fries";
            selectedContents.Add("Chicken");
            selectedContents.Add("Fries");
            unitPrice += useCustomBundlePrices ? chickenFriesBundlePrice : GetPriceForItem("Chicken") + GetPriceForItem("Fries");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(chickenBurgerToggle))
        {
            orderName = "Chicken + Burger";
            selectedContents.Add("Chicken");
            selectedContents.Add("Burger");
            unitPrice += useCustomBundlePrices ? chickenBurgerBundlePrice : GetPriceForItem("Chicken") + GetPriceForItem("Burger");
            mainFood = CustomerGroup.FoodType.Chicken;
        }
        else if (IsOn(burgerFriesToggle))
        {
            orderName = "Burger + Fries";
            selectedContents.Add("Burger");
            selectedContents.Add("Fries");
            unitPrice += useCustomBundlePrices ? burgerFriesBundlePrice : GetPriceForItem("Burger") + GetPriceForItem("Fries");
            mainFood = CustomerGroup.FoodType.Burger;
        }

        if (IsOn(cokeToggle))
        {
            selectedContents.Add("Coke");
            unitPrice += GetPriceForItem("Coke");
            selectedDrink = CustomerGroup.DrinkType.Coke;
        }
        else if (IsOn(pineappleToggle))
        {
            selectedContents.Add("Pineapple");
            unitPrice += GetPriceForItem("Pineapple");
            selectedDrink = CustomerGroup.DrinkType.Pineapple;
        }
        else if (IsOn(iceTeaToggle))
        {
            selectedContents.Add("Ice Tea");
            unitPrice += GetPriceForItem("Ice Tea");
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