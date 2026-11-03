using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static partial class CampaignSaveStore
{
    private const string CashPurchasePrefix = "restaurant-cash:";
    private static float combinedMoneyExpires;
    private static string combinedMoneyAccount;
    private static string combinedMoneyError;
    private static long combinedMoney;
    // This is a view of career funds, never another wallet. Runtime service still reads Money.
    public static long DisplayMoney => AtMenu ? (TryGetCombinedMoney(out var total, out _) ? total : 0) : Money;

    public static bool TryGetCombinedMoney(out long total, out string error, bool refresh = false)
    {
        total = 0; error = null;
        string account = LocalSaveManager.Data.progressAccount;
        var auth = PlayFabAuthManager.Instance;
        if (LocalSaveManager.NeedsRecovery || auth?.IsLoggedIn == true && auth.PlayFabId != account)
        { error = "Your account money is still loading."; return false; }
        try {
            if (refresh || combinedMoneyAccount != account || Time.realtimeSinceStartup >= combinedMoneyExpires) {
                var snapshot = ReadAccount(false);
                if (snapshot.account != account || !ValidContent(snapshot)) throw new InvalidDataException("Your saved restaurant funds need recovery.");
                combinedMoney = CombinedMoney(snapshot);
                combinedMoneyError = null;
                combinedMoneyAccount = account; combinedMoneyExpires = Time.realtimeSinceStartup + .5f;
            }
            if (combinedMoneyError != null) { error = combinedMoneyError; return false; }
            total = combinedMoney; return true;
        } catch (Exception) {
            combinedMoneyAccount = account; combinedMoneyExpires = Time.realtimeSinceStartup + .5f;
            error = combinedMoneyError = "Your saved restaurant funds need recovery. Reload your account before buying.";
            return false;
        }
    }

    internal static long CombinedMoney(AccountSnapshot snapshot)
    {
        long total = 0;
        var ids = new[] { LocalSaveManager.CasualDiningId, LocalSaveManager.FastFoodId, LocalSaveManager.FineDiningId };
        var pairs = new[] { snapshot.casual, snapshot.fastFood, snapshot.fineDining };
        for (int i = 0; i < pairs.Length; i++)
            if (snapshot.profile.unlockedRestaurantIds?.Contains(ids[i]) == true) total += SpendableMoney(pairs[i]);
        return total;
    }
    private static int SpendableMoney(Snapshot pair)
    {
        if (pair == null || string.IsNullOrEmpty(pair.save)) return 0; // No fictitious starting cash from unplayed careers.
        if (!Valid(pair)) throw new InvalidDataException("A restaurant save needs recovery.");
        int cash = JsonUtility.FromJson<GameSaveData>(pair.save).money;
        return string.IsNullOrEmpty(pair.checkpoint) ? cash : Math.Min(cash, JsonUtility.FromJson<GameSaveData>(pair.checkpoint).money);
    }
    internal static int SuccessfulDays(AccountSnapshot snapshot, bool fastFood)
    {
        var pair = fastFood ? snapshot.fastFood : snapshot.casual;
        var data = string.IsNullOrEmpty(pair?.save) ? null : JsonUtility.FromJson<GameSaveData>(pair.save);
        int days = new HashSet<int>(data?.successfulRestaurantDays?.FindAll(d => d > 0) ?? new List<int>()).Count;
        string prefix = "restaurant-day:" + (fastFood ? LocalSaveManager.FastFoodId : LocalSaveManager.CasualDiningId) + ":";
        return days + new HashSet<string>(snapshot.profile.ownedItemIds.FindAll(r => r != null && r.StartsWith(prefix, StringComparison.Ordinal))).Count;
    }

    // Pure preparation; the caller commits the careers and ownership through the existing account journal.
    internal static AccountSnapshot PrepareCashPurchase(AccountSnapshot source, string id, int price,
        int casualRequired, int fastRequired, string transaction)
    {
        if (!ValidContent(source) || price <= 0 || casualRequired < 1 || fastRequired < 0 ||
            (id != LocalSaveManager.FastFoodId && id != LocalSaveManager.FineDiningId) || !Guid.TryParseExact(transaction, "N", out _))
            throw new InvalidOperationException("Restaurant purchase settings or saved progress are unavailable.");
        var value = JsonUtility.FromJson<AccountSnapshot>(JsonUtility.ToJson(source));
        if (value.profile.unlockedRestaurantIds.Contains(id)) return value;
        if (SuccessfulDays(value, false) < casualRequired || SuccessfulDays(value, true) < fastRequired ||
            id == LocalSaveManager.FineDiningId && !value.profile.unlockedRestaurantIds.Contains(LocalSaveManager.FastFoodId))
            throw new InvalidOperationException("Complete the required successful days first.");
        long total = CombinedMoney(value);
        if (total < price) throw new InvalidOperationException("Not enough combined Normal Money.");
        string receipt = CashPurchasePrefix + id + ":" + transaction;
        var ids = new[] { LocalSaveManager.CasualDiningId, LocalSaveManager.FastFoodId, LocalSaveManager.FineDiningId };
        var pairs = new[] { value.casual, value.fastFood, value.fineDining };
        var balances = new int[3]; var shares = new int[3]; int remaining = price;
        for (int i = 0; i < pairs.Length; i++) {
            balances[i] = value.profile.unlockedRestaurantIds.Contains(ids[i]) ? SpendableMoney(pairs[i]) : 0;
            shares[i] = (int)((long)price * balances[i] / total); remaining -= shares[i];
        }
        for (int i = 0; remaining > 0 && i < pairs.Length; i++)
            if (balances[i] > shares[i]) { shares[i]++; remaining--; }
        if (remaining != 0) throw new InvalidOperationException("Could not allocate the restaurant payment.");
        for (int i = 0; i < pairs.Length; i++) {
            if (shares[i] == 0) continue;
            pairs[i].save = DebitCareerDocument(pairs[i].save, shares[i], receipt, id);
            if (!string.IsNullOrEmpty(pairs[i].checkpoint)) pairs[i].checkpoint = DebitCareerDocument(pairs[i].checkpoint, shares[i], receipt, id);
            pairs[i].hash = Hash(pairs[i].save, pairs[i].checkpoint);
        }
        value.profile.unlockedRestaurantIds.Add(id); value.profile.ownedItemIds.Add(receipt);
        Seal(value); return value;
    }
    private static string DebitCareerDocument(string json, int amount, string receipt, string id)
    {
        var data = JsonUtility.FromJson<GameSaveData>(json);
        data.campaignCreditReceipts ??= new List<string>();
        if (!data.campaignCreditReceipts.Contains(receipt)) {
            if (data.money < amount) throw new InvalidOperationException("Restaurant funds changed. Reopen purchase details.");
            data.money -= amount; data.polishDayStartMoney = Math.Max(0, data.polishDayStartMoney - amount);
            data.campaignCreditReceipts.Add(receipt);
            data.moneyTransactions ??= new List<MoneyTransactionSaveEntry>();
            data.moneyTransactions.Add(new MoneyTransactionSaveEntry { day = data.currentDay, amountDelta = -amount,
                description = "Restaurant expansion: " + id, adjustment = true });
        }
        return JsonUtility.ToJson(data, true);
    }
    public static bool TryPurchaseRestaurantCash(string account, string id, int price, int casualRequired, int fastRequired, out string message)
    {
        message = null;
        try {
            if ((id != LocalSaveManager.FastFoodId && id != LocalSaveManager.FineDiningId) ||
                !AtMenu || PlayFabAuthManager.Instance?.IsLoggedIn != true || PlayFabAuthManager.Instance.PlayFabId != account ||
                LocalSaveManager.Data.progressAccount != account || !CampaignCloudSync.CanEnterGameplay())
                throw new InvalidOperationException("Sign in and finish loading your progress first.");
            var before = ReadAccount();
            if (before.account != account) throw new InvalidOperationException("Account changed. Reopen purchase details.");
            if (before.profile.unlockedRestaurantIds.Contains(id)) { message = "This restaurant is already yours."; return true; }
            if (File.Exists(Path.Combine(LocalDirectory, "restaurant_purchase_" + id + ".json")) ||
                before.profile.ownedItemIds.Exists(r => r != null && r.StartsWith("restaurant-order:" + id + ":", StringComparison.Ordinal)))
                throw new InvalidOperationException("Check the previous GC purchase before making another payment.");
            var purchased = PrepareCashPurchase(before, id, price, casualRequired, fastRequired, Guid.NewGuid().ToString("N"));
            ObserveSnapshot(purchased, ReadAccountState(), before.device ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow.ToString("o"));
            Seal(purchased);
            InstallAccount(purchased, false); // Existing write-ahead journal commits every career + ownership together.
            combinedMoneyExpires = 0; NotifyLocalWrite();
            message = "Restaurant purchased with Normal Money."; return true;
        } catch (Exception e) {
            combinedMoneyExpires = 0;
            message = e is InvalidOperationException ? e.Message : "Purchase saving could not finish. Reopen the menu to recover its saved status before retrying.";
            return false;
        }
    }

    internal static void RequireCashPurchaseHistory(AccountSnapshot restored, AccountSnapshot current)
    {
        // Older cloud progress must not keep a permanent unlock while refunding its cash debit.
        foreach (string receipt in current.profile?.ownedItemIds ?? new List<string>())
            if (receipt != null && receipt.StartsWith(CashPurchasePrefix, StringComparison.Ordinal) &&
                restored.profile?.ownedItemIds?.Contains(receipt) != true)
                throw new InvalidOperationException("This save predates a restaurant cash purchase. Keep current device progress to preserve the payment.");
    }
}
