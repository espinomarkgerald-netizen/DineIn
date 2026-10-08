#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Store = CampaignSaveStore;
using Decision = CampaignSyncPolicy.Action;

/// <summary>No menu item and no live service calls. Only temporary files are used.</summary>
public static class CampaignCloudSyncRegression
{
    [Serializable] private sealed class Journal { public Store.AccountSnapshot snapshot; public Store.AccountState state; }
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run save regression outside Play Mode.");
        string directory = Path.Combine(Path.GetTempPath(), "DineIn-cloud-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        void Check(bool result, string label) { if (!result) throw new Exception(label); report.AppendLine("PASS " + label); }
        Store.Snapshot Pair(int day, int money)
        {
            var data = new GameSaveData { currentDay = day, money = money };
            data.unlockedRecipeIDs.Add("recipe-test"); data.unlockedEquipmentIDs.Add("equipment-test");
            data.inventoryStocks.Add(new InventorySaveEntry { stock = 12 });
            string json = JsonUtility.ToJson(data);
            return new Store.Snapshot { save = json, checkpoint = json, hash = Store.Hash(json, json) };
        }
        Store.Snapshot FineDiningPair()
        {
            var data = new GameSaveData { currentDay = 7, money = 18450 };
            data.unlockedEquipmentIDs.Add("fine-dining-table-6");
            data.fineDiningReservations = new FineDiningReservationSave { day = 7, groupBudget = 12 };
            data.fineDiningReservations.bookings.Add(new FineDiningBooking { id = "saved-booking", guestName = "Robin party",
                customerType = 2, size = 3, arrivalMinute = 1080, tableId = "table-6", status = FineDiningBookingStatus.Confirmed });
            string saved = JsonUtility.ToJson(data);
            return new Store.Snapshot { save = saved, checkpoint = saved, hash = Store.Hash(saved, saved) };
        }
        Store.AccountSnapshot Snapshot(int casual, int fast)
        {
            var value = new Store.AccountSnapshot { account = "test-account-A", lineage = "test-lineage", revision = 70,
                casual = Pair(casual, 123720), fastFood = Pair(fast, 140300), fineDining = FineDiningPair(), profile = new Store.ProfileProgress(),
                savedUtc = "2026-10-01T00:00:00Z", device = "test-install" };
            value.profile.tutorials[0] = value.profile.tutorials[1] = 1;
            value.profile.roles.Add(new LocalSaveManager.RoleEntry { roleId = "waiter", highestUnlockedLevel = 3 });
            value.profile.appearance = "{\"Version\":1,\"HeadColorIndex\":2}";
            Store.Seal(value); return value;
        }
        var cloud = Snapshot(20, 12);
        Check(Store.ValidAccount(cloud, "test-account-A"), "canonical checksum, all three careers and profile validate");
        Check(!Store.ValidAccount(cloud, "test-account-B"), "different account payload rejected");
        string json = JsonUtility.ToJson(cloud);
        var diningTamper = JsonUtility.FromJson<Store.AccountSnapshot>(json);
        diningTamper.fineDining.save = Pair(99, 999999).save;
        Check(!Store.ValidAccount(diningTamper, "test-account-A"), "Fine Dining tampering rejected");
        Check(Store.ValidAccount(JsonUtility.FromJson<Store.AccountSnapshot>(json), "test-account-A"), "serialized canonical payload round-trips");
        var tampered = JsonUtility.FromJson<Store.AccountSnapshot>(json); tampered.revision++;
        Check(!Store.ValidAccount(tampered, "test-account-A"), "metadata tampering detected");
        tampered = JsonUtility.FromJson<Store.AccountSnapshot>(json); tampered.casual.save = "{broken";
        Check(!Store.ValidAccount(tampered, "test-account-A"), "corrupt career rejected before restore");
        tampered = JsonUtility.FromJson<Store.AccountSnapshot>(json); tampered.version = 99; Store.Seal(tampered);
        Check(!Store.ValidAccount(tampered, "test-account-A"), "future cloud schema rejected");
        Check(CampaignSyncPolicy.Decide(true, true, "local", null, null) == Decision.Upload, "first local-to-cloud migration");
        Check(CampaignSyncPolicy.Decide(true, false, "default", "cloud", null) == Decision.Download, "new device default restores cloud");
        Check(CampaignSyncPolicy.Decide(true, true, "base", "cloud-new", "base") == Decision.Download, "unchanged old device downloads newer cloud");
        Check(CampaignSyncPolicy.Decide(true, true, "local-new", "base", "base") == Decision.Upload, "offline local continuation uploads");
        Check(CampaignSyncPolicy.Decide(true, true, "branch-A", "branch-B", "base") == Decision.Conflict, "independent offline branches require choice");
        Check(CampaignSyncPolicy.Decide(true, true, "same", "same", "base") == Decision.None, "same content never upload/download loops");
        Check(CampaignSyncPolicy.Decide(true, false, "default", null, null) == Decision.None, "empty device never manufactures cloud progress");
        Check(CampaignSyncPolicy.Decide(false, false, "corrupt", "cloud", "base") == Decision.Download, "valid cloud can recover invalid local");
        Check(CampaignSyncPolicy.Decide(false, false, "corrupt", null, null) == Decision.None, "invalid local never uploads");
        Check(CampaignSyncPolicy.Decide(true, true, "local", null, "old-cloud") == Decision.Conflict, "missing previously-synced cloud requires choice");
        var empty = new Store.AccountSnapshot { casual = Pair(1, 5000), fastFood = Pair(1, 5000), fineDining = Pair(1, 5000), profile = new Store.ProfileProgress() };
        Store.Seal(empty);
        Check(Store.ValidContent(empty) && !Store.Meaningful(empty), "default Day 1/starter save does not create conflict");
        Check(Store.Meaningful(cloud), "real independent restaurant progress detected");
        empty.casual.save = Pair(2, 5000).save; empty.casual.hash = Store.Hash(empty.casual.save, empty.casual.checkpoint);
        Check(Store.Meaningful(empty), "live progression detected even with a default day-start checkpoint");
        report.Append(RunReadiness());
        var localBranch = Snapshot(30, 18); var cloudBranch = Snapshot(28, 22);
        Check(CampaignSyncPolicy.Decide(true, true, localBranch.contentHash, cloudBranch.contentHash, "common-ancestor") == Decision.Conflict,
            "mixed Casual/Fast Food progress is not merged by maximum days or money");
        var state = new Store.AccountState { account = cloud.account, revision = cloud.revision, lineage = cloud.lineage,
            contentHash = cloud.contentHash, baseHash = cloud.contentHash, profile = cloud.profile };
        var recover = typeof(Store).GetMethod("RecoverAccountInstall", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(Action<Store.ProfileProgress, string>) }, null);
        string journal = Path.Combine(directory, "campaign_restore.pending.json");
        Store.AtomicWrite(Path.Combine(directory, "dinein_save.json"), Pair(2, 500).save);
        Store.AtomicWrite(journal, JsonUtility.ToJson(new Journal { snapshot = cloud, state = state }));
        bool injectedFailure = false;
        Action<Store.ProfileProgress, string> failProfile = (_, __) => throw new IOException("simulated profile write failure");
        try { recover.Invoke(null, new object[] { directory, failProfile }); }
        catch (TargetInvocationException) { injectedFailure = true; }
        Check(injectedFailure && File.Exists(journal), "interrupted multi-file restore retains recovery journal");
        Store.ProfileProgress restoredProfile = null;
        Action<Store.ProfileProgress, string> applyProfile = (profile, owner) => { restoredProfile = profile; Check(owner == cloud.account, "restored profile ownership"); };
        recover.Invoke(null, new object[] { directory, applyProfile });
        Check(!File.Exists(journal), "successful restore completes journal");
        Check(File.ReadAllText(Path.Combine(directory, "dinein_save.json")) == cloud.casual.save &&
            File.ReadAllText(Path.Combine(directory, "dinein_fastfood_save.json")) == cloud.fastFood.save,
            "Device B normal local career files contain exact downloaded state");
        var coldCasual = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(Path.Combine(directory, "dinein_save.json")));
        var coldFast = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(Path.Combine(directory, "dinein_fastfood_save.json")));
        Check(coldCasual.currentDay == 20 && coldFast.currentDay == 12 && coldCasual.inventoryStocks[0].stock == 12 &&
            coldCasual.unlockedRecipeIDs.Contains("recipe-test"), "offline cold read restores days, inventory and unlocks");
        string diningPath = Path.Combine(directory, "dinein_finedining_save.json");
        Check(File.ReadAllText(diningPath) == cloud.fineDining.save, "Fine Dining exact cloud payload restores to local career");
        var coldDining = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(diningPath));
        var booking = coldDining.fineDiningReservations.bookings[0];
        Check(coldDining.currentDay == 7 && coldDining.money == 18450 && coldDining.unlockedEquipmentIDs.Contains("fine-dining-table-6") &&
            booking.id == "saved-booking" && booking.arrivalMinute == 1080 && booking.tableId == "table-6" &&
            booking.status == FineDiningBookingStatus.Confirmed && booking.customerType == 2 && booking.size == 3,
            "Fine Dining offline cold read preserves progression, booking, table and alien identity");
        Check(restoredProfile.tutorials[0] == 1 && restoredProfile.roles[0].highestUnlockedLevel == 3 &&
            restoredProfile.appearance == cloud.profile.appearance, "profile callback receives tutorials, role progress and appearance");
        Check(Store.ReadAccountState(directory).revision == 70, "cloud restore preserves revision without increment");
        string atomic = Path.Combine(directory, "atomic.json"); Store.AtomicWrite(atomic, "old"); Store.AtomicWrite(atomic, "new");
        Check(File.ReadAllText(atomic) == "new" && File.ReadAllText(atomic + ".bak") == "old", "atomic replacement keeps previous copy");
        var resolve = typeof(Store).GetMethod("ResolveDirectory", BindingFlags.Static | BindingFlags.NonPublic);
        string a = (string)resolve.Invoke(null, new object[] { "test-account-A", directory });
        string b = (string)resolve.Invoke(null, new object[] { "test-account-B", directory });
        Check(a == directory && b != a && b.StartsWith(directory), "second account gets separate career directory");
        var observe = typeof(Store).GetMethod("ObserveSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
        long previousRevision = state.revision;
        Check(!(bool)observe.Invoke(null, new object[] { cloud, state, "test-device", "2026-10-01T01:00:00Z" }) && state.revision == previousRevision,
            "menu refresh / identical restore does not increment revision");
        var changed = Snapshot(21, 12);
        Check((bool)observe.Invoke(null, new object[] { changed, state, "test-device", "2026-10-01T01:00:00Z" }) && state.revision == previousRevision + 1,
            "successful changed snapshot increments revision once");
        Check(!(bool)observe.Invoke(null, new object[] { changed, state, "test-device", "2026-10-01T01:00:01Z" }), "repeated same save does not create another revision");
        var uiRoot = new GameObject("Cloud save UI regression") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            bool? choice = null;
            var ui = uiRoot.AddComponent<CampaignSaveConflictUI>(); ui.Show(localBranch, cloudBranch, value => choice = value);
            Canvas.ForceUpdateCanvases();
            foreach (var text in uiRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                text.ForceMeshUpdate();
                Check(text.rectTransform.rect.width > 0 && text.rectTransform.rect.height > 0 && !text.isTextOverflowing,
                    "conflict UI text fits: " + text.name);
            }
            var buttons = uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Check(buttons.Length == 2, "conflict UI has both explicit choices");
            buttons[0].onClick.Invoke();
            Check(choice == true && !buttons[0].interactable && !buttons[1].interactable, "device choice disables duplicate submissions");
            ui.Show(localBranch, cloudBranch, value => choice = value); buttons[1].onClick.Invoke();
            Check(choice == false, "cloud choice routes separately");
        }
        finally { UnityEngine.Object.DestroyImmediate(uiRoot); }
        report.AppendLine("Canonical fixture bytes=" + Encoding.UTF8.GetByteCount(json));
        report.AppendLine("Temporary test files=" + directory);
        return report.ToString();
    }
    public static string RunReadiness()
    {
        var result = new StringBuilder();
        void Check(CampaignSyncPolicy.EntryState expected, bool valid, bool meaningful, bool conflict, bool account, bool pending, string name)
        {
            if (CampaignSyncPolicy.Entry(valid, meaningful, conflict, account, pending) != expected) throw new Exception(name);
            result.AppendLine("PASS " + name);
        }
        Check(CampaignSyncPolicy.EntryState.Ready, true, true, false, true, false, "synced local ready");
        Check(CampaignSyncPolicy.EntryState.Ready, true, true, false, true, true, "meaningful local ready during initial checking");
        Check(CampaignSyncPolicy.EntryState.Ready, true, true, false, true, false, "offline/failed/upload-pending local ready");
        Check(CampaignSyncPolicy.EntryState.AwaitingRestore, true, false, false, true, true, "default authenticated install waits for bounded first check");
        Check(CampaignSyncPolicy.EntryState.Ready, true, false, false, true, false, "empty install ready after absent cloud or failed check");
        Check(CampaignSyncPolicy.EntryState.Conflict, true, true, true, true, false, "actual conflict blocks entry");
        Check(CampaignSyncPolicy.EntryState.InvalidLocal, false, true, false, true, false, "invalid local still protected");
        Check(CampaignSyncPolicy.EntryState.AccountSwitch, true, true, false, false, false, "other account never silently inherits local career");
        return result.ToString();
    }

}
#endif
