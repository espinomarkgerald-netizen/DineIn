using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class MenuProductToggleBinding
{
    public Recipe product;
    public Toggle toggle;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public TMP_Text availableText;
    public Image icon;
}

[System.Serializable]
public class MenuBundleToggleBinding
{
    public string bundleId;
    public Toggle toggle;
    public TMP_Text nameText;
    public TMP_Text priceText;
}

public class OrderChecklistUI : MonoBehaviour
{
    public static OrderChecklistUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text tableNumberText;
    [SerializeField] private TMP_Text customerMessageText;

    [Header("Customer Type UI")]
    [SerializeField] private TMP_Text customerTypeText;
    [SerializeField] private Image customerImage;

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

    [Header("Data-Driven Menu Bindings")]
    [Tooltip("Optional extensible bindings. Add a row when a new product gets a notepad UI slot. Existing scenes use the legacy slots below automatically.")]
    [SerializeField] private List<MenuProductToggleBinding> productBindings = new List<MenuProductToggleBinding>();
    [Tooltip("Optional extensible bundle bindings. bundleId must match an entry in MenuCatalog.")]
    [SerializeField] private List<MenuBundleToggleBinding> bundleBindings = new List<MenuBundleToggleBinding>();

    private CustomerGroup group;
    private Coroutine typingRoutine;

    private readonly List<string> requestedContents = new List<string>();
    private readonly Dictionary<Toggle, Recipe> productByToggle = new Dictionary<Toggle, Recipe>();
    private readonly Dictionary<Toggle, MenuBundle> bundleByToggle = new Dictionary<Toggle, MenuBundle>();
    private readonly Dictionary<Toggle, TMP_Text> priceTextByToggle = new Dictionary<Toggle, TMP_Text>();
    private readonly Dictionary<Toggle, TMP_Text> availableTextByToggle = new Dictionary<Toggle, TMP_Text>();

    private MenuCatalog catalog;

    private string cachedOpeningMessage;
    private string cachedCustomerTypeName;
    private Sprite cachedCustomerImage;

    private void OnEnable()
    {
        UnlockManager.OnRecipeUnlocked += HandleRecipeUnlocked;
        BuildMenuBindings();
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
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
            SetToggleUnlocked(pair.Key, pair.Value != null && pair.Value.IsUnlocked);

        foreach (KeyValuePair<Toggle, MenuBundle> pair in bundleByToggle)
        {
            bool unlocked = pair.Value != null && pair.Value.availableOnMenu;
            if (unlocked)
            {
                for (int i = 0; i < pair.Value.products.Count; i++)
                {
                    if (pair.Value.products[i] == null || !pair.Value.products[i].IsUnlocked)
                    {
                        unlocked = false;
                        break;
                    }
                }
            }

            SetToggleUnlocked(pair.Key, unlocked);
        }
    }

