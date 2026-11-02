using System;

// Integration boundary, NOT an implemented or deployed grant service.
// The provider must validate eligibility and atomically own run/day claims and all awards.
// Return an existing outcome for repeated offer IDs; unknown awards must be resolved, not rerolled.
public interface IDailyRewardBackend
{
    void GetEligibleOffer(string account, string restaurant, string rulesVersion, Action<string> completed);
    void ResolveSpin(string offerId, Action<DailyRewardResolution> completed);
    void Skip(string offerId);
}
[Serializable] public sealed class DailyRewardResolution
{
    public bool confirmed;
    public int prizeIndex = -1;
    public int amount;
    public bool gold;
    public string message;
}
