using System;

// Game-owned claims backed by the existing PlayFab account data and wallet.
// Classic currency calls have no idempotency key: pending GC grants are never resubmitted.
public interface IDailyRewardBackend
{
    void GetEligibleOffer(DailyRewardCompletionContext context, string rulesVersion, Action<string> completed);
    void ResolveSpin(string offerId, Action<DailyRewardResolution> completed);
    void Skip(string offerId);
}
[Serializable] public sealed class DailyRewardCompletionContext
{
    public string account, restaurant, mode, runId;
    public int day;
}
[Serializable] public sealed class DailyRewardResolution
{
    public bool confirmed;
    public int prizeIndex = -1;
    public int amount;
    public bool gold;
    public string message;
    public string status, receiptId;
}
