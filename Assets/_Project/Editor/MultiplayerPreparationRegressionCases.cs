#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Called by an explicit external Play Mode runner only. No import-time work.
public static class MultiplayerPreparationRegressionCases
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static void Run()
    {
        if (!Application.isPlaying || MultiplayerProgressionContext.IsActive)
            throw new InvalidOperationException("Use the external runner's empty Play Mode scene.");
        var fixtures = new List<Object>();
        var flowField = typeof(GameFlowManager).GetField("<Instance>k__BackingField", Members);
        var saveField = typeof(GameSaveManager).GetField("<Instance>k__BackingField", Members);
        object previousFlow = flowField.GetValue(null), previousSaves = saveField.GetValue(null);
        try
        {
            flowField.SetValue(null, null); saveField.SetValue(null, null);
            GameObject Root(string name)
            { var root = new GameObject(name); root.SetActive(false); fixtures.Add(root); return root; }
            var item = ScriptableObject.CreateInstance<ItemData>(); fixtures.Add(item);
            item.itemType = ItemType.Drumsticks; item.unitsPerBox = 12;
            var inventory = Root("preparation inventory").AddComponent<InventoryManager>();
            inventory.ConfigureItems(new List<ItemData> { item }, grantStarterStock: false);
            var empty = new GameSaveData(); inventory.FillSaveData(empty);
            Assert(inventory.IsTracked(item.itemType) && inventory.GetStock(item.itemType) == 0
                && empty.inventoryStockBatches.Count == 0, "Fresh run granted stock or an inventory batch.");
            inventory.AddStock(item.itemType, 7);
            var purchased = new GameSaveData(); inventory.FillSaveData(purchased);
            string batchId = purchased.inventoryStockBatches[0].batchID;
            inventory.ConfigureItems(new List<ItemData> { item }, grantStarterStock: false);
            inventory.ApplySaveData(purchased); inventory.ApplySaveData(purchased);
            var restored = new GameSaveData(); inventory.FillSaveData(restored);
            Assert(inventory.GetStock(item.itemType) == 7 && restored.inventoryStockBatches.Count == 1
                && restored.inventoryStockBatches[0].batchID == batchId,
                "Rejoin/snapshot application duplicated, refilled or discarded purchased inventory.");
            inventory.ConfigureItems(new List<ItemData> { item });
            Assert(inventory.GetStock(item.itemType) == 12 && item.unitsPerBox == 12,
                "Campaign default or authored ingredient definition changed.");
            inventory.ApplySaveData(purchased);
            Assert(inventory.GetStock(item.itemType) == 7, "Restoring a campaign save replaced its stock with starter boxes.");

            var polish = Root("newspaper identity").AddComponent<CasualDiningPolishManager>();
            Set(polish, "preparedDay", 2);
            var issues = Get<List<NewspaperIssueSaveEntry>>(polish, "newspaperIssues");
            var old = new NewspaperIssueSaveEntry { day = 1, issueID = "archive", presentationVersion = 2 };
            var current = new NewspaperIssueSaveEntry { day = 2, issueID = "today", presentationVersion = 2 };
            issues.Add(old); issues.Add(current);
            int notices = 0; polish.NewspaperStateChanged += () => notices++;
            Assert(!polish.MarkIssueViewed(1, "archive") && !polish.MarkIssueViewed(2, "another issue")
                && !old.viewed && !current.viewed, "An archive or mismatched issue marked today's newspaper read.");
            var presenter = Root("displayed newspaper").AddComponent<DailyNewspaperPresenter>();
            var canvas = Root("newspaper canvas"); var overlay = Root("newspaper content");
            Set(presenter, "manager", polish); Set(presenter, "canvasRoot", canvas); Set(presenter, "overlayRoot", overlay);
            Set(presenter, "displayedIssueDay", 2); Set(presenter, "displayedIssueId", "today");
            Call(presenter, "AcknowledgeDisplayedIssue");
            Assert(!current.viewed, "Hidden content counted as read.");
            canvas.SetActive(true); overlay.SetActive(true);
            Call(presenter, "AcknowledgeDisplayedIssue"); Call(presenter, "AcknowledgeDisplayedIssue");
            Assert(current.viewed && notices == 1, "Visible exact issue did not count once before animation completion.");
            current.viewed = false;
            Set(presenter, "displayedIssueDay", 1); Set(presenter, "displayedIssueId", "archive");
            Call(presenter, "ForceOpenFinal");
            Assert(!current.viewed && !old.viewed, "Finishing an archive animation marked the current issue.");

            var bridge = Root("parallel management receipt").AddComponent<MultiplayerRestaurantBridge>();
            var commandType = typeof(MultiplayerRestaurantBridge).GetNestedType("Command", BindingFlags.NonPublic);
            var replyType = typeof(MultiplayerRestaurantBridge).GetNestedType("Reply", BindingFlags.NonPublic);
            var management = Activator.CreateInstance(commandType, true); Set(management, "id", "management-action");
            var newspaper = Activator.CreateInstance(commandType, true); Set(newspaper, "id", "newspaper-action");
            Set(bridge, "pending", management); Set(bridge, "newspaperPending", newspaper);
            var reply = Activator.CreateInstance(replyType, true); Set(reply, "id", "newspaper-action"); Set(reply, "accepted", true);
            Call(bridge, "Receive", reply); Call(bridge, "Receive", reply);
            Assert(ReferenceEquals(Get<object>(bridge, "pending"), management) && Get<object>(bridge, "newspaperPending") == null,
                "Newspaper acknowledgment consumed the unrelated management request or failed to deduplicate.");
        }
        finally
        {
            flowField.SetValue(null, previousFlow); saveField.SetValue(null, previousSaves);
            foreach (var fixture in fixtures) if (fixture != null) Object.Destroy(fixture);
        }
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Members).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
