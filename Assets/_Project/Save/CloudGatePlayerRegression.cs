#if UNITY_EDITOR || DINEIN_CLOUD_GATE_TEST
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Store = CampaignSaveStore;

/// <summary>Opt-in regression player. Only runs in the separately named diagnostic build.</summary>
public sealed class CloudGatePlayerRegression : MonoBehaviour
{
    const string Product = "DineInCloudGateVerification";
    const string Account = "cloud-gate-fixture";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string report;
    static void Check(bool value, string label)
    {
        File.AppendAllText(report, (value ? "PASS " : "FAIL ") + label + "\n");
        if (!value) throw new Exception(label);
    }
    static void Field(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static void Property(Type type, object obj, string name, object value) => type.GetProperty(name).SetValue(obj, value);
    static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Prepare()
    {
        if (Application.productName != Product || Array.IndexOf(Environment.GetCommandLineArgs(), "--cloud-gate-test") < 0) return;
        Directory.CreateDirectory(Application.persistentDataPath);
        report = Path.Combine(Application.persistentDataPath, "cloud-gate-results.txt");
        File.WriteAllText(report, "Development player regression; isolated fixture account; no live cloud requests.\n");
        // This product has its own player preferences and persistent directory.
        PlayerPrefs.DeleteAll();
        WriteFixture(7);
        new GameObject("Cloud gate regression").AddComponent<CloudGatePlayerRegression>();
    }
    static void WriteFixture(int day)
    {
        var data = new GameSaveData { currentDay = day, money = day == 1 ? 5000 : 23456 };
        var root = Application.persistentDataPath;
        foreach (string name in new[] { "dinein_save", "dinein_fastfood_save" })
        {
            File.WriteAllText(Path.Combine(root, name + ".json"), JsonUtility.ToJson(data));
            File.WriteAllText(Path.Combine(root, name + "_day_start.json"), JsonUtility.ToJson(data));
        }
        File.WriteAllText(Path.Combine(root, "campaign_sync.json"), JsonUtility.ToJson(new Store.AccountState { account = Account }));
        File.WriteAllText(Path.Combine(root, "singleplayer_save.json"), JsonUtility.ToJson(new LocalSaveManager.SaveData { progressAccount = Account }));
    }
    void Awake() { DontDestroyOnLoad(gameObject); }
    IEnumerator Start()
    {
        var run = Run();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception e) { File.AppendAllText(report, "ERROR " + e + "\n"); Application.Quit(1); yield break; }
            yield return next;
        }
        File.AppendAllText(report, "COMPLETE\n");
        Application.Quit(0);
    }
    IEnumerator Run()
    {
        float deadline = Time.realtimeSinceStartup + 60;
        while (PlayFabAuthManager.Instance == null && Time.realtimeSinceStartup < deadline) yield return null;
        var auth = PlayFabAuthManager.Instance;
        Check(auth != null, "authentication component loaded");
        auth.enabled = false;
        var sync = CampaignCloudSync.Instance;
        sync.enabled = false; // Deterministically inject unavailable/checking states without hitting a real account.
        Property(typeof(PlayFabAuthManager), auth, "IsLoggedIn", true);
        Property(typeof(PlayFabAuthManager), auth, "PlayFabId", Account);
        Field(sync, "account", Account);
        while (SceneLoader.HasPendingLoad || SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) yield return null;
        SceneLoader.Instance.LoadScene("NewGameMenu");
        deadline = Time.realtimeSinceStartup + 60;
        while ((SceneManager.GetActiveScene().name != "NewGameMenu" || SceneLoader.Instance.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(SceneManager.GetActiveScene().name == "NewGameMenu", "actual NewGameMenu loaded");
        foreach (var state in new[] { CampaignCloudSync.SyncState.Synced, CampaignCloudSync.SyncState.Checking,
            CampaignCloudSync.SyncState.Unavailable, CampaignCloudSync.SyncState.PendingUpload })
        {
            Property(typeof(CampaignCloudSync), null, "CloudState", state);
            Field(sync, "initialCheckComplete", state != CampaignCloudSync.SyncState.Checking);
            Check(CampaignCloudSync.CanEnterGameplay(), "meaningful local + " + state + " permits Play");
        }
        Call(sync, "Failed", "Injected network offline failure");
        Check(CampaignCloudSync.CanEnterGameplay(), "network failure permits local Play");
        Call(sync, "Failed", "Injected download timeout");
        Check(CampaignCloudSync.CanEnterGameplay(), "download timeout permits local Play");
        Property(typeof(CampaignCloudSync), null, "HasConflict", true);
        Check(!CampaignCloudSync.CanEnterGameplay(), "confirmed conflict blocks entry");
        Call(sync, "ClearConflict");

        var original = Store.ReadAccount();
        WriteFixture(1);
        typeof(LocalSaveManager).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        foreach (string key in Store.ProgressPreferenceKeys) PlayerPrefs.SetInt(key, 0);
        Field(sync, "initialCheckComplete", false);
        Check(!Store.Meaningful(Store.ReadAccount(false)), "generated Day 1 documents are not meaningful progress");
        Check(!CampaignCloudSync.CanEnterGameplay(), "fresh local waits for initial cloud restore");
        Store.InstallAccount(original, true);
        Field(sync, "initialCheckComplete", true);
        Check(CampaignCloudSync.CanEnterGameplay(), "restored meaningful save permits Play");
        Check(JsonUtility.FromJson<GameSaveData>(File.ReadAllText(Path.Combine(Store.LocalDirectory, "dinein_save.json"))).currentDay == 7,
            "cloud fixture restore persists as a real local career");
        WriteFixture(1);
        typeof(LocalSaveManager).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        Field(sync, "initialCheckComplete", true);
        Check(CampaignCloudSync.CanEnterGameplay(), "fresh local + completed no-cloud check permits new game");
        Store.InstallAccount(original, true);

        PlayerCustomizationData.SelectProfile(Account);
        var catalog = Resources.Load<DineIn.Appearance.AppearanceCatalog>("AppearanceCatalog");
        if (catalog == null) catalog = Resources.FindObjectsOfTypeAll<DineIn.Appearance.AppearanceCatalog>()[0];
        Check(PlayerCustomizationData.CommitAppearance(new DineIn.Appearance.AppearanceRecipe(), catalog), "offline customization commits locally");
        var savedProfile = JsonUtility.FromJson<LocalSaveManager.SaveData>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "singleplayer_save.json")));
        Check(savedProfile.appearances.Exists(x => x.pendingUpload && !string.IsNullOrEmpty(x.json)), "offline appearance persisted and pending upload");
        Check(Store.ReadAccountState().contentHash != Store.ReadAccountState().baseHash, "local modification remains dirty for later sync");

        PlayerPrefs.SetInt("TutorialCompleted", 1);
        Field(sync, "initialCheckComplete", false);
        Property(typeof(CampaignCloudSync), null, "CloudState", CampaignCloudSync.SyncState.Checking);
        var controller = FindFirstObjectByType<GameModePopupController>(FindObjectsInactive.Include);
        Check(controller != null, "real campaign menu controller present");
        controller.ChooseCampaign();
        bool restoreRejected = false;
        try { Store.InstallAccount(original, true); } catch (InvalidOperationException) { restoreRejected = true; }
        Check(restoreRejected, "cloud restore cannot replace local data during scene transition");
        deadline = Time.realtimeSinceStartup + 60;
        while (SceneManager.GetActiveScene().name != "Lobby1" && Time.realtimeSinceStartup < deadline) yield return null;
        Check(SceneManager.GetActiveScene().name == "Lobby1", "real Play route enters Casual Dining while cloud is checking");
        Call(sync, "ShowConflict", original, original);
        Check(CampaignCloudSync.HasConflict && sync.GetComponent<CampaignSaveConflictUI>() == null, "late conflict retained without gameplay modal");
        restoreRejected = false;
        try { Store.InstallAccount(original, true); } catch (InvalidOperationException) { restoreRejected = true; }
        Check(restoreRejected, "cloud restore rejected inside restaurant gameplay");
        Call(sync, "ClearConflict");
        while (SceneLoader.Instance.IsLoading) yield return null;
        SceneLoader.Instance.LoadScene("NewGameMenu");
        deadline = Time.realtimeSinceStartup + 60;
        while ((SceneManager.GetActiveScene().name != "NewGameMenu" || SceneLoader.Instance.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
        Call(sync, "Failed", "Injected backend failure");
        controller = FindFirstObjectByType<GameModePopupController>(FindObjectsInactive.Include);
        controller.ChooseCampaign();
        deadline = Time.realtimeSinceStartup + 60;
        while (SceneManager.GetActiveScene().name != "Lobby1" && Time.realtimeSinceStartup < deadline) yield return null;
        Check(SceneManager.GetActiveScene().name == "Lobby1", "real Play route enters Casual Dining while cloud is unavailable");
    }
}
#endif
