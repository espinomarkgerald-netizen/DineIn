using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Client-owned game rewards, persisted in PlayFab UserData and the existing career save.
// This is not a server-authoritative economy. Classic GC has no safe automatic retry.
public sealed class DailyRewardGameBackend : IDailyRewardBackend
{
    [Serializable] private sealed class Claim
    {
        public string id, account, restaurant, rules, state;
        public int prizeIndex = -1, amount;
        public bool gold;
    }
    private readonly Dictionary<string, Claim> claims = new();
    private readonly HashSet<string> inFlight = new();
    private readonly System.Random random = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() => DailyRewardSlotUI.Backend = new DailyRewardGameBackend();
    private static bool Current(string account) => PlayFabAuthManager.Instance?.IsLoggedIn == true && PlayFabAuthManager.Instance.PlayFabId == account;
    private static string Id(DailyRewardCompletionContext context)
    {
        using var hash = SHA256.Create();
        return "DR_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
            context.account + ":" + context.restaurant + ":" + context.mode + ":" + context.runId + ":" + context.day))).Replace("-", "").Substring(0, 48);
    }
    private static string PathFor(Claim claim) => Path.Combine(Application.persistentDataPath, "daily_rewards", claim.account, claim.id + ".json");
    private static void SaveLocal(Claim claim) => CampaignSaveStore.AtomicWrite(PathFor(claim), JsonUtility.ToJson(claim));
    private static int Rank(string state) => state == "confirmed" ? 4 : state == "pending" ? 3 : state == "selected" ? 2 : state == "skipped" ? 1 : 0;
    private static void SaveCloud(Claim claim, Action success, Action failure)
    {
        if (!Current(claim.account)) { failure(); return; }
        SaveLocal(claim);
        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
            Data = new Dictionary<string, string> { [claim.id] = JsonUtility.ToJson(claim) }, Permission = UserDataPermission.Private
        }, _ => { if (Current(claim.account)) success(); else failure(); }, _ => failure());
    }
    public void GetEligibleOffer(DailyRewardCompletionContext context, string rulesVersion, Action<string> completed)
    {
        if (context == null || !Current(context.account) || string.IsNullOrEmpty(context.runId) || context.day < 1)
        { completed(null); return; }
        string id = Id(context);
        PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { id } }, response => {
            if (!Current(context.account)) { completed(null); return; }
            try
            {
                Claim claim = response.Data != null && response.Data.TryGetValue(id, out var entry)
                    ? JsonUtility.FromJson<Claim>(entry.Value) : null;
                var seed = new Claim { id = id, account = context.account, restaurant = context.restaurant, rules = rulesVersion, state = "eligible" };
                if (File.Exists(PathFor(seed))) {
                    var local = JsonUtility.FromJson<Claim>(File.ReadAllText(PathFor(seed)));
                    // A local dispatched grant is never downgraded to a retryable cloud state.
                    if (claim == null || Rank(local.state) > Rank(claim.state)) claim = local;
                }
                claim ??= seed;
                if (claim.id != id || claim.account != context.account || claim.restaurant != context.restaurant || claim.rules != rulesVersion)
                { completed(null); return; }
                claims[id] = claim;
                SaveCloud(claim, () => completed(claim.state == "skipped" ? null : id), () => completed(null));
            }
            catch (Exception error) { Debug.LogWarning("[Daily reward] Offer preserved: " + error.Message); completed(null); }
        }, _ => completed(null));
    }
    public void ResolveSpin(string offerId, Action<DailyRewardResolution> completed)
    {
        PlayFabWalletManager.EnsureInstance();
        if (!claims.TryGetValue(offerId, out var claim) || !Current(claim.account) || !inFlight.Add(offerId)) return;
        void Finish(bool confirmed, string message)
        {
            inFlight.Remove(offerId);
            completed(new DailyRewardResolution { confirmed = confirmed, status = claim.state, receiptId = claim.id,
                prizeIndex = claim.prizeIndex, amount = claim.amount, gold = claim.gold, message = message });
        }
        if (claim.state == "confirmed") { Finish(true, null); return; }
        if (claim.state == "pending" || claim.state == "skipped") { Finish(false, "This reward is pending. No extra grant will be sent."); return; }
        var config = Resources.Load<DailyRewardSlotSettings>("DailyRewardSlot");
        if (config == null || config.rulesVersion != claim.rules) { Finish(false, "Reward rules are unavailable."); return; }
        if (claim.state == "eligible") {
            int[] weights = Array.ConvertAll(config.prizes, p => p.weight);
            claim.prizeIndex = DailyRewardSlotOdds.Select(random.Next(DailyRewardSlotOdds.Total(weights, config.noPrizeWeight)), weights);
            if (claim.prizeIndex >= 0) { var prize = config.prizes[claim.prizeIndex]; claim.amount = prize.amount; claim.gold = prize.gold; }
            claim.state = "selected";
        }
        void Confirm()
        {
            claim.state = "confirmed";
            SaveCloud(claim, () => Finish(true, null), () => Finish(true, "Reward credited; account receipt sync is pending."));
        }
        SaveCloud(claim, () => {
            if (claim.prizeIndex < 0) { Confirm(); return; }
            if (!claim.gold) {
                try {
                    CampaignSaveStore.QueueDailyRewardCash(claim.account, claim.restaurant, claim.amount, claim.id);
                    if (CampaignSaveStore.ApplyDailyRewardCash(claim.account, claim.restaurant, claim.amount, claim.id)) Confirm();
                    else Finish(false, "Cash reward saved for recovery. Return to this account and try again."); }
                catch (Exception error) { Finish(false, error.Message); }
                return;
            }
            var wallet = PlayFabWalletManager.Instance;
            if (wallet == null) { Finish(false, "Wallet is unavailable. The selected prize is saved."); return; }
            claim.state = "pending"; // Persist before dispatch; a crash/unknown response cannot issue a duplicate.
            SaveCloud(claim, () => wallet.CreditDailyRewardGold(claim.amount, claim.account, Confirm,
                message => Finish(false, "GC reward pending: " + message)), () => Finish(false, "GC reward pending. No grant was retried."));
        }, () => Finish(false, "Could not save this reward online. The selected result is preserved."));
    }
    public void Skip(string offerId)
    {
        if (!claims.TryGetValue(offerId, out var claim) || claim.state != "eligible") return;
        claim.state = "skipped"; SaveCloud(claim, () => { }, () => { });
    }
}
