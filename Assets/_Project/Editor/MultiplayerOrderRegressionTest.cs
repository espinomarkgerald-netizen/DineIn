#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit external-only entry point. Import never launches Play Mode or runs
// tests. This exercises real stock commits and the real cooking coroutine in an
// isolated empty scene; networked host/guest controls are separate release gates.
[InitializeOnLoad]
public static class MultiplayerOrderRegressionTest
{
    private const string Running = "DineIn.MultiplayerOrder.Running", Batch = "DineIn.MultiplayerOrder.Batch";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<Object> fixtures = new();
    private static readonly List<string> results = new();
    private static readonly List<Action> restore = new();
    private static IEnumerator checks;
    private static InventoryManager inventory;
    private static KitchenManager kitchen;
    private static MenuCatalog catalog;
    private static Recipe recipe;
    private static int started, finished;

    static MultiplayerOrderRegressionTest()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(Running, false) && EditorApplication.isPlaying) EditorApplication.delayCall += Begin;
    }
    [MenuItem("Tools/Dine In/Run Multiplayer Order Regressions (External)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start outside Play Mode.");
        if (!SessionState.GetBool(Batch, false) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    public static void RunBatch() { SessionState.SetBool(Batch, true); Run(); }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (SessionState.GetBool(Running, false) && state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.delayCall += Begin;
    }
    private static void Begin()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || checks != null) return;
        results.Clear(); fixtures.Clear(); restore.Clear();
        checks = Verify();
        EditorApplication.update += Pump;
    }
    private static void Pump()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode stopped before checks completed.");
            if (!checks.MoveNext()) Finish(0);
        }
        catch (Exception error)
        {
            results.Add("FAIL: " + error);
            Debug.LogException(error);
            Finish(1);
        }
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Pump;
        checks = null;
        if (kitchen != null) kitchen.StopAllCoroutines();
        for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
        foreach (var fixture in fixtures) if (fixture != null) Object.Destroy(fixture);
        restore.Clear(); fixtures.Clear();
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "MultiplayerOrderRegression.txt"), results);
        SessionState.SetBool(Running, false);
        bool batch = SessionState.GetBool(Batch, false); SessionState.SetBool(Batch, false);
        if (batch) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }

    private static IEnumerator Verify()
    {
        Assert(!MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer, "Fixture must run outside a Photon session.");
        Setup();
        yield return null; // Let the kitchen's authored Start hook finish before setting fixture timing.
        Set(kitchen, "preparationDelaySeconds", 0.02f); kitchen.cookSeconds = 0.02f;
        ValidateSelection();
        ValidateRollback();
        var botSeated = VerifyCooking(false, 501);
        while (botSeated.MoveNext()) yield return botSeated.Current;
        var humanSeated = VerifyCooking(true, 502);
        while (humanSeated.MoveNext()) yield return humanSeated.Current;
    }

    private static void Setup()
    {
        float previousTimeScale = Time.timeScale;
        restore.Add(() => Time.timeScale = previousTimeScale);
        Time.timeScale = 1f;
        var ingredient = ScriptableObject.CreateInstance<ItemData>(); fixtures.Add(ingredient);
        ingredient.itemType = ItemType.Drumsticks; ingredient.itemID = "regression-ingredient";
        ingredient.unitsPerBox = 12;
        recipe = ScriptableObject.CreateInstance<Recipe>(); fixtures.Add(recipe);
        recipe.recipeID = "regression-food"; recipe.recipeName = "Fixture Meal";
        recipe.category = MenuProductCategory.Food; recipe.sellPrice = 17;
        recipe.ingredients = new List<RecipeIngredient> { new RecipeIngredient { item = ingredient, amount = 2 } };
        catalog = ScriptableObject.CreateInstance<MenuCatalog>(); fixtures.Add(catalog);
        Set(catalog, "products", new List<Recipe> { recipe });
        Override(typeof(MenuCatalog), "cachedDefault", catalog);
        Override(typeof(MenuCatalog), "cachedSceneName", SceneManager.GetActiveScene().name);
        Override(typeof(MenuCatalog), "hasRestaurantOverride", false);
        // A disposable fixture must not write an account career or trigger its day counters.
        Override(typeof(GameSaveManager), "<Instance>k__BackingField", null);
        Override(typeof(GameDayManager), "<Instance>k__BackingField", null);
        inventory = Root("fixture inventory").AddComponent<InventoryManager>();
        Override(typeof(InventoryManager), "Instance", inventory);
        var stock = Root("fixture stock").AddComponent<LobbyStockBridge>();
        Override(typeof(LobbyStockBridge), "<Instance>k__BackingField", stock);
        var numbers = Root("fixture order numbers").AddComponent<OrderNumberManager>();
        Override(typeof(OrderNumberManager), "Instance", numbers);
        Set(numbers, "nextOrderNumber", 700);
        kitchen = Root("fixture kitchen").AddComponent<KitchenManager>();
        var slot = Root("fixture pickup slot").transform; slot.SetParent(kitchen.transform, false);
        slot.gameObject.SetActive(true);
        kitchen.traySpawnPoints = new[] { slot };
        var tray = Root("fixture tray template").AddComponent<FoodTray>();
        tray.gameObject.AddComponent<BoxCollider>();
        tray.gameObject.AddComponent<FoodTrayInteractable>();
        tray.gameObject.SetActive(true);
        kitchen.foodTrayPrefab = tray;
        kitchen.OrderStarted += (_, _) => started++;
        kitchen.OrderFinished += (_, _, success) => { if (success) finished++; };
        kitchen.gameObject.SetActive(true);
        started = finished = 0;
        ResetStock(12);
    }

    private static void ValidateSelection()
    {
        var group = Group(401, false);
        var selected = ReviewedOrderSelection.FromLines(group.currentOrder.lines);
        Assert(ReviewedOrderSubmission.TryBuild(group, selected, catalog, out var submitted, out var products,
            out _, out _, out _), "Correct reviewed quantities were rejected.");
        Assert(products.Count == 2 && submitted.TotalPrice == 34, "Catalog quantity/price did not determine submitted order.");
        selected.lines[0].quantity = 1;
        Assert(!ReviewedOrderSubmission.TrySubmit(group, 401, selected, catalog, kitchen, out var failure)
            && failure == ReviewedOrderSubmission.Failure.IncorrectSelection, "Partial order entered the kitchen.");
        selected.lines[0].quantity = 2; selected.lines[0].itemId = "another-food";
        Assert(!ReviewedOrderSubmission.TrySubmit(group, 401, selected, catalog, kitchen, out _), "Different reviewed item was accepted.");
        Assert(inventory.GetStock(ItemType.Drumsticks) == 12 && started == 0 && group.IsPlayerReviewingOrder,
            "A rejected selection changed stock, cooking or review.");
        Pass("actual reviewed product IDs and quantities are validated before any commit");
    }

    private static void ValidateRollback()
    {
        var group = Group(402, false);
        var selection = ReviewedOrderSelection.FromLines(group.currentOrder.lines);
        ResetStock(1);
        Assert(!ReviewedOrderSubmission.TrySubmit(group, 402, selection, catalog, kitchen, out var failure)
            && failure == ReviewedOrderSubmission.Failure.UnavailableStock, "Insufficient stock entered the kitchen.");
        Assert(inventory.GetStock(ItemType.Drumsticks) == 1 && !group.HasConfirmedOrder && group.IsPlayerReviewingOrder,
            "Unavailable stock changed review/confirmation.");
        ResetStock(12);
        var before = BatchJson();
        int stockNotifications = 0;
        Action<ItemType, int> stockChanged = (_, _) => stockNotifications++;
        inventory.OnStockChanged += stockChanged;
        try
        {
            Set(group, "waitingForRemake", true);
            var completed = Get<HashSet<int>>(kitchen, "completedOrders");
            completed.Add(700); // Simulate an order-number collision discovered only at kitchen acceptance.
            var originalSubmission = group.submittedOrder;
            Assert(!ReviewedOrderSubmission.TrySubmit(group, 402, selection, catalog, kitchen, out failure)
                && failure == ReviewedOrderSubmission.Failure.UnavailableKitchen, "Rejected kitchen was acknowledged as accepted.");
            Assert(inventory.GetStock(ItemType.Drumsticks) == 12 && BatchJson() == before && stockNotifications == 0,
                "Failed commit consumed stock, recreated batch age/storage, or published provisional stock.");
            Assert(group.currentOrderNumber == 402 && group.state == CustomerGroup.GroupState.ReadyToOrder
                && group.IsPlayerReviewingOrder && !group.HasConfirmedOrder && group.IsWaitingForRemake()
                && ReferenceEquals(group.submittedOrder, originalSubmission), "Failed kitchen did not restore the original retryable review.");
            Assert(started == 0 && Get<HashSet<int>>(kitchen, "cookingOrders").Count == 0
                && kitchen.ActiveForecastCount == 0, "A rejected stock/order commit left cooking active.");
            completed.Remove(700);
        }
        finally { inventory.OnStockChanged -= stockChanged; }
        Pass("stock shortage and kitchen rejection preserve reviewed state and exact original inventory batches");
    }

    private static IEnumerator VerifyCooking(bool pauseAfterSeating, int order)
    {
        ResetStock(12);
        var group = Group(order, pauseAfterSeating);
        var selection = ReviewedOrderSelection.FromLines(group.currentOrder.lines);
        int previousStarts = started, previousFinishes = finished;
        Assert(ReviewedOrderSubmission.TrySubmit(group, order, selection, catalog, kitchen, out var failure),
            "Reviewed submission failed: " + failure);
        Assert(kitchen.TryGetForecast(order, out var forecast) && !forecast.IsPaused,
            "Human confirmation paused cooking depending on who seated the customer.");
        Assert(!ReviewedOrderSubmission.TrySubmit(group, order, selection, catalog, kitchen, out _), "Duplicate confirmation accepted.");
        Assert(inventory.GetStock(ItemType.Drumsticks) == 8 && started == previousStarts + 1,
            "Reviewed confirmation consumed ingredients or started cooking more than once.");
        float deadline = Time.realtimeSinceStartup + 5f;
        while (kitchen.GetPreparedResult(order) == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert(kitchen.TryGetPreparedTray(order, out var tray), "Normal cooking never produced a registered pickup tray.");
        Assert(kitchen.TryGetForecast(order, out var completed) && completed.Group == group
            && completed.OrderNumber == order && completed.State == KitchenManager.ForecastState.Completed,
            "Cooking completion discarded the forecast required by human pickup claims.");
        Assert(tray.TargetGroup == group && tray.orderNumber == order && tray.DeliveredProductIds.Count == 2
            && tray.GetComponent<FoodTrayInteractable>().IsDeliveryPickable && finished == previousFinishes + 1,
            "Prepared tray lost customer/products, pickup interaction, or completed more than once.");
        Assert(kitchen.traySpawnPoints[0].GetComponentsInChildren<FoodTray>(true).Length == 1,
            "Confirmation created duplicate trays in a pickup slot.");
        Object.Destroy(tray.gameObject);
        yield return null;
        Pass((pauseAfterSeating ? "human-seated" : "bot-seated") + " reviewed order cooks normally and spawns exactly one usable registered tray");
    }

    private static CustomerGroup Group(int order, bool paused)
    {
        var group = Root("fixture customer " + order).AddComponent<CustomerGroup>();
        group.state = CustomerGroup.GroupState.ReadyToOrder;
        group.currentOrderNumber = order; group.PauseAfterSeating = paused;
        var line = new CustomerGroup.OrderLine(); line.SetProduct(recipe, 2);
        group.currentOrder.SetLines(new[] { line }, catalog);
        Assert(group.BeginPlayerOrderReview(), "Fixture could not enter player review.");
        return group;
    }
    private static void ResetStock(int units)
    {
        var stocks = Get<Dictionary<ItemType, int>>(inventory, "inventory"); stocks.Clear();
        stocks.Add(ItemType.Drumsticks, units);
        var batches = Get<List<InventoryStockBatchSaveEntry>>(inventory, "stockBatches"); batches.Clear();
        batches.Add(new InventoryStockBatchSaveEntry { batchID = "original-batch", itemType = ItemType.Drumsticks,
            unitsRemaining = units, receivedDay = 2, expiresDay = 9, currentStorage = RestockStorageType.Frozen, wrongStorage = true });
    }
    private static string BatchJson() => JsonUtility.ToJson(Get<List<InventoryStockBatchSaveEntry>>(inventory, "stockBatches")[0]);
    private static GameObject Root(string name)
    { var root = new GameObject(name); root.SetActive(false); fixtures.Add(root); return root; }
    private static void Override(Type type, string name, object value)
    {
        var field = type.GetField(name, Members) ?? throw new MissingFieldException(type.Name, name);
        var original = field.GetValue(null); restore.Add(() => field.SetValue(null, original)); field.SetValue(null, value);
    }
    private static void Set(object target, string name, object value) =>
        (target.GetType().GetField(name, Members) ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    private static T Get<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, Members) ?? throw new MissingFieldException(target.GetType().Name, name)).GetValue(target);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pass(string message) { results.Add("PASS: " + message); Debug.Log("[Order regression] " + message); }
}
#endif
