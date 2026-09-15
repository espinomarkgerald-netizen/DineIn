using System;
using System.Collections.Generic;
using System.IO;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[Serializable]
public sealed class MultiplayerRunParticipant
{
    public string accountId, displayName;
    public int actor, completedDay, provisionalDay, highestDay = 1, provisionalHighestDay, loadEpoch;
    public double disconnectDeadline;
    public bool departed;
}

// Deliberately contains no restaurant save state or credentials.
[Serializable]
public sealed class MultiplayerRunRecord
{
    public int schema = 2, revision = 1, hostActor, partySize, highestDay = 1, completedDay;
    public string runId, hostAccountId, gameVersion, rulesVersion = MultiplayerSessionManager.ResultRulesVersion;
    public string restaurant = "CasualDining", startedUtc, endedUtc, endReason;
    public List<MultiplayerRunParticipant> participants = new();
}

public sealed class MultiplayerRunRecords : MonoBehaviour
{
    [Serializable] private sealed class Pending
    {
        public string accountId;
        public MultiplayerRunRecord record;
        public bool host, final;
        [NonSerialized] public float retryAfter;
    }
    [Serializable] private sealed class Outbox { public List<Pending> pending = new(); }
    [Serializable] private sealed class Reply { public bool accepted; public int bestCompletedDay; public string message; }
    private static MultiplayerRunRecords instance;
    private Outbox outbox;
    private bool busy;
    private bool preserveUnreadableFile;
    private float nextAttempt;
    public static string Status { get; private set; } = "Run records ready.";
    private static string PathName => Path.Combine(Application.persistentDataPath, "multiplayer_results_v2.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        instance = new GameObject("[Multiplayer Run Records]").AddComponent<MultiplayerRunRecords>();
        DontDestroyOnLoad(instance.gameObject);
    }
    private void Awake()
    {
        try
        {
            outbox = File.Exists(PathName) ? JsonUtility.FromJson<Outbox>(File.ReadAllText(PathName)) : new Outbox();
            if (!ValidOutbox(outbox)) throw new InvalidDataException("Invalid result outbox");
        }
        catch (Exception)
        {
            try
            {
                outbox = File.Exists(PathName + ".bak") ? JsonUtility.FromJson<Outbox>(File.ReadAllText(PathName + ".bak")) : null;
                if (!ValidOutbox(outbox)) outbox = new Outbox();
                if (File.Exists(PathName)) File.Copy(PathName, PathName + ".unreadable." + DateTime.UtcNow.Ticks);
                Status = "Recovered result storage; the unreadable file was preserved.";
            }
            catch (Exception)
            {
                outbox = new Outbox(); preserveUnreadableFile = true;
                Status = "Result storage is unavailable. Account sync will still be attempted.";
            }
        }
    }
    private static bool ValidOutbox(Outbox value) => value?.pending != null
        && !value.pending.Exists(p => p == null || !Valid(p.record)
            || !p.record.participants.Exists(member => member.accountId == p.accountId));
    public static bool Valid(MultiplayerRunRecord value)
    {
        if (value == null || value.schema != 2 || !Guid.TryParseExact(value.runId, "N", out _)
            || value.rulesVersion != MultiplayerSessionManager.ResultRulesVersion || value.restaurant != "CasualDining"
            || value.revision < 1 || value.completedDay < 0 || value.completedDay > 1000000 || value.highestDay > 1000001
            || value.highestDay < Math.Max(1, value.completedDay)
            || value.partySize < 2 || value.partySize > 4 || value.participants?.Count != value.partySize) return false;
        var accounts = new HashSet<string>(StringComparer.Ordinal);
        var actors = new HashSet<int>();
        bool host = false;
        foreach (var p in value.participants)
        {
            if (p == null || string.IsNullOrEmpty(p.accountId) || p.accountId.Length > 64 || p.actor <= 0
                || !accounts.Add(p.accountId) || !actors.Add(p.actor) || p.completedDay < 0
                || p.completedDay > value.completedDay || p.provisionalDay < 0 || p.provisionalDay > value.completedDay
                || p.highestDay < Math.Max(1, p.completedDay) || p.highestDay > value.highestDay
                || p.provisionalHighestDay < 0 || p.provisionalHighestDay > value.highestDay) return false;
            host |= p.actor == value.hostActor && p.accountId == value.hostAccountId;
        }
        return host;
    }
    public static void Record(MultiplayerRunRecord run, int actor, bool host, bool final = false)
    {
        if (instance?.outbox == null || !Valid(run)) return;
        var participant = run.participants.Find(p => p.actor == actor);
        if (participant == null) return;
        // Identity is taken from the frozen roster, never from the account currently logged in later.
        // Retain every host revision until acknowledged. A guest may need an earlier
        // completed-day receipt after leaving while the host continues offline.
        var pending = instance.outbox.pending.Find(p => p.accountId == participant.accountId && p.record.runId == run.runId
            && (!host && !p.host || p.record.revision == run.revision));
        if (pending != null && pending.record.revision > run.revision) return;
        if (pending == null)
        {
            pending = new Pending { accountId = participant.accountId };
            instance.outbox.pending.Add(pending);
        }
        pending.host |= host;
        pending.final |= final || !string.IsNullOrEmpty(run.endReason) || participant.departed;
        pending.record = JsonUtility.FromJson<MultiplayerRunRecord>(JsonUtility.ToJson(run));
        instance.Save();
        instance.nextAttempt = Mathf.Min(instance.nextAttempt, Time.realtimeSinceStartup + 1f);
    }
    private void Save()
    {
        if (preserveUnreadableFile) return;
        try
        {
            string temp = PathName + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(outbox));
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak");
            else File.Move(temp, PathName);
            Status = "Run result recorded locally; account sync pending.";
        }
        catch (Exception) { Status = "Unable to store the run receipt; account submission will still be attempted."; }
    }
    private void Update()
    {
        if (outbox == null || busy || Time.realtimeSinceStartup < nextAttempt) return;
        nextAttempt = Time.realtimeSinceStartup + 1f;
        var auth = PlayFabAuthManager.Instance;
        if (auth == null || !auth.IsLoggedIn || Application.internetReachability == NetworkReachability.NotReachable) return;
        var pending = outbox.pending.Find(p => p.accountId == auth.PlayFabId && p.retryAfter <= Time.realtimeSinceStartup);
        if (pending == null) return;
        var context = new PlayFabAuthenticationContext();
        context.CopyFrom(PlayFabSettings.staticPlayer);
        if (context.PlayFabId != pending.accountId || !context.IsClientLoggedIn()) return;
        // An unresolved guest receipt must not block later runs for the same account.
        pending.retryAfter = Time.realtimeSinceStartup + 30f;
        string sent = JsonUtility.ToJson(pending);
        busy = true;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            AuthenticationContext = context,
            FunctionName = pending.host ? "DineInPublishRunV2" : "DineInClaimRunV2",
            FunctionParameter = new { record = pending.record, final = pending.final },
            GeneratePlayStreamEvent = false
        }, response =>
        {
            busy = false;
            // A late callback cannot clear or submit another signed-in account's receipt.
            if (PlayFabAuthManager.Instance == null || PlayFabAuthManager.Instance.PlayFabId != pending.accountId
                || PlayFabSettings.staticPlayer.ClientSessionTicket != context.ClientSessionTicket) return;
            if (response.Error != null || response.FunctionResult == null)
            { Status = "Run result pending: account recording service is unavailable."; return; }
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(PlayFab.Json.PlayFabSimpleJson.SerializeObject(response.FunctionResult)); }
            catch (Exception) { Status = "Run result pending: invalid account response."; return; }
            if (reply == null || !reply.accepted)
            { Status = "Run result pending: " + (reply?.message ?? "awaiting host receipt"); return; }
            if (JsonUtility.ToJson(pending) == sent) { outbox.pending.Remove(pending); Save(); }
            nextAttempt = Time.realtimeSinceStartup + 1f;
            Status = "Account record synced. Best: " + reply.bestCompletedDay + " completed days.";
        }, error => { busy = false; Status = "Run result pending; account sync will retry."; });
    }
}
