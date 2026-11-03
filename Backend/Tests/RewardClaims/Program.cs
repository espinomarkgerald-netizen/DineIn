using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PlayFab.ClientModels;
using UnityEngine;

static class Program
{
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static DailyRewardCompletionContext Context(string run) => new() { account = "accountA", restaurant = "Lobby3", mode = "campaign", runId = run, day = 5 };
    static string Offer(DailyRewardGameBackend backend, DailyRewardCompletionContext context) {
        string id = null; backend.GetEligibleOffer(context, "daily-slot-1", value => id = value); return id;
    }
    static DailyRewardResolution Spin(DailyRewardGameBackend backend, string id) {
        DailyRewardResolution result = null; backend.ResolveSpin(id, value => result = value); return result;
    }
    static void Prize(int index, bool gold = false) {
        var settings = new DailyRewardSlotSettings { noPrizeWeight = index < 0 ? 1 : 0, prizes = new DailyRewardSlotSettings.Prize[6] };
        for (int i = 0; i < 6; i++) settings.prizes[i] = new() { amount = 250, gold = gold, weight = i == index ? 1 : 0 };
        Resources.Value = settings;
    }
    static void Main() {
        string temp = Path.Combine(Path.GetTempPath(), "DineIn-reward-check-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = temp;
        try {
            Prize(0); var backend = new DailyRewardGameBackend(); var ctx = Context("cash"); var id = Offer(backend, ctx);
            Check(Spin(backend, id).confirmed, "Cash grant failed");
            Check(Spin(backend, id).confirmed && CampaignSaveStore.Cash == 250, "Repeated spin granted twice");
            backend = new(); id = Offer(backend, ctx); Spin(backend, id);
            Check(CampaignSaveStore.Cash == 250, "Reopened claim granted twice");
            Prize(4, true); backend = new(); ctx = Context("gc"); id = Offer(backend, ctx);
            Check(Spin(backend, id).confirmed && PlayFabWalletManager.Grants == 1, "GC grant failed");
            backend = new(); Spin(backend, Offer(backend, ctx));
            Check(PlayFabWalletManager.Grants == 1, "GC grant replayed");
            PlayFabWalletManager.Unknown = true; backend = new(); ctx = Context("unknown"); id = Offer(backend, ctx);
            Check(!Spin(backend, id).confirmed, "Unknown grant was reported confirmed");
            backend = new(); Spin(backend, Offer(backend, ctx));
            Check(PlayFabWalletManager.Grants == 2, "Unknown GC grant was retried");
            PlayFabWalletManager.Unknown = false;
            Prize(0); backend = new(); ctx = Context("selected"); id = Offer(backend, ctx);
            PlayFab.PlayFabClientAPI.FailWrites = true;
            Check(!Spin(backend, id).confirmed, "Failed claim write granted currency");
            PlayFab.PlayFabClientAPI.FailWrites = false; Prize(2); backend = new();
            Check(Spin(backend, Offer(backend, ctx)).prizeIndex == 0, "Persisted outcome was rerolled");
            backend = new(); ctx = Context("skip"); id = Offer(backend, ctx); backend.Skip(id);
            Check(Offer(new(), ctx) == null, "Skipped offer reopened");
            Prize(-1); backend = new(); ctx = Context("loss"); id = Offer(backend, ctx);
            Check(Spin(backend, id).confirmed && CampaignSaveStore.Cash == 500, "No-match changed cash");
            backend = new(); id = Offer(backend, Context("switch")); PlayFabAuthManager.Instance.PlayFabId = "accountB";
            Check(Spin(backend, id) == null && PlayFabWalletManager.Grants == 2, "Account switch credited the wrong account");
            Console.WriteLine("PASS: " + assertions + " source-linked reward claim checks; no real PlayFab calls.");
        } finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
}

// Minimal external boundaries; the production provider above is compiled unchanged.
public class PlayFabAuthManager { public static PlayFabAuthManager Instance = new(); public bool IsLoggedIn = true; public string PlayFabId = "accountA"; }
public class PlayFabWalletManager {
    public static PlayFabWalletManager Instance = new(); public static int Grants; public static bool Unknown;
    public void CreditDailyRewardGold(int amount, string account, Action success, Action<string> failure) { Grants++; if (Unknown) failure("timeout"); else success(); }
}
public static class DailyRewardSlotUI { public static IDailyRewardBackend Backend; }
public static class CampaignSaveStore {
    public static int Cash; static HashSet<string> receipts = new();
    public static void QueueDailyRewardCash(string account, string restaurant, int amount, string receipt) {}
    public static void AtomicWrite(string path, string json) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, json); }
    public static bool ApplyDailyRewardCash(string account, string restaurant, int amount, string receipt) { if (receipts.Add(receipt)) Cash += amount; return true; }
}
namespace TMPro { public class TMP_FontAsset {} }
namespace UnityEngine {
    public class ScriptableObject {} public class Sprite {} public class AudioClip {} public struct Vector2 {}
    public class MinAttribute : Attribute { public MinAttribute(float value) {} }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) {} }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) {} }
    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public static class Resources { public static object Value; public static T Load<T>(string name) => (T)Value; }
    public static class Application { public static string persistentDataPath; }
    public static class Debug { public static void LogWarning(object value) => Console.WriteLine(value); }
    public static class JsonUtility {
        static readonly JsonSerializerOptions options = new() { IncludeFields = true };
        public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, options);
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, options);
    }
}
namespace PlayFab.ClientModels {
    public enum UserDataPermission { Private }
    public class UserDataRecord { public string Value; }
    public class GetUserDataRequest { public List<string> Keys; }
    public class GetUserDataResult { public Dictionary<string, UserDataRecord> Data = new(); }
    public class UpdateUserDataRequest { public Dictionary<string, string> Data; public UserDataPermission Permission; }
}
namespace PlayFab {
    public static class PlayFabClientAPI {
        static readonly Dictionary<string, string> data = new(); public static bool FailWrites;
        public static void GetUserData(GetUserDataRequest req, Action<GetUserDataResult> success, Action<object> failure) {
            var result = new GetUserDataResult(); foreach (var key in req.Keys) if (data.TryGetValue(key, out var value)) result.Data[key] = new() { Value = value }; success(result);
        }
        public static void UpdateUserData(UpdateUserDataRequest req, Action<object> success, Action<object> failure) {
            if (FailWrites) { failure(null); return; } foreach (var entry in req.Data) data[entry.Key] = entry.Value; success(null);
        }
    }
}
