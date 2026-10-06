using System;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One configurable exchange package: spend goldCost Gold Coins, receive
/// normalMoneyReward Normal Money.
/// </summary>
[Serializable]
public class CurrencyExchangePackage
{
    public string packageName = "Small Pack";
    public int goldCost = 10;
    public int normalMoneyReward = 1000;
}

/// <summary>
/// Lets the player spend Gold Coins to buy Normal Money.
///
/// Single Responsibility: this script never logs in/out and never edits
/// PlayFabAuthManager. It reads PlayFabAuthManager.Instance.IsLoggedIn to
/// gate purchases, and it reads/refreshes balances only through
/// PlayFabWalletManager - it never caches its own copy of GoldCoins /
/// NormalMoney to avoid drifting out of sync with the wallet.
///
/// Notifications: every status change (failures, "Processing...",
/// "Purchase complete.") updates exchangeStatusText as before, and
/// noteworthy ones (failures/success, not routine progress text) are also
/// surfaced through the standalone NotificationPopupController via
/// ReportStatus() - see the "STATUS / NOTIFICATIONS" section below. This
/// script has zero knowledge of how the popup animates; it just calls
/// Show() the same way any other script would.
///
/// Uses the existing PlayFab Gold Coin debit, then records the confirmed
/// Campaign credit locally with a receipt to prevent duplicate application.
/// An uncertain debit response is not automatically retried; an atomic
/// server-side purchase would require existing backend support.
/// </summary>
public class CurrencyExchangeManager : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Optional override. If left empty, PlayFabWalletManager.Instance is used.")]
    [SerializeField] private PlayFabWalletManager walletManager;

    [Header("UI")]
    [SerializeField] private TMP_Text exchangeStatusText;
    [Tooltip("Optional. All buttons here are disabled while a purchase is processing and re-enabled afterward.")]
    [SerializeField] private Button[] exchangeButtons;

    [Header("Notifications")]
    [Tooltip("Optional. If left empty, NotificationPopupController.Instance is used automatically when one exists in the scene.")]
    [SerializeField] private NotificationPopupController popupOverride;
    [Tooltip("If true, purchase failures/success are also surfaced through NotificationPopupController, in addition to exchangeStatusText.")]
    [SerializeField] private bool showPopupNotifications = true;

    [Header("Packages")]
    [Tooltip("Index 0 = Small, 1 = Medium, 2 = Large by convention for the Buy*Package() shortcuts below. Add more and use BuyPackageByIndex() for anything beyond that.")]
    [SerializeField]
    private CurrencyExchangePackage[] exchangePackages = new CurrencyExchangePackage[]
    {
        new CurrencyExchangePackage { packageName = "Small Pack",  goldCost = 10,  normalMoneyReward = 1000 },
        new CurrencyExchangePackage { packageName = "Medium Pack", goldCost = 50,  normalMoneyReward = 6000 },
        new CurrencyExchangePackage { packageName = "Large Pack",  goldCost = 100, normalMoneyReward = 15000 },
    };

    [SerializeField] private ShopPanelPopulator shop;
    private readonly System.Collections.Generic.HashSet<string> uncertainAccounts = new System.Collections.Generic.HashSet<string>();
    public bool HasUncertainPurchase => PlayFabAuthManager.Instance != null && (uncertainAccounts.Contains(PlayFabAuthManager.Instance.PlayFabId) || CampaignSaveStore.HasPendingPurchaseCredit(PlayFabAuthManager.Instance.PlayFabId));
    public bool IsPurchaseInProgress { get; private set; }

    // ================= HOOK-READY EVENTS =================
    public event Action<CurrencyExchangePackage> OnExchangeSucceeded;
    public event Action<string> OnExchangeFailed; // friendly message
    // ========================================================

    private PlayFabWalletManager Wallet => walletManager != null ? walletManager : PlayFabWalletManager.Instance;
    private NotificationPopupController Popup => popupOverride != null ? popupOverride : NotificationPopupController.Instance;

    // ================= PUBLIC BUTTON HOOKS =================
    public void BuySmallPackage() => BuyPackageByIndex(0);
    public void BuyMediumPackage() => BuyPackageByIndex(1);
    public void BuyLargePackage() => BuyPackageByIndex(2);

    public void BuyPackageByIndex(int index)
    {
        if (exchangePackages == null || index < 0 || index >= exchangePackages.Length)
        {
            Fail("Invalid package.", NotificationPopupController.PopupType.Error);
            return;
        }

        if (shop != null) shop.RequestPackage(exchangePackages[index]);
        else Fail("Shop confirmation is not configured.");
    }

    /// <summary>
    /// Buys a package described by a ShopItemData asset instead of a fixed
    /// array index - lets dynamically-instantiated shop items (see
    /// ShopPanelPopulator) trigger a purchase without needing to exist in
    /// exchangePackages or the Exchange Buttons list at all. Only
    /// itemData.displayName/price/normalMoneyReward are used here; this
    /// method doesn't care how the item was categorized or displayed.
    /// </summary>
    public void BuyPackage(ShopItemData itemData)
    {
        if (itemData == null)
        {
            Fail("Invalid package.", NotificationPopupController.PopupType.Error);
            return;
        }

        if (shop != null) shop.RequestPurchase(itemData);
        else Fail("Shop confirmation is not configured.");
    }
    internal void PurchaseConfirmed(ShopItemData itemData)
    {
        if (itemData == null || itemData.category != ShopCategory.Currency || itemData.priceCurrencyCode != "GC" || itemData.price <= 0 || itemData.normalMoneyReward <= 0)
        { Fail("Invalid money package configuration."); return; }
        TryBuyPackage(new CurrencyExchangePackage { packageName = itemData.displayName, goldCost = itemData.price, normalMoneyReward = itemData.normalMoneyReward });
    }

    // ================= PURCHASE FLOW =================
    private void TryBuyPackage(CurrencyExchangePackage package)
    {
        if (!CampaignSaveStore.AtMenu)
        {
            Fail("Campaign currency purchases are available in the Main Menu.", NotificationPopupController.PopupType.Info);
            return;
        }
        if (HasUncertainPurchase) { Fail("The previous debit is unconfirmed. Contact support to reconcile it; another money-pack charge is blocked."); return; }
        if (IsPurchaseInProgress || CampaignSaveStore.IsCreditPending || (Wallet != null && Wallet.IsGoldCoinSpendInFlight))
            { Fail("Another purchase is still processing."); return; }

        if (PlayFabAuthManager.Instance == null || !PlayFabAuthManager.Instance.IsLoggedIn)
        {
            Fail("Not logged in.", NotificationPopupController.PopupType.Warning);
            return;
        }

        var wallet = Wallet;
        if (wallet == null || !wallet.IsCurrentWallet)
        {
            Fail("Wallet still loading.", NotificationPopupController.PopupType.Info);
            return;
        }

        if (!wallet.HasEnoughGoldCoins(package.goldCost))
        {
            Fail("Not enough Gold Coins.", NotificationPopupController.PopupType.Warning);
            return;
        }

        IsPurchaseInProgress = true;
        SetButtonsInteractable(false);
        SetStatus("Processing...");

        ExecuteExchange_ClientSide_TEMP(package,
            onSuccess: () =>
            {
                IsPurchaseInProgress = false;
                SetButtonsInteractable(true);
                ReportStatus("Purchase complete.", NotificationPopupController.PopupType.Success);

                wallet.RefreshWallet();
                OnExchangeSucceeded?.Invoke(package);
            },
            onFailure: message =>
            {
                IsPurchaseInProgress = false;
                SetButtonsInteractable(true);
                Fail("Purchase failed: " + message, NotificationPopupController.PopupType.Error);

                wallet.RefreshWallet();
            });
    }

    // The existing premium spend stays online; confirmed rewards go to the Campaign wallet.
    private void ExecuteExchange_ClientSide_TEMP(CurrencyExchangePackage package, Action onSuccess, Action<string> onFailure)
    {
        string account = PlayFabAuthManager.Instance.PlayFabId;
        try
        {
            if (!CampaignSaveStore.AccountMatches(account))
            {
                onFailure("This Campaign belongs to another account.");
                return;
            }
        }
        catch (Exception e) { onFailure(e.Message); return; }
        string receipt = Guid.NewGuid().ToString("N");
        string restaurant = CampaignSaveStore.RestaurantScene;
        string savePath = CampaignSaveStore.SavePath;
        try { CampaignSaveStore.RecordPendingDebit(account, receipt, package.normalMoneyReward, restaurant); }
        catch (Exception e) { onFailure("Could not prepare the purchase receipt. No charge submitted. " + e.Message); return; }
        CampaignSaveStore.IsCreditPending = true;
        PlayFabClientAPI.SubtractUserVirtualCurrency(
            new SubtractUserVirtualCurrencyRequest
            {
                VirtualCurrency = "GC",
                AuthenticationContext = PlayFabSettings.staticPlayer,
                Amount = package.goldCost
            },
            result =>
            {
                Wallet?.ApplyConfirmedGoldBalance(account, result.Balance);
                try
                {
                    CampaignSaveStore.QueueConfirmedCredit(account, package.normalMoneyReward, receipt, restaurant, savePath);
                    if (CampaignSaveStore.IsCreditQueued(receipt)) onFailure("GC charged; your confirmed Campaign credit is saved and waiting for recovery in the original Campaign.");
                    else onSuccess();
                }
                catch (Exception e)
                {
                    CampaignSaveStore.IsCreditPending = false;
                    onFailure("Gold Coins charged; Campaign credit needs recovery: " + e.Message);
                }
            },
            subtractError =>
            {
                CampaignSaveStore.IsCreditPending = false;
                if (PlayFabWalletManager.IsUncertainPurchaseError(subtractError)) uncertainAccounts.Add(account);
                if (!PlayFabWalletManager.IsUncertainPurchaseError(subtractError))
                {
                    try { CampaignSaveStore.ClearRejectedDebit(receipt); }
                    catch (Exception e) { Debug.LogWarning("Purchase rejected; receipt cleanup needs recovery: " + e.Message); }
                }
                onFailure(PlayFabWalletManager.IsUncertainPurchaseError(subtractError)
                    ? "Debit status is uncertain. Wallet will refresh, but it cannot prove the Campaign grant. Contact support before retrying."
                    : subtractError.ErrorMessage);
            }
        );
    }

    // ================= STATUS / NOTIFICATIONS =================
    private void Fail(string message, NotificationPopupController.PopupType type = NotificationPopupController.PopupType.Warning)
    {
        ReportStatus(message, type);
        OnExchangeFailed?.Invoke(message);
    }

    private void SetStatus(string message)
    {
        if (exchangeStatusText != null)
            exchangeStatusText.text = message;
    }

    /// <summary>
    /// Updates exchangeStatusText and - if showPopupNotifications is true
    /// and a NotificationPopupController exists - also surfaces the message
    /// as a popup. Used for noteworthy outcomes (failures/success), not for
    /// routine progress text like "Processing...".
    /// </summary>
    private void ReportStatus(string message, NotificationPopupController.PopupType type)
    {
        SetStatus(message);

        if (showPopupNotifications && !string.IsNullOrEmpty(message))
            Popup?.Show(message, type);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (exchangeButtons == null) return;

        foreach (var button in exchangeButtons)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }
}
