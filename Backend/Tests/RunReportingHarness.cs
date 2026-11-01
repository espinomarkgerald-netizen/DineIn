// Headless tests of the actual ranking state, flow adapter and uploader sources.
// Unity/SDK surfaces below are test doubles. This is not Unity or live PlayFab verification.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace UnityEngine
{
    public class MonoBehaviour { public GameObject gameObject = new GameObject("test"); protected static void DontDestroyOnLoad(object value) { } }
    public class GameObject { public GameObject(string name) { } public T AddComponent<T>() where T : new() => new T(); }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { } }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static string persistentDataPath, version = "reporting-test"; public static NetworkReachability internetReachability; }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); }
    public static class Debug { public static void LogWarning(string text) { } }
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = MakeOptions();
        private static JsonSerializerOptions MakeOptions()
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info => {
                if (info.Kind == JsonTypeInfoKind.Object && info.CreateObject == null && !info.Type.IsAbstract)
                    info.CreateObject = () => Activator.CreateInstance(info.Type, true);
                for (int i = info.Properties.Count - 1; i >= 0; i--)
                    if (info.Properties[i].AttributeProvider is FieldInfo field && field.IsDefined(typeof(NonSerializedAttribute)))
                        info.Properties.RemoveAt(i);
            });
            return new JsonSerializerOptions { IncludeFields = true, TypeInfoResolver = resolver };
        }
        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
    }
}
namespace PlayFab
{
    public class PlayFabAuthenticationContext
    {
        public string PlayFabId, ClientSessionTicket;
        public void CopyFrom(PlayFabAuthenticationContext value) { PlayFabId = value.PlayFabId; ClientSessionTicket = value.ClientSessionTicket; }
        public bool IsClientLoggedIn() => !string.IsNullOrEmpty(ClientSessionTicket);
    }
    public static class PlayFabSettings { public static PlayFabAuthenticationContext staticPlayer = new PlayFabAuthenticationContext(); }
    public class PlayFabError { }
    public static class PlayFabClientAPI
    {
        public sealed class Call
        {
            public ClientModels.ExecuteCloudScriptRequest Request;
            public Action<ClientModels.ExecuteCloudScriptResult> Success;
            public Action<PlayFabError> Failure;
        }
        public static List<Call> Calls = new List<Call>();
        public static bool FailSend;
        public static void ExecuteCloudScript(ClientModels.ExecuteCloudScriptRequest request,
            Action<ClientModels.ExecuteCloudScriptResult> success, Action<PlayFabError> failure) =>
            Send(request, success, failure);
        private static void Send(ClientModels.ExecuteCloudScriptRequest request, Action<ClientModels.ExecuteCloudScriptResult> success, Action<PlayFabError> failure)
        {
            if (FailSend) throw new IOException("simulated synchronous SDK error");
            Calls.Add(new Call { Request = request, Success = success, Failure = failure });
        }
    }
}
namespace PlayFab.ClientModels
{
    public enum CloudScriptRevisionOption { Live }
    public class ExecuteCloudScriptRequest
    {
        public PlayFab.PlayFabAuthenticationContext AuthenticationContext;
        public string FunctionName;
        public object FunctionParameter;
        public bool GeneratePlayStreamEvent;
        public CloudScriptRevisionOption RevisionSelection;
    }
    public class ExecuteCloudScriptResult { public object Error, FunctionResult; }
}
namespace PlayFab.Json
{
    public static class PlayFabSimpleJson { public static string SerializeObject(object value) => UnityEngine.JsonUtility.ToJson(value); }
}
public class PlayFabAuthManager
{
    public static PlayFabAuthManager Instance = new PlayFabAuthManager();
    public string PlayFabId;
    public bool IsLoggedIn;
}
public static class MultiplayerSessionManager
{
    public const string SceneName = "Lobby1 Multiplayer", ResultRulesVersion = "casual-session-2";
}
public static class LocalSaveManager
{
    public const string CasualDiningId = "casual", FastFoodId = "fast", FineDiningId = "fine";
    public static int Credits;
    public static bool CreditRestaurantDays(string account, string restaurant, string run, List<int> days) { Credits++; return true; }
}
public static class CampaignSaveStore
{
    public sealed class AccountState { public string account; }
    public static string Owner = "AA", RestaurantScene = "Lobby1";
    public static bool ProtectedSession, FailWrites, FailOwnerRead;
    public static AccountState ReadAccountState()
    {
        if (FailOwnerRead) throw new IOException("simulated account metadata outage");
        return new AccountState { account = Owner };
    }
    public static void AtomicWrite(string path, string value)
    {
        if (FailWrites) throw new IOException("simulated disk outage");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, value);
        if (File.ReadAllText(temp) != value) throw new IOException("verification failed");
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
    }
}
public class GameSaveData { public SoloRankingV1 soloRankingV1; }
public partial class GameFlowManager
{
    private string restaurantSceneName = "Lobby1";
    public enum RestaurantSessionState { PreOpen, Running, DayComplete }
    private RestaurantSessionState restaurantSessionState;
    public void Scene(string name) { restaurantSceneName = CampaignSaveStore.RestaurantScene = name; }
    public GameSaveData Capture() { var value = new GameSaveData(); CaptureSoloRanking(value); return value; }
    public void Load(GameSaveData data) => LoadSoloRanking(data);
    public void Start() { StartSoloRankingService(); restaurantSessionState = RestaurantSessionState.Running; }
    public void Finish() { SettleSoloRankingDay(); restaurantSessionState = RestaurantSessionState.DayComplete; }
    public void Next() { AdvanceSoloRankingDay(); restaurantSessionState = RestaurantSessionState.PreOpen; }
}
public static class Program
{
    private static int assertions, scenarios;
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private static void Test(string name, Action action) { action(); scenarios++; Console.WriteLine("PASS " + name); }
    private static void Login(string account, string ticket = null)
    {
        PlayFabAuthManager.Instance.PlayFabId = account;
        PlayFabAuthManager.Instance.IsLoggedIn = account != null;
        PlayFab.PlayFabSettings.staticPlayer.PlayFabId = account;
        PlayFab.PlayFabSettings.staticPlayer.ClientSessionTicket = ticket ?? account;
    }
    private static MultiplayerRunRecords Reporter(string path = null)
    {
        UnityEngine.Application.persistentDataPath = path ?? Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(UnityEngine.Application.persistentDataPath);
        UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
        UnityEngine.Time.realtimeSinceStartup = 100;
        CampaignSaveStore.FailWrites = CampaignSaveStore.FailOwnerRead = false;
        CampaignSaveStore.ProtectedSession = false;
        CampaignSaveStore.Owner = "AA";
        PlayFab.PlayFabClientAPI.Calls.Clear();
        PlayFab.PlayFabClientAPI.FailSend = false;
        LocalSaveManager.Credits = 0;
        Login("AA");
        var reporter = new MultiplayerRunRecords();
        typeof(MultiplayerRunRecords).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, reporter);
        Invoke(reporter, "Awake");
        return reporter;
    }
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Tick(MultiplayerRunRecords reporter) { UnityEngine.Time.realtimeSinceStartup += 31; Invoke(reporter, "Update"); }
    private static int Pending(MultiplayerRunRecords reporter)
    {
        var box = typeof(MultiplayerRunRecords).GetField("outbox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reporter);
        return ((System.Collections.IList)box.GetType().GetField("pending").GetValue(box)).Count;
    }
    private static SoloRunRecord Solo(int count = 1, string scene = "Lobby1", string id = null)
    {
        var progress = SoloRankingV1.NewRun(); progress.completedDays = progress.lastCreditedDayOrdinal = count;
        progress.dayOrdinal = count;
        if (id != null) progress.runId = id;
        return SoloRunRecord.FromProgress(progress, scene, "test");
    }
    private static void Ack(PlayFab.PlayFabClientAPI.Call call, SoloRunRecord record, int best = 100) => call.Success(
        new PlayFab.ClientModels.ExecuteCloudScriptResult { FunctionResult = new {
            accepted = true, runId = record.runId, restaurant = record.restaurant, completedDays = record.completedDays, bestCompletedDays = best } });
    public static void Main()
    {
        foreach (string scene in new[] { "Lobby1", "Lobby2", "Lobby3" })
        {
            Test(scene + " actual adapter: completion, duplicate, paid advance, retry, endless and restart", () => {
                var reporter = Reporter(); var flow = new GameFlowManager(); flow.Scene(scene); flow.BeginSoloRankingRun();
                var initial = flow.Capture(); var id = initial.soloRankingV1.runId;
                Check(initial.soloRankingV1.completedDays == 0, "creation awards nothing");
                flow.Finish(); Check(flow.Capture().soloRankingV1.completedDays == 0, "no service, no completion");
                flow.Start(); flow.Finish(); flow.Finish();
                var one = flow.Capture(); Check(one.soloRankingV1.completedDays == 1, "duplicate settlement");
                Check(Pending(reporter) == 0, "uncommitted state must not upload");
                flow.RecoverPersistedSoloRanking(one); Check(Pending(reporter) == 1, "committed completion queued");
                flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1.completedDays == 1, "retry same logical day");
                flow.Next(); flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1.completedDays == 2, "paid continue to next day");
                for (int i = 3; i <= 32; i++) { flow.Next(); flow.Start(); flow.Finish(); }
                Check(flow.Capture().soloRankingV1.completedDays == 32, "endless advances beyond displayed day 30");
                var resumed = new GameFlowManager(); resumed.Scene(scene); resumed.Load(flow.Capture()); resumed.Finish();
                Check(resumed.Capture().soloRankingV1.completedDays == 32, "load/results awards nothing");
                Check(resumed.Capture().soloRankingV1.runId == id, "resume identity");
                resumed.BeginSoloRankingRun(); Check(resumed.Capture().soloRankingV1.runId != id, "new run identity");
            });
            Test(scene + " checkpoint restores settled high-water and excludes duplicate replay", () => {
                var progress = SoloRankingV1.NewRun(); var checkpoint = SoloRankingV1.Copy(progress);
                Check(SoloRankingV1.Settle(progress), "first settlement");
                var restored = SoloRankingV1.RestoreCheckpoint(checkpoint, progress);
                Check(restored.completedDays == 1 && !SoloRankingV1.Settle(restored), "same-day checkpoint cannot recredit");
                Check(SoloRankingV1.Advance(restored) && SoloRankingV1.Settle(restored) && restored.completedDays == 2, "next day credits once");
                var older = SoloRankingV1.RestoreCheckpoint(checkpoint, restored);
                Check(older.completedDays == 2 && !SoloRankingV1.Settle(older), "older checkpoint retains high-water");
                Check(SoloRankingV1.Advance(older) && !SoloRankingV1.Settle(older), "replaying second credited day adds nothing");
                Check(SoloRankingV1.Advance(older) && SoloRankingV1.Settle(older) && older.completedDays == 3, "new day beyond high-water credits once");
                var otherRun = SoloRankingV1.NewRun();
                Check(SoloRankingV1.RestoreCheckpoint(otherRun, progress).completedDays == 0, "different runs never merged");
            });
            Test(scene + " offline queue, restart and ownership-bound retry", () => {
                var reporter = Reporter(); var record = Solo(4, scene);
                UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.NotReachable;
                MultiplayerRunRecords.RecordSolo("AA", record); MultiplayerRunRecords.RecordSolo("AA", record);
                Tick(reporter); Check(PlayFab.PlayFabClientAPI.Calls.Count == 0 && Pending(reporter) == 1, "offline duplicate");
                string path = UnityEngine.Application.persistentDataPath;
                reporter = Reporter(path); Login("BB"); Tick(reporter);
                Check(PlayFab.PlayFabClientAPI.Calls.Count == 0, "B cannot send A receipt");
                Login("AA"); Tick(reporter); var call = PlayFab.PlayFabClientAPI.Calls.Last();
                Check(call.Request.FunctionName == SoloRunRecord.SubmissionFunction && call.Request.AuthenticationContext.PlayFabId == "AA", "authenticated solo dispatch");
                Ack(call, record); Check(Pending(reporter) == 0, "acknowledged");
                MultiplayerRunRecords.RecordSolo("AA", record);
                Check(Pending(reporter) == 0, "acknowledged count does not requeue during this session");
                Check(LocalSaveManager.Credits == 0, "solo cannot award multiplayer experience");
            });
        }
        Test("legacy careers, guest owners and protected sessions stay unranked", () => {
            Reporter(); var flow = new GameFlowManager(); flow.Scene("Lobby1"); flow.Load(new GameSaveData());
            flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1 == null, "missing metadata");
            CampaignSaveStore.Owner = null; flow.BeginSoloRankingRun(); flow.Start(); flow.Finish();
            Check(flow.Capture().soloRankingV1 == null, "guest cannot acquire later identity");
            CampaignSaveStore.Owner = "AA"; flow.BeginSoloRankingRun(); CampaignSaveStore.ProtectedSession = true;
            flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1 == null, "tutorial/multiplayer excluded");
        });
        Test("account switch and stale session callback cannot acknowledge A", () => {
            var reporter = Reporter(); var record = Solo(); MultiplayerRunRecords.RecordSolo("AA", record); Tick(reporter);
            var call = PlayFab.PlayFabClientAPI.Calls.Last(); Login("BB"); Ack(call, record);
            Check(Pending(reporter) == 1, "late cross-account ack");
            Login("AA", "replacement-session"); Ack(call, record); Check(Pending(reporter) == 1, "stale ticket ack");
            Tick(reporter); Ack(PlayFab.PlayFabClientAPI.Calls.Last(), record); Check(Pending(reporter) == 0, "new valid ticket");
        });
        Test("failed upload and old in-flight result retain newer pending count", () => {
            var reporter = Reporter(); var record = Solo(2); MultiplayerRunRecords.RecordSolo("AA", record); Tick(reporter);
            PlayFab.PlayFabClientAPI.Calls.Last().Failure(new PlayFab.PlayFabError());
            Check(Pending(reporter) == 1, "failed upload retained"); Tick(reporter);
            var old = PlayFab.PlayFabClientAPI.Calls.Last(); var newer = Solo(3, id: record.runId);
            MultiplayerRunRecords.RecordSolo("AA", newer); Ack(old, record);
            Check(Pending(reporter) == 1, "older response cannot remove newer completion"); Tick(reporter);
            Ack(PlayFab.PlayFabClientAPI.Calls.Last(), newer); Check(Pending(reporter) == 0, "latest ack removes latest");
        });
        Test("mismatched server response does not lose pending result", () => {
            var reporter = Reporter(); var record = Solo(); MultiplayerRunRecords.RecordSolo("AA", record); Tick(reporter);
            Ack(PlayFab.PlayFabClientAPI.Calls.Last(), Solo()); Check(Pending(reporter) == 1, "mismatched run identity");
        });
        Test("disk failure retains memory, retries storage and recovers from committed state", () => {
            var reporter = Reporter(); CampaignSaveStore.FailWrites = true; var record = Solo(3);
            MultiplayerRunRecords.RecordSolo("AA", record); Check(Pending(reporter) == 1, "memory retained on disk failure");
            string path = UnityEngine.Application.persistentDataPath;
            Check(!File.Exists(Path.Combine(path, "solo_results_v1.json")), "simulated disk failure");
            CampaignSaveStore.FailWrites = false; UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.NotReachable;
            Tick(reporter); Check(File.Exists(Path.Combine(path, "solo_results_v1.json")), "background persistence retry");
            reporter = Reporter(); var flow = new GameFlowManager(); flow.Scene("Lobby1");
            var data = new GameSaveData { soloRankingV1 = new SoloRankingV1 { runId = record.runId, dayOrdinal = 3, lastCreditedDayOrdinal = 3, completedDays = 3 } };
            flow.Load(data); flow.RecoverPersistedSoloRanking(data); Check(Pending(reporter) == 1, "recover already committed receipt");
            CampaignSaveStore.Owner = "BB"; flow.RecoverPersistedSoloRanking(data); Check(Pending(reporter) == 1, "recovery cannot cross owner");
        });
        Test("corrupt solo journal preserves original and leaves multiplayer journal untouched", () => {
            var reporter = Reporter(); string path = UnityEngine.Application.persistentDataPath;
            string multiplayerPath = Path.Combine(path, "multiplayer_results_v2.json");
            File.WriteAllText(multiplayerPath, "{\"pending\":[]}");
            var record = Solo(1); MultiplayerRunRecords.RecordSolo("AA", record);
            MultiplayerRunRecords.RecordSolo("AA", Solo(2, id: record.runId));
            Check(File.ReadAllText(multiplayerPath) == "{\"pending\":[]}", "legacy queue byte-preserved");
            File.WriteAllText(Path.Combine(path, "solo_results_v1.json"), "broken"); reporter = Reporter(path);
            Check(Pending(reporter) == 1, "recover valid solo backup");
            Check(Directory.GetFiles(path, "solo_results_v1.json.unreadable.*").Length == 1, "corrupt original preserved");
            Check(File.ReadAllText(multiplayerPath) == "{\"pending\":[]}", "corruption isolated");
        });
        Test("reporting faults cannot escape the actual flow adapter", () => {
            Reporter(); var flow = new GameFlowManager(); CampaignSaveStore.FailOwnerRead = true;
            flow.BeginSoloRankingRun(); flow.Start(); flow.Finish(); flow.Next(); flow.Capture(); flow.Load(new GameSaveData());
            Check(true, "all reporter exceptions isolated");
        });
        Test("synchronous SDK exception stays pending and transport retries", () => {
            var reporter = Reporter(); var record = Solo(); MultiplayerRunRecords.RecordSolo("AA", record);
            PlayFab.PlayFabClientAPI.FailSend = true; Tick(reporter);
            Check(Pending(reporter) == 1, "synchronous transport failure retained");
            PlayFab.PlayFabClientAPI.FailSend = false; Tick(reporter);
            Check(PlayFab.PlayFabClientAPI.Calls.Count == 1, "busy flag released after SDK exception");
            Ack(PlayFab.PlayFabClientAPI.Calls.Last(), record); Check(Pending(reporter) == 0, "retry acknowledged");
        });
        Test("failed new-run ownership lookup cannot reuse previous run identity", () => {
            Reporter(); var flow = new GameFlowManager(); flow.Scene("Lobby1"); flow.BeginSoloRankingRun();
            flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1.completedDays == 1, "previous run");
            CampaignSaveStore.FailOwnerRead = true; flow.BeginSoloRankingRun(); CampaignSaveStore.FailOwnerRead = false;
            flow.Start(); flow.Finish(); Check(flow.Capture().soloRankingV1 == null, "failed new identity disables reporting");
        });
        Test("real uploader preserves multiplayer host/guest dispatch alongside solo", () => {
            foreach (var destination in new[] { ("CasualDining", "Lobby1 Multiplayer", "casual-session-2"),
                ("FastFood", "Lobby2 Multiplayer", "fastfood-session-2"), ("FineDining", "Lobby3 Multiplayer", "finedining-session-1") })
            {
                var reporter = Reporter(); var run = new MultiplayerRunRecord { runId = Guid.NewGuid().ToString("N"),
                    restaurant = destination.Item1, sceneName = destination.Item2, rulesVersion = destination.Item3,
                    hostAccountId = "AA", hostActor = 1, partySize = 2, completedDay = 3, highestDay = 3, gameVersion = "test",
                    startedUtc = DateTime.UtcNow.ToString("o"), participants = new List<MultiplayerRunParticipant> {
                        new MultiplayerRunParticipant { accountId = "AA", actor = 1, completedDay = 3, highestDay = 3 },
                        new MultiplayerRunParticipant { accountId = "BB", actor = 2, completedDay = 3, highestDay = 3 } } };
                MultiplayerRunRecords.Record(run, 1, true); var solo = Solo(2); MultiplayerRunRecords.RecordSolo("AA", solo);
                Tick(reporter); var host = PlayFab.PlayFabClientAPI.Calls.Last();
                Check(host.Request.FunctionName == "DineInPublishRunV2", "host function unchanged");
                host.Success(new PlayFab.ClientModels.ExecuteCloudScriptResult { FunctionResult = new { accepted = true,
                    bestCompletedDay = 3, successfulDays = new[] { 1, 2, 3 } } });
                Check(LocalSaveManager.Credits == 1 && Pending(reporter) == 1, "host credit and solo pending remain separate");
                Tick(reporter); Ack(PlayFab.PlayFabClientAPI.Calls.Last(), solo);
                Check(LocalSaveManager.Credits == 1 && Pending(reporter) == 0, "solo adds no experience");
                MultiplayerRunRecords.Record(run, 2, false, true); Login("BB"); Tick(reporter);
                var guest = PlayFab.PlayFabClientAPI.Calls.Last(); Check(guest.Request.FunctionName == "DineInClaimRunV2", "guest function unchanged");
                Check(guest.Request.AuthenticationContext.PlayFabId == "BB", "guest owns the claim");
                guest.Success(new PlayFab.ClientModels.ExecuteCloudScriptResult { FunctionResult = new { accepted = true,
                    bestCompletedDay = 3, successfulDays = new[] { 1, 2, 3 } } });
                Check(Pending(reporter) == 0 && LocalSaveManager.Credits == 2, "guest credit unchanged");
            }
        });
        Console.WriteLine($"PASS: {scenarios} headless scenarios / {assertions} assertions. Unity and PlayFab are test doubles.");
    }
}
