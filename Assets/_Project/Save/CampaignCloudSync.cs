using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PlayFab;
using PlayFab.DataModels;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Store = CampaignSaveStore;
using Snapshot = CampaignSaveStore.AccountSnapshot;
using State = CampaignSaveStore.AccountState;

/// <summary>Local-first whole-account sync over the existing PlayFab Entity File.</summary>
public sealed class CampaignCloudSync : MonoBehaviour
{
    public static CampaignCloudSync Instance { get; private set; }
    public static bool HasConflict { get; private set; }
    public static string Status { get; private set; } = "Progress is saved on this device.";
    [SerializeField, Min(1)] private float debounceSeconds = 2f;
    [SerializeField, Min(10)] private float retrySeconds = 30f;
    private float nextCheck, operationDeadline;
    private bool busy, initialCheckComplete;
    public enum SyncState { NotChecked, Checking, PendingUpload, Synced, Unavailable }
    public static SyncState CloudState { get; private set; }
    public static bool LocalReady { get; private set; }
    private bool conflictPresented;
    private static bool SafeMenu => Store.AtMenu && !SceneLoader.HasPendingLoad &&
        (SceneLoader.Instance == null || !SceneLoader.Instance.IsLoading);
    private int generation;
    private string account;
    private Snapshot conflictLocal, conflictCloud;
    private string choiceLocalHash, choiceCloudHash;
    private bool? chooseDevice;
    private CampaignSaveConflictUI conflictUI;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        HasConflict = false; LocalReady = false; CloudState = SyncState.NotChecked; Status = "Progress is saved on this device.";
        if (Instance != null) return;
        var root = new GameObject("[Campaign Cloud Sync]");
        DontDestroyOnLoad(root); root.AddComponent<CampaignCloudSync>();
    }
    private void Awake()
    {
        Instance = this;
        try { Store.InitializeLocalProfile(); }
        catch (Exception e) { Failed("Local profile initialization: " + e.Message); }
    }
    private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; Store.LocalWritten += LocalWritten; }
    private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; Store.LocalWritten -= LocalWritten; generation++; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public static void RequestSync() { if (Instance != null) Instance.nextCheck = 0; }
    private void OnApplicationFocus(bool focused) { if (focused) RequestSync(); }
    private void OnApplicationPause(bool paused) { if (paused) RequestSync(); }
    private void LocalWritten() { nextCheck = Mathf.Min(nextCheck, Time.unscaledTime + debounceSeconds); }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NewGameMenu" || scene.name == "NewMainMenu") Store.NeedsReload = true;
        if (scene.name == "Lobby1" || scene.name == "Lobby2")
        {
            Store.SelectRestaurant(scene.name);
            if (Store.RuntimeCampaign && GameSaveManager.Instance != null &&
                (Store.NeedsReload || !GameSaveManager.Instance.HasCompletedInitialLoad)) GameSaveManager.Instance.ReloadCampaignFromDisk();
        }
        nextCheck = 0;
        if (HasConflict && Store.AtMenu && conflictLocal != null) ShowConflict(conflictLocal, conflictCloud);
    }
    public static bool CanEnterGameplay()
    {
        var sync = Instance;
        if (sync == null) return true;
        try
        {
            // Validate the local documents, not the outcome of a network request.
            var local = Store.ReadAccount(false);
            LocalReady = Store.ValidContent(local);
            bool meaningful = Store.Meaningful(local);
            var auth = PlayFabAuthManager.Instance;
            bool loggedIn = auth?.IsLoggedIn == true;
            bool accountReady = !loggedIn || string.IsNullOrEmpty(local.account) || local.account == auth.PlayFabId;
            var readiness = CampaignSyncPolicy.Entry(LocalReady, meaningful, HasConflict, accountReady,
                loggedIn && (!sync.initialCheckComplete || sync.account != auth.PlayFabId));
            Trace($"Local state ready={LocalReady}; meaningful={meaningful}; revision={local.revision}; cloud={CloudState}; entry={readiness}");
            if (readiness == CampaignSyncPolicy.EntryState.Ready)
            {
                Trace("Gameplay allowed from valid local state.");
                return true;
            }
            string message = readiness == CampaignSyncPolicy.EntryState.Conflict ? "Choose which progress to continue with first." :
                readiness == CampaignSyncPolicy.EntryState.InvalidLocal ? "Your local progress needs recovery before you can continue." :
                readiness == CampaignSyncPolicy.EntryState.AccountSwitch ? "Your local account is being prepared. Please try again." :
                "Checking for progress from your other device. Please try again in a moment.";
            NotificationPopupController.Instance?.Show(message, NotificationPopupController.PopupType.Warning);
        }
        catch (Exception e)
        {
            LocalReady = false;
            Trace("Local validation failed: " + e.Message);
            NotificationPopupController.Instance?.Show("Your local progress needs recovery before you can continue.",
                NotificationPopupController.PopupType.Warning);
        }
        return false;
    }
    private void Update()
    {
        string current = PlayFabAuthManager.Instance?.IsLoggedIn == true ? PlayFabAuthManager.Instance.PlayFabId : null;
        if (current != account)
        {
            if (Time.unscaledTime < nextCheck) return;
            if (!SafeMenu || Photon.Pun.PhotonNetwork.InRoom) return;
            generation++; busy = false; initialCheckComplete = false; CloudState = SyncState.NotChecked; ClearConflict(); nextCheck = 0;
            try { if (!string.IsNullOrEmpty(current)) Store.ActivateAccount(current); account = current; }
            catch (Exception e) { Failed(e.Message); return; }
        }
        if (HasConflict && !conflictPresented && SafeMenu && conflictLocal != null) ShowConflict(conflictLocal, conflictCloud);
        if (Store.ProtectedSession || Photon.Pun.PhotonNetwork.InRoom) return;
        if (busy)
        {
            if (Time.realtimeSinceStartup > operationDeadline) { generation++; Failed("Sync request timed out."); }
            return;
        }
        if (Time.unscaledTime < nextCheck || HasConflict && chooseDevice == null) return;
        nextCheck = Time.unscaledTime + retrySeconds;
        try
        {
            Store.ApplyConfirmedCredits();
            if ((!Store.AtMenu && !Store.RuntimeCampaign) || string.IsNullOrEmpty(account)) return;
            if (Application.internetReachability == NetworkReachability.NotReachable) { Failed("Network reachability reports NotReachable."); return; }
            var context = new PlayFabAuthenticationContext(); context.CopyFrom(PlayFabSettings.staticPlayer);
            if (context.PlayFabId != account || !context.IsEntityLoggedIn() || context.EntityType != "title_player_account")
            { Failed($"Account context unavailable: accountMatches={context.PlayFabId == account}, entityLoggedIn={context.IsEntityLoggedIn()}, entityType={context.EntityType}."); return; }
            if (Store.IsCreditPending) return;
            Sync(context);
        }
        catch (Exception e) { Failed(e.Message); }
    }
    private bool Current(int token, PlayFabAuthenticationContext context) => token == generation &&
        !Store.ProtectedSession && !Photon.Pun.PhotonNetwork.InRoom && account == context.PlayFabId &&
        PlayFabAuthManager.Instance?.IsLoggedIn == true && PlayFabAuthManager.Instance.PlayFabId == account &&
        PlayFabSettings.staticPlayer.ClientSessionTicket == context.ClientSessionTicket;
    private void Sync(PlayFabAuthenticationContext context)
    {
        var local = Store.ReadAccount();
        busy = true; CloudState = SyncState.Checking; Status = "Checking cloud save..."; Trace("Cloud check started."); operationDeadline = Time.realtimeSinceStartup + 100f;
        int token = ++generation;
        var entity = new EntityKey { Id = context.EntityId, Type = context.EntityType };
        PlayFabDataAPI.GetFiles(new GetFilesRequest { Entity = entity, AuthenticationContext = context }, result =>
        {
            if (!Current(token, context)) return;
            try
            {
                bool unavailable = result.Metadata == null || !result.Metadata.TryGetValue(Store.AccountCloudFile, out var saved) || string.IsNullOrEmpty(saved.DownloadUrl);
                if (unavailable && RecoverOwnedUpload(context, entity, result.ProfileVersion))
                { Failed("Recovering this device's interrupted upload."); return; }
                StartCoroutine(ReadCloud(context, entity, result, local, token));
            }
            catch (Exception e) { Failed(e.Message); }
        }, error => { if (Current(token, context)) Failed($"PlayFab {error.Error} (HTTP {error.HttpCode}): {error.ErrorMessage}"); });
    }
    private IEnumerator Download(string url, Action<string> completed, Action<string> failed)
    {
        using var request = UnityWebRequest.Get(url); request.timeout = 30;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) { failed($"Cloud download failed: HTTP {request.responseCode}, result={request.result}, error={request.error}."); yield break; }
        completed(request.downloadHandler.text);
    }
    private IEnumerator ReadCloud(PlayFabAuthenticationContext context, EntityKey entity, GetFilesResponse files, Snapshot local, int token)
    {
        bool failed = false; string json = null;
        void Fail(string reason) { failed = true; if (Current(token, context)) Failed(reason); }
        if (files.Metadata != null && files.Metadata.TryGetValue(Store.AccountCloudFile, out var file))
        {
            if (string.IsNullOrEmpty(file.DownloadUrl)) { Fail("Cloud upload pending; local data preserved."); yield break; }
            yield return Download(file.DownloadUrl, value => json = value, Fail);
            if (failed || !Current(token, context)) yield break;
        }
        Snapshot cloud = null; bool legacy = false;
        try
        {
            if (json != null)
            {
                var header = Newtonsoft.Json.Linq.JObject.Parse(json);
                if ((int?)header["version"] == 2)
                {
                    cloud = JsonUtility.FromJson<Snapshot>(json);
                    if (!Store.ValidAccount(cloud, account)) throw new InvalidDataException("Invalid cloud account save.");
                }
                else if ((int?)header["version"] != 1) throw new InvalidDataException("Unsupported cloud save version.");
                else legacy = true;
            }
            else legacy = true;
        }
        catch (Exception e) { Fail(e.Message); }
        if (failed) yield break;
        if (legacy)
        {
            string fastJson = null;
            if (files.Metadata != null && files.Metadata.TryGetValue(Store.LegacyFastFoodCloudFile, out var fast))
            {
                if (string.IsNullOrEmpty(fast.DownloadUrl)) { Fail("Legacy cloud upload pending."); yield break; }
                yield return Download(fast.DownloadUrl, value => fastJson = value, Fail);
                if (failed || !Current(token, context)) yield break;
            }
            bool cosmeticsDone = false; string appearance = null;
            DineIn.Appearance.PlayFabAppearanceAdapter.ReadLegacy(context,
                value => { appearance = value; cosmeticsDone = true; }, reason => { Fail(reason); cosmeticsDone = true; });
            while (!cosmeticsDone && Current(token, context)) yield return null;
            if (failed || !Current(token, context)) yield break;
            try
            {
                if (json != null || fastJson != null || !string.IsNullOrEmpty(appearance))
                {
                    Store.Snapshot Pair(string text)
                    {
                        if (text == null) return new Store.Snapshot { hash = Store.Hash(null, null) };
                        var pair = JsonUtility.FromJson<Store.Snapshot>(text);
                        if (!Store.Valid(pair)) throw new InvalidDataException("Invalid legacy career save.");
                        return pair;
                    }
                    cloud = new Snapshot { account = account, lineage = "legacy", casual = Pair(json), fastFood = Pair(fastJson),
                        profile = new Store.ProfileProgress { appearance = appearance } };
                    // Legacy cloud never contained roles/tutorials. Infer only completed tutorial prerequisites
                    // from an existing career; do not invent rewards or combine divergent career values.
                    if (Store.Valid(cloud.casual)) cloud.profile.tutorials[0] = 1;
                    if (Store.Valid(cloud.fastFood)) cloud.profile.tutorials[1] = 1;
                    Store.Seal(cloud);
                    if (!Store.ValidAccount(cloud, account)) throw new InvalidDataException("Invalid legacy profile.");
                }
            }
            catch (Exception e) { Fail(e.Message); }
        }
        if (!failed && Current(token, context)) Reconcile(context, entity, files.ProfileVersion, local, cloud, legacy, token);
    }
    private void Reconcile(PlayFabAuthenticationContext context, EntityKey entity, int version, Snapshot local, Snapshot cloud, bool legacy, int token)
    {
        busy = false; initialCheckComplete = true; Trace("Cloud check completed.");
        try
        {
            if (!Current(token, context) || Store.IsCreditPending || Store.ReadAccount().contentHash != local.contentHash) { RequestSync(); return; }
            if (cloud == null && !Store.ValidContent(local)) throw new InvalidDataException("Local data needs recovery; cloud is absent.");
            var state = Store.ReadAccountState();
            if (cloud != null && state.pendingHash == cloud.contentHash) Acknowledge(cloud);
            state = Store.ReadAccountState();
            var action = CampaignSyncPolicy.Decide(Store.ValidContent(local), Store.Meaningful(local), local.contentHash,
                cloud?.contentHash, state.baseHash);
            if (chooseDevice.HasValue)
            {
                if (choiceLocalHash == local.contentHash && choiceCloudHash == cloud?.contentHash)
                    action = chooseDevice.Value ? CampaignSyncPolicy.Action.Upload : CampaignSyncPolicy.Action.Download;
                else { chooseDevice = null; ShowConflict(local, cloud); return; }
            }
            Trace($"local revision={local.revision}; cloud revision={cloud?.revision}; decision={action}");
            if (action == CampaignSyncPolicy.Action.None)
            {
                if (cloud != null) Acknowledge(cloud);
                if (legacy && cloud != null) { Upload(context, entity, version, local, cloud, token); return; }
                ClearConflict(); CloudState = SyncState.Synced; Status = "Progress saved."; return;
            }
            if (action == CampaignSyncPolicy.Action.Conflict) { ShowConflict(local, cloud); return; }
            if (action == CampaignSyncPolicy.Action.Download)
            {
                if (!SafeMenu) { Status = "Cloud progress is ready. Return to the menu to continue."; Trace("Restore deferred until a safe menu; active local progress preserved."); return; }
                Store.InstallAccount(cloud, true);
                LocalReady = true; CloudState = SyncState.Synced;
                ClearConflict(); Status = "Cloud progress saved on this device.";
                Trace("Download validated; local restore persisted.");
                if (legacy) RequestSync();
                return;
            }
            if (!Store.ValidAccount(local, account)) throw new InvalidDataException("Local save is invalid; upload paused.");
            Upload(context, entity, version, local, cloud, token);
        }
        catch (Exception e) { Failed(e.Message); }
    }
    private void Upload(PlayFabAuthenticationContext context, EntityKey entity, int version, Snapshot local, Snapshot cloud, int token)
    {
        // Preserve the losing cloud copy before an explicit replacement.
        if (chooseDevice == true && cloud != null)
            Store.AtomicWrite(Path.Combine(Store.LocalDirectory, "campaign_previous_cloud.json"), JsonUtility.ToJson(cloud));
        local.revision = Math.Max(local.revision, (cloud?.revision ?? 0) + 1);
        if (string.IsNullOrEmpty(local.lineage) || local.lineage == "legacy") local.lineage = Guid.NewGuid().ToString("N");
        Store.Seal(local);
        var state = Store.ReadAccountState(); state.pendingHash = local.contentHash; Store.SaveAccountState(state);
        busy = true; CloudState = SyncState.PendingUpload; Status = "Saving progress..."; operationDeadline = Time.realtimeSinceStartup + 65f;
        PlayFabDataAPI.InitiateFileUploads(new InitiateFileUploadsRequest { Entity = entity, AuthenticationContext = context,
            ProfileVersion = version, FileNames = new List<string> { Store.AccountCloudFile } }, initiated =>
        {
            if (!Current(token, context)) { Abort(context, entity, initiated.ProfileVersion); return; }
            try
            {
                var pending = Store.ReadAccountState(); pending.pendingProfileVersion = initiated.ProfileVersion;
                pending.pendingSinceUtc = DateTime.UtcNow.ToString("o"); Store.SaveAccountState(pending);
                StartCoroutine(UploadBytes(context, entity, initiated, local, token));
            }
            catch (Exception e) { Abort(context, entity, initiated.ProfileVersion); Failed(e.Message); }
        }, error =>
        {
            if (!Current(token, context)) return;
            if (error.Error == PlayFabErrorCode.EntityFileOperationPending) RecoverOwnedUpload(context, entity, version);
            Failed($"PlayFab {error.Error} (HTTP {error.HttpCode}): {error.ErrorMessage}");
        });
    }
    private static bool RecoverOwnedUpload(PlayFabAuthenticationContext context, EntityKey entity, int version)
    {
        var pending = Store.ReadAccountState();
        if (pending.pendingProfileVersion != version || !DateTime.TryParse(pending.pendingSinceUtc, out var started) ||
            (DateTime.UtcNow - started.ToUniversalTime()).TotalSeconds <= 330) return false;
        Abort(context, entity, version);
        return true;
    }
    private IEnumerator UploadBytes(PlayFabAuthenticationContext context, EntityKey entity, InitiateFileUploadsResponse initiated, Snapshot local, int token)
    {
        var details = initiated.UploadDetails?.Find(x => x.FileName == Store.AccountCloudFile);
        if (details == null) { Abort(context, entity, initiated.ProfileVersion); Failed("Missing upload URL."); yield break; }
        using var request = UnityWebRequest.Put(details.UploadUrl, Encoding.UTF8.GetBytes(JsonUtility.ToJson(local)));
        request.timeout = 30; request.SetRequestHeader("x-ms-blob-type", "BlockBlob");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success || !Current(token, context))
        {
            Abort(context, entity, initiated.ProfileVersion);
            if (Current(token, context)) Failed($"Upload interrupted: HTTP {request.responseCode}, result={request.result}, error={request.error}.");
            yield break;
        }
        PlayFabDataAPI.FinalizeFileUploads(new FinalizeFileUploadsRequest { Entity = entity, AuthenticationContext = context,
            ProfileVersion = initiated.ProfileVersion, FileNames = new List<string> { Store.AccountCloudFile } }, result =>
        {
            if (!Current(token, context)) return;
            busy = false;
            try { Acknowledge(local); ClearConflict(); CloudState = SyncState.Synced; Status = "Progress saved."; Trace("Upload confirmed revision=" + local.revision); }
            catch (Exception e) { Failed(e.Message); }
        }, error => { if (Current(token, context)) Failed($"PlayFab {error.Error} (HTTP {error.HttpCode}): {error.ErrorMessage}"); });
    }
    private static void Abort(PlayFabAuthenticationContext context, EntityKey entity, int version)
    {
        // Exact upload version only; never cancel another device's work.
        PlayFabDataAPI.AbortFileUploads(new AbortFileUploadsRequest { Entity = entity, AuthenticationContext = context,
            ProfileVersion = version, FileNames = new List<string> { Store.AccountCloudFile } }, _ => { }, _ => { });
    }
    private void Acknowledge(Snapshot confirmed)
    {
        var current = Store.ReadAccount(); var state = Store.ReadAccountState();
        state.baseHash = confirmed.contentHash; state.pendingHash = null; state.pendingProfileVersion = -1; state.pendingSinceUtc = null;
        state.revision = Math.Max(state.revision, confirmed.revision);
        if (current.contentHash == confirmed.contentHash)
        { state.lineage = confirmed.lineage; state.savedUtc = confirmed.savedUtc; state.device = confirmed.device; }
        Store.SaveAccountState(state);
        DineIn.Appearance.PlayFabAppearanceAdapter.Acknowledge(account, confirmed.profile.appearance);
    }
    private void ShowConflict(Snapshot local, Snapshot cloud)
    {
        HasConflict = true; conflictLocal = local; conflictCloud = cloud; chooseDevice = null;
        Status = "Choose which progress to continue with.";
        if (!SafeMenu) { conflictPresented = false; Trace("Conflict discovered; resolution deferred to menu."); return; }
        conflictPresented = true; Trace("Conflict discovered; menu decision required.");
        if (conflictUI == null) conflictUI = gameObject.AddComponent<CampaignSaveConflictUI>();
        conflictUI.Show(local, cloud, value =>
        {
            chooseDevice = value; choiceLocalHash = conflictLocal.contentHash; choiceCloudHash = conflictCloud?.contentHash;
            RequestSync();
        });
    }
    private void ClearConflict()
    {
        HasConflict = false; conflictPresented = false; conflictLocal = conflictCloud = null; chooseDevice = null;
        if (conflictUI != null) conflictUI.Hide();
    }
    private void Failed(string reason)
    {
        busy = false; initialCheckComplete = true; CloudState = SyncState.Unavailable;
        nextCheck = Time.unscaledTime + retrySeconds;
        // A cloud failure is status, not a modal or a failure of local persistence.
        Status = "Cloud sync pending. Local progress remains on this device.";
        if (conflictUI != null && HasConflict) conflictUI.RetryAvailable();
        Trace("Sync deferred: " + reason);
    }
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void Trace(string message) => Debug.Log("[CloudSave] " + message);
}

