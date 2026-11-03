#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;

public static class GameplayConsistencyRegression
{
    public static void VerifyAndBuildWindows() { Run(); File.Copy("Logs/GameplayConsistency.result", "Logs/GameplayConsistency-Windows.result", true); MockDefenseBuild.Windows(); }
    public static void VerifyAndBuildAndroid() { Run(); File.Copy("Logs/GameplayConsistency.result", "Logs/GameplayConsistency-Android.result", true); MockDefenseBuild.Android(); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    [MenuItem("Dine In/Validation/Check Gameplay Consistency")]
    public static void Run()
    {
        var progress = typeof(TutorialGameModeEntry).GetMethod("HasCareerProgress", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (int day in new[] { 1, 5 }) {
            var career = new CampaignSaveStore.Snapshot { save = JsonUtility.ToJson(new GameSaveData { currentDay = day, money = 30000 }) };
            career.hash = CampaignSaveStore.Hash(career.save, null);
            Require((bool)progress.Invoke(null, new object[] { career }) == (day > 1), "Tutorial migration failed for Day " + day);
        }
        var completedDayOne = new CampaignSaveStore.Snapshot { save = JsonUtility.ToJson(new GameSaveData { fineDiningDayComplete = true }) };
        completedDayOne.hash = CampaignSaveStore.Hash(completedDayOne.save, null);
        Require((bool)progress.Invoke(null, new object[] { completedDayOne }), "Completed Day 1 was redirected into onboarding.");
        var initialize = typeof(LocalSaveManager).GetMethod("InitializeRestaurantProgression", BindingFlags.Static | BindingFlags.NonPublic);
        var fresh = new LocalSaveManager.SaveData(); initialize.Invoke(null, new object[] { fresh, false });
        Require(fresh.unlockedRestaurantIds.SequenceEqual(new[] { LocalSaveManager.CasualDiningId }), "Fresh account unlocks changed.");
        var legacy = new LocalSaveManager.SaveData(); initialize.Invoke(null, new object[] { legacy, true });
        Require(legacy.unlockedRestaurantIds.Count == 3, "Legacy unlocks lost.");
        var partial = new LocalSaveManager.SaveData { restaurantProgressionVersion = 1, unlockedRestaurantIds = new List<string> { "casual", "fastfood" } };
        initialize.Invoke(null, new object[] { partial, false });
        Require(partial.unlockedRestaurantIds.Count == 2, "Partial progress changed.");
        var root = new GameObject("Isolated stock regression");
        try {
            var inventory = root.AddComponent<InventoryManager>();
            var batches = new List<InventoryStockBatchSaveEntry> {
                new() { itemType = ItemType.Butter, unitsRemaining = 4, expiresDay = 0 },
                new() { itemType = ItemType.Butter, unitsRemaining = 2, expiresDay = 5 },
                new() { itemType = ItemType.Butter, unitsRemaining = 3, expiresDay = 6, wrongStorage = true }
            };
            typeof(InventoryManager).GetField("stockBatches", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, batches);
            Require(inventory.GetExpiredStock(ItemType.Butter, 5) == 2, "Zero-expiry or fresh stock was reported expired.");
            Require(inventory.GetExpiredStock(ItemType.Butter, 4) == 0, "Early expiry.");
            Require(inventory.GetWrongStorageStock(ItemType.Butter) == 3, "Wrong-storage units disagree.");
            Require(inventory.GetNextFreshExpiryDay(ItemType.Butter, 5) == 6, "Next expiry is inaccurate.");
        } finally { UnityEngine.Object.DestroyImmediate(root); }
        var receipt = typeof(CampaignSaveStore).GetMethod("ApplyRewardToDocument", BindingFlags.NonPublic | BindingFlags.Static);
        var cash = new GameSaveData { money = 5000, polishDayStartMoney = 5000 };
        receipt.Invoke(null, new object[] { cash, 250, "test-receipt" });
        var restored = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(cash));
        receipt.Invoke(null, new object[] { restored, 250, "test-receipt" });
        Require(restored.money == 5250 && restored.campaignCreditReceipts.Count == 1, "Cash receipt applied twice after serialization.");
        foreach (int count in new[] { 6, 9, 12 }) {
            var empty = new CampaignSaveStore.Snapshot { hash = CampaignSaveStore.Hash(null, null) };
            var snapshot = new CampaignSaveStore.AccountSnapshot { casual = empty, fastFood = empty, fineDining = empty,
                profile = new CampaignSaveStore.ProfileProgress { tutorials = new int[count], restaurantProgressionVersion = 1,
                    unlockedRestaurantIds = new List<string> { "casual" } } };
            Require(CampaignSaveStore.ValidContent(snapshot), "Older tutorial progress length rejected: " + count);
        }
        var high = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/HighGraphics.asset");
        Require(high != null && Mathf.Approximately(high.renderScale, 1f) && high.msaaSampleCount == 4, "High graphics is not full resolution with 4x MSAA.");
        CheckCard();
        var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
        var scenes = profile != null && new SerializedObject(profile).FindProperty("m_OverrideGlobalSceneList").boolValue
            ? profile.scenes : EditorBuildSettings.scenes;
        foreach (string name in new[] { "Lobby1", "Lobby2", "Lobby3", "Lobby1Tutorial", "Lobby2Tutorial", "Lobby3Tutorial", "Lobby1 Multiplayer", "Lobby2 Multiplayer", "Lobby3 Multiplayer" })
            Require(scenes.Any(s => s.enabled && Path.GetFileNameWithoutExtension(s.path) == name), "Build profile omits " + name);
        foreach (var scenePath in scenes.Where(s => s.enabled && Path.GetFileNameWithoutExtension(s.path).StartsWith("Lobby"))) {
            var preview = EditorSceneManager.OpenPreviewScene(scenePath.path);
            try {
                var roots = preview.GetRootGameObjects();
                Require(roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Any(), "Restaurant camera missing: " + scenePath.path);
                bool navigation = roots.SelectMany(r => r.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).Any(s => s.navMeshData != null);
                // TutorialSystem binds its separately serialized bake to the scene Surface at runtime.
                navigation |= roots.SelectMany(r => r.GetComponentsInChildren<TutorialSystem>(true)).Any(t => {
                    var serialized = new SerializedObject(t);
                    return serialized.FindProperty("lobbyNavigationSurface").objectReferenceValue != null &&
                        serialized.FindProperty("lobbyNavigationData").objectReferenceValue != null;
                });
                Require(navigation, "Baked restaurant navigation missing: " + scenePath.path);
            } finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GameplayConsistency.result", "PASS: career tutorial routing, fresh/legacy/partial ownership, old cloud tutorial arrays, expiry boundaries, wrong storage, serialized cash receipt idempotency, full-resolution High graphics, restock statistic geometry/text, all restaurant/mode scenes.");
        Debug.Log("[GameplayConsistency] PASS");
    }

    private static void CheckCard()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try {
            var canvas = new GameObject("Card check canvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 900);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogCard.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            var card = instance.GetComponent<ManagementComputerCatalogCardUI>();
            foreach (var size in new[] { new Vector2(160, 220), new Vector2(200, 240), new Vector2(260, 300) }) {
            rect.sizeDelta = size;
            typeof(ManagementComputerCatalogCardUI).GetMethod("ApplyRestockReadability", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, null);
            typeof(ManagementComputerCatalogCardUI).GetMethod("SetStatValues", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(card, new object[] { 15, 0 });
            Canvas.ForceUpdateCanvases();
            foreach (var pair in new[] { ("inStockLabelText", "IN STOCK"), ("inStockValueText", "15"), ("neededTodayLabelText", "NEEDED TODAY"), ("neededTodayValueText", "0") }) {
                var text = (TMP_Text)typeof(ManagementComputerCatalogCardUI).GetField(pair.Item1, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(card);
                text.ForceMeshUpdate(true, true);
                Require(text.text == pair.Item2 && text.enabled && text.rectTransform.rect.height >= text.fontSizeMin, "Unreadable restock statistic: " + pair.Item1);
                Require(text.textInfo.characterCount > 0 && !text.isTextTruncated, "Empty/truncated restock statistic: " + pair.Item1);
            }
            }
        } finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
