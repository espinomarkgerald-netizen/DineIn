using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the visible shop item list per category by instantiating
/// ShopItemView prefabs from a flat list of ShopItemData assets, and routes
/// Buy clicks to whichever manager owns that category.
///
/// Single Responsibility: this script only decides which ShopItemData
/// belongs under which category container and who to notify when Buy is
/// clicked. It never talks to PlayFab directly and never decides whether a
/// purchase should succeed - CurrencyExchangeManager (and, later, a
/// CosmeticPurchaseManager shaped the same way) own that entirely.
///
/// Runs its population in OnEnable() rather than Awake(), since this is
/// meant to sit on a shop/wallet panel that starts disabled - Unity can't
/// run coroutines (or reliably a few other things) on a disabled
/// GameObject's Awake, so waiting for OnEnable (which only fires once the
/// panel is actually active) avoids that class of bug.
/// </summary>
public class ShopPanelPopulator : MonoBehaviour
{
    [Header("Item Source")]
    [Tooltip("The ShopItem prefab (with a ShopItemView component) to instantiate once per matching ShopItemData.")]
    [SerializeField] private ShopItemView itemPrefab;
    [Tooltip("Every ShopItemData asset the shop can possibly show, across all categories. Drag every item asset in here.")]
    [SerializeField] private List<ShopItemData> allItems = new List<ShopItemData>();

    [Header("Category Containers")]
    [Tooltip("Parent transform items with category == Currency are instantiated under.")]
    [SerializeField] private Transform currencyContainer;
    [Tooltip("Parent transform items with category == Cosmetic are instantiated under. Safe to leave unassigned until cosmetics exist - those items are simply skipped.")]
    [SerializeField] private Transform cosmeticsContainer;

    [Header("Purchase Routing")]
    [Tooltip("Handles Buy clicks for Currency category items.")]
    [SerializeField] private CurrencyExchangeManager currencyExchangeManager;
    // [Tooltip("Handles Buy clicks for Cosmetic category items.")]
    // [SerializeField] private CosmeticPurchaseManager cosmeticPurchaseManager; // add when cosmetics exist

    [Header("Behaviour")]
    [Tooltip("If true, the panel repopulates every time it becomes active (e.g. shop reopened). If false, only the first OnEnable populates and later ones are ignored.")]
    [SerializeField] private bool populateOnEveryEnable = true;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = false;

    [Header("Mobile layout")]
    [SerializeField] private Vector2 mobileContentPadding = new Vector2(24f, 64f);
    private Rect previousSafeArea;
    private Vector2Int previousScreenSize;
    private bool mobileLayoutDirty;
    private bool hasPopulatedOnce;
    private AnimatedPanel animatedPanel;
    private bool waitingForOpenAnimation;

    private void Awake()
    {
        animatedPanel = GetComponent<AnimatedPanel>();
        if (animatedPanel != null) animatedPanel.OnOpened.AddListener(OnPanelOpened);
    }

    private void OnDestroy()
    {
        if (animatedPanel != null) animatedPanel.OnOpened.RemoveListener(OnPanelOpened);
    }

    private void OnPanelOpened()
    {
        waitingForOpenAnimation = false;
        mobileLayoutDirty = true;
    }

    private void LateUpdate()
    {
        if (!MobileUISettings.UseMobileLayout || waitingForOpenAnimation) return;
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (!mobileLayoutDirty && size == previousScreenSize && Screen.safeArea == previousSafeArea) return;
        previousScreenSize = size;
        previousSafeArea = Screen.safeArea;
        mobileLayoutDirty = false;
        FitMobileContent();
    }

    private void FitMobileContent()
    {
        if (currencyContainer == null || transform is not RectTransform panel) return;
        Transform contentRoot = currencyContainer;
        while (contentRoot.parent != null && contentRoot.parent != transform) contentRoot = contentRoot.parent;
        if (contentRoot.parent != transform || contentRoot is not RectTransform content) return;
        Canvas.ForceUpdateCanvases();
        content.localScale = Vector3.one;
        // Fit the actual cards, not unused space in the layout container.
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var item in currencyContainer.GetComponentsInChildren<ShopItemView>())
        {
            Bounds card = RectTransformUtility.CalculateRelativeRectTransformBounds(content, item.transform);
            if (!hasBounds) { bounds = card; hasBounds = true; }
            else bounds.Encapsulate(card);
        }
        if (!hasBounds) return;
        if (bounds.size.x <= 0 || bounds.size.y <= 0) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, Screen.safeArea.min, camera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, Screen.safeArea.max, camera, out Vector2 max);
        min = Vector2.Max(min, panel.rect.min) + new Vector2(mobileContentPadding.x, mobileContentPadding.x);
        max = Vector2.Min(max, panel.rect.max) - mobileContentPadding;
        float scale = Mathf.Clamp(Mathf.Min((max.x - min.x) / bounds.size.x, (max.y - min.y) / bounds.size.y), .01f, 1f);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
        content.localScale = new Vector3(scale, scale, 1f);
        content.anchoredPosition = (min + max) * .5f - (Vector2)bounds.center * scale - panel.rect.center;
    }

    private void OnEnable()
    {
        // Screen-to-local measurements must use the settled panel transform.
        waitingForOpenAnimation = animatedPanel != null && animatedPanel.enabled;
        mobileLayoutDirty = true;
        if (hasPopulatedOnce && !populateOnEveryEnable)
            return;

        Repopulate();
        hasPopulatedOnce = true;
    }

    /// <summary>Clears and rebuilds every category container from allItems. Safe to call manually (e.g. after allItems changes at runtime).</summary>
    public void Repopulate()
    {
        mobileLayoutDirty = true;
        PopulateCategory(ShopCategory.Currency, currencyContainer);
        PopulateCategory(ShopCategory.Cosmetic, cosmeticsContainer);
    }

    private void PopulateCategory(ShopCategory category, Transform container)
    {
        if (container == null)
        {
            if (verboseLogging)
                Debug.Log("ShopPanelPopulator: no container assigned for category " + category + ", skipping.");
            return;
        }

        if (itemPrefab == null)
        {
            Debug.LogWarning("ShopPanelPopulator: itemPrefab is not assigned - cannot populate " + category + ".");
            return;
        }

        // Clear whatever was there before (e.g. from a previous open, or
        // leftover placeholder children left in the editor).
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        int spawned = 0;

        foreach (var item in allItems)
        {
            if (item == null || item.category != category)
                continue;

            var view = Instantiate(itemPrefab, container);
            view.Bind(item, HandleBuyClicked);
            spawned++;
        }

        if (verboseLogging)
            Debug.Log("ShopPanelPopulator: spawned " + spawned + " item(s) for category " + category + ".");
    }

    private void HandleBuyClicked(ShopItemData item)
    {
        if (item == null)
            return;

        switch (item.category)
        {
            case ShopCategory.Currency:
                if (currencyExchangeManager != null)
                    currencyExchangeManager.BuyPackage(item);
                else
                    Debug.LogWarning("ShopPanelPopulator: Currency item '" + item.itemId + "' clicked but currencyExchangeManager is not assigned.");
                break;

            case ShopCategory.Cosmetic:
                // cosmeticPurchaseManager?.BuyCosmetic(item); // wire up once CosmeticPurchaseManager exists
                if (verboseLogging)
                    Debug.Log("ShopPanelPopulator: Cosmetic item '" + item.itemId + "' clicked - no CosmeticPurchaseManager wired up yet.");
                break;

            default:
                Debug.LogWarning("ShopPanelPopulator: unhandled ShopCategory " + item.category + " for item '" + item.itemId + "'.");
                break;
        }
    }
}