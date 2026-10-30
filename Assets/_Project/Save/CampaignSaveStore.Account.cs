using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static partial class CampaignSaveStore
{
    // Packages the existing career documents; GameSaveManager still owns runtime persistence.
    public const string AccountCloudFile = "CampaignSave_v1.json";
    public const string LegacyFastFoodCloudFile = "FastFoodCampaignSave_v1.json";
    private const string StateName = "campaign_sync.json";
    private const string ActiveAccountKey = "DineIn_ActiveCampaignAccount";
    private static string localDirectory;
    private static bool installingAccount;
    public static event Action LocalWritten;
    public static string LocalDirectory => localDirectory ??= ResolveDirectory(PlayerPrefs.GetString(ActiveAccountKey, ""));
    internal static readonly string[] ProgressPreferenceKeys = {
        "TutorialCompleted", "DineIn_FastFoodTutorial_Completed", "DineIn_FastFoodTutorial_Skipped",
        "DineIn_FastFoodTutorial_Chapter_v1", "DineIn_OfficeTutorial_Done", "DineIn_Tutorial_CurrentDay",
        "DineIn_FineDiningTutorial_Completed", "DineIn_FineDiningTutorial_Skipped", "DineIn_FineDiningTutorial_Chapter_v1"
    };

    [Serializable] public sealed class ProfileProgress
    {
        public int[] tutorials = new int[ProgressPreferenceKeys.Length];
        public int coins;
        public List<string> ownedItemIds = new List<string>();
        public List<LocalSaveManager.RoleEntry> roles = new List<LocalSaveManager.RoleEntry>();
        public string appearance;
        public int restaurantProgressionVersion;
        public List<string> unlockedRestaurantIds;
        public LocalSaveManager.ApprovalProgress sharedApproval;
    }
    [Serializable] public sealed class AccountSnapshot
    {
        public int version = 5;
        public string account, lineage;
        public long revision;
        public string savedUtc, device;
        public Snapshot casual, fastFood, fineDining;
        public ProfileProgress profile;
        public string contentHash, checksum;
    }
    [Serializable] private sealed class LegacyProfileProgress
    {
        public int[] tutorials;
        public int coins;
        public List<string> ownedItemIds;
        public List<LocalSaveManager.RoleEntry> roles;
        public string appearance;
    }
    [Serializable] private sealed class LegacyV3AccountSnapshot
    {
        public int version;
        public string account, lineage;
        public long revision;
        public string savedUtc, device;
        public Snapshot casual, fastFood, fineDining;
        public LegacyProfileProgress profile;
        public string contentHash, checksum;
    }
    [Serializable] private sealed class LegacyV4ProfileProgress
    {
        public int[] tutorials;
        public int coins;
        public List<string> ownedItemIds;
        public List<LocalSaveManager.RoleEntry> roles;
        public string appearance;
        public int restaurantProgressionVersion;
        public List<string> unlockedRestaurantIds;
    }
    [Serializable] private sealed class LegacyV4AccountSnapshot
    {
        public int version;
        public string account, lineage;
        public long revision;
        public string savedUtc, device;
        public Snapshot casual, fastFood, fineDining;
        public LegacyV4ProfileProgress profile;
        public string contentHash, checksum;
    }
    private static string ProfileJson(AccountSnapshot value) => value.version < 4
        ? JsonUtility.ToJson(LegacyProfile(value.profile)) : value.version == 4
        ? JsonUtility.ToJson(JsonUtility.FromJson<LegacyV4ProfileProgress>(JsonUtility.ToJson(value.profile)))
        : JsonUtility.ToJson(value.profile);

    private static LegacyProfileProgress LegacyProfile(ProfileProgress profile) =>
        JsonUtility.FromJson<LegacyProfileProgress>(JsonUtility.ToJson(profile));
    private static ProfileProgress NewProfile() => new ProfileProgress {
        restaurantProgressionVersion = LocalSaveManager.RestaurantProgressionVersion,
        unlockedRestaurantIds = new List<string> { LocalSaveManager.CasualDiningId } };

    // Preserve version 2 checksum verification when reading existing cloud saves.
    [Serializable] private sealed class LegacyAccountSnapshot
    {
        public int version;
        public string account, lineage;
        public long revision;
        public string savedUtc, device;
        public Snapshot casual, fastFood;
        public LegacyProfileProgress profile;
        public string contentHash, checksum;
    }

    [Serializable] public sealed class AccountState
    {
        public int version = 2;
        public string account, lineage;
        public long revision;
        public string savedUtc, device, contentHash, baseHash, pendingHash;
        public int pendingProfileVersion = -1;
        public string pendingSinceUtc;
        public ProfileProgress profile;
    }
    [Serializable] private sealed class AccountJournal
    {
        public AccountSnapshot snapshot;
        public AccountState state;
    }
    private static string AccountFolder(string account, string root) => Path.Combine(root,
        "campaign_accounts", Hash(account, null).Replace('/', '_').Replace('+', '-'));
    private static string ResolveDirectory(string account, string root = null)
    {
        root ??= Application.persistentDataPath;
        if (string.IsNullOrEmpty(account)) return root;
        var rootState = ReadAccountState(root);
        return string.IsNullOrEmpty(rootState.account) || rootState.account == account ? root : AccountFolder(account, root);
    }
    public static AccountState ReadAccountState(string directory = null)
    {
        string path = Path.Combine(directory ?? LocalDirectory, StateName);
        if (!File.Exists(path))
        {
            string owner = null;
            foreach (string file in new[] { "dinein_save.json", "dinein_fastfood_save.json" })
            {
                string legacy = Path.Combine(directory ?? LocalDirectory, file + ".cloud_state.json");
                if (!File.Exists(legacy)) continue;
                string legacyJson = File.ReadAllText(legacy);
                var binding = JsonUtility.FromJson<Binding>(legacyJson);
                if (binding == null || string.IsNullOrEmpty(binding.account) || !legacyJson.Contains("\"account\"")) throw new InvalidDataException("Invalid legacy account binding.");
                if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(binding.account) && owner != binding.account)
                    throw new InvalidDataException("Restaurant saves have different account owners.");
                if (!string.IsNullOrEmpty(binding.account)) owner = binding.account;
            }
            return new AccountState { account = owner };
        }
        string json = File.ReadAllText(path);
        var state = JsonUtility.FromJson<AccountState>(json);
        if (state == null || state.version != 2 || state.revision < 0 || !json.Contains("\"account\"") || !json.Contains("\"version\"")) throw new InvalidDataException("Invalid sync metadata.");
        return state;
    }
    public static void SaveAccountState(AccountState state) => AtomicWrite(Path.Combine(LocalDirectory, StateName), JsonUtility.ToJson(state));
    public static void ActivateAccount(string account)
    {
        if (string.IsNullOrEmpty(account) || !AtMenu) return;
        RecoverAccountInstall();
        var previous = ReadAccountState();
        previous.profile = CaptureProfile(previous.account);
        SaveAccountState(previous);
        string target = ResolveDirectory(account);
        bool switched = !string.Equals(LocalDirectory, target, StringComparison.OrdinalIgnoreCase);
        localDirectory = target;
        RecoverAccountInstall();
        var state = ReadAccountState();
        if (!string.IsNullOrEmpty(state.account) && state.account != account) throw new InvalidDataException("Account boundary mismatch.");
        // Reserve the slot before async work so another login cannot inherit the first account's data.
        state.account = account;
        installingAccount = true;
        try
        {
            if (switched)
            {
                state.profile ??= NewProfile();
                SaveAccountState(state);
                ApplyProfile(state.profile, account);
            }
            else
            {
                state.profile ??= CaptureProfile(previous.account);
                if (string.IsNullOrEmpty(previous.account) && state.profile != null) ApplyProfile(state.profile, account);
            }
            SaveAccountState(state);
        }
        finally { installingAccount = false; }
        PlayerPrefs.SetString(ActiveAccountKey, account); PlayerPrefs.Save();
        NeedsReload = true; cachedStamp = null;
    }
    private static Snapshot ReadCareer(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        string checkpoint = Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "_day_start.json");
        string journal = path + ".pending_pair.json";
        if (File.Exists(journal))
        {
            var pair = JsonUtility.FromJson<Snapshot>(File.ReadAllText(journal));
            if (!Valid(pair)) throw new InvalidDataException("Invalid pending career save.");
            AtomicWrite(path, pair.save); WriteOrRemove(checkpoint, pair.checkpoint); File.Delete(journal);
        }
        var result = new Snapshot { save = File.Exists(path) ? File.ReadAllText(path) : null,
            checkpoint = File.Exists(checkpoint) ? File.ReadAllText(checkpoint) : null };
        result.hash = Hash(result.save, result.checkpoint);
        return result;
    }
    public static AccountSnapshot ReadAccount(bool observeChanges = true)
    {
        RecoverAccountInstall();
        var state = ReadAccountState();
        var value = new AccountSnapshot { account = state.account, lineage = state.lineage,
            revision = state.revision, savedUtc = state.savedUtc, device = state.device,
            casual = ReadCareer(LocalDirectory, "dinein_save.json"), fastFood = ReadCareer(LocalDirectory, "dinein_fastfood_save.json"),
            fineDining = ReadCareer(LocalDirectory, "dinein_finedining_save.json"),
            profile = LocalSaveManager.NeedsRecovery ? null : CaptureProfile(state.account) };
        value.contentHash = ContentHash(value);
        if (observeChanges && ValidContent(value) && value.contentHash != state.contentHash)
        {
            string device = PlayerPrefs.GetString("DineIn_SaveInstall", "");
            if (string.IsNullOrEmpty(device)) { device = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("DineIn_SaveInstall", device); PlayerPrefs.Save(); }
            ObserveSnapshot(value, state, device, DateTime.UtcNow.ToString("o"));
            SaveAccountState(state);
        }
        Seal(value);
        return value;
    }
    internal static bool ObserveSnapshot(AccountSnapshot value, AccountState state, string device, string utc)
    {
        if (!ValidContent(value) || value.contentHash == state.contentHash) return false;
        value.revision = checked(state.revision + 1);
        value.lineage = string.IsNullOrEmpty(state.lineage) ? Guid.NewGuid().ToString("N") : state.lineage;
        value.savedUtc = utc; value.device = device;
        state.contentHash = value.contentHash; state.revision = value.revision; state.lineage = value.lineage;
        state.savedUtc = utc; state.device = device; state.profile = value.profile;
        return true;
    }
    public static void NotifyLocalWrite()
    {
        if (installingAccount) return;
        try { ReadAccount(); }
        catch (Exception e) { Debug.LogWarning("[CloudSave] Local files preserved; metadata update deferred: " + e.Message); }
        LocalWritten?.Invoke();
    }
    public static void InitializeLocalProfile()
    {
        RecoverAccountInstall();
        var state = ReadAccountState();
        var data = LocalSaveManager.Data;
        if (data.progressAccount == state.account && !LocalSaveManager.NeedsRecovery) return;
        installingAccount = true;
        try
        {
            if (state.profile != null) ApplyProfile(state.profile, state.account);
            else if (string.IsNullOrEmpty(data.progressAccount) && !LocalSaveManager.NeedsRecovery)
            { data.progressAccount = state.account; LocalSaveManager.Save(); }
        }
        finally { installingAccount = false; }
    }
    private static ProfileProgress CaptureProfile(string account)
    {
        var data = LocalSaveManager.Data;
        if (LocalSaveManager.NeedsRecovery || !string.IsNullOrEmpty(data.progressAccount) && data.progressAccount != account) return null;
        var record = data.appearances?.Find(x => x != null && x.accountId == (string.IsNullOrEmpty(account) ? "guest" : "playfab:" + account));
        var profile = new ProfileProgress { coins = data.coins, roles = data.roles, ownedItemIds = data.ownedItemIds, appearance = record?.json,
            restaurantProgressionVersion = data.restaurantProgressionVersion, unlockedRestaurantIds = data.unlockedRestaurantIds, sharedApproval = data.sharedApproval };
        for (int i = 0; i < ProgressPreferenceKeys.Length; i++) profile.tutorials[i] = PlayerPrefs.GetInt(ProgressPreferenceKeys[i], 0);
        return JsonUtility.FromJson<ProfileProgress>(JsonUtility.ToJson(profile));
    }
    private static void ApplyProfile(ProfileProgress profile, string account)
    {
        for (int i = 0; i < ProgressPreferenceKeys.Length; i++)
            PlayerPrefs.SetInt(ProgressPreferenceKeys[i], profile.tutorials != null && i < profile.tutorials.Length ? profile.tutorials[i] : 0);
        PlayerPrefs.Save();
        var data = LocalSaveManager.Data;
        bool sameProgressAccount = data.progressAccount == account;
        var previousApproval = sameProgressAccount ? data.sharedApproval : null;
        data.sharedApproval = profile.sharedApproval == null ? null :
            JsonUtility.FromJson<LocalSaveManager.ApprovalProgress>(JsonUtility.ToJson(profile.sharedApproval));
        if (previousApproval?.lockedAt100 == true || previousApproval?.value == 100)
        {
            data.sharedApproval ??= new LocalSaveManager.ApprovalProgress();
            data.sharedApproval.value = 100; data.sharedApproval.lockedAt100 = true;
            data.sharedApproval.congratulationsDelivered |= previousApproval.congratulationsDelivered;
        }
        data.restaurantProgressionVersion = profile.restaurantProgressionVersion;
        data.unlockedRestaurantIds = profile.unlockedRestaurantIds == null ? null : new List<string>(profile.unlockedRestaurantIds);
        LocalSaveManager.InitializeRestaurantProgression(data, true);
        data.progressAccount = account;
        data.coins = profile.coins; data.roles = profile.roles ?? new List<LocalSaveManager.RoleEntry>();
        var restaurantCredit = sameProgressAccount ? data.ownedItemIds.FindAll(id => id != null && (id.StartsWith("restaurant-day:", StringComparison.Ordinal) || id.StartsWith("restaurant-order:", StringComparison.Ordinal))) : new List<string>();
        data.ownedItemIds = new List<string>(profile.ownedItemIds ?? new List<string>());
        foreach (string id in restaurantCredit) if (!data.ownedItemIds.Contains(id)) data.ownedItemIds.Add(id);
        DineIn.Appearance.PlayFabAppearanceAdapter.InstallSyncedAppearance(account, profile.appearance);
        LocalSaveManager.MarkRestored();
        if (!LocalSaveManager.TrySave()) throw new IOException("Could not persist restored profile.");
    }
    public static string ContentHash(AccountSnapshot value)
    {
        string casual = Hash(value.casual?.save, value.casual?.checkpoint);
        string fastFoodAndProfile = Hash(Hash(value.fastFood?.save, value.fastFood?.checkpoint), ProfileJson(value));
        if (value.version == 2) return Hash(casual, fastFoodAndProfile);
        string fineDiningAndProfile = Hash(Hash(value.fineDining?.save, value.fineDining?.checkpoint), ProfileJson(value));
        return Hash(casual, Hash(Hash(value.fastFood?.save, value.fastFood?.checkpoint), fineDiningAndProfile));
    }
    public static void Seal(AccountSnapshot value)
    {
        value.contentHash = ContentHash(value); value.checksum = null;
        if (value.version == 2)
        {
            var legacy = JsonUtility.FromJson<LegacyAccountSnapshot>(JsonUtility.ToJson(value));
            value.checksum = Hash(JsonUtility.ToJson(legacy), null);
        }
        else if (value.version == 3)
        {
            var legacy = JsonUtility.FromJson<LegacyV3AccountSnapshot>(JsonUtility.ToJson(value));
            value.checksum = Hash(JsonUtility.ToJson(legacy), null);
        }
        else if (value.version == 4)
        {
            var legacy = JsonUtility.FromJson<LegacyV4AccountSnapshot>(JsonUtility.ToJson(value));
            value.checksum = Hash(JsonUtility.ToJson(legacy), null);
        }
        else value.checksum = Hash(JsonUtility.ToJson(value), null);
    }
    public static bool ValidAccount(AccountSnapshot value, string account)
    {
        if (value == null || (value.version != 2 && value.version != 3 && value.version != 4 && value.version != 5) || value.account != account ||
            value.revision < 0 || !ValidContent(value) || value.contentHash != ContentHash(value)) return false;
        if (value.version == 2)
        {
            var legacy = new LegacyAccountSnapshot { version = 2, account = value.account, lineage = value.lineage,
                revision = value.revision, savedUtc = value.savedUtc, device = value.device,
                casual = value.casual, fastFood = value.fastFood, profile = LegacyProfile(value.profile),
                contentHash = value.contentHash, checksum = value.checksum };
            string checksum = legacy.checksum; legacy.checksum = null;
            return !string.IsNullOrEmpty(checksum) && checksum == Hash(JsonUtility.ToJson(legacy), null);
        }
        if (value.version == 3)
        {
            var legacy = JsonUtility.FromJson<LegacyV3AccountSnapshot>(JsonUtility.ToJson(value));
            string checksum = legacy.checksum; legacy.checksum = null;
            return !string.IsNullOrEmpty(checksum) && checksum == Hash(JsonUtility.ToJson(legacy), null);
        }
        if (value.version == 4)
        {
            var legacy = JsonUtility.FromJson<LegacyV4AccountSnapshot>(JsonUtility.ToJson(value));
            string checksum = legacy.checksum; legacy.checksum = null;
            return !string.IsNullOrEmpty(checksum) && checksum == Hash(JsonUtility.ToJson(legacy), null);
        }
        var copy = JsonUtility.FromJson<AccountSnapshot>(JsonUtility.ToJson(value));
        string currentChecksum = copy.checksum; copy.checksum = null;
        return !string.IsNullOrEmpty(currentChecksum) && currentChecksum == Hash(JsonUtility.ToJson(copy), null);
    }
    public static bool ValidContent(AccountSnapshot value)
    {
        bool Career(Snapshot pair) => pair != null && pair.version == 1 && pair.hash == Hash(pair.save, pair.checkpoint) &&
            ((string.IsNullOrEmpty(pair.save) && string.IsNullOrEmpty(pair.checkpoint)) || Valid(pair));
        if (value == null || !Career(value.casual) || !Career(value.fastFood) ||
            (value.version >= 3 && !Career(value.fineDining)) ||
            (value.profile?.tutorials?.Length != 6 && value.profile?.tutorials?.Length != ProgressPreferenceKeys.Length) ||
            value.profile.coins < 0 || value.profile.roles == null || value.profile.ownedItemIds == null) return false;
        if (value.version >= 4 && (value.profile.restaurantProgressionVersion < 1 || value.profile.unlockedRestaurantIds == null)) return false;
        if (!LocalSaveManager.ValidApproval(value.profile.sharedApproval)) return false;
        foreach (int flag in value.profile.tutorials) if (flag < 0 || flag > 100) return false;
        foreach (var role in value.profile.roles) if (role == null || role.highestUnlockedLevel < 1 || role.completedLevels == null) return false;
        return string.IsNullOrEmpty(value.profile.appearance) || PlayerCustomizationData.TryReadAppearance(value.profile.appearance, out _);
    }
    public static bool Meaningful(AccountSnapshot value)
    {
        if (!ValidContent(value)) return false;
        foreach (var pair in new[] { value.casual, value.fastFood, value.version >= 3 ? value.fineDining : null })
        {
            if (pair == null) continue;
            foreach (string json in new[] { pair.save, pair.checkpoint })
            {
                if (string.IsNullOrEmpty(json)) continue;
                var data = JsonUtility.FromJson<GameSaveData>(json);
                if (data.currentDay > 1 || data.money != 5000 || data.campaignCompleted || data.lobbyCompleted || data.kitchenCompleted ||
                    data.financeHistory?.Count > 0 || data.moneyTransactions?.Count > 0 || data.purchasedEquipmentIDs?.Count > 0 ||
                    data.campaignCreditReceipts?.Count > 0 || data.restockOrders?.Count > 0 || data.fastFoodDayComplete ||
                    data.fineDiningDayComplete) return true;
            }
        }
        foreach (int flag in value.profile.tutorials) if (flag > 0) return true;
        foreach (var role in value.profile.roles) if (role.highestUnlockedLevel > 1 || role.completedLevels.Count > 0) return true;
        if (value.profile.sharedApproval?.lockedAt100 == true) return true;
        return value.profile.unlockedRestaurantIds?.Exists(id => id != LocalSaveManager.CasualDiningId) == true || value.profile.coins != 0 || value.profile.ownedItemIds.Count > 0 || !string.IsNullOrEmpty(value.profile.appearance);
    }
    public static void InstallAccount(AccountSnapshot value, bool acknowledge)
    {
        if (!AtMenu || SceneLoader.HasPendingLoad || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) ||
            IsCreditPending || Photon.Pun.PhotonNetwork.InRoom || !ValidAccount(value, ReadAccountState().account))
            throw new InvalidOperationException("Progress cannot be restored here.");
        var before = ReadAccount(false);
        AtomicWrite(Path.Combine(LocalDirectory, "campaign_before_cloud.json"), JsonUtility.ToJson(before));
        var state = ReadAccountState();
        state.lineage = value.lineage; state.revision = value.revision; state.savedUtc = value.savedUtc;
        state.device = value.device; state.contentHash = value.contentHash; state.profile = value.profile;
        if (acknowledge) { state.baseHash = value.contentHash; state.pendingHash = null; state.pendingProfileVersion = -1; state.pendingSinceUtc = null; }
        AtomicWrite(Path.Combine(LocalDirectory, "campaign_restore.pending.json"), JsonUtility.ToJson(new AccountJournal { snapshot = value, state = state }));
        RecoverAccountInstall();
    }
    public static void RecoverAccountInstall() => RecoverAccountInstall(LocalDirectory, ApplyProfile);
    // Directory/callback parameters also permit Editor tests against throwaway files, without live accounts.
    internal static void RecoverAccountInstall(string directory, Action<ProfileProgress, string> applyProfile)
    {
        string path = Path.Combine(directory, "campaign_restore.pending.json");
        if (!File.Exists(path)) return;
        var journal = JsonUtility.FromJson<AccountJournal>(File.ReadAllText(path));
        if (journal?.state == null || !ValidAccount(journal.snapshot, journal.state.account)) throw new InvalidDataException("Invalid restore journal.");
        installingAccount = true;
        try
        {
            WriteCareer(directory, "dinein_save.json", journal.snapshot.casual);
            WriteCareer(directory, "dinein_fastfood_save.json", journal.snapshot.fastFood);
            WriteCareer(directory, "dinein_finedining_save.json", journal.snapshot.fineDining ??
                new Snapshot { hash = Hash(null, null) });
            applyProfile(journal.snapshot.profile, journal.state.account);
            AtomicWrite(Path.Combine(directory, StateName), JsonUtility.ToJson(journal.state));
            File.Delete(path); NeedsReload = true; cachedStamp = null;
        }
        finally { installingAccount = false; }
    }
    private static void WriteCareer(string directory, string name, Snapshot value)
    {
        string path = Path.Combine(directory, name);
        WriteOrRemove(path, value.save);
        WriteOrRemove(Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + "_day_start.json"), value.checkpoint);
        if (File.Exists(path + ".pending_pair.json")) File.Delete(path + ".pending_pair.json");
    }
    private static void WriteOrRemove(string path, string content)
    {
        if (!string.IsNullOrEmpty(content)) AtomicWrite(path, content);
        else if (File.Exists(path)) File.Delete(path);
    }
}