    private void SetToggleUnlocked(Toggle toggle, bool unlocked)
    {
        if (toggle == null) return;

        if (!unlocked)
        {
            toggle.interactable = false;
            toggle.SetIsOnWithoutNotify(false);
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

        BuildMenuBindings();

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
        BuildMenuBindings();
        RefreshPriceTexts();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildMenuBindings()
    {
        catalog = MenuCatalog.Default;
        productByToggle.Clear();
        bundleByToggle.Clear();
        priceTextByToggle.Clear();
        availableTextByToggle.Clear();

        if (catalog == null)
            return;

        for (int i = 0; i < productBindings.Count; i++)
        {
            MenuProductToggleBinding binding = productBindings[i];
            if (binding == null || binding.toggle == null || binding.product == null)
                continue;

            AddProductBinding(
                binding.toggle,
                binding.product,
                binding.priceText,
                binding.availableText,
                binding.nameText,
                binding.icon);
        }

        for (int i = 0; i < bundleBindings.Count; i++)
        {
            MenuBundleToggleBinding binding = bundleBindings[i];
            if (binding == null || binding.toggle == null)
                continue;

            MenuBundle bundle = catalog.FindBundle(binding.bundleId);
            AddBundleBinding(binding.toggle, bundle, binding.priceText, binding.nameText);
        }

        // Compatibility adapter for the current notepad layout. New UI slots should
        // use productBindings/bundleBindings so adding a product requires no code.
        AddLegacyProductBinding(chickenToggle, ItemTypeKitchen.Chicken, chickenPriceText, chickenAvailableText);
        AddLegacyProductBinding(friesToggle, ItemTypeKitchen.Fries, friesPriceText, friesAvailableText);
        AddLegacyProductBinding(burgerToggle, ItemTypeKitchen.Burger, burgerPriceText, burgerAvailableText);
        AddLegacyProductBinding(cokeToggle, ItemTypeKitchen.Coke, cokePriceText, null);
        AddLegacyProductBinding(pineappleToggle, ItemTypeKitchen.Pineapple, pineapplePriceText, null);
        AddLegacyProductBinding(iceTeaToggle, ItemTypeKitchen.IcedTea, iceTeaPriceText, null);

        AddLegacyBundleBinding(
            chickenFriesToggle,
            chickenFriesBundlePriceText,
            ItemTypeKitchen.Chicken,
            ItemTypeKitchen.Fries);
        AddLegacyBundleBinding(
            chickenBurgerToggle,
            chickenBurgerBundlePriceText,
            ItemTypeKitchen.Chicken,
            ItemTypeKitchen.Burger);
        AddLegacyBundleBinding(
            burgerFriesToggle,
            burgerFriesBundlePriceText,
            ItemTypeKitchen.Burger,
            ItemTypeKitchen.Fries);
    }

    private void AddLegacyProductBinding(
        Toggle toggle,
        ItemTypeKitchen kitchenItem,
        TMP_Text priceText,
        TMP_Text availableText)
    {
        if (toggle == null || productByToggle.ContainsKey(toggle))
            return;

        AddProductBinding(toggle, catalog.FindByKitchenItem(kitchenItem), priceText, availableText, null, null);
    }

    private void AddProductBinding(
        Toggle toggle,
        Recipe product,
        TMP_Text priceText,
        TMP_Text availableText,
        TMP_Text nameText,
        Image icon)
    {
        if (toggle == null || product == null)
            return;

        productByToggle[toggle] = product;
        if (priceText != null) priceTextByToggle[toggle] = priceText;
        if (availableText != null) availableTextByToggle[toggle] = availableText;
        if (nameText == null) nameText = FindLegacyNameText(toggle, priceText, availableText);
        if (nameText != null) nameText.text = product.DisplayName;
        if (icon != null) icon.sprite = product.sprite;
    }

    private void AddLegacyBundleBinding(
        Toggle toggle,
        TMP_Text priceText,
        params ItemTypeKitchen[] kitchenItems)
    {
        if (toggle == null || bundleByToggle.ContainsKey(toggle))
            return;

        List<Recipe> products = new List<Recipe>();
        for (int i = 0; i < kitchenItems.Length; i++)
        {
            Recipe product = catalog.FindByKitchenItem(kitchenItems[i]);
            if (product != null) products.Add(product);
        }

        AddBundleBinding(toggle, catalog.FindBundle(products), priceText, null);
    }

    private void AddBundleBinding(Toggle toggle, MenuBundle bundle, TMP_Text priceText, TMP_Text nameText)
    {
        if (toggle == null || bundle == null)
            return;

        bundleByToggle[toggle] = bundle;
        if (priceText != null) priceTextByToggle[toggle] = priceText;
        if (nameText == null) nameText = FindLegacyNameText(toggle, priceText, null);
        if (nameText != null) nameText.text = bundle.displayName;
    }

    private static TMP_Text FindLegacyNameText(
        Toggle toggle,
        TMP_Text priceText,
        TMP_Text availableText)
    {
        if (toggle == null || toggle.transform.parent == null || toggle.transform.parent.parent == null)
            return null;

        TMP_Text[] texts = toggle.transform.parent.parent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || candidate == priceText || candidate == availableText)
                continue;

            return candidate;
        }

        return null;
    }

