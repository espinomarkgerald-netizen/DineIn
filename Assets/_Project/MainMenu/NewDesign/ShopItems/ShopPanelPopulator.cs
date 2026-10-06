using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// Presentation and confirmation only; the existing wallet/exchange managers settle purchases.
public partial class ShopPanelPopulator : MonoBehaviour
{
    [SerializeField] private ShopItemView itemPrefab;
    [SerializeField] private List<ShopItemData> allItems = new List<ShopItemData>();
    [SerializeField] private RectTransform itemContainer;
    [SerializeField] private CurrencyExchangeManager currencyExchangeManager;
    [SerializeField] private UnityEngine.UI.ScrollRect sectionsScroll;
    [SerializeField] private UnityEngine.UI.Button moneyTab, costumesTab;
    [SerializeField] private TMP_Text balanceText, shopStatus;
    [SerializeField] private TMP_Text campaignBalanceText;
    [SerializeField] private Sprite selectedTabSprite, normalTabSprite;
    [SerializeField] private Color selectedTabTextColor = Color.white;
    [SerializeField] private Color normalTabTextColor = new Color(.04f, .24f, .31f, 1);
    private PlayFabWalletManager wallet;
    private AnimatedPanel animatedPanel;
    private ShopCategory category;
    private readonly float[] scrollPositions = { 1, 1 };
    private readonly Dictionary<ShopItemData, int> quotedPrices = new Dictionary<ShopItemData, int>();
    private readonly List<ShopItemView> views = new List<ShopItemView>();
    private void Awake()
    {
        animatedPanel = GetComponent<AnimatedPanel>();
        moneyTab.onClick.AddListener(() => SelectCategory(ShopCategory.Currency));
        costumesTab.onClick.AddListener(() => SelectCategory(ShopCategory.Cosmetic));
        confirmButton.onClick.AddListener(ConfirmPurchase);
        cancelButton.onClick.AddListener(DismissPurchase);
        purchaseClose.onClick.AddListener(DismissPurchase);
        PlayerCustomizationData.ProfileChanged += AccountChanged;
        PlayerCustomizationData.CostumeOwnershipChanged += RefreshCards;
        if (currencyExchangeManager != null)
        {
            currencyExchangeManager.OnExchangeSucceeded += MoneySucceeded;
            currencyExchangeManager.OnExchangeFailed += PurchaseFailed;
        }
    }
    private void Start() { SubscribeWallet(); RefreshWalletView(); }
    private void OnEnable()
    {
        SubscribeWallet(); Repopulate(); RefreshWalletView();
        if (selectedItem != null) { purchaseRoot.SetActive(true); RenderPurchase(); if (success) BeginSuccessAnimation(); }
        else purchaseRoot.SetActive(false);
        wallet?.RefreshWallet();
    }
    private void SubscribeWallet()
    {
        var current = PlayFabWalletManager.Instance;
        if (wallet == current) return;
        if (wallet != null) { wallet.OnWalletUpdated -= WalletUpdated; wallet.OnWalletCleared -= AccountChanged; wallet.OnWalletRefreshFailed -= WalletFailed; }
        wallet = current;
        if (wallet != null) { wallet.OnWalletUpdated += WalletUpdated; wallet.OnWalletCleared += AccountChanged; wallet.OnWalletRefreshFailed += WalletFailed; }
    }
    private void OnDisable()
    {
        if (sectionsScroll != null) scrollPositions[(int)category] = sectionsScroll.verticalNormalizedPosition;
        StopAllCoroutines();
        if (!submitted && !success) { quoteVersion++; selectedItem = null; }
    }
    private void OnDestroy()
    {
        PlayerCustomizationData.ProfileChanged -= AccountChanged;
        PlayerCustomizationData.CostumeOwnershipChanged -= RefreshCards;
        if (wallet != null) { wallet.OnWalletUpdated -= WalletUpdated; wallet.OnWalletCleared -= AccountChanged; wallet.OnWalletRefreshFailed -= WalletFailed; }
        if (currencyExchangeManager != null) { currencyExchangeManager.OnExchangeSucceeded -= MoneySucceeded; currencyExchangeManager.OnExchangeFailed -= PurchaseFailed; }
    }
    private void Update()
    {
        if (selectedItem == null) return;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) DismissPurchase();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) DismissPurchase();
