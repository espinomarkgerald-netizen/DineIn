using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class GameModePopupController
{
    [Header("Restaurant access")]
    [SerializeField, Min(1)] private int fastFoodCasualDays = 10;
    [SerializeField, Min(1)] private int fastFoodCashPrice = 10000;
    [SerializeField, Min(1)] private int fineDiningCashPrice = 25000;
    [SerializeField, Min(1)] private int fineDiningCasualDays = 30;
    [SerializeField, Min(1)] private int fineDiningFastFoodDays = 15;
    [Tooltip("The authored Restaurants root containing the three visible restaurant models, in selection order.")]
    [SerializeField] private Transform restaurantModels;
    private static readonly string[] RestaurantIds = { LocalSaveManager.CasualDiningId, LocalSaveManager.FastFoodId, LocalSaveManager.FineDiningId };
    private sealed class ModelAppearance
    {
        public Renderer renderer;
        public MaterialPropertyBlock[] original;
        public bool darkened;
    }
    private readonly List<ModelAppearance>[] appearances = { new(), new(), new() };
    private RestaurantPurchaseView purchaseView;
    private int purchaseSelection, casualDays, fastDays;
    private bool dirty = true, confirming, purchaseWasOwned;
    private float nextRefresh;
    private string appearanceSignature, purchaseAccount, cashMessage;
    private PlayFabWalletManager subscribedWallet;

    private void OnEnable() { CampaignSaveStore.LocalWritten += ProgressChanged; }
    private void OnDisable()
    {
        CampaignSaveStore.LocalWritten -= ProgressChanged;
        if (subscribedWallet != null) subscribedWallet.OnWalletUpdated -= WalletUpdated;
        subscribedWallet = null;
        restaurantSelector?.SetPurchaseOpen(false);
        if (purchaseView != null) purchaseView.gameObject.SetActive(false);
    }
    private void ProgressChanged() { dirty = true; appearanceSignature = null; }
    private bool LocalAccountReady
    {
        get
        {
            var data = LocalSaveManager.Data;
            var auth = PlayFabAuthManager.Instance;
            return !LocalSaveManager.NeedsRecovery && data.unlockedRestaurantIds != null &&
                data.unlockedRestaurantIds.Contains(LocalSaveManager.CasualDiningId) &&
                (auth?.IsLoggedIn != true || data.progressAccount == auth.PlayFabId);
        }
    }
    public bool EnsureRestaurantAccess()
    {
        if (!CampaignCloudSync.CanEnterGameplay()) return false;
        if (!LocalAccountReady)
        {
            NotificationPopupController.Instance?.Show(LocalSaveManager.Data.unlockedRestaurantIds?.Contains(LocalSaveManager.CasualDiningId) != true
                    ? "Your restaurant access needs recovery. Please reopen the game to reload your saved profile."
                    : "Your restaurant progress is still loading. Please try again shortly.", NotificationPopupController.PopupType.Info);
            return false;
        }
        if (SelectedRestaurantIndex < 0 || SelectedRestaurantIndex >= RestaurantIds.Length) return false;
        if (LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[SelectedRestaurantIndex])) return true;
        ShowRestaurantPurchase(); return false;
    }
    private void InitializeRestaurantAccess()
    {
        // The old stop discs are disabled by district authoring. Bind the visible buildings instead.
        if (restaurantModels == null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == "Restaurants") { restaurantModels = candidate; break; }
        if (restaurantModels == null || restaurantModels.childCount < RestaurantIds.Length)
        { Debug.LogError("[RestaurantAccess] Assign the authored Restaurants model root.", this); return; }
        for (int i = 0; i < RestaurantIds.Length; i++)
            foreach (var renderer in restaurantModels.GetChild(i).GetComponentsInChildren<Renderer>(true))
            {
                var entry = new ModelAppearance { renderer = renderer, original = new MaterialPropertyBlock[renderer.sharedMaterials.Length] };
                for (int slot = 0; slot < entry.original.Length; slot++)
                { entry.original[slot] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(entry.original[slot], slot); }
                appearances[i].Add(entry);
            }
        RefreshRestaurantAppearance();
    }
    private void RefreshRestaurantAppearance()
    {
        if (!LocalAccountReady) { appearanceSignature = null; return; }
        for (int i = 0; i < RestaurantIds.Length; i++)
        {
            bool dark = !LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[i]);
            foreach (var entry in appearances[i])
            {
                if (entry.renderer == null || entry.darkened == dark) continue;
                for (int slot = 0; slot < entry.original.Length; slot++)
                {
                    if (!dark) { entry.renderer.SetPropertyBlock(entry.original[slot], slot); continue; }
                    var block = new MaterialPropertyBlock(); entry.renderer.GetPropertyBlock(block, slot);
                    block.SetColor("_BaseColor", Color.black); block.SetColor("_Color", Color.black);
                    block.SetColor("_EmissionColor", Color.black); block.SetColor("_SpecColor", Color.black);
                    block.SetFloat("_Smoothness", 0f); block.SetFloat("_Metallic", 0f);
                    entry.renderer.SetPropertyBlock(block, slot);
                }
                entry.darkened = dark;
            }
        }
    }
    private void Update()
    {
        var wallet = PlayFabWalletManager.Instance;
        if (wallet != subscribedWallet)
        {
            if (subscribedWallet != null) subscribedWallet.OnWalletUpdated -= WalletUpdated;
            subscribedWallet = wallet;
            if (wallet != null) wallet.OnWalletUpdated += WalletUpdated;
        }
        string signature = LocalAccountReady ? LocalSaveManager.Data.progressAccount + ":" + string.Join("|", LocalSaveManager.Data.unlockedRestaurantIds) : null;
        if (signature != appearanceSignature) { dirty = true; RefreshRestaurantAppearance(); appearanceSignature = signature; }
        if (purchaseView == null || !purchaseView.IsOpen) return;
        if (!LocalAccountReady || purchaseAccount != LocalSaveManager.Data.progressAccount)
        { purchaseView.Hide(); return; }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .5f; RefreshRestaurantPurchase(); }
    }
    private void WalletUpdated(int gc, int money)
    {
        if (purchaseView == null || !purchaseView.IsOpen) return;
        RefreshRestaurantPurchase();
    }
    private static int SuccessfulDays(string file)
    {
        string path = Path.Combine(CampaignSaveStore.LocalDirectory, file);
        if (!File.Exists(path)) return 0;
        var save = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        return save?.successfulRestaurantDays == null ? 0 : new HashSet<int>(save.successfulRestaurantDays.FindAll(day => day > 0)).Count;
    }
    private static int MultiDays(string id) => LocalSaveManager.Data.ownedItemIds.FindAll(x => x != null && x.StartsWith("restaurant-day:" + id + ":", StringComparison.Ordinal)).Count;
    private void ShowRestaurantPurchase()
    {
        HidePopup();
        if (purchaseView == null)
        {
            var shop = FindFirstObjectByType<ShopPanelPopulator>(FindObjectsInactive.Include);
            if (shop != null && gamemodePopupUI != null) purchaseView = shop.CreateRestaurantPurchaseView(gamemodePopupUI.transform.parent);
            if (purchaseView == null)
            {
                Debug.LogError("[RestaurantAccess] Authored shop purchase card is unavailable.", this);
                NotificationPopupController.Instance?.Show("Restaurant details are unavailable. Please reopen the menu.", NotificationPopupController.PopupType.Warning);
                return;
            }
            purchaseView.Buy = PurchaseSelected;
            purchaseView.Closed = () => restaurantSelector?.SetPurchaseOpen(false);
        }
        purchaseSelection = SelectedRestaurantIndex; purchaseAccount = LocalSaveManager.Data.progressAccount;
        confirming = false; dirty = true; cashMessage = null;
        purchaseWasOwned = LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[purchaseSelection]);
        purchaseView.Show(); restaurantSelector?.SetPurchaseOpen(true);
        var wallet = PlayFabWalletManager.Instance;
        if (wallet != null && wallet.HasPendingRestaurantPurchase(RestaurantIds[purchaseSelection]))
            wallet.RecoverRestaurantPurchase(RestaurantIds[purchaseSelection], RefreshRestaurantPurchase);
        RefreshRestaurantPurchase();
    }
    private void PurchaseSelected()
    {
        if (!LocalAccountReady || purchaseAccount != LocalSaveManager.Data.progressAccount) return;
        var wallet = PlayFabWalletManager.Instance;
        string id = RestaurantIds[purchaseSelection];
        if (LocalSaveManager.IsRestaurantUnlocked(id)) { purchaseView.Hide(); return; }
        if (wallet == null || wallet.RestaurantPurchaseBusy || wallet.IsGoldCoinSpendInFlight) return;
        if (wallet.HasPendingRestaurantPurchase(id)) { wallet.RecoverRestaurantPurchase(id, RefreshRestaurantPurchase); return; }
        bool fine = purchaseSelection == 2;
        int price = fine ? fineDiningCashPrice : fastFoodCashPrice;
        if (!CampaignSaveStore.TryGetCombinedMoney(out long money, out _, true) || money < price) return;
        // Refresh successful-day counts again at confirmation; the store also revalidates the snapshot.
        casualDays = SuccessfulDays("dinein_save.json") + MultiDays(LocalSaveManager.CasualDiningId);
        fastDays = SuccessfulDays("dinein_fastfood_save.json") + MultiDays(LocalSaveManager.FastFoodId);
        if (casualDays < (fine ? fineDiningCasualDays : fastFoodCasualDays) || fine && fastDays < fineDiningFastFoodDays) return;
        if (!confirming) { confirming = true; RefreshRestaurantPurchase(); return; }
        confirming = false;
        wallet.PurchaseRestaurant(id, price, fine ? fineDiningCasualDays : fastFoodCasualDays,
            fine ? fineDiningFastFoodDays : 0, () => { cashMessage = wallet.RestaurantPurchaseStatus; RefreshRestaurantPurchase(); });
        RefreshRestaurantPurchase();
    }
    private void RefreshRestaurantPurchase()
    {
        if (purchaseView == null || !purchaseView.IsOpen || purchaseAccount != LocalSaveManager.Data.progressAccount) return;
        try {
            if (dirty) {
                casualDays = SuccessfulDays("dinein_save.json") + MultiDays(LocalSaveManager.CasualDiningId);
                fastDays = SuccessfulDays("dinein_fastfood_save.json") + MultiDays(LocalSaveManager.FastFoodId); dirty = false;
            }
            bool fine = purchaseSelection == 2; int required = fine ? fineDiningCasualDays : fastFoodCasualDays;
            int price = fine ? fineDiningCashPrice : fastFoodCashPrice;
            var wallet = PlayFabWalletManager.Instance;
            bool moneyReady = CampaignSaveStore.TryGetCombinedMoney(out long money, out var moneyError);
            bool ready = LocalAccountReady && PlayFabAuthManager.Instance?.IsLoggedIn == true && moneyReady;
            bool busy = CampaignSaveStore.IsCreditPending || wallet != null && (wallet.RestaurantPurchaseBusy || wallet.IsGoldCoinSpendInFlight);
            bool pending = wallet != null && wallet.HasPendingRestaurantPurchase(RestaurantIds[purchaseSelection]);
            bool unlocked = LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[purchaseSelection]);
            bool experienced = casualDays >= required && (!fine || fastDays >= fineDiningFastFoodDays);
            string requirements = "SUCCESSFUL DAYS • CAMPAIGN / MULTIPLAYER\nCasual Dining    " + casualDays + " / " + required +
                (fine ? "\nFast Food    " + fastDays + " / " + fineDiningFastFoodDays : "");
            string message = unlocked ? "This restaurant is yours. Select Play to choose a game mode." :
                pending ? !wallet.IsCurrentWallet ? "Connect and refresh your wallet to check the previous GC order." :
                    wallet.RestaurantPurchaseStatus ?? "Check your previous GC order before paying with cash." :
                !ready ? moneyError ?? "Sign in and finish loading your restaurant funds." :
                busy ? "Saving your purchase…" :
                !experienced ? "Complete the successful-day requirements to expand." :
                money < price ? "You need ₱" + (price - money).ToString("N0") + " more Normal Money." :
                confirming ? "Confirm purchase? Cash is drawn proportionally from your owned restaurants." :
                cashMessage ?? "Uses combined Normal Money. GC is not charged.";
            bool action = unlocked || ready && !busy && wallet != null && (pending ? wallet.IsCurrentWallet : experienced && money >= price);
            purchaseView.Render(fine ? "FINE DINING" : "FAST FOOD", price, moneyReady ? money : -1, requirements, message,
                unlocked ? "CONTINUE" : pending ? "CHECK ORDER" : confirming ? "CONFIRM" : "BUY RESTAURANT", action, unlocked && !purchaseWasOwned);
            purchaseWasOwned = unlocked;
        } catch (Exception error) {
            Debug.LogWarning("[RestaurantAccess] Progress unavailable: " + error.Message);
            purchaseView.Render(purchaseSelection == 2 ? "FINE DINING" : "FAST FOOD", purchaseSelection == 2 ? fineDiningCashPrice : fastFoodCashPrice,
                0, "", "Your progress needs recovery. Please return to the menu.", "BUY", false, false);
        }
    }
}