    public void Open(CustomerGroup g)
    {
        if (g == null) return;

        group = g;
        cachedOpeningMessage = group.GetCustomerOpeningMessage();
        cachedCustomerTypeName = group.GetCustomerTypeName();
        cachedCustomerImage = group.GetCustomerTypeImage();

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
        {
            group.SetOrderPause(false);
            if (group.state == CustomerGroup.GroupState.ReadyToOrder)
            {
                RestaurantTaskClaim.ReleasePlayer(group);
                group.SetOrderTaskClaimedByStaff(false);
            }
        }

        group = null;
        requestedContents.Clear();

        cachedOpeningMessage = string.Empty;
        cachedCustomerTypeName = string.Empty;
        cachedCustomerImage = null;

        gameObject.SetActive(false);
    }

    private void RefreshPriceTexts()
    {
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            if (priceTextByToggle.TryGetValue(pair.Key, out TMP_Text target))
                SetPriceText(target, pair.Value.sellPrice);
        }

        foreach (KeyValuePair<Toggle, MenuBundle> pair in bundleByToggle)
        {
            if (priceTextByToggle.TryGetValue(pair.Key, out TMP_Text target))
                SetPriceText(target, pair.Value.GetPrice());
        }
    }

    private void SetPriceText(TMP_Text target, int value)
    {
        if (target == null) return;
        target.text = value.ToString("0.00");
    }

    private void RefreshAvailableStockUI()
    {
        foreach (KeyValuePair<Toggle, TMP_Text> pair in availableTextByToggle)
        {
            int stock = 0;
            if (LobbyStockBridge.Instance != null && productByToggle.TryGetValue(pair.Key, out Recipe product))
                stock = LobbyStockBridge.Instance.GetProductStock(product);

            SetAvailableStockText(pair.Value, stock);
        }
    }

    private void SetAvailableStockText(TMP_Text textUI, int amount)
    {
        if (textUI == null) return;
        textUI.text = "x" + amount;
    }

    private void RefreshFoodAvailabilityUI()
    {
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            bool available = pair.Value.availableOnMenu &&
                (LobbyStockBridge.Instance == null || LobbyStockBridge.Instance.HasProductStock(pair.Value));
            SetToggleInteractable(pair.Key, available && pair.Value.IsUnlocked);
        }

        foreach (KeyValuePair<Toggle, MenuBundle> pair in bundleByToggle)
        {
            bool available = pair.Value.availableOnMenu;
            for (int i = 0; available && i < pair.Value.products.Count; i++)
            {
                Recipe product = pair.Value.products[i];
                available = product != null && product.IsUnlocked &&
                    (LobbyStockBridge.Instance == null || LobbyStockBridge.Instance.HasProductStock(product));
            }

            SetToggleInteractable(pair.Key, available);
        }
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
        RefreshCustomerTypeUI();
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

    private void RefreshCustomerTypeUI()
    {
        if (customerTypeText != null)
        {
            customerTypeText.text = string.IsNullOrWhiteSpace(cachedCustomerTypeName)
                ? "Customer Type: Regular"
                : $"Customer Type: {cachedCustomerTypeName}";
        }

        if (customerImage != null)
        {
            customerImage.sprite = cachedCustomerImage;
            customerImage.enabled = cachedCustomerImage != null;
            customerImage.gameObject.SetActive(cachedCustomerImage != null);
        }
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

        string orderSentence;

        if (foods.Count == 1 && !string.IsNullOrEmpty(drinkItem))
            orderSentence = $"I'll have a {foods[0]} with a {drinkItem}.";
        else if (foods.Count == 1)
            orderSentence = $"I'll have a {foods[0]}.";
        else if (foods.Count >= 2 && !string.IsNullOrEmpty(drinkItem))
            orderSentence = $"I'll have a {foods[0]} and {foods[1]} bundle with a {drinkItem}.";
        else if (foods.Count >= 2)
            orderSentence = $"I'll have a {foods[0]} and {foods[1]} bundle.";
        else if (!string.IsNullOrEmpty(drinkItem))
            orderSentence = $"I'll have a {drinkItem}.";
        else
            orderSentence = "Order not found.";

        if (string.IsNullOrWhiteSpace(cachedOpeningMessage))
            return orderSentence;

        return $"{cachedOpeningMessage} {orderSentence}";
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
        Recipe product = catalog != null ? catalog.FindProduct(item) : null;
        return product != null && product.category == MenuProductCategory.Drink;
    }

    private bool IsFoodItem(string item)
    {
        Recipe product = catalog != null ? catalog.FindProduct(item) : null;
        return product != null && product.category == MenuProductCategory.Food;
    }

    private Sprite GetSprite(string item)
    {
        Recipe product = catalog != null ? catalog.FindProduct(item) : null;
        return product != null ? product.sprite : null;
    }

    public int GetPriceForItem(string item)
    {
        Recipe product = catalog != null ? catalog.FindProduct(item) : null;
        return product != null ? product.sellPrice : 0;
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

        if (catalog == null)
            return false;

        List<Recipe> foodProducts = catalog.ResolveProducts(foods);
        MenuBundle bundle = catalog.FindBundle(foodProducts);
        if (bundle == null)
            return false;

        price = bundle.GetPrice();
        return true;
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

            if (IsFoodItem(item))
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

            if (IsDrink(item))
                total += GetPriceForItem(item);
        }

        return total;
    }

    public int GetOrderTotalFromContents(List<string> contents)
    {
        return catalog != null
            ? catalog.GetOrderTotal(contents)
            : GetFoodTotalFromContents(contents) + GetDrinkTotalFromContents(contents);
    }

    private void Confirm()
    {
        if (group == null) return;

        if (!TryBuildSelection(
            out List<Recipe> selectedProducts,
            out string orderName,
            out int unitPrice,
            out CustomerGroup.FoodType mainFood,
            out CustomerGroup.DrinkType selectedDrink))
            return;

        if (LobbyStockBridge.Instance != null)
        {
            int stockMultiplier = Mathf.Max(1, group.Size);
            if (!LobbyStockBridge.Instance.HasOrderStock(selectedProducts, stockMultiplier))
            {
                ShowWarning("One or more products in this order are no longer available.");
                RefreshFoodAvailabilityUI();
                RefreshAvailableStockUI();
                return;
            }

            if (!LobbyStockBridge.Instance.TryUseOrderStock(selectedProducts, stockMultiplier))
            {
                ShowWarning("Stock changed before the order could be submitted. Please try again.");
                RefreshFoodAvailabilityUI();
                RefreshAvailableStockUI();
                return;
            }
        }

        if (group.submittedOrder == null)
            group.submittedOrder = new CustomerGroup.SimpleOrder();

        group.submittedOrder.SetProducts(selectedProducts, orderName, unitPrice);
        group.submittedOrder.quantity = Mathf.Max(1, group.Size);

        group.currentOrder.quantity = group.submittedOrder.quantity;

        group.TakeOrderFromWaiter(mainFood, selectedDrink);
        RestaurantTaskClaim.Complete(group);

        if (group.IsTakeout)
        {
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

    private bool TryBuildSelection(
        out List<Recipe> selectedProducts,
        out string orderName,
        out int unitPrice,
        out CustomerGroup.FoodType mainFood,
        out CustomerGroup.DrinkType selectedDrink)
    {
        selectedProducts = new List<Recipe>();
        orderName = string.Empty;
        unitPrice = 0;
        mainFood = CustomerGroup.FoodType.Chicken;
        selectedDrink = CustomerGroup.DrinkType.Coke;

        Recipe selectedFood = null;
        MenuBundle selectedBundle = null;
        Recipe selectedDrinkProduct = null;

        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            if (!IsOn(pair.Key)) continue;

            if (pair.Value.category == MenuProductCategory.Food)
                selectedFood = pair.Value;
            else if (pair.Value.category == MenuProductCategory.Drink)
                selectedDrinkProduct = pair.Value;
        }

        foreach (KeyValuePair<Toggle, MenuBundle> pair in bundleByToggle)
        {
            if (IsOn(pair.Key))
                selectedBundle = pair.Value;
        }

        if (selectedFood != null && selectedBundle != null)
        {
            ShowWarning("You can't check a solo food and a bundle at the same time.");
            return false;
        }

        if (selectedFood == null && selectedBundle == null)
        {
            ShowWarning("Please select a food or a bundle first.");
            return false;
        }

        if (selectedFood != null)
        {
            selectedProducts.Add(selectedFood);
            orderName = selectedFood.DisplayName;
            mainFood = ToLegacyFoodType(selectedFood);
        }
        else
        {
            selectedProducts.AddRange(selectedBundle.products);
            orderName = selectedBundle.displayName;
            mainFood = ToLegacyFoodType(selectedBundle.products[0]);
        }

        if (selectedDrinkProduct == null && group != null && catalog != null)
        {
            selectedDrinkProduct = FindLegacyDrinkProduct(group.chosenDrink);
        }

        if (selectedDrinkProduct != null)
        {
            selectedProducts.Add(selectedDrinkProduct);
            selectedDrink = ToLegacyDrinkType(selectedDrinkProduct);
        }

        if (catalog == null)
        {
            ShowWarning("The menu catalog could not be loaded.");
            return false;
        }

        unitPrice = catalog.GetOrderTotal(catalog.GetProductIds(selectedProducts));
        return true;
    }

    private Recipe FindLegacyDrinkProduct(CustomerGroup.DrinkType drinkType)
    {
        if (catalog == null) return null;

        switch (drinkType)
        {
            case CustomerGroup.DrinkType.Pineapple:
                return catalog.FindByKitchenItem(ItemTypeKitchen.Pineapple);
            case CustomerGroup.DrinkType.IceTea:
                return catalog.FindByKitchenItem(ItemTypeKitchen.IcedTea);
            default:
                return catalog.FindByKitchenItem(ItemTypeKitchen.Coke);
        }
    }

    private static CustomerGroup.FoodType ToLegacyFoodType(Recipe product)
    {
        if (product == null) return CustomerGroup.FoodType.Chicken;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Fries:  return CustomerGroup.FoodType.Fries;
            case ItemTypeKitchen.Burger: return CustomerGroup.FoodType.Burger;
            default:                     return CustomerGroup.FoodType.Chicken;
        }
    }

    private static CustomerGroup.DrinkType ToLegacyDrinkType(Recipe product)
    {
        if (product == null) return CustomerGroup.DrinkType.Coke;

        switch (product.kitchenItemType)
        {
            case ItemTypeKitchen.Pineapple: return CustomerGroup.DrinkType.Pineapple;
            case ItemTypeKitchen.IcedTea:   return CustomerGroup.DrinkType.IceTea;
            default:                        return CustomerGroup.DrinkType.Coke;
        }
    }

    private void BindToggleLogic()
    {
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            if (pair.Value.category == MenuProductCategory.Food)
                BindFoodToggle(pair.Key);
            else
                BindDrinkToggle(pair.Key);
        }

        foreach (Toggle toggle in bundleByToggle.Keys)
            BindFoodToggle(toggle);
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
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            if (pair.Key == null || pair.Key == current || pair.Value.category != MenuProductCategory.Food)
                continue;
            if (pair.Key.isOn) return true;
        }

        foreach (Toggle toggle in bundleByToggle.Keys)
        {
            if (toggle == null || toggle == current) continue;
            if (toggle.isOn) return true;
        }

        return false;
    }

    private bool IsAnotherDrinkToggleOn(Toggle current)
    {
        foreach (KeyValuePair<Toggle, Recipe> pair in productByToggle)
        {
            if (pair.Key == null || pair.Key == current || pair.Value.category != MenuProductCategory.Drink)
                continue;
            if (pair.Key.isOn) return true;
        }

        return false;
    }

    private void ResetToggles()
    {
        foreach (Toggle toggle in productByToggle.Keys)
            SetToggle(toggle, false);

        foreach (Toggle toggle in bundleByToggle.Keys)
            SetToggle(toggle, false);
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
