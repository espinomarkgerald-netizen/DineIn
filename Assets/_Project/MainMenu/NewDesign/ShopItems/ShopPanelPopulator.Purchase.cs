using System.Collections;
using TMPro;
using UnityEngine;

public partial class ShopPanelPopulator
{
    [Header("Purchase confirmation")]
    [SerializeField] private GameObject purchaseRoot, successCheck;
    [SerializeField] private AnimatedPanel purchasePanel;
    [SerializeField] private UnityEngine.UI.Image purchaseIcon;
    [SerializeField] private TMP_Text purchaseHeading, purchaseName, purchaseDetails, purchaseStatus, confirmLabel, cancelLabel;
    [SerializeField] private UnityEngine.UI.Button confirmButton, cancelButton, purchaseClose;
    [SerializeField, Min(.5f)] private float successSeconds = 1.8f;
    [SerializeField, Range(0, .25f)] private float successPop = .12f;
    [SerializeField] private AudioSource successAudio;
    [SerializeField] private AudioClip successSound;
    private ShopItemData selectedItem;
    private string selectedAccount, error;
    private int confirmedPrice, confirmedReward, quoteVersion, balanceBefore;
    private bool checking, submitted, success;
    private Coroutine feedback;

    public void RequestPurchase(ShopItemData item)
    {
        if (item == null || !allItems.Contains(item) || submitted) return;
        if (!gameObject.activeInHierarchy) { if (animatedPanel != null) animatedPanel.Open(); else gameObject.SetActive(true); }
        SubscribeWallet();
        selectedItem = item; selectedAccount = PlayerCustomizationData.AccountId;
        success = false; error = null; confirmedPrice = item.price; confirmedReward = item.normalMoneyReward;
        balanceBefore = wallet != null ? wallet.GoldCoins : 0;
        purchaseRoot.SetActive(true); purchaseRoot.transform.SetAsLastSibling();
        purchasePanel?.Open();
        CheckQuote();
    }
    private string EligibilityError()
    {
        if (wallet == null || !wallet.IsCurrentWallet) return "Sign in and refresh your wallet before buying.";
        if (selectedAccount != PlayerCustomizationData.AccountId) return "Account changed. Open confirmation again.";
        if (selectedItem.priceCurrencyCode != "GC" || confirmedPrice <= 0) return "This item has an invalid GC price.";
        if (wallet.IsGoldCoinSpendInFlight || (currencyExchangeManager != null && currencyExchangeManager.IsPurchaseInProgress) || CampaignSaveStore.IsCreditPending) return "Another purchase is still processing.";
        if (selectedItem.category == ShopCategory.Cosmetic && PlayerCustomizationData.OwnsCostume(selectedItem.cosmeticUnlockId)) return "You already own this costume.";
        if (selectedItem.category == ShopCategory.Cosmetic && wallet.IsCostumeUncertain(selectedItem.itemId)) return "The previous purchase is unconfirmed. Refresh inventory; another charge is blocked.";
        if (selectedItem.category == ShopCategory.Currency && currencyExchangeManager != null && currencyExchangeManager.HasUncertainPurchase) return "A previous money-pack debit is unconfirmed. Contact support before retrying.";
        if (wallet.GoldCoins < confirmedPrice) return "Not enough Gold Coins for this item.";
        return null;
    }
    private void CheckQuote()
    {
        int token = ++quoteVersion;
        checking = false; error = null;
        if (wallet == null || !wallet.IsCurrentWallet) { wallet?.RefreshWallet(); error = "Wallet is not ready. Refresh and try again."; RenderPurchase(); return; }
        if (selectedItem.category == ShopCategory.Currency) { confirmedPrice = selectedItem.price; confirmedReward = selectedItem.normalMoneyReward; RenderPurchase(); return; }
        checking = true; RenderPurchase();
        wallet.GetCostumeQuote(selectedItem, (price, failure) =>
        {
            if (this == null || selectedItem == null || token != quoteVersion) return;
            checking = false; error = failure;
            if (failure != null) Debug.LogWarning("[Shop] " + failure);
            if (failure == null) { confirmedPrice = price; quotedPrices[selectedItem] = price; RefreshCards(); }
            RenderPurchase();
        });
    }
    private void RenderPurchase()
    {
        if (selectedItem == null || purchaseRoot == null) return;
        purchaseIcon.sprite = selectedItem.icon;
        purchaseName.text = selectedItem.displayName;
        purchaseHeading.text = success ? "Purchased!" : "Confirm Purchase";
        successCheck.SetActive(success);
        purchaseDetails.text = success
            ? (selectedItem.category == ShopCategory.Currency ? "+" + confirmedReward.ToString("N0") + " money" : "Item unlocked!")
            : confirmedPrice.ToString("N0") + " GC";
        string reason = success || submitted || checking ? null : error ?? EligibilityError();
        purchaseStatus.text = success ? "" : submitted ? "Purchasing..." : checking ? "Checking item..."
            : reason != null ? PurchaseMessage(reason) : "Buy this item?";
        confirmButton.gameObject.SetActive(!success);
        confirmLabel.text = submitted ? "Purchasing..." : error != null ? "Try again" : "Buy";
        confirmButton.interactable = !submitted && !checking && (error != null || reason == null);
        cancelLabel.text = success || submitted ? "Close" : "Cancel";
    }
    // Presentation only: retain full diagnostics and the existing eligibility/retry safeguards.
    private static string PurchaseMessage(string detail)
    {
        string message = (detail ?? "").ToLowerInvariant();
        if (message.Contains("not enough")) return "Not enough GC.";
        if (message.Contains("already own")) return "Already owned.";
        if (message.Contains("unconfirmed") || message.Contains("uncertain") || message.Contains("gc charged") || message.Contains("gold coins charged"))
            return "Purchase pending. Contact support before retrying.";
        if (message.Contains("store setup") || message.Contains("item not found")) return "Item not found. Try later.";
        if (message.Contains("catalog") || message.Contains("not configured") || message.Contains("invalid")) return "Item unavailable. Try later.";
        if (message.Contains("account changed")) return "Account changed. Reopen the shop.";
        if (message.Contains("sign in") || message.Contains("not logged in")) return "Sign in to purchase.";
        if (message.Contains("wallet") || message.Contains("profile")) return "Wallet unavailable. Try again.";
        if (message.Contains("processing")) return "Another purchase is processing.";
        if (message.Contains("verify ownership")) return "Could not check ownership. Try again.";
        return "Purchase failed. Try again.";
    }
    private void ConfirmPurchase()
    {
        if (selectedItem == null || submitted || checking || success) return;
        if (error != null) { CheckQuote(); return; }
        string reason = EligibilityError();
        if (reason != null) { error = reason; RenderPurchase(); return; }
        if (selectedItem.category == ShopCategory.Currency)
        {
            if (confirmedPrice != selectedItem.price || confirmedReward != selectedItem.normalMoneyReward)
            { CheckQuote(); purchaseStatus.text = "Package changed. Please confirm again."; return; }
            if (currencyExchangeManager == null) { PurchaseFailed("Money purchases are not configured."); return; }
            submitted = true; RenderPurchase(); RefreshCards(); currencyExchangeManager.PurchaseConfirmed(selectedItem); return;
        }
        int token = ++quoteVersion; checking = true; RenderPurchase();
        wallet.GetCostumeQuote(selectedItem, (price, failure) =>
        {
            if (this == null || selectedItem == null || token != quoteVersion) return;
            checking = false;
            if (failure != null) { PurchaseFailed(failure); return; }
            if (price != confirmedPrice)
            { confirmedPrice = price; quotedPrices[selectedItem] = price; RenderPurchase(); RefreshCards(); purchaseStatus.text = "Price changed. Please confirm again."; return; }
            string eligibility = EligibilityError(); if (eligibility != null) { PurchaseFailed(eligibility); return; }
            submitted = true; RenderPurchase(); RefreshCards();
            wallet.TryPurchaseCostume(selectedItem, confirmedPrice, PurchaseSucceeded, PurchaseFailed);
        });
    }
    private void MoneySucceeded(CurrencyExchangePackage package)
    {
        if (selectedItem != null && submitted && selectedItem.category == ShopCategory.Currency) PurchaseSucceeded();
    }
    private void PurchaseSucceeded()
    {
        if (this == null || selectedItem == null || selectedAccount != PlayerCustomizationData.AccountId) return;
        submitted = checking = false; success = true; error = null; RefreshCards();
        SetShopStatus(selectedItem.displayName + " purchased."); RenderPurchase();
        if (isActiveAndEnabled) BeginSuccessAnimation();
    }
    private void PurchaseFailed(string message)
    {
        if (this == null || selectedItem == null || selectedAccount != PlayerCustomizationData.AccountId) return;
        submitted = checking = false; error = message; RefreshCards(); RenderPurchase(); SetShopStatus(PurchaseMessage(message));
        Debug.LogWarning("[Shop] " + message);
    }
    private void BeginSuccessAnimation()
    {
        if (feedback != null) StopCoroutine(feedback);
        if (successAudio != null && successSound != null) successAudio.PlayOneShot(successSound);
        feedback = StartCoroutine(SuccessFeedback());
    }
    private IEnumerator SuccessFeedback()
    {
        float elapsed = 0;
        while (elapsed < successSeconds && selectedItem != null && success)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .4f);
            purchaseIcon.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * successPop);
            if (wallet != null && wallet.IsCurrentWallet) balanceText.text = "G" + Mathf.RoundToInt(Mathf.Lerp(balanceBefore, wallet.GoldCoins, Mathf.Clamp01(elapsed / .6f))).ToString("N0");
            yield return null;
        }
        feedback = null; DismissPurchase();
    }
    private void DismissPurchase()
    {
        if (submitted) { purchaseRoot.SetActive(false); return; } // UI can close; the manager still owns settlement.
        quoteVersion++; selectedItem = null; checking = success = false;
        if (feedback != null) StopCoroutine(feedback); feedback = null;
        purchaseIcon.rectTransform.localScale = Vector3.one;
        purchaseRoot.SetActive(false); RefreshWalletView();
    }
}
