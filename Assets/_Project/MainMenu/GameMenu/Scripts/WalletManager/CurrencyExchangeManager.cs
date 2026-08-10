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
/// TESTING-ONLY IMPLEMENTATION: the actual currency movement below
/// (SubtractUserVirtualCurrency then AddUserVirtualCurrency) is two
/// separate client calls, which means a dropped connection between them
/// could leave a player's GC spent with no NM granted. It is isolated in
/// ExecuteExchange_ClientSide_TEMP() specifically so it can be deleted and
/// replaced with a single call to a CloudScript function (e.g.
/// "ExchangeGoldCoinsForMoney") without touching anything else in this
/// class - see the boundary comment on that method.
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

        TryBuyPackage(exchangePackages[index]);
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

        var package = new CurrencyExchangePackage
        {
            packageName = itemData.displayName,
            goldCost = itemData.price,
            normalMoneyReward = itemData.normalMoneyReward
        };

        TryBuyPackage(package);
    }

    // ================= PURCHASE FLOW =================
    private void TryBuyPackage(CurrencyExchangePackage package)
    {
        if (IsPurchaseInProgress)
            return; // silently ignore double-clicks rather than stacking status messages

        if (PlayFabAuthManager.Instance == null || !PlayFabAuthManager.Instance.IsLoggedIn)
        {
            Fail("Not logged in.", NotificationPopupController.PopupType.Warning);
            return;
        }

        var wallet = Wallet;
        if (wallet == null || !wallet.HasLoadedWallet)
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

                // Re-sync in case GC was subtracted but NM failed to add -
                // see the warning on ExecuteExchange_ClientSide_TEMP.
                wallet.RefreshWallet();
            });
    }

    // ============================================================
    // TEMPORARY / TESTING-ONLY EXCHANGE IMPLEMENTATION
    //
    // >>> REPLACE THIS METHOD BODY when a CloudScript function exists. <<<
    //
    // Everything above this point (TryBuyPackage and its callers) should
    // stay exactly the same. Swap this method for a single call like:
    //
    //   PlayFabClientAPI.ExecuteCloudScript(
    //       new ExecuteCloudScriptRequest {
    //           FunctionName = "ExchangeGoldCoinsForMoney",
    //           FunctionParameter = new { packageName = package.packageName },
    //       },
    //       result => onSuccess(),
    //       error => onFailure(error.ErrorMessage));
    //
    // The server-side function would check the GC balance, subtract GC,
    // add NM, and return updated balances atomically - removing the
    // partial-transaction risk described below.
    // ============================================================
    private void ExecuteExchange_ClientSide_TEMP(CurrencyExchangePackage package, Action onSuccess, Action<string> onFailure)
    {
        PlayFabClientAPI.SubtractUserVirtualCurrency(
            new SubtractUserVirtualCurrencyRequest
            {
                VirtualCurrency = "GC",
                Amount = package.goldCost
            },
            _ =>
            {
                // GC has now been spent. If the AddUserVirtualCurrency call
                // below fails (e.g. connection drop), the player is left
                // with GC gone and no NM granted. This is exactly the
                // partial-transaction risk a CloudScript function avoids -
                // acceptable for testing only.
                PlayFabClientAPI.AddUserVirtualCurrency(
                    new AddUserVirtualCurrencyRequest
                    {
                        VirtualCurrency = "NM",
                        Amount = package.normalMoneyReward
                    },
                    _2 => onSuccess(),
                    addError =>
                    {
                        Debug.LogError("CurrencyExchangeManager: GC was subtracted but NM add failed: " + addError.ErrorMessage);
                        onFailure(addError.ErrorMessage);
                    }
                );
            },
            subtractError => onFailure(subtractError.ErrorMessage)
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