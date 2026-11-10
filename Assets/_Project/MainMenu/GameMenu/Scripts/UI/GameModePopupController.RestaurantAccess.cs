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
    [SerializeField, Min(1)] private int fastFoodCashPrice = 10000;
    [SerializeField, Min(1)] private int fineDiningCashPrice = 25000;
    [SerializeField, Min(1)] private int fineDiningCasualDays = 30;
    [SerializeField, Min(1)] private int fineDiningFastFoodDays = 15;
    [Tooltip("The authored Restaurants root containing the three visible restaurant models, in selection order.")]
    [SerializeField] private Transform restaurantModels;
    [SerializeField] private Material lockedRestaurantMaterial;
    [SerializeField] private Camera restaurantMenuCamera;
    [SerializeField] private TMP_Text restaurantPlayLabel;
    [SerializeField] private Sprite restaurantLockedPadlock;
    [SerializeField] private Sprite restaurantRequirementsLock;
    [SerializeField] private string restaurantLockedText = "LOCKED";
    [SerializeField] private string restaurantRequirementsText = "View Requirements";
    private readonly RectTransform[] restaurantLockMarkers = new RectTransform[3];
    private readonly Vector3[] restaurantLockPositions = new Vector3[3];
    private readonly bool[] restaurantLocked = new bool[3];
    private Canvas restaurantMenuCanvas;
    private RectTransform restaurantRequirementsContent;
    private static readonly string[] RestaurantIds = { LocalSaveManager.CasualDiningId, LocalSaveManager.FastFoodId, LocalSaveManager.FineDiningId };
    private sealed class ModelAppearance
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public MaterialPropertyBlock originalRendererOverrides;
        public MaterialPropertyBlock[] originalSlotOverrides;
        public bool darkened;
    }
    private readonly List<ModelAppearance>[] appearances = { new(), new(), new() };
    private RestaurantPurchaseView purchaseView;
    private int purchaseSelection, casualDays, fastDays;
    private bool dirty = true, confirming, purchaseWasOwned;
    private float nextRefresh;
    private string appearanceSignature, purchaseAccount, cashMessage;
    private PlayFabWalletManager subscribedWallet;

    private void OnEnable()
    {
        CampaignSaveStore.LocalWritten += ProgressChanged;
        if (restaurantSelector != null)
            restaurantSelector.OnRestaurantSelected += RefreshRestaurantButton;
        appearanceSignature = null;
    }
    private void OnDisable()
    {
        CampaignSaveStore.LocalWritten -= ProgressChanged;
        if (restaurantSelector != null)
            restaurantSelector.OnRestaurantSelected -= RefreshRestaurantButton;
        foreach (var marker in restaurantLockMarkers)
            if (marker != null) marker.gameObject.SetActive(false);
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
                appearances[i].Add(new ModelAppearance { renderer = renderer });
            }
        if (lockedRestaurantMaterial == null)
            Debug.LogError("[RestaurantAccess] Assign the locked restaurant material.", this);
        InitializeRestaurantLockPresentation();
        RefreshRestaurantAppearance();
    }
    private void RefreshRestaurantAppearance()
    {
        bool accountReady = LocalAccountReady;
        for (int i = 0; i < RestaurantIds.Length; i++)
            restaurantLocked[i] = accountReady && !LocalSaveManager.IsRestaurantUnlocked(RestaurantIds[i]);
        RefreshRestaurantButton(SelectedRestaurantIndex);
        if (!accountReady) return;
        for (int i = 0; i < RestaurantIds.Length; i++)
        {
            bool dark = restaurantLocked[i];
            foreach (var entry in appearances[i])
            {
                if (entry.renderer == null || entry.darkened == dark) continue;
                if (dark)
                {
                    if (lockedRestaurantMaterial == null) continue;
                    entry.originalMaterials = entry.renderer.sharedMaterials;
                    entry.originalRendererOverrides = new MaterialPropertyBlock();
                    entry.renderer.GetPropertyBlock(entry.originalRendererOverrides);
                    entry.originalSlotOverrides = new MaterialPropertyBlock[entry.originalMaterials.Length];
                    var lockedMaterials = new Material[entry.originalMaterials.Length];
                    for (int slot = 0; slot < lockedMaterials.Length; slot++)
                    {
                        var overrides = new MaterialPropertyBlock();
                        entry.renderer.GetPropertyBlock(overrides, slot);
                        entry.originalSlotOverrides[slot] = overrides.isEmpty ? null : overrides;
                        entry.renderer.SetPropertyBlock(null, slot);
                        lockedMaterials[slot] = lockedRestaurantMaterial;
                    }
                    entry.renderer.SetPropertyBlock(null);
                    entry.renderer.sharedMaterials = lockedMaterials;
                }
                else
                {
                    entry.renderer.sharedMaterials = entry.originalMaterials;
                    entry.renderer.SetPropertyBlock(entry.originalRendererOverrides);
                    for (int slot = 0; slot < entry.originalSlotOverrides.Length; slot++)
                        entry.renderer.SetPropertyBlock(entry.originalSlotOverrides[slot], slot);
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
        string signature = LocalAccountReady ? LocalSaveManager.Data.progressAccount + ":" + string.Join("|", LocalSaveManager.Data.unlockedRestaurantIds) : string.Empty;
        if (signature != appearanceSignature) { dirty = true; RefreshRestaurantAppearance(); appearanceSignature = signature; }
        if (purchaseView == null || !purchaseView.IsOpen) return;
        if (!LocalAccountReady || purchaseAccount != LocalSaveManager.Data.progressAccount)
        { purchaseView.Hide(); return; }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .5f; RefreshRestaurantPurchase(); }
    }
    private void InitializeRestaurantLockPresentation()
    {
        if (restaurantPlayLabel == null || restaurantMenuCamera == null ||
            restaurantLockedPadlock == null || restaurantRequirementsLock == null) return;

        restaurantMenuCanvas = restaurantPlayLabel.GetComponentInParent<Canvas>()?.rootCanvas;
        if (restaurantMenuCanvas == null) return;

        restaurantRequirementsContent = CreateRestaurantGraphic("Requirements Content", restaurantPlayLabel.rectTransform.parent,
            out Image icon, restaurantRequirementsLock);
        restaurantRequirementsContent.anchorMin = Vector2.zero;
        restaurantRequirementsContent.anchorMax = Vector2.one;
        restaurantRequirementsContent.sizeDelta = Vector2.zero;
        icon.rectTransform.sizeDelta = new Vector2(21f, 21f);
        icon.rectTransform.anchoredPosition = new Vector2(-59f, 4f);
        var requirements = CreateRestaurantLabel("Requirements Label", restaurantRequirementsContent, restaurantRequirementsText, 17f);
        requirements.rectTransform.anchorMin = Vector2.zero;
        requirements.rectTransform.anchorMax = Vector2.one;
        requirements.rectTransform.offsetMin = new Vector2(31f, 4f);
        requirements.rectTransform.offsetMax = new Vector2(-7f, 4f);
        requirements.enableAutoSizing = true;
        requirements.fontSizeMin = 12f;
        requirements.fontSizeMax = 17f;

        for (int i = 0; i < RestaurantIds.Length; i++)
        {
            Bounds? bounds = null;
            foreach (var entry in appearances[i])
            {
                if (!entry.renderer.enabled || !entry.renderer.gameObject.activeInHierarchy) continue;
                Bounds combined = bounds ?? entry.renderer.bounds;
                combined.Encapsulate(entry.renderer.bounds);
                bounds = combined;
            }
            if (!bounds.HasValue) continue;
            restaurantLockPositions[i] = bounds.Value.center + Vector3.up * (bounds.Value.size.y * .15f);
            var marker = CreateRestaurantGraphic("Restaurant Lock " + RestaurantIds[i], restaurantMenuCanvas.transform,
                out Image padlock, restaurantLockedPadlock);
            marker.SetAsFirstSibling();
            marker.sizeDelta = new Vector2(110f, 125f);
            padlock.rectTransform.sizeDelta = new Vector2(100f, 100f);
            padlock.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            var label = CreateRestaurantLabel("Locked Label", marker, restaurantLockedText, 22f);
            label.rectTransform.sizeDelta = new Vector2(110f, 26f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -46f);
            marker.gameObject.SetActive(false);
            restaurantLockMarkers[i] = marker;
        }
    }

    private static RectTransform CreateRestaurantGraphic(string name, Transform parent, out Image image, Sprite sprite)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        var graphic = new GameObject("Padlock", typeof(RectTransform), typeof(Image));
        graphic.transform.SetParent(rect, false);
        image = graphic.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI CreateRestaurantLabel(string name, Transform parent, string label, float size)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = restaurantPlayLabel.font;
        text.fontSharedMaterial = restaurantPlayLabel.fontSharedMaterial;
        text.text = label;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void RefreshRestaurantButton(int index)
    {
        if (restaurantRequirementsContent == null) return;
        bool showRequirements = index >= 0 && index < restaurantLocked.Length && restaurantLocked[index];
        restaurantRequirementsContent.gameObject.SetActive(showRequirements);
        restaurantPlayLabel.enabled = !showRequirements;
    }

    private void LateUpdate()
    {
        if (restaurantMenuCanvas == null || restaurantMenuCamera == null) return;
        var canvasRect = (RectTransform)restaurantMenuCanvas.transform;
        var canvasCamera = restaurantMenuCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : restaurantMenuCanvas.worldCamera;
        for (int i = 0; i < restaurantLockMarkers.Length; i++)
        {
            var marker = restaurantLockMarkers[i];
            if (marker == null) continue;
            Vector3 screenPoint = restaurantMenuCamera.WorldToScreenPoint(restaurantLockPositions[i]);
            bool visible = restaurantLocked[i] && restaurantPlayLabel.gameObject.activeInHierarchy &&
                screenPoint.z > 0f && RectTransformUtility.RectangleContainsScreenPoint(canvasRect, screenPoint, canvasCamera);
            marker.gameObject.SetActive(visible);
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 local))
                marker.localPosition = local;
        }
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
            purchaseView.Render(fine, price, moneyReady ? money : -1, casualDays, required, fastDays,
                fine ? fineDiningFastFoodDays : 0, message,
                unlocked ? "CONTINUE" : pending ? "CHECK ORDER" : confirming ? "CONFIRM" : "BUY RESTAURANT",
                action, unlocked, unlocked && !purchaseWasOwned);
            purchaseWasOwned = unlocked;
        } catch (Exception error) {
            Debug.LogWarning("[RestaurantAccess] Progress unavailable: " + error.Message);
            bool fine = purchaseSelection == 2;
            purchaseView.Render(fine, fine ? fineDiningCashPrice : fastFoodCashPrice, -1, 0, 0, 0, 0,
                "Your progress needs recovery. Please return to the menu.", "BUY", false, false, false);
        }
    }
}