public static class CampaignSyncPolicy
{
    public enum Action { None, Upload, Download, Conflict }
    public enum EntryState { Ready, InvalidLocal, Conflict, AccountSwitch, AwaitingRestore }
    public static EntryState Entry(bool valid, bool meaningful, bool conflict, bool accountReady, bool initialCheckPending)
    {
        if (conflict) return EntryState.Conflict;
        if (!valid) return EntryState.InvalidLocal;
        if (!accountReady) return EntryState.AccountSwitch;
        return !meaningful && initialCheckPending ? EntryState.AwaitingRestore : EntryState.Ready;
    }
    public static Action Decide(bool valid, bool meaningful, string localHash, string cloudHash, string baseHash)
    {
        if (!valid) return cloudHash != null ? Action.Download : Action.None;
        if (localHash == cloudHash) return Action.None;
        if (cloudHash == null) return string.IsNullOrEmpty(baseHash) ? (meaningful ? Action.Upload : Action.None) : Action.Conflict;
        if (localHash == baseHash || !meaningful && string.IsNullOrEmpty(baseHash)) return Action.Download;
        if (cloudHash == baseHash) return Action.Upload;
        return Action.Conflict;
    }
    public static Action Decide(bool localExists, string localHash, string cloudHash, string baseHash) =>
        Decide(true, localExists, localHash, cloudHash, baseHash);
}
