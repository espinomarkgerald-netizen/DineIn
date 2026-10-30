using System;
using System.IO;
using UnityEngine;

public partial class AlienApprovalManager
{
    // The authored manager remains the single source of fresh-campaign starting approval.
    public int StartingApproval => Mathf.Clamp(startingApproval, 0, 100);
    public bool UsesSharedCampaignApproval => CampaignSaveStore.RuntimeCampaign &&
        !TutorialSystem.IsTutorialMode && !MultiplayerProgressionContext.IsActive &&
        !MultiplayerProgressionContext.RestorationInProgress && !GameSaveManager.IsPersistenceSuspended;
    private LocalSaveManager.ApprovalProgress unsavedApproval;
    private float nextSharedCheck;
    private bool migrationErrorReported;
    private float nextMigrationAttempt;
    private LocalSaveManager.SaveData verifiedProfile;
    private string verifiedAccount, verifiedDirectory;

    private bool TryGetSharedApproval(out LocalSaveManager.ApprovalProgress state)
    {
        state = null;
        if (!UsesSharedCampaignApproval) return false;
        var data = LocalSaveManager.Data;
        if (LocalSaveManager.NeedsRecovery) return false;
        try
        {
            string directory = CampaignSaveStore.LocalDirectory;
            if (!ReferenceEquals(verifiedProfile, data) || verifiedAccount != data.progressAccount || verifiedDirectory != directory)
            {
                string owner = CampaignSaveStore.ReadAccountState().account;
                if (!string.Equals(data.progressAccount ?? "", owner ?? "", StringComparison.Ordinal)) return false;
                verifiedProfile = data; verifiedAccount = data.progressAccount; verifiedDirectory = directory;
                migrationErrorReported = false; nextMigrationAttempt = 0f;
            }
            if (!LocalSaveManager.ValidApproval(data.sharedApproval)) throw new InvalidDataException("Invalid shared approval.");
            if (data.sharedApproval == null)
            {
                if (Time.unscaledTime < nextMigrationAttempt) return false;
                int highest = -1;
                foreach (string name in new[] { "dinein_save.json", "dinein_fastfood_save.json", "dinein_finedining_save.json" })
                {
                    string path = Path.Combine(CampaignSaveStore.LocalDirectory, name);
                    if (!File.Exists(path)) continue;
                    string json = File.ReadAllText(path);
                    var pair = new CampaignSaveStore.Snapshot { save = json, hash = CampaignSaveStore.Hash(json, null) };
                    if (!CampaignSaveStore.Valid(pair) || !json.Contains("\"approval\""))
                        throw new InvalidDataException("Career approval needs recovery: " + name);
                    int approval = JsonUtility.FromJson<GameSaveData>(json).approval;
                    if (approval < 0 || approval > 100) throw new InvalidDataException("Invalid saved approval: " + name);
                    highest = Math.Max(highest, approval);
                }
                int initial = highest < 0 ? StartingApproval : highest;
                data.sharedApproval = new LocalSaveManager.ApprovalProgress { value = initial, lockedAt100 = initial == 100 };
                SaveSharedApproval(data.sharedApproval);
            }
            state = data.sharedApproval;
            // Normalize an existing 100% value even if an older writer omitted its lock.
            if (state.value == 100 && !state.lockedAt100) { state.lockedAt100 = true; SaveSharedApproval(state); }
            return true;
        }
        catch (Exception error)
        {
            if (!migrationErrorReported) Debug.LogError("[AlienApproval] Shared approval preserved; initialization deferred: " + error.Message);
            migrationErrorReported = true;
            nextMigrationAttempt = Time.unscaledTime + 5f;
            return false;
        }
    }

    private void SaveSharedApproval(LocalSaveManager.ApprovalProgress state)
    {
        unsavedApproval = LocalSaveManager.TrySave() ? null : state;
        if (unsavedApproval != null) Debug.LogWarning("[AlienApproval] Shared approval save pending; it will be retried.");
    }

    private int ResolveCampaignApproval(int requested)
    {
        if (!UsesSharedCampaignApproval) return Mathf.Clamp(requested, 0, 100);
        if (!TryGetSharedApproval(out var state)) return Approval;
        int value = state.lockedAt100 ? 100 : Mathf.Clamp(requested, 0, 100);
        if (state.value != value || value == 100 && !state.lockedAt100)
        {
            state.value = value;
            state.lockedAt100 |= value == 100;
            SaveSharedApproval(state);
        }
        return state.value;
    }

    public bool CongratulationsPending => TryGetSharedApproval(out var state) && state.lockedAt100 && !state.congratulationsDelivered;

    public bool MarkCongratulationsDelivered(string account)
    {
        if (!string.Equals(LocalSaveManager.Data.progressAccount ?? "", account ?? "", StringComparison.Ordinal) ||
            !TryGetSharedApproval(out var state) || !state.lockedAt100) return false;
        state.congratulationsDelivered = true;
        if (LocalSaveManager.TrySave()) return true;
        state.congratulationsDelivered = false;
        return false;
    }

    private void Update()
    {
        if (!UsesSharedCampaignApproval || Time.unscaledTime < nextSharedCheck ||
            GameSaveManager.Instance != null && (!GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave)) return;
        nextSharedCheck = Time.unscaledTime + 1f;
        if (!TryGetSharedApproval(out var state)) return;
        if (ReferenceEquals(unsavedApproval, state)) SaveSharedApproval(state);
        if (Approval != state.value) { Approval = state.value; OnApprovalChanged?.Invoke(Approval); }
    }
}
