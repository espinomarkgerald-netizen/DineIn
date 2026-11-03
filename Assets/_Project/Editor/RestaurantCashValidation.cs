#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit, isolated checks; never run automatically and never build a player.
public static class RestaurantCashValidation
{
    [MenuItem("Dine In/Validation/Restaurant Cash and Purchase UI")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        CheckJournal(); CheckLayout();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/RestaurantCashValidation.result", "PASS: native account-journal interruption/recovery and restaurant purchase clone text geometry. No live account changes or player builds.");
        Debug.Log("[RestaurantCashValidation] PASS: isolated journal and purchase layout.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void CheckJournal()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DineIn-RestaurantCash-" + Guid.NewGuid().ToString("N"));
        bool reload = CampaignSaveStore.NeedsReload;
        try {
            Directory.CreateDirectory(directory);
            var empty = new CampaignSaveStore.Snapshot { hash = CampaignSaveStore.Hash(null, null) };
            var career = new GameSaveData { money = 15000, polishDayStartMoney = 15000, successfulRestaurantDays = Enumerable.Range(1,10).ToList() };
            var pair = new CampaignSaveStore.Snapshot { save = JsonUtility.ToJson(career), checkpoint = JsonUtility.ToJson(career) };
            pair.hash = CampaignSaveStore.Hash(pair.save,pair.checkpoint);
            var account = new CampaignSaveStore.AccountSnapshot { account = "isolated-cash-fixture", lineage = Guid.NewGuid().ToString("N"), revision = 1,
                casual = pair, fastFood = empty, fineDining = empty, profile = new CampaignSaveStore.ProfileProgress {
                    restaurantProgressionVersion = LocalSaveManager.RestaurantProgressionVersion,
                    unlockedRestaurantIds = new() { LocalSaveManager.CasualDiningId } } };
            var prepare = typeof(CampaignSaveStore).GetMethod("PrepareCashPurchase", BindingFlags.Static|BindingFlags.NonPublic);
            var purchased = (CampaignSaveStore.AccountSnapshot)prepare.Invoke(null, new object[] { account, LocalSaveManager.FastFoodId, 10000, 10, 0, Guid.NewGuid().ToString("N") });
            var state = new CampaignSaveStore.AccountState { account = purchased.account, lineage = purchased.lineage,
                revision = purchased.revision, contentHash = purchased.contentHash, profile = purchased.profile };
            string path = Path.Combine(directory,"campaign_restore.pending.json");
            File.WriteAllText(path, JsonUtility.ToJson(new Journal { snapshot = purchased, state = state }));
            var recover = typeof(CampaignSaveStore).GetMethod("RecoverAccountInstall", BindingFlags.Static|BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(Action<CampaignSaveStore.ProfileProgress,string>) }, null);
            try {
                recover.Invoke(null, new object[] { directory, new Action<CampaignSaveStore.ProfileProgress,string>((_,_) => throw new IOException("Simulated profile-write interruption")) });
                throw new Exception("Interruption fixture did not interrupt.");
            } catch (TargetInvocationException e) when (e.InnerException is IOException) { }
            Require(File.Exists(path), "Interrupted account install lost its recovery journal.");
            bool owned = false;
            Action<CampaignSaveStore.ProfileProgress,string> apply = (profile, owner) => owned = owner == purchased.account && profile.unlockedRestaurantIds.Contains(LocalSaveManager.FastFoodId);
            recover.Invoke(null, new object[] { directory, apply });
            recover.Invoke(null, new object[] { directory, apply });
            Require(owned && !File.Exists(path), "Purchase ownership did not recover.");
            foreach (string name in new[] { "dinein_save.json", "dinein_save_day_start.json" })
                Require(JsonUtility.FromJson<GameSaveData>(File.ReadAllText(Path.Combine(directory,name))).money == 5000, "Journal recovery charged twice or restored unspent cash.");
        } finally {
            CampaignSaveStore.NeedsReload = reload;
            // This exact directory is created above; no user save path is used.
            if (Directory.Exists(directory) && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(directory, true);
        }
    }
    [Serializable] sealed class Journal { public CampaignSaveStore.AccountSnapshot snapshot; public CampaignSaveStore.AccountState state; }
    static void CheckLayout()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
        try {
            var shop = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ShopPanelPopulator>(true)).Single();
            var canvas = new GameObject("Isolated purchase canvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas,scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(800,450);
            var view = shop.CreateRestaurantPurchaseView(canvas.transform);
            Require(view != null,"Shop purchase template missing.");
            foreach (bool fine in new[] { false,true }) {
                view.Render(fine?"FINE DINING":"FAST FOOD", fine?25000:10000, 123456789,
                    "SUCCESSFUL DAYS • CAMPAIGN / MULTIPLAYER\nCasual Dining    30 / 30"+(fine?"\nFast Food    15 / 15":""),
                    "Confirm purchase? Cash is drawn proportionally from your owned restaurants.", "BUY RESTAURANT", true,false);
                Canvas.ForceUpdateCanvases();
                var texts = typeof(RestaurantPurchaseView).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)
                    .Where(f=>f.FieldType==typeof(TMP_Text)).Select(f=>(TMP_Text)f.GetValue(view)).ToArray();
                foreach (var text in texts) {
                    text.ForceMeshUpdate(true,true);
                    Require(!text.isTextTruncated && text.preferredHeight <= text.rectTransform.rect.height+1,
                        "Purchase text does not fit: "+text.name);
                }
                var fields = texts;
                for(int i=0;i<fields.Length;i++) for(int j=i+1;j<fields.Length;j++)
                    Require(!WorldRect(fields[i].rectTransform).Overlaps(WorldRect(fields[j].rectTransform)), "Purchase text bands overlap: "+fields[i].name+" / "+fields[j].name);
                var badge = view.transform.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Purchase Preview Backing");
                Require(badge==null || !badge.gameObject.activeSelf,"Old badge overlaps the restaurant name.");
            }
        } finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static Rect WorldRect(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
}
#endif
