#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicitly opted-in diagnostics. Never runs a shift or writes a campaign save.</summary>
public static class FineDiningDrinkRegression
{
    public const string SessionKey = "DineIn.FineDiningDrinkRegression.NoSave";
    private static readonly Func<bool> SaveGuard = () => true;
    private static CustomerGroup previewGroup;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string PreparationCheckResult { get; private set; } = "NOT RUN";
    public static bool PreparationCheckRunning => preparation != null;
    private static PreparationRun preparation;

    private sealed class PreparationRun
    {
        public GameObject root;
        public CustomerGroup group;
        public CustomerGroup.SimpleOrder meal;
        public Recipe drink;
        public FoodTray mealTray, output, originalHeldTray;
        public WaiterHands hands, originalHands;
        public KitchenManager kitchen;
        public InventoryManager inventory;
        public GameSaveData inventorySnapshot;
        public OrderNumberManager numbers;
        public int nextNumberBefore, nextNumberAfter, order = -1, mealPrice, checks, deliveredFrame = -1;
        public bool accepted;
        public double started;
        public Dictionary<ItemType, int> requirements, stockBefore;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void GuardDiagnosticSession()
    {
        if (SessionState.GetBool(SessionKey, false)) GameSaveManager.PersistenceSuspended = SaveGuard;
    }

    [InitializeOnLoadMethod]
    private static void WatchPlayMode()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= CancelPreparationCheck;
        AssemblyReloadEvents.beforeAssemblyReload += CancelPreparationCheck;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) CancelPreparationCheck();
        if (change != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(SessionKey, false);
        if (GameSaveManager.PersistenceSuspended == SaveGuard) GameSaveManager.PersistenceSuspended = null;
    }