#endif
    }
    private void WalletUpdated(int gold, int money) { RefreshWalletView(); if (selectedItem != null) RenderPurchase(); }
    private void WalletFailed(string message) { SetShopStatus("Wallet unavailable. Try again."); Debug.LogWarning("[Shop] Wallet refresh failed: " + message); RefreshCards(); if (selectedItem != null && !submitted) RenderPurchase(); }
    private void AccountChanged()
    {
        quoteVersion++; selectedItem = null; submitted = success = checking = false; quotedPrices.Clear();
        if (purchaseRoot != null) purchaseRoot.SetActive(false);
        RefreshWalletView();
    }
    private void RefreshWalletView()
    {
        if (balanceText != null && !success) balanceText.text = wallet != null && wallet.IsCurrentWallet ? "G" + wallet.GoldCoins.ToString("N0") : "G--";
        if (campaignBalanceText != null) campaignBalanceText.text = wallet != null && wallet.IsCurrentWallet ? "$" + wallet.NormalMoney.ToString("N0") : "$--";
        RefreshCards();
    }
    private void SetShopStatus(string message) { if (shopStatus != null) shopStatus.text = message; }
    private void SelectCategory(ShopCategory next)
    {
        if (selectedItem != null) return;
        scrollPositions[(int)category] = sectionsScroll.verticalNormalizedPosition;
        category = next; Repopulate();
    }
    public void Repopulate()
    {
        foreach (var view in views) if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
        views.Clear();
        if (itemContainer == null || itemPrefab == null) return;
        foreach (var item in allItems.Where(i => i != null && i.category == category))
        { var view = Instantiate(itemPrefab, itemContainer); view.Bind(item, RequestPurchase); views.Add(view); }
        StyleTab(moneyTab, category == ShopCategory.Currency);
        StyleTab(costumesTab, category == ShopCategory.Cosmetic);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(itemContainer);
        sectionsScroll.verticalNormalizedPosition = scrollPositions[(int)category];
        SetShopStatus(category == ShopCategory.Currency ? "Exchange GC for Campaign money." : "Costumes unlock in Customize after purchase.");
        RefreshCards();
    }
    private void StyleTab(UnityEngine.UI.Button tab, bool selected)
    {
        tab.image.sprite = selected ? selectedTabSprite : normalTabSprite;
        var label = tab.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = selected ? selectedTabTextColor : normalTabTextColor;
    }
    private void RefreshCards()
    {
        foreach (var view in views)
        {
            if (view == null || view.Data == null) continue;
            bool owned = view.Data.category == ShopCategory.Cosmetic && PlayerCustomizationData.OwnsCostume(view.Data.cosmeticUnlockId);
            int price = quotedPrices.TryGetValue(view.Data, out int cost) ? cost : view.Data.price;
            view.SetPurchaseState(owned, submitted || (wallet != null && wallet.IsGoldCoinSpendInFlight) || (currencyExchangeManager != null && currencyExchangeManager.IsPurchaseInProgress), true, price);
        }
    }
    public void RequestPackage(CurrencyExchangePackage package)
    {
        var item = allItems.Find(i => i != null && i.category == ShopCategory.Currency && i.price == package.goldCost && i.normalMoneyReward == package.normalMoneyReward);
        if (item != null) RequestPurchase(item);
        else { if (animatedPanel != null) animatedPanel.Open(); SetShopStatus("Item unavailable. Try later."); Debug.LogWarning("[Shop] Package has no matching shop item; nothing submitted."); }
    }
}
