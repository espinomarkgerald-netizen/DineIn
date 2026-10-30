using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameModePopupController
{
    [Header("Restaurant access")]
    [SerializeField, Min(1)] private int fastFoodCasualDays = 10;
    [SerializeField, Min(1)] private int fineDiningCasualDays = 30;
    [SerializeField, Min(1)] private int fineDiningFastFoodDays = 15;
    private static readonly string[] RestaurantIds = { LocalSaveManager.CasualDiningId, LocalSaveManager.FastFoodId, LocalSaveManager.FineDiningId };
    private readonly Dictionary<SpriteRenderer, Color> restaurantColors = new();
    private GameObject restaurantPurchasePanel;
    private TMP_Text restaurantPurchaseText;
    private int purchaseSelection;
    private Button buy;
    private bool confirming, dirty = true;
    private int quote, casualDays, fastDays;
    private string quoteError, ownership;
    private float nextRefresh;
    private void OnEnable() { CampaignSaveStore.LocalWritten += ProgressChanged; }
    private void OnDisable() { CampaignSaveStore.LocalWritten -= ProgressChanged; }
    private void ProgressChanged() { dirty = true; }

    private string accessAccount;

    public bool EnsureRestaurantAccess()
    {
        if (HasRestaurantAccess()) return true;
        ShowRestaurantPurchase(); return false;
    }
    private bool HasRestaurantAccess() => SelectedRestaurantIndex >= 0 && SelectedRestaurantIndex < RestaurantIds.Length &&
        LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[SelectedRestaurantIndex]);

    private void InitializeRestaurantAccess()
    {
        // These are the authored restaurant presentation sprites, not shared material assets.
        if (restaurantSelector == null) return;
        foreach (var point in restaurantSelector.travelPoints)
            if (point != null && point.TryGetComponent<SpriteRenderer>(out var sprite)) restaurantColors[sprite] = sprite.color;
        RefreshRestaurantAppearance();
    }

    private void RefreshRestaurantAppearance()
    {
        if (restaurantSelector == null) return;
        for (int i = 0; i < restaurantSelector.travelPoints.Length && i < RestaurantIds.Length; i++)
        {
            var point = restaurantSelector.travelPoints[i];
            if (point == null || !point.TryGetComponent<SpriteRenderer>(out var sprite) || !restaurantColors.TryGetValue(sprite, out var color)) continue;
            sprite.color = LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[i]) ? color : new Color(0, 0, 0, color.a);
        }
    }

    private void Update()
    {
        string account = LocalSaveManager.Data.progressAccount;
        if (accessAccount != account) { accessAccount = account; dirty = true; RefreshRestaurantAppearance(); HidePopup(); if (restaurantPurchasePanel != null) restaurantPurchasePanel.SetActive(false); }
        string signature = string.Join("|", LocalSaveManager.Data.unlockedRestaurantIds ?? new List<string>());
        if (ownership != signature) { ownership = signature; RefreshRestaurantAppearance(); }
        if (restaurantPurchasePanel != null && restaurantPurchasePanel.activeSelf && Time.unscaledTime >= nextRefresh)
        { nextRefresh = Time.unscaledTime + .5f; RefreshRestaurantPurchase(); }
    }

    private static int SuccessfulDays(string file)
    {
        string path = Path.Combine(CampaignSaveStore.LocalDirectory, file);
        if (!File.Exists(path)) return 0;
        // A checkpoint rollback must not inflate experience. Only committed career results count.
        var save = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        return save?.successfulRestaurantDays == null ? 0 : new HashSet<int>(save.successfulRestaurantDays.FindAll(day => day > 0)).Count;
    }

    private void ShowRestaurantPurchase()
    {
        HidePopup();
        purchaseSelection = SelectedRestaurantIndex; quote = 0; quoteError = null; confirming = false; dirty = true;
        if (gamemodePopupUI == null) return;
        if (restaurantPurchasePanel == null)
        {
            restaurantPurchasePanel = new GameObject("RestaurantPurchasePanel", typeof(RectTransform), typeof(Image));
            restaurantPurchasePanel.transform.SetParent(gamemodePopupUI.transform.parent, false);
            var rect = (RectTransform)restaurantPurchasePanel.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            restaurantPurchasePanel.GetComponent<Image>().color = new Color(.025f, .035f, .06f, .98f);
            var text = new GameObject("Requirements", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(rect, false);
            var tr = (RectTransform)text.transform; tr.anchorMin = new Vector2(.12f,.25f); tr.anchorMax = new Vector2(.88f,.85f); tr.offsetMin = tr.offsetMax = Vector2.zero;
            restaurantPurchaseText = text.GetComponent<TMP_Text>(); restaurantPurchaseText.fontSize = 26; restaurantPurchaseText.enableAutoSizing = true; restaurantPurchaseText.fontSizeMin = 14;
            restaurantPurchaseText.alignment = TextAlignmentOptions.Center;
            buy = CreateRestaurantButton(rect, "Buy", .3f, PurchaseSelected);
            CreateRestaurantButton(rect, "Back", .7f, () => restaurantPurchasePanel.SetActive(false));
        }
        restaurantPurchasePanel.transform.SetAsLastSibling(); restaurantPurchasePanel.SetActive(true);
        var wallet = PlayFabWalletManager.Instance;
        string id = RestaurantIds[purchaseSelection];
        wallet?.GetRestaurantQuote(id, (price, error) => { if (RestaurantIds[purchaseSelection] != id) return; quote = price; quoteError = error; RefreshRestaurantPurchase(); });
        if (wallet != null && wallet.HasPendingRestaurantPurchase(id)) wallet.RecoverRestaurantPurchase(id, RefreshRestaurantPurchase);
        RefreshRestaurantPurchase();
    }

    private static Button CreateRestaurantButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
        var root = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); root.transform.SetParent(parent, false);
        var r = (RectTransform)root.transform; r.anchorMin = new Vector2(x-.16f,.1f); r.anchorMax = new Vector2(x+.16f,.2f); r.offsetMin = r.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(.15f,.25f,.35f);
        var button = root.GetComponent<Button>(); button.interactable = action != null; if (action != null) button.onClick.AddListener(action);
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(root.transform, false);
        var tr = (RectTransform)text.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var tmp = text.GetComponent<TMP_Text>(); tmp.text = label; tmp.fontSize = 20; tmp.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static int MultiDays(string id) => LocalSaveManager.Data.ownedItemIds.FindAll(x => x != null && x.StartsWith("restaurant-day:" + id + ":", StringComparison.Ordinal)).Count;
    private void PurchaseSelected()
    {
        RefreshRestaurantPurchase();
        if (!buy.interactable) return;
        var wallet = PlayFabWalletManager.Instance;
        string id = RestaurantIds[purchaseSelection];
        if (wallet.HasPendingRestaurantPurchase(id)) { wallet.RecoverRestaurantPurchase(id, RefreshRestaurantPurchase); return; }
        if (!confirming) { confirming = true; RefreshRestaurantPurchase(); return; }
        confirming = false;
        wallet.PurchaseRestaurant(id, quote, RefreshRestaurantPurchase);
        RefreshRestaurantPurchase();
    }

    private void RefreshRestaurantPurchase()
    {
        try
        {
            if (dirty)
            {
                casualDays = SuccessfulDays("dinein_save.json") + MultiDays(LocalSaveManager.CasualDiningId);
                fastDays = SuccessfulDays("dinein_fastfood_save.json") + MultiDays(LocalSaveManager.FastFoodId);
                dirty = false;
            }
            int casual = casualDays, fast = fastDays;
            bool fine = purchaseSelection == 2;
            int required = fine ? fineDiningCasualDays : fastFoodCasualDays;
            int price = quote;
            var wallet = PlayFabWalletManager.Instance;
            string balance = wallet != null && wallet.IsCurrentWallet ? wallet.GoldCoins + " GC" : "Loading / sign in required";
            restaurantPurchaseText.text = (fine ? "Fine Dining" : "Fast Food") + "\nPrice: " + (price > 0 ? price + " GC" : "Not published") +
                "\nBalance: " + balance + "\nCasual Dining: " + casual + " / " + required + " successful days" +
                (fine ? "\nFast Food: " + fast + " / " + fineDiningFastFoodDays + " successful days" : "") +
                "\n" + (casual < required ? "Complete " + (required-casual) + " more Casual Dining days. " : "") +
                (fine && fast < fineDiningFastFoodDays ? "Complete " + (fineDiningFastFoodDays-fast) + " more Fast Food days. " : "") +
                (price > 0 && wallet != null && wallet.IsCurrentWallet && wallet.GoldCoins < price ? "Insufficient GC. " : "") +
                "\n" + (quoteError ?? wallet?.RestaurantPurchaseStatus ?? "");
            string id = RestaurantIds[purchaseSelection];
            bool pending = wallet != null && wallet.HasPendingRestaurantPurchase(id);
            bool unlocked = LocalSaveManager.IsRestaurantUnlocked(id);
            buy.interactable = !unlocked && !LocalSaveManager.NeedsRecovery && wallet != null && !wallet.RestaurantPurchaseBusy && !wallet.IsGoldCoinSpendInFlight && !wallet.IsRefreshing &&
                (pending || (price > 0 && wallet.IsCurrentWallet && wallet.GoldCoins >= price && casual >= required && (!fine || fast >= fineDiningFastFoodDays)));
            buy.GetComponentInChildren<TMP_Text>().text = unlocked ? "Unlocked" : pending ? "Recover purchase" : confirming ? "Confirm " + quote + " GC" : "Buy";
        }
        catch (Exception) { if (buy != null) buy.interactable = false; restaurantPurchaseText.text = "Progress needs recovery. Purchases are unavailable. Use Back to return."; }
    }
}