    public static string ArmRuntimeChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Arm before entering Play Mode.");
        SessionState.SetBool(SessionKey, true);
        return "Armed a disposable no-save Play Mode session. Enter Play Mode separately; no test starts automatically.";
    }

    private static object Get(object owner, string field) => owner.GetType().GetField(field, Private).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    private static void Flag(CustomerGroup group, string property, bool value) => typeof(CustomerGroup).GetProperty(property).SetValue(group, value);
    private static CustomerGroup.SimpleOrder Meal(Recipe food, Recipe drink)
    {
        var meal = new CustomerGroup.SimpleOrder();
        var first = new CustomerGroup.OrderLine(); first.SetProduct(food);
        var second = new CustomerGroup.OrderLine(); second.SetProduct(drink);
        meal.SetLines(new[] { first, second });
        return meal;
    }
    private static void StageDrink(CustomerGroup group, Recipe drink)
    {
        Set(group, "fineDiningMeal", group.currentOrder);
        Set(group, "fineDiningSubmittedMeal", group.submittedOrder);
        Set(group, "fineDiningMealNumber", group.currentOrderNumber);
        Set(group, "fineDiningDrink", drink);
        var line = new CustomerGroup.OrderLine(); line.SetProduct(drink);
        group.currentOrder = new CustomerGroup.SimpleOrder(); group.currentOrder.SetLines(new[] { line });
        group.submittedOrder = new CustomerGroup.SimpleOrder();
        group.currentOrderNumber = 907;
        Flag(group, "IsFineDiningAdditionalDrinkOrder", true);
        group.state = CustomerGroup.GroupState.OrderTaken;
    }

    public static string RunDataChecks()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Data checks use an isolated Edit Mode preview scene.");
        var catalog = MenuCatalog.Default;
        if (catalog == null || catalog.RestaurantType != RestaurantType.FineDining)
            throw new InvalidOperationException("Keep Lobby3 active so the real Fine Dining catalog is used.");
        var food = catalog.Products.First(p => p != null && p.category == MenuProductCategory.Food);
        var drink = catalog.Products.First(p => p != null && p.category == MenuProductCategory.Drink);
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); checks++; }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = new GameObject("Isolated drink billing checks");
            SceneManager.MoveGameObjectToScene(go, scene);
            var group = go.AddComponent<CustomerGroup>();
            group.currentOrder = Meal(food, drink);
            group.submittedOrder = group.currentOrder;
            group.currentOrderNumber = 73;
            group.state = CustomerGroup.GroupState.Eating;
            int mealPrice = group.GetCurrentOrderTotal();
            var originalMeal = group.currentOrder;
            StageDrink(group, drink);
            Call(group, "FinishFineDiningDrinks", false);
            Check(ReferenceEquals(group.currentOrder, originalMeal) && group.currentOrderNumber == 73, "Canceled drink restores meal identity.");
            Check(group.GetCurrentOrderTotal() == mealPrice, "Canceled drink adds no charge.");
            Check(group.state == CustomerGroup.GroupState.Eating && !group.HasFineDiningDrinkEvent, "Canceled drink returns to dining.");
            StageDrink(group, drink);
            Call(group, "FinishFineDiningDrinks", true);
            Check(group.GetCurrentOrderTotal() == mealPrice + drink.EffectiveSellPrice, "Delivered drink is appended to the original meal bill.");
            Check(group.currentOrderNumber == 73 && group.currentOrder.lines.Count == 3, "Additional service keeps the original bill/order identity.");
            Call(group, "FinishFineDiningDrinks", true);
            Check(group.GetCurrentOrderTotal() == mealPrice + drink.EffectiveSellPrice, "Repeated completion cannot add a second charge.");
            Set(group, "fineDiningDrink", drink); Flag(group, "HasWineRefillRequest", true);
            Call(group, "FinishFineDiningDrinks", false);
            Check(group.GetCurrentOrderTotal() == mealPrice + drink.EffectiveSellPrice, "Canceled refill adds no charge.");
            return "PASS: " + checks + " isolated drink billing/cancel checks. Stock, UI and live pouring were not run.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static string RunRuntimeChecks(bool keepNotepadPreview = false)
    {
        if (!EditorApplication.isPlaying || !GameSaveManager.IsPersistenceSuspended || !SessionState.GetBool(SessionKey, false))
            throw new InvalidOperationException("Use ArmRuntimeChecks before entering a disposable no-save Play Mode session.");
        if (SceneManager.GetActiveScene().name != "Lobby3" || GameDayManager.Instance == null || GameDayManager.Instance.ServiceActive)
            throw new InvalidOperationException("Run only in Lobby3 preparation; do not start the shift.");
        if (previewGroup != null) CleanupRuntimePreview();
        var catalog = MenuCatalog.Default;
        var inventory = InventoryManager.Instance;
        var stock = LobbyStockBridge.Instance;
        var kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenManager>();
        var ui = UnityEngine.Object.FindFirstObjectByType<OrderChecklistUI>(FindObjectsInactive.Include);
        if (catalog == null || inventory == null || stock == null || kitchen == null || !kitchen.HasDrinkServiceOutput || ui == null)
            throw new InvalidOperationException("Scene stock, bar output and notepad bindings must be ready.");
        var food = catalog.GetProducts(MenuProductCategory.Food).First();
        var drink = catalog.GetProducts(MenuProductCategory.Drink).First(p => p.ingredients != null && p.ingredients.Any(i => i?.item != null && i.amount > 0));
        var requirements = new Dictionary<ItemType, int>();
        foreach (var ingredient in drink.ingredients)
            if (ingredient?.item != null && ingredient.amount > 0 && inventory.IsTracked(ingredient.item.itemType))
            {
                requirements.TryGetValue(ingredient.item.itemType, out int amount);
                requirements[ingredient.item.itemType] = amount + ingredient.amount;
            }
        if (requirements.Count == 0) throw new InvalidOperationException("Selected drink has no tracked stock recipe.");
        var snapshot = new GameSaveData(); inventory.FillSaveData(snapshot);
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); checks++; }
        var go = new GameObject("Disposable Fine Dining drink check");
        go.transform.position = new Vector3(0, -1000, 0);
        var group = go.AddComponent<CustomerGroup>(); group.enabled = false;
        FoodTray output = null;
        int outputOrder = -1;
        try
        {
            foreach (var need in requirements) inventory.AddStock(need.Key, need.Value * 3);
            group.currentOrder = Meal(food, drink); group.submittedOrder = group.currentOrder;
            group.currentOrderNumber = 906; group.state = CustomerGroup.GroupState.Eating;
            group.ConfirmOrder(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke);
            int mealPrice = group.GetCurrentOrderTotal();
            var beforeStock = requirements.ToDictionary(p => p.Key, p => inventory.GetStock(p.Key));
            Set(group, "fineDiningDrink", drink); Flag(group, "HasWineRefillRequest", true);
            Call(group, "CompleteWineRefill");
            foreach (var need in requirements)
                Check(inventory.GetStock(need.Key) == beforeStock[need.Key] - need.Value, "Refill consumes its existing inventory recipe once.");
            Check(group.GetCurrentOrderTotal() == mealPrice + drink.EffectiveSellPrice, "Refill completion bills one drink.");
            Call(group, "CompleteWineRefill");
            foreach (var need in requirements)
                Check(inventory.GetStock(need.Key) == beforeStock[need.Key] - need.Value, "Duplicate completion consumes no additional stock.");
            Check(group.GetCurrentOrderTotal() == mealPrice + drink.EffectiveSellPrice, "Duplicate refill completion adds no charge.");

            Set(group, "fineDiningDrink", drink); Call(group, "BeginAdditionalDrinkOrder");
            ui.Open(group);
            var entries = (List<NotepadMenuEntryUI>)Get(ui, "menuEntries");
            Check(entries.Count > 0 && entries.All(e => e.Kind == NotepadMenuEntryUI.EntryKind.Product && e.Product.category == MenuProductCategory.Drink), "Real notepad contains drinks only.");
            var foodTab = (UnityEngine.UI.Button)Get(ui, "foodTabButton");
            Check(foodTab != null && !foodTab.gameObject.activeSelf, "Food tab is hidden for the additional drink.");
            var drinkPanel = (GameObject)Get(ui, "drinkPanel");
            Check(drinkPanel != null && drinkPanel.activeSelf, "Drinks page opens by default.");
            ui.Close();
            group.ConfirmOrder(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke);
            group.state = CustomerGroup.GroupState.OrderTaken;
            group.submittedOrder = group.currentOrder;
            outputOrder = group.currentOrderNumber;
            var slot = kitchen.drinkTraySpawnPoints.First(p => p != null && p.GetComponentInChildren<FoodTray>(true) == null);
            output = (FoodTray)Call(kitchen, "SpawnOutputTray", slot, group);
            output.Init(group, preserveOrderSnapshot: true);
            ((IDictionary)Get(kitchen, "preparedResults"))[outputOrder] = output;
            ((IDictionary)Get(kitchen, "preparedSlots"))[outputOrder] = Array.IndexOf(kitchen.drinkTraySpawnPoints, slot);
            Check(kitchen.TryGetPreparedTray(outputOrder, out var validated) && validated == output, "Prepared bar tray passes existing order/slot validation.");
            Check((Transform)Get(output.GetComponent<FoodTrayInteractable>(), "pickupPoint") == kitchen.drinkPickupApproach, "Bar tray uses the authored pickup approach.");
            Call(group, "FinishFineDiningDrinks", false);
            group.state = CustomerGroup.GroupState.ReadyToOrder;
            Set(group, "hasConfirmedOrder", false);
            ui.Open(group);
            entries = (List<NotepadMenuEntryUI>)Get(ui, "menuEntries");
            Check(foodTab.gameObject.activeSelf && entries.Any(e => e.Category == MenuProductCategory.Food), "Opening an ordinary order restores food choices and tab.");
            ui.Close();
            if (keepNotepadPreview)
            {
                group.state = CustomerGroup.GroupState.Eating;
                Set(group, "fineDiningDrink", drink); Call(group, "BeginAdditionalDrinkOrder");
                ui.Open(group); previewGroup = group;
            }
            return "PASS: " + checks + " no-save runtime stock/billing/notepad/bar-binding checks. Mouse/touch pouring and autonomous delivery are not tested by this helper.";
        }
        finally
        {
            if (outputOrder >= 0)
            {
                ((IDictionary)Get(kitchen, "preparedResults")).Remove(outputOrder);
                ((IDictionary)Get(kitchen, "preparedSlots")).Remove(outputOrder);
            }
            if (output != null) { output.gameObject.SetActive(false); UnityEngine.Object.Destroy(output.gameObject); }
            inventory.ApplySaveData(snapshot);
            if (previewGroup != group)
            {
                ui.Close();
                go.SetActive(false); UnityEngine.Object.Destroy(go);
            }
        }
    }

    // Explicitly started and polled by Editor update; normal kitchen timing is never overridden.
    public static string StartPreparationCheck()
    {
        if (preparation != null) throw new InvalidOperationException("A preparation check is already running.");
        if (!EditorApplication.isPlaying || !GameSaveManager.IsPersistenceSuspended || !SessionState.GetBool(SessionKey, false))
            throw new InvalidOperationException("ArmRuntimeChecks before entering a disposable no-save Play Mode session.");
        if (SceneManager.GetActiveScene().name != "Lobby3" || GameDayManager.Instance == null || GameDayManager.Instance.ServiceActive
            || MultiplayerRestaurantBridge.IsActive)
            throw new InvalidOperationException("Run only in single-player Lobby3 preparation; do not start a shift.");
        var kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenManager>();
        var inventory = InventoryManager.Instance;
        var catalog = MenuCatalog.Default;
        if (kitchen == null || !kitchen.HasDrinkServiceOutput || kitchen.foodTrayPrefab == null || inventory == null
            || LobbyStockBridge.Instance == null || catalog == null || WaiterHands.Instance == null || OrderNumberManager.Instance == null)
            throw new InvalidOperationException("Scene kitchen, bar, stock, hands and order-number bindings must be ready.");
        if (!kitchen.drinkTraySpawnPoints.Any(p => p != null && p.GetComponentInChildren<FoodTray>(true) == null))
            throw new InvalidOperationException("The real bar needs an unused output slot; no existing food will be moved.");
        CleanupRuntimePreview();
        var food = catalog.GetProducts(MenuProductCategory.Food).First();
        var drink = catalog.GetProducts(MenuProductCategory.Drink).First(p => p.servingPrefab != null && p.ingredients != null
            && p.ingredients.Any(i => i?.item != null && i.amount > 0 && inventory.IsTracked(i.item.itemType)));
        var run = new PreparationRun
        {
            kitchen = kitchen, inventory = inventory, drink = drink, inventorySnapshot = new GameSaveData(),
            originalHands = WaiterHands.Instance, originalHeldTray = WaiterHands.Instance.holdingTray,
            numbers = OrderNumberManager.Instance, requirements = new Dictionary<ItemType, int>(),
            started = EditorApplication.timeSinceStartup
        };
        run.nextNumberBefore = (int)Get(run.numbers, "nextOrderNumber");
        inventory.FillSaveData(run.inventorySnapshot);
        preparation = run;
        try
        {
            foreach (var ingredient in drink.ingredients)
                if (ingredient?.item != null && ingredient.amount > 0 && inventory.IsTracked(ingredient.item.itemType))
                {
                    run.requirements.TryGetValue(ingredient.item.itemType, out int amount);
                    run.requirements[ingredient.item.itemType] = amount + ingredient.amount;
                }
            foreach (var need in run.requirements) inventory.AddStock(need.Key, need.Value * 3);
            run.stockBefore = run.requirements.ToDictionary(p => p.Key, p => inventory.GetStock(p.Key));
            run.root = new GameObject("Disposable real drink preparation check");
            run.root.transform.position = new Vector3(0f, -1000f, 0f);
            var customer = new GameObject("Isolated dining group"); customer.transform.SetParent(run.root.transform, false);
            run.group = customer.AddComponent<CustomerGroup>(); run.group.enabled = false;
            run.group.currentOrder = Meal(food, drink); run.group.submittedOrder = run.group.currentOrder;
            run.meal = run.group.currentOrder;
            run.group.currentOrderNumber = 906;
            run.group.state = CustomerGroup.GroupState.Eating;
            run.group.ConfirmOrder(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke);
            run.mealPrice = run.group.GetCurrentOrderTotal();
            run.mealTray = UnityEngine.Object.Instantiate(kitchen.foodTrayPrefab, run.root.transform);
            run.mealTray.transform.localPosition = Vector3.zero;
            run.mealTray.Init(run.group, preserveOrderSnapshot: true);
            run.mealTray.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
            WaiterHands.SetAllColliders(run.mealTray.gameObject, false);
            Set(run.group, "activeFoodTray", run.mealTray);
            var handObject = new GameObject("Isolated waiter hands"); handObject.transform.SetParent(run.root.transform, false);
            // The existing singleton remains active. Duplicate staff hands disable their Update in Awake;
            // their public carry/delivery methods still exercise the normal implementation.
            run.hands = handObject.AddComponent<WaiterHands>(); run.hands.enabled = false;
            PreparationAssert(run, WaiterHands.Instance == run.originalHands, "The real hands singleton must stay unchanged.");
            Set(run.group, "fineDiningDrink", drink);
            Call(run.group, "BeginAdditionalDrinkOrder");
            run.order = run.group.currentOrderNumber;
            run.nextNumberAfter = (int)Get(run.numbers, "nextOrderNumber");
            PreparationAssert(run, run.group.IsFineDiningAdditionalDrinkOrder && run.group.state == CustomerGroup.GroupState.ReadyToOrder,
                "The real additional drink request must enter ReadyToOrder.");
            PreparationAssert(run, !kitchen.HasAcceptedOrder(run.order), "The fresh drink number must not collide with an existing kitchen order.");
            PreparationAssert(run, run.group.TakeOrderFromWaiter(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke, null),
                "The real waiter order-taking path must accept the drink.");
            CheckPreparationStock(run);
            PreparationAssert(run, !run.group.TakeOrderFromWaiter(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke, null),
                "A duplicate order-taking attempt must be rejected.");
            CheckPreparationStock(run);
            run.accepted = kitchen.ProcessOrder(run.group);
            PreparationAssert(run, run.accepted, "The real kitchen must accept the drink job.");
            PreparationCheckResult = "RUNNING: order #" + run.order + " is preparing through the real kitchen/bar path (45-second limit).";
            EditorApplication.update -= UpdatePreparationCheck;
            EditorApplication.update += UpdatePreparationCheck;
            return PreparationCheckResult;
        }
        catch (Exception ex)
        {
            FinishPreparationCheck("FAIL: " + ex.GetBaseException().Message);
            return PreparationCheckResult;
        }
    }

    private static void PreparationAssert(PreparationRun run, bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
        run.checks++;
    }

    private static void CheckPreparationStock(PreparationRun run)
    {
        foreach (var need in run.requirements)
            PreparationAssert(run, run.inventory.GetStock(need.Key) == run.stockBefore[need.Key] - need.Value,
                "Real drink service must consume its recipe exactly once (" + need.Key + ").");
    }

    private static void UpdatePreparationCheck()
    {
        var run = preparation;
        if (run == null) { EditorApplication.update -= UpdatePreparationCheck; return; }
        try
        {
            if (!EditorApplication.isPlaying || !GameSaveManager.IsPersistenceSuspended || SceneManager.GetActiveScene().name != "Lobby3"
                || GameDayManager.Instance == null || GameDayManager.Instance.ServiceActive)
                throw new InvalidOperationException("The save-protected Lobby3 preparation session ended during the check.");
            if (EditorApplication.timeSinceStartup - run.started > 45d)
                throw new TimeoutException("The real drink kitchen/delivery path did not complete within 45 seconds.");
            if (WaiterHands.Instance != run.originalHands || run.originalHands.holdingTray != run.originalHeldTray)
                throw new InvalidOperationException("The real player's carried tray must remain untouched.");
            if (run.deliveredFrame < 0)
            {
                if (!run.group.IsFineDiningAdditionalDrinkOrder)
                    throw new InvalidOperationException("The kitchen canceled the drink before a deliverable bar tray appeared.");
                if (!run.kitchen.TryGetPreparedTray(run.order, out var output)) return;
                run.output = output;
                PreparationAssert(run, output.TargetGroup == run.group && output.transform.parent != null
                    && Array.IndexOf(run.kitchen.drinkTraySpawnPoints, output.transform.parent) >= 0,
                    "The real kitchen must place this drink at an authored bar slot.");
                PreparationAssert(run, (Transform)Get(output.GetComponent<FoodTrayInteractable>(), "pickupPoint") == run.kitchen.drinkPickupApproach,
                    "The real prepared drink must use the bar pickup approach.");
                PreparationAssert(run, run.hands.PickupTray(output) && run.hands.holdingTray == output,
                    "Temporary waiter hands must pick up the real prepared tray.");
                var contents = output.DeliveredContents;
                PreparationAssert(run, run.hands.TryDeliverTrayTo(run.group, destroyTrayObject: false) && run.hands.holdingTray == null,
                    "Normal waiter delivery validation must accept the requested drink.");
                output.transform.SetParent(run.root.transform, true);
                output.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
                run.group.ReceiveFoodFromWaiter(contents, output);
                PreparationAssert(run, ReferenceEquals(run.group.currentOrder, run.meal) && run.group.currentOrderNumber == 906,
                    "Delivery must restore the original meal and billing number.");
                PreparationAssert(run, run.group.state == CustomerGroup.GroupState.Eating && !run.group.HasFineDiningDrinkEvent,
                    "Delivery must return the group to Eating.");
                PreparationAssert(run, run.group.GetCurrentOrderTotal() == run.mealPrice + run.drink.EffectiveSellPrice,
                    "Delivery must append exactly one drink charge to the existing meal.");
                // Repeated arrival callbacks may occur; they cannot duplicate stock or billing.
                run.group.ReceiveFoodFromWaiter(contents, output);
                Call(run.group, "FinishFineDiningDrinks", true);
                PreparationAssert(run, run.group.GetCurrentOrderTotal() == run.mealPrice + run.drink.EffectiveSellPrice,
                    "Repeated drink completion must not charge again.");
                CheckPreparationStock(run);
                run.deliveredFrame = Time.frameCount;
                PreparationCheckResult = "RUNNING: drink delivered; checking deferred tray/glass cleanup.";
                return;
            }
            if (Time.frameCount <= run.deliveredFrame) return;
            PreparationAssert(run, run.output == null, "The additional serving tray must be removed after delivery.");
            PreparationAssert(run, (FoodTray)Get(run.group, "activeFoodTray") == run.mealTray
                && run.root.GetComponentsInChildren<FoodTray>(true).Length == 1,
                "The original meal tray must remain alone, with no stacked duplicate tray.");
            var drinkAnchor = (Transform)Get(run.mealTray, "drinkAnchor");
            PreparationAssert(run, drinkAnchor != null && drinkAnchor.childCount == 1,
                "The existing meal must contain one replacement drink visual, with no overlapping old glass.");
            CheckPreparationStock(run);
            FinishPreparationCheck("PASS: " + run.checks + " real preparation/delivery checks in "
                + (EditorApplication.timeSinceStartup - run.started).ToString("F1")
                + " seconds. Real stock withdrawal, timed bar output, carry/delivery, meal continuation, once-only billing and tray/glass replacement passed.");
        }
        catch (Exception ex) { FinishPreparationCheck("FAIL: " + ex.GetBaseException().Message); }
    }

    public static void CancelPreparationCheck()
    {
        if (preparation != null) FinishPreparationCheck("CANCELED: preparation diagnostic stopped and temporary state cleaned up.");
    }

    private static void FinishPreparationCheck(string result)
    {
        EditorApplication.update -= UpdatePreparationCheck;
        var run = preparation;
        preparation = null;
        PreparationCheckResult = result;
        if (run == null) return;
        try
        {
            if (run.group != null)
            {
                Call(run.group, "FinishFineDiningDrinks", false);
                RestaurantTaskClaim.Complete(run.group);
                // Invalidate only this job; no real kitchen coroutines or other orders are stopped.
                run.group.currentOrderNumber = -1;
                run.group.gameObject.SetActive(false);
            }
            if (run.kitchen != null && run.accepted)
            {
                var prepared = run.kitchen.GetPreparedResult(run.order);
                if (prepared != null && prepared.TargetGroup == run.group) run.output = prepared;
                foreach (string field in new[] { "preparedResults", "preparedSlots", "activeOrderForecasts", "completedOrderForecasts" })
                    ((IDictionary)Get(run.kitchen, field)).Remove(run.order);
                foreach (string field in new[] { "cookingOrders", "completedOrders", "spawningResults" })
                    ((HashSet<int>)Get(run.kitchen, field)).Remove(run.order);
            }
            if (run.output != null)
            {
                RestaurantTaskClaim.Complete(run.output);
                run.output.GetComponent<FoodTrayInteractable>()?.NotifyDeliveredToTable();
                run.output.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(run.output.gameObject);
            }
            if (run.numbers != null && run.nextNumberAfter > run.nextNumberBefore
                && (int)Get(run.numbers, "nextOrderNumber") == run.nextNumberAfter)
                Set(run.numbers, "nextOrderNumber", run.nextNumberBefore);
        }
        catch (Exception ex) { PreparationCheckResult += " CLEANUP ISSUE: " + ex.GetBaseException().Message; }
        finally
        {
            try { if (run.inventory != null) run.inventory.ApplySaveData(run.inventorySnapshot); }
            catch (Exception ex) { PreparationCheckResult += " INVENTORY RESTORE ISSUE: " + ex.GetBaseException().Message; }
            if (run.root != null) { run.root.SetActive(false); UnityEngine.Object.Destroy(run.root); }
        }
    }

    public static void CleanupRuntimePreview()
    {
        if (previewGroup == null) return;
        OrderChecklistUI.Instance?.Close();
        previewGroup.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(previewGroup.gameObject);
        previewGroup = null;
    }
}
#endif
