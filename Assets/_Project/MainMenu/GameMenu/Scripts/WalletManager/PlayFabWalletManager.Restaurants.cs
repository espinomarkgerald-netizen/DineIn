using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public partial class PlayFabWalletManager
{
    [Header("Restaurant purchases")]
    [SerializeField] private string restaurantCatalogVersion = "RestaurantsV1";
    public enum RestaurantPurchaseState { PaymentUnknown, PaymentFailed, PaymentConfirmed, OwnershipPersistencePending, Completed }
    [Serializable] private sealed class RestaurantOrder
    {
        public int version = 1;
        public string account, restaurant, transaction, order, provider, path;
        public int price;
        public bool paymentSent, confirmationSent;
        public RestaurantPurchaseState state;
    }
    public string RestaurantPurchaseStatus { get; private set; }
    public bool RestaurantPurchaseBusy { get; private set; }
    private string RestaurantOrderPath(string id) => Path.Combine(CampaignSaveStore.LocalDirectory, "restaurant_purchase_" + id + ".json");
    private bool RestaurantContext(string account) => IsCurrentWallet && CurrentAccount(account) &&
        CampaignSaveStore.AccountMatches(account) && LocalSaveManager.Data.progressAccount == account && !LocalSaveManager.NeedsRecovery;
    private static bool RestaurantId(string id) => id == LocalSaveManager.FastFoodId || id == LocalSaveManager.FineDiningId;
    private static void SaveRestaurantOrder(RestaurantOrder order) => CampaignSaveStore.AtomicWrite(order.path, JsonUtility.ToJson(order));
    public bool HasPendingRestaurantPurchase(string id) => RestaurantId(id) && (File.Exists(RestaurantOrderPath(id)) ||
        LocalSaveManager.Data.ownedItemIds.Exists(x => x != null && x.StartsWith("restaurant-order:" + id + ":", StringComparison.Ordinal)));

    public void GetRestaurantQuote(string id, Action<int, string> reply)
    {
        if (!RestaurantId(id) || !RestaurantContext(walletAccount)) { reply(0, "Sign in and load your account and wallet."); return; }
        string account = walletAccount; int generation = authGeneration;
        PlayFabClientAPI.GetCatalogItems(new GetCatalogItemsRequest { CatalogVersion = restaurantCatalogVersion, AuthenticationContext = PlayFabSettings.staticPlayer }, result =>
        {
            if (generation != authGeneration || !RestaurantContext(account)) return;
            var item = result.Catalog?.Find(x => x.ItemId == id);
            if (item == null || item.VirtualCurrencyPrices == null || !item.VirtualCurrencyPrices.TryGetValue(goldCurrencyCode, out uint price) || price == 0 || price > int.MaxValue)
            { reply(0, "Restaurant price is not published."); return; }
            reply((int)price, null);
        }, error => { if (generation == authGeneration && RestaurantContext(account)) reply(0, error.ErrorMessage); });
    }

    // Caller must recheck the configured experience requirements before invoking this.
    public void PurchaseRestaurant(string id, int price, Action changed)
    {
        if (!RestaurantId(id) || !RestaurantContext(walletAccount) || RestaurantPurchaseBusy || goldCoinSpendInFlight || IsRefreshing || CampaignSaveStore.IsCreditPending) return;
        if (LocalSaveManager.IsRestaurantUnlocked(id)) return;
        if (HasPendingRestaurantPurchase(id)) { RecoverRestaurantPurchase(id, changed); return; }
        if (price <= 0 || GoldCoins < price) { RestaurantPurchaseStatus = "Insufficient GC or price unavailable."; changed?.Invoke(); return; }
        var context = new PlayFabAuthenticationContext(); context.CopyFrom(PlayFabSettings.staticPlayer);
        var order = new RestaurantOrder { account = walletAccount, restaurant = id, transaction = Guid.NewGuid().ToString("N"), price = price,
            path = RestaurantOrderPath(id), state = RestaurantPurchaseState.PaymentUnknown };
        int generation = authGeneration;
        bool Current() => generation == authGeneration && RestaurantContext(order.account);
        void Stop(string message) { if (!Current()) return; RestaurantPurchaseBusy = goldCoinSpendInFlight = false; RestaurantPurchaseStatus = message; RefreshWallet(); changed?.Invoke(); }
        try { SaveRestaurantOrder(order); } catch (Exception) { Stop("Could not save purchase journal. Nothing charged."); return; }
        RestaurantPurchaseBusy = goldCoinSpendInFlight = true;
        RestaurantPurchaseStatus = "Creating purchase order…";
        void StartOrder() => PlayFabClientAPI.StartPurchase(new StartPurchaseRequest { AuthenticationContext = context, CatalogVersion = restaurantCatalogVersion,
            Items = new List<ItemPurchaseRequest> { new ItemPurchaseRequest { ItemId = id, Quantity = 1, Annotation = order.transaction } } }, start =>
        {
            if (!Current()) return;
            order.order = start.OrderId;
            var option = start.PaymentOptions?.Find(x => x.Currency == goldCurrencyCode && x.Price == (uint)price);
            if (option == null) { order.state = RestaurantPurchaseState.PaymentFailed; try { SaveRestaurantOrder(order); } catch {} Stop("Price changed. Recover this unpaid order before retrying."); return; }
            order.provider = option.ProviderName;
            // Write-ahead marker: a crash before/after send is treated as unknown, never replayed.
            order.paymentSent = true;
            try { SaveRestaurantOrder(order); } catch { Stop("Payment was not sent: unable to persist order."); return; }
            RestaurantPurchaseStatus = "Payment pending…";
            PlayFabClientAPI.PayForPurchase(new PayForPurchaseRequest { AuthenticationContext = context, OrderId = order.order, Currency = goldCurrencyCode, ProviderName = order.provider }, paid =>
            {
                if (!Current()) return;
                order.confirmationSent = true;
                try { SaveRestaurantOrder(order); } catch { Stop("Payment status unknown; recover order status."); return; }
                PlayFabClientAPI.ConfirmPurchase(new ConfirmPurchaseRequest { AuthenticationContext = context, OrderId = order.order }, confirmed =>
                {
                    if (!Current()) return;
                    if (confirmed.Items == null || !confirmed.Items.Any(x => x.ItemId == id)) { Stop("Confirmation uncertain; recover order status."); return; }
                    order.state = RestaurantPurchaseState.PaymentConfirmed;
                    try { SaveRestaurantOrder(order); } catch { Stop("Confirmed payment; recover order status to save ownership."); return; }
                    PersistRestaurantOwnership(order); Stop(RestaurantPurchaseStatus);
                }, error => Stop("Confirmation status unknown. Recover this order; no new charge will be sent."));
            }, error => Stop("Payment status unknown. Recover this order; payment will not be resent."));
        }, error =>
        {
            if (!Current()) return;
            // StartPurchase creates a cart only. No payment call was made.
            order.state = RestaurantPurchaseState.PaymentFailed;
            try { SaveRestaurantOrder(order); } catch {}
            Stop("No payment sent. Recover the journal before retrying.");
        });
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest { AuthenticationContext = context }, inventory =>
        {
            if (!Current()) return;
            if (inventory.Inventory?.Any(i => i.CatalogVersion == restaurantCatalogVersion && i.ItemId == id) == true)
            {
                Stop("Server inventory already contains this restaurant. No charge sent. Recover the original order or contact support."); return;
            }
            StartOrder();
        }, error => Stop("Could not verify ownership. No payment sent."));
    }

    private void PersistRestaurantOwnership(RestaurantOrder order)
    {
        if (!RestaurantContext(order.account)) return;
        order.state = RestaurantPurchaseState.OwnershipPersistencePending;
        try { SaveRestaurantOrder(order); } catch { RestaurantPurchaseStatus = "Payment confirmed; ownership save pending."; return; }
        var ids = LocalSaveManager.Data.unlockedRestaurantIds;
        bool added = !ids.Contains(order.restaurant);
        if (added) ids.Add(order.restaurant);
        string receipt = "restaurant-order:" + order.restaurant + ":" + order.order;
        var receipts = LocalSaveManager.Data.ownedItemIds;
        bool addedReceipt = !string.IsNullOrEmpty(order.order) && !receipts.Contains(receipt);
        if (addedReceipt) receipts.Add(receipt);
        if (!LocalSaveManager.TrySave())
        {
            if (added) ids.Remove(order.restaurant);
            if (addedReceipt) receipts.Remove(receipt);
            RestaurantPurchaseStatus = "Payment confirmed; ownership save pending. Recover without paying again."; return;
        }
        order.state = RestaurantPurchaseState.Completed;
        try { SaveRestaurantOrder(order); } catch { /* The confirmed journal remains recoverable. */ }
        RestaurantPurchaseStatus = "Purchase completed. Restaurant unlocked.";
    }

    public void RecoverRestaurantPurchase(string id, Action changed)
    {
        if (!RestaurantId(id) || !RestaurantContext(walletAccount) || RestaurantPurchaseBusy || goldCoinSpendInFlight) return;
        RestaurantOrder order;
        try
        {
            string path = RestaurantOrderPath(id);
            if (!File.Exists(path))
            {
                string prefix = "restaurant-order:" + id + ":";
                string receipt = LocalSaveManager.Data.ownedItemIds.Find(x => x != null && x.StartsWith(prefix, StringComparison.Ordinal));
                if (receipt == null) return;
                order = new RestaurantOrder { account = walletAccount, restaurant = id, transaction = Guid.NewGuid().ToString("N"),
                    order = receipt.Substring(prefix.Length), path = path, paymentSent = true, confirmationSent = true,
                    state = RestaurantPurchaseState.PaymentUnknown };
                SaveRestaurantOrder(order);
            }
            order = JsonUtility.FromJson<RestaurantOrder>(File.ReadAllText(path));
            if (order == null || order.version != 1 || order.account != walletAccount || order.restaurant != id || string.IsNullOrEmpty(order.transaction))
                throw new InvalidDataException();
            order.path = path;
        }
        catch { RestaurantPurchaseStatus = "Purchase journal needs recovery; another charge is blocked."; changed?.Invoke(); return; }
        if (order.state == RestaurantPurchaseState.PaymentConfirmed || order.state == RestaurantPurchaseState.OwnershipPersistencePending || order.state == RestaurantPurchaseState.Completed)
        { PersistRestaurantOwnership(order); changed?.Invoke(); return; }
        if (!order.paymentSent)
        {
            try { File.Delete(order.path); RestaurantPurchaseStatus = "No payment was sent. You may retry."; }
            catch { RestaurantPurchaseStatus = "Unable to clear unpaid purchase journal."; }
            changed?.Invoke(); return;
        }
        if (string.IsNullOrEmpty(order.order)) { RestaurantPurchaseStatus = "Order reference missing; manual recovery required. Another charge is blocked."; changed?.Invoke(); return; }
        string account = order.account; int generation = authGeneration;
        RestaurantPurchaseBusy = true;
        PlayFabClientAPI.GetPurchase(new GetPurchaseRequest { AuthenticationContext = PlayFabSettings.staticPlayer, OrderId = order.order }, result =>
        {
            if (generation != authGeneration || !RestaurantContext(account)) return;
            RestaurantPurchaseBusy = false;
            if (result.OrderId != order.order) { RestaurantPurchaseStatus = "Invalid order response; charge blocked."; changed?.Invoke(); return; }
            if (result.TransactionStatus == "Succeeded")
            {
                order.state = RestaurantPurchaseState.PaymentConfirmed;
                try { SaveRestaurantOrder(order); PersistRestaurantOwnership(order); }
                catch { RestaurantPurchaseStatus = "Payment confirmed; ownership persistence pending."; }
            }
            else if (result.TransactionStatus == "Approved" && !order.confirmationSent)
            {
                // The journal proves confirmation has never been submitted. Send it once, not a retry.
                order.confirmationSent = true;
                try { SaveRestaurantOrder(order); }
                catch { RestaurantPurchaseStatus = "Unable to persist confirmation marker. Nothing submitted."; changed?.Invoke(); return; }
                RestaurantPurchaseBusy = goldCoinSpendInFlight = true;
                PlayFabClientAPI.ConfirmPurchase(new ConfirmPurchaseRequest { AuthenticationContext = PlayFabSettings.staticPlayer, OrderId = order.order }, confirmed =>
                {
                    if (generation != authGeneration || !RestaurantContext(account)) return;
                    RestaurantPurchaseBusy = goldCoinSpendInFlight = false;
                    if (confirmed.Items?.Any(item => item.ItemId == id) == true)
                    {
                        order.state = RestaurantPurchaseState.PaymentConfirmed;
                        try { SaveRestaurantOrder(order); PersistRestaurantOwnership(order); }
                        catch { RestaurantPurchaseStatus = "Payment confirmed; ownership persistence pending."; }
                    }
                    else RestaurantPurchaseStatus = "Confirmation uncertain; recover order status.";
                    RefreshWallet(); changed?.Invoke();
                }, error =>
                {
                    if (generation != authGeneration || !RestaurantContext(account)) return;
                    RestaurantPurchaseBusy = goldCoinSpendInFlight = false;
                    RestaurantPurchaseStatus = "Confirmation status unknown; recover order status. Confirmation will not be resent.";
                    RefreshWallet(); changed?.Invoke();
                });
                return;
            }
            else if (result.TransactionStatus == "Failed")
            {
                order.state = RestaurantPurchaseState.PaymentFailed;
                try { SaveRestaurantOrder(order); File.Delete(order.path); RestaurantPurchaseStatus = "Payment failed. You may retry."; }
                catch { RestaurantPurchaseStatus = "Failed payment journal could not be saved; retry blocked."; }
            }
            else RestaurantPurchaseStatus = "Payment status " + result.TransactionStatus + "; another charge is blocked. Recover later or contact support with order " + order.order;
            RefreshWallet(); changed?.Invoke();
        }, error => { if (generation != authGeneration || !RestaurantContext(account)) return; RestaurantPurchaseBusy = false; RestaurantPurchaseStatus = "Order status unknown; another charge is blocked."; changed?.Invoke(); });
    }
}
