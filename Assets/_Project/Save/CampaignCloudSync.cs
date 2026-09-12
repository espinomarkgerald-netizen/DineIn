using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PlayFab;
using PlayFab.DataModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using SyncAction = CampaignSyncPolicy.Action;

/// <summary>Local-first, whole-snapshot sync using PlayFab's optimistic concurrency control.</summary>
public sealed class CampaignCloudSync : MonoBehaviour
{
    private const string FileName = "CampaignSave_v1.json";
    [Serializable] private sealed class SyncState
    {
        public string account;
        public string baseHash;
        public string pendingHash;
    }
    private float nextCheck;
    private bool busy;
    private string lastNotice;
    private string lastAccount;
    private bool uploadPending;
    private int pendingProfileVersion = -1;
    private float pendingVersionSince;
    public static bool HasConflict { get; private set; }
    public static string Status { get; private set; } = "Campaign saves locally. Cloud sync is waiting for an account.";
    private static string StatePath => CampaignSaveStore.SavePath + ".cloud_state.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        HasConflict = false;
        Status = "Campaign saves locally. Cloud sync is waiting for an account.";
        var root = new GameObject("[Campaign Cloud Sync]");
        DontDestroyOnLoad(root);
        root.AddComponent<CampaignCloudSync>();
    }
    private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NewGameMenu" || scene.name == "NewMainMenu") CampaignSaveStore.NeedsReload = true;
        if (scene.name == "Lobby1") ReloadCampaign();
        nextCheck = 0f;
    }
    private void ReloadCampaign()
    {
        // sceneLoaded runs after all Awake calls and before Start/gameplay callbacks.
        if (CampaignSaveStore.RuntimeCampaign && GameSaveManager.Instance != null &&
            (CampaignSaveStore.NeedsReload || !GameSaveManager.Instance.HasCompletedInitialLoad))
            GameSaveManager.Instance.ReloadCampaignFromDisk();
    }
    private void Update()
    {
        if (CampaignSaveStore.ProtectedSession) return;
        string account = PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.IsLoggedIn
            ? PlayFabAuthManager.Instance.PlayFabId : null;
        if (lastAccount != account)
        {
            lastAccount = account; nextCheck = 0f; lastNotice = null;
            uploadPending = false; pendingProfileVersion = -1; HasConflict = false;
        }
        if (busy || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 15f;
        try { CampaignSaveStore.ApplyConfirmedCredits(); }
        catch (Exception e) { Failed(e.Message); return; }
        if ((!CampaignSaveStore.AtMenu && !CampaignSaveStore.RuntimeCampaign) ||
            Application.internetReachability == NetworkReachability.NotReachable) return;
        var auth = PlayFabAuthManager.Instance;
        if (auth == null || !auth.IsLoggedIn || string.IsNullOrEmpty(auth.PlayFabId)) return;
        var context = new PlayFabAuthenticationContext();
        context.CopyFrom(PlayFabSettings.staticPlayer);
        if (context.PlayFabId != auth.PlayFabId || !context.IsEntityLoggedIn() || CampaignSaveStore.IsCreditPending ||
            context.EntityType != "title_player_account") return;
        try { Sync(context); }
        catch (Exception e) { Failed(e.Message); }
    }

    private static bool StillCurrent(PlayFabAuthenticationContext context) =>
        !CampaignSaveStore.ProtectedSession && PlayFabAuthManager.Instance != null &&
        PlayFabAuthManager.Instance.IsLoggedIn && PlayFabAuthManager.Instance.PlayFabId == context.PlayFabId &&
        PlayFabSettings.staticPlayer.ClientSessionTicket == context.ClientSessionTicket;

    private void Sync(PlayFabAuthenticationContext context)
    {
        var local = CampaignSaveStore.Read();
        var state = File.Exists(StatePath) ? JsonUtility.FromJson<SyncState>(File.ReadAllText(StatePath)) : new SyncState();
        if (state == null || File.Exists(StatePath) && string.IsNullOrEmpty(state.account))
            throw new InvalidDataException("Unreadable cloud sync metadata; local progress preserved.");
        if (!string.IsNullOrEmpty(state.account) && state.account != context.PlayFabId)
        {
            Notice("This Campaign belongs to a different account. Local progress is preserved; cloud sync is paused.", true);
            return;
        }
        busy = true;
        var entity = new EntityKey { Id = context.EntityId, Type = context.EntityType };
        PlayFabDataAPI.GetFiles(new GetFilesRequest
        {
            Entity = entity, AuthenticationContext = context
        }, result =>
        {
            if (!StillCurrent(context)) { busy = false; return; }
            if (uploadPending)
            {
                if (pendingProfileVersion != result.ProfileVersion)
                {
                    pendingProfileVersion = result.ProfileVersion;
                    pendingVersionSince = Time.realtimeSinceStartup;
                }
                // Upload URLs expire after five minutes. Clear only an unchanged, expired
                // pending version, so reconnecting after a crash cannot abort a fresh upload.
                if (Time.realtimeSinceStartup - pendingVersionSince > 330f)
                {
                    uploadPending = false;
                    AbortUpload(context, entity, result.ProfileVersion);
                    return;
                }
            }
            if (result.Metadata != null && result.Metadata.TryGetValue(FileName, out GetFileMetadata saved))
            {
                if (string.IsNullOrEmpty(saved.DownloadUrl))
                {
                    uploadPending = true;
                    Failed("A Campaign file upload is still pending.");
                    return;
                }
                StartCoroutine(Download(saved.DownloadUrl, cloud => Reconcile(context, entity, result.ProfileVersion, local, state, cloud)));
            }
            else Reconcile(context, entity, result.ProfileVersion, local, state, null);
        }, error => Failed(error.ErrorMessage));
    }

    private IEnumerator Download(string url, Action<CampaignSaveStore.Snapshot> completed)
    {
        using var request = UnityWebRequest.Get(url);
        request.timeout = 30;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) { Failed("Cloud download unavailable."); yield break; }
        CampaignSaveStore.Snapshot cloud;
        try { cloud = JsonUtility.FromJson<CampaignSaveStore.Snapshot>(request.downloadHandler.text); }
        catch (Exception e) { Failed(e.Message); yield break; }
        if (!CampaignSaveStore.Valid(cloud)) { Failed("Cloud Campaign is invalid or from a newer game version."); yield break; }
        completed(cloud);
    }

    private void Reconcile(PlayFabAuthenticationContext context, EntityKey entity, int profileVersion,
        CampaignSaveStore.Snapshot local, SyncState state, CampaignSaveStore.Snapshot cloud)
    {
            busy = false;
            if (!StillCurrent(context) || CampaignSaveStore.IsCreditPending) return;
            try
            {
                if (CampaignSaveStore.Read().hash != local.hash) return; // Local play continued during the read.
                string remoteHash = cloud?.hash;
                if (remoteHash == local.hash)
                {
                    Acknowledge(state, context.PlayFabId, remoteHash);
                    Status = "Campaign cloud save is synced.";
                    return;
                }
                // Recover a successful upload whose acknowledgement was lost, even if local play continued.
                if (!string.IsNullOrEmpty(state.pendingHash) && remoteHash == state.pendingHash)
                    Acknowledge(state, context.PlayFabId, remoteHash);
                bool localExists = !string.IsNullOrEmpty(local.save) || !string.IsNullOrEmpty(local.checkpoint);
                SyncAction action = CampaignSyncPolicy.Decide(localExists, local.hash, remoteHash, state.baseHash);
                if (action == SyncAction.Download)
                {
                    if (!CampaignSaveStore.AtMenu)
                    {
                        Notice("A newer Campaign cloud save is available. Return to the Main Menu to sync it.");
                        return;
                    }
                    state.account = context.PlayFabId;
                    SaveState(state);
                    CampaignSaveStore.Install(cloud);
                    Acknowledge(state, context.PlayFabId, remoteHash);
                    Notice("Campaign cloud save downloaded. Continue Campaign to resume it.");
                    return;
                }
                if (action == SyncAction.Conflict)
                {
                    state.account = context.PlayFabId;
                    SaveState(state);
                    PreserveConflict(local, cloud, context.PlayFabId);
                    return;
                }
                if (action != SyncAction.Upload) return;
                if (!CampaignSaveStore.Valid(local)) throw new InvalidDataException("Local Campaign is unreadable; sync paused.");
                state.account = context.PlayFabId;
                state.pendingHash = local.hash;
                SaveState(state); // Durable before starting an upload.
                busy = true;
                PlayFabDataAPI.InitiateFileUploads(new InitiateFileUploadsRequest
                {
                    Entity = entity, AuthenticationContext = context,
                    ProfileVersion = profileVersion, FileNames = new List<string> { FileName }
                }, initiated =>
                {
                    StartCoroutine(Upload(context, entity, initiated, local, state));
                }, error =>
                {
                    if (StillCurrent(context) && error.Error == PlayFabErrorCode.EntityFileOperationPending)
                        uploadPending = true;
                    Failed(error.ErrorMessage);
                });
            }
            catch (Exception e) { Failed(e.Message); }
    }

    private IEnumerator Upload(PlayFabAuthenticationContext context, EntityKey entity,
        InitiateFileUploadsResponse initiated, CampaignSaveStore.Snapshot local, SyncState state)
    {
        uploadPending = false;
        pendingProfileVersion = -1;
        if (!StillCurrent(context)) { AbortUpload(context, entity, initiated.ProfileVersion); yield break; }
        var details = initiated.UploadDetails?.Find(entry => entry.FileName == FileName);
        if (details == null) { Failed("Cloud upload URL missing."); yield break; }
        using var request = UnityWebRequest.Put(details.UploadUrl, Encoding.UTF8.GetBytes(JsonUtility.ToJson(local)));
        request.timeout = 30;
        request.SetRequestHeader("x-ms-blob-type", "BlockBlob");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success || !StillCurrent(context))
        {
            AbortUpload(context, entity, initiated.ProfileVersion);
            yield break;
        }
        PlayFabDataAPI.FinalizeFileUploads(new FinalizeFileUploadsRequest
        {
            Entity = entity, AuthenticationContext = context,
            ProfileVersion = initiated.ProfileVersion, FileNames = new List<string> { FileName }
        }, _ =>
        {
            busy = false;
            if (!StillCurrent(context)) return;
            try { Acknowledge(state, context.PlayFabId, local.hash); Status = "Campaign cloud save is synced."; }
            catch (Exception e) { Failed(e.Message); }
        }, error => Failed(error.ErrorMessage));
    }

    private void AbortUpload(PlayFabAuthenticationContext context, EntityKey entity, int version)
    {
        // Abort only the upload version we own. Never abort another device's work.
        PlayFabDataAPI.AbortFileUploads(new AbortFileUploadsRequest
        {
            Entity = entity, AuthenticationContext = context, ProfileVersion = version,
            FileNames = new List<string> { FileName }
        }, _ => Failed("Upload interrupted; will retry."), error => Failed(error.ErrorMessage));
    }
    private static void SaveState(SyncState state) => CampaignSaveStore.AtomicWrite(StatePath, JsonUtility.ToJson(state));
    private static void Acknowledge(SyncState state, string account, string hash)
    {
        state.account = account;
        state.baseHash = hash;
        state.pendingHash = null;
        SaveState(state);
        HasConflict = false;
    }
    private void PreserveConflict(CampaignSaveStore.Snapshot local, CampaignSaveStore.Snapshot cloud, string account)
    {
        string key = CampaignSaveStore.Hash(local.hash, cloud?.hash).Replace('/', '_').Replace('+', '-');
        string path = Path.Combine(Application.persistentDataPath, "campaign_conflicts", account);
        Directory.CreateDirectory(path);
        CampaignSaveStore.AtomicWrite(Path.Combine(path, key + "_local.json"), JsonUtility.ToJson(local));
        if (cloud != null) CampaignSaveStore.AtomicWrite(Path.Combine(path, key + "_cloud.json"), JsonUtility.ToJson(cloud));
        Notice("Campaign sync conflict: both saves changed. Both copies are preserved in " + path +
            ". Local play can continue; neither save was merged or overwritten.", true);
    }
    private void Failed(string message)
    {
        busy = false;
        if (!HasConflict) Status = "Campaign saved locally. Cloud sync will retry: " + message;
        Debug.LogWarning("[Campaign Sync] Deferred; local saves remain available. " + message);
    }
    private void Notice(string message, bool conflict = false)
    {
        HasConflict = conflict;
        if (lastNotice == message) return;
        lastNotice = message;
        Status = message;
        Debug.LogWarning("[Campaign Sync] " + message);
        NotificationPopupController.Instance?.Show(message, NotificationPopupController.PopupType.Warning);
    }
}

/// <summary>Pure reconciliation policy, independent of connectivity and device clocks.</summary>
public static class CampaignSyncPolicy
{
    public enum Action { None, Upload, Download, Conflict }
    public static Action Decide(bool localExists, string localHash, string cloudHash, string baseHash)
    {
        if (localHash == cloudHash || !localExists && cloudHash == null) return Action.None;
        if (cloudHash != null && (!localExists && string.IsNullOrEmpty(baseHash) || localHash == baseHash))
            return Action.Download;
        if (cloudHash != baseHash) return Action.Conflict;
        return localExists ? Action.Upload : Action.Conflict;
    }
}
