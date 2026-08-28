#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Deterministic Lobby1 trolley integration test. It exercises the real
/// LobbyAutonomousService, NavMesh agents, authored trolley prefabs, tray
/// lifecycle, delivery, cleanup, parking, and same-day reuse.
/// </summary>
[InitializeOnLoad]
public static class TrolleyGameplaySmokeTest
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string RunningKey = "DineIn.TrolleyGameplaySmokeTest.Running";
    private const string RequestFileName = "RunTrolleyGameplaySmokeTest.request";
    private const string ResultFileName = "TrolleyGameplaySmokeTest.result";

    private enum Phase
    {
        None,
        WaitingForLobby,
        WaitingForTrolleys,
        WaitingForFirstWaiterBatch,
        WaitingForBusserBatch,
        WaitingForSecondWaiterBatch,
        WaitingForSingleWaiterDelivery,
        WaitingForContextualBusserRoute
    }

    private static Phase phase;
    private static double phaseStartedAt;
    private static LobbyAutonomousService service;
    private static KitchenManager kitchen;
    private static BotTrolleyCarrier waiterTrolley;
    private static BotTrolleyCarrier busserTrolley;
    private static AutonomousStaffBot waiterBot;
    private static AutonomousStaffBot busserBot;
    private static readonly List<CustomerGroup> groups = new List<CustomerGroup>();
    private static readonly List<FoodTray> trays = new List<FoodTray>();
    private static int waiterMaximumLoad;
    private static int busserMaximumLoad;
    private static bool waiterGripObserved;
    private static bool busserGripObserved;
    private static bool waiterBoostObserved;
    private static bool busserBoostObserved;
    private static bool forecastObserved;
    private static bool singleWaiterUsedTrolley;
    private static Booth bundledDirtyBooth;
    private static int initialCleanedCount;

    static TrolleyGameplaySmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        string requestPath = Path.Combine(ProjectRoot, "Temp", RequestFileName);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Dine In/Run Trolley Gameplay Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[TrolleyGameplaySmokeTest] Stop Play Mode before running the test.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ResetRuntimeFields();
        SessionState.SetBool(RunningKey, true);
        WriteResult("RUNNING");
        Debug.Log("[TrolleyGameplaySmokeTest] Starting deterministic waiter/busser trolley test.");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SetPhase(Phase.WaitingForLobby);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            ResetRuntimeFields();
        }
    }

    private static void Tick()
    {
        try
        {
            switch (phase)
            {
                case Phase.WaitingForLobby:
                    TryPrepareLobby();
                    break;
                case Phase.WaitingForTrolleys:
                    TryPrepareFirstWaiterBatch();
                    break;
                case Phase.WaitingForFirstWaiterBatch:
                    TrackFirstWaiterBatch();
                    break;
                case Phase.WaitingForBusserBatch:
                    TrackBusserBatch();
                    break;
                case Phase.WaitingForSecondWaiterBatch:
                    TrackSecondWaiterBatch();
                    break;
                case Phase.WaitingForSingleWaiterDelivery:
                    TrackSingleWaiterDelivery();
                    break;
                case Phase.WaitingForContextualBusserRoute:
                    TrackContextualBusserRoute();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail("Unhandled test exception: " + exception);
        }
    }

    private static void TryPrepareLobby()
    {
        service = UnityEngine.Object.FindFirstObjectByType<LobbyAutonomousService>();
        kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenManager>();
        EmployeeManager employees = EmployeeManager.Instance;
        EquipmentManager equipment = EquipmentManager.Instance;
        GameDayManager day = GameDayManager.Instance;

        if (service == null || kitchen == null || employees == null || equipment == null || day == null ||
            GameSaveManager.Instance == null || !GameSaveManager.Instance.HasCompletedInitialLoad)
        {
            if (Elapsed > 20d)
                Fail("Lobby systems did not initialize within 20 seconds.");
            return;
        }

        GameSaveManager.Instance.SuppressWritesForTests = true;
        EnsureEveryRoleAssigned(employees);
        Assert(employees.HasAllRequiredRolesAssigned,
            "The test could not assign every required employee role.");

        Assert(equipment.DebugUnlockAndPurchase(EquipmentUpgradeService.WaiterTrolleyID),
            "Waiter trolley upgrade data is missing from EquipmentManager.");
        Assert(equipment.DebugUnlockAndPurchase(EquipmentUpgradeService.BusserTrolleyID),
            "Busser trolley upgrade data is missing from EquipmentManager.");

        ConfigureServiceForTest(service);
        if (!day.ServiceActive)
            day.StartShift();
        Assert(day.ServiceActive, "The shift did not start for the trolley test.");
        StopNormalCustomerSpawning(day);
        RemoveExistingCustomers();
        SetPhase(Phase.WaitingForTrolleys);
    }

    private static void TryPrepareFirstWaiterBatch()
    {
        ResolveRuntimeActors();
        if (waiterTrolley == null || busserTrolley == null || waiterBot == null || busserBot == null)
        {
            if (Elapsed > 15d)
                Fail("Purchased trolleys or their assigned bots were not created within 15 seconds.");
            return;
        }

        Assert(waiterTrolley.IsConfigured, "Waiter trolley is not configured: " + waiterTrolley.ConfigurationProblem);
        Assert(busserTrolley.IsConfigured, "Busser trolley is not configured: " + busserTrolley.ConfigurationProblem);
        Assert(waiterTrolley.MinimumBatchSize == 2 && busserTrolley.MinimumBatchSize == 2,
            "Trolley minimum batch size is not two.");
        Assert(waiterTrolley.Capacity == 4 && busserTrolley.Capacity == 4,
            "Trolley capacity is not four.");

        ConfigureFastBot(waiterBot);
        ConfigureFastBot(busserBot);
        SpawnDeliveryBatch(4, 9100);
        waiterMaximumLoad = 0;
        waiterGripObserved = false;
        waiterBoostObserved = false;
        SetPhase(Phase.WaitingForFirstWaiterBatch);
    }

    private static void TrackFirstWaiterBatch()
    {
        TrackTrolleyUse(
            waiterTrolley,
            waiterBot,
            ref waiterMaximumLoad,
            ref waiterGripObserved,
            ref waiterBoostObserved);

        if (trays.Count == 4 && trays.All(tray => tray != null) &&
            groups.Count == 4 && groups.All(group => group != null && group.state == CustomerGroup.GroupState.Eating) &&
            waiterTrolley.CurrentState == BotTrolleyCarrier.TrolleyState.ParkedIdle && !waiterTrolley.IsInUse)
        {
            Assert(waiterMaximumLoad == 4,
                "Waiter route never loaded all four prepared trays before delivering them.");
            Assert(waiterGripObserved,
                "Waiter trolley was never aligned to the dedicated grip while moving.");
            Assert(waiterBoostObserved,
                "Waiter trolley never applied its movement upgrade while being pushed.");
            Assert(Mathf.Approximately(waiterBot.TrolleyMovementSpeedMultiplier, 1f),
                "Waiter speed did not return to normal after parking the trolley.");
            Assert(Vector3.Distance(waiterTrolley.transform.position, waiterTrolley.ParkingPosition) <= 0.08f,
                "Waiter trolley did not return to its authored parking position.");

            PrepareDeliveredTraysForCleanup();
            busserMaximumLoad = 0;
            busserGripObserved = false;
            busserBoostObserved = false;
            initialCleanedCount = GetTrayCleanedCount();
            SetPhase(Phase.WaitingForBusserBatch);
            return;
        }

        FailOnRecovery(waiterTrolley, "waiter");
        if (Elapsed > 90d)
            Fail("Waiter did not load and deliver four trays in one trolley route within 90 seconds. " +
                 Describe(waiterTrolley, waiterMaximumLoad));
    }

    private static void TrackBusserBatch()
    {
        TrackTrolleyUse(
            busserTrolley,
            busserBot,
            ref busserMaximumLoad,
            ref busserGripObserved,
            ref busserBoostObserved);

        bool allCleaned = trays.Count == 4 && trays.All(tray => tray == null);
        if (allCleaned && busserTrolley.CurrentState == BotTrolleyCarrier.TrolleyState.ParkedIdle &&
            !busserTrolley.IsInUse)
        {
            Assert(busserMaximumLoad == 4,
                "Busser route never collected all four dirty trays before visiting the sink.");
            Assert(busserGripObserved,
                "Busser trolley was never aligned to the dedicated grip while moving.");
            Assert(busserBoostObserved,
                "Busser trolley never applied its movement upgrade while being pushed.");
            Assert(Mathf.Approximately(busserBot.TrolleyMovementSpeedMultiplier, 1f),
                "Busser speed did not return to normal after parking the trolley.");
            Assert(GetTrayCleanedCount() - initialCleanedCount == 4,
                "The four-tray busser route did not register exactly four cleaned trays.");
            Assert(bundledDirtyBooth == null || !bundledDirtyBooth.IsDirty,
                "Busser did not clean the dirty booth during the same trolley stop.");
            Assert(Vector3.Distance(busserTrolley.transform.position, busserTrolley.ParkingPosition) <= 0.08f,
                "Busser trolley did not return to its authored parking position.");

            DestroyTestGroups();
            SpawnForecastDeliveryPair(9200);
            waiterMaximumLoad = 0;
            waiterGripObserved = false;
            waiterBoostObserved = false;
            SetPhase(Phase.WaitingForSecondWaiterBatch);
            return;
        }

        FailOnRecovery(busserTrolley, "busser");
        if (Elapsed > 90d)
            Fail("Busser did not collect four dirty trays before one sink trip within 90 seconds. " +
                 Describe(busserTrolley, busserMaximumLoad));
    }

    private static void TrackSecondWaiterBatch()
    {
        CaptureForecastTray();
        TrackTrolleyUse(
            waiterTrolley,
            waiterBot,
            ref waiterMaximumLoad,
            ref waiterGripObserved,
            ref waiterBoostObserved);

        if (trays.Count == 2 && trays.All(tray => tray != null) &&
            groups.Count == 2 && groups.All(group => group != null && group.state == CustomerGroup.GroupState.Eating) &&
            waiterTrolley.CurrentState == BotTrolleyCarrier.TrolleyState.ParkedIdle && !waiterTrolley.IsInUse)
        {
            Assert(waiterMaximumLoad == 2,
                "The same-day second waiter route did not batch both trays.");
            Assert(waiterGripObserved,
                "The waiter did not reacquire the trolley on the second same-day route.");
            Assert(waiterBoostObserved,
                "The waiter did not receive the trolley movement boost on the second route.");
            Assert(forecastObserved,
                "The kitchen forecast was not exposed while the second tray was cooking.");
            Assert(Mathf.Approximately(waiterBot.TrolleyMovementSpeedMultiplier, 1f),
                "Waiter trolley boost stacked or remained active after same-day reuse.");

            DestroyTestObjects();
            SpawnDeliveryBatch(1, 9300);
            singleWaiterUsedTrolley = false;
            SetPhase(Phase.WaitingForSingleWaiterDelivery);
            return;
        }

        FailOnRecovery(waiterTrolley, "second waiter");
        if (Elapsed > 75d)
            Fail("Waiter trolley was not reusable for a second same-day batch. " +
                 Describe(waiterTrolley, waiterMaximumLoad));
    }

    private static void TrackSingleWaiterDelivery()
    {
        if (waiterTrolley != null && waiterTrolley.IsInUse)
            singleWaiterUsedTrolley = true;

        if (trays.Count == 1 && trays[0] != null && groups.Count == 1 &&
            groups[0] != null && groups[0].state == CustomerGroup.GroupState.Eating)
        {
            Assert(!singleWaiterUsedTrolley,
                "Waiter fetched the trolley for one isolated tray with no imminent second order.");

            PrepareDeliveredTraysForCleanup();
            Booth booth = groups[0].assignedBooth;
            booth.ClearCurrentGroup();
            booth.ForceDirtyForTest();
            bundledDirtyBooth = booth;
            busserMaximumLoad = 0;
            busserGripObserved = false;
            busserBoostObserved = false;
            initialCleanedCount = GetTrayCleanedCount();
            SetPhase(Phase.WaitingForContextualBusserRoute);
            return;
        }

        if (Elapsed > 60d)
            Fail("Waiter did not complete the isolated one-tray fallback within 60 seconds.");
    }

    private static void TrackContextualBusserRoute()
    {
        TrackTrolleyUse(
            busserTrolley,
            busserBot,
            ref busserMaximumLoad,
            ref busserGripObserved,
            ref busserBoostObserved);

        bool trayCleaned = trays.Count == 1 && trays[0] == null;
        if (trayCleaned && bundledDirtyBooth != null && !bundledDirtyBooth.IsDirty &&
            busserTrolley.CurrentState == BotTrolleyCarrier.TrolleyState.ParkedIdle &&
            !busserTrolley.IsInUse)
        {
            Assert(busserMaximumLoad == 1,
                "Contextual busser route did not carry exactly its one useful tray.");
            Assert(busserGripObserved && busserBoostObserved,
                "Contextual busser route did not use the trolley grip and speed benefit.");
            Assert(GetTrayCleanedCount() - initialCleanedCount == 1,
                "Contextual busser route did not clean its tray exactly once.");
            Assert(Mathf.Approximately(busserBot.TrolleyMovementSpeedMultiplier, 1f),
                "Busser speed did not return to normal after contextual trolley use.");
            Pass();
            return;
        }

        FailOnRecovery(busserTrolley, "contextual busser");
        if (Elapsed > 75d)
            Fail("Busser did not combine one tray pickup and booth cleaning into one trolley route. " +
                 Describe(busserTrolley, busserMaximumLoad));
    }

    private static void SpawnDeliveryBatch(int count, int firstOrderNumber)
    {
        DestroyTestObjects();

        Booth[] booths = UnityEngine.Object.FindObjectsByType<Booth>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(booth => booth != null && booth.approachPoint != null && FindFoodDrop(booth) != null)
            .Take(count)
            .ToArray();
        Assert(booths.Length == count,
            $"Lobby1 has only {booths.Length} usable booths; {count} are required.");
        Assert(kitchen.foodTrayPrefab != null, "KitchenManager has no FoodTray prefab.");
        Assert(kitchen.traySpawnPoints != null && kitchen.traySpawnPoints.Length >= count,
            $"KitchenManager needs at least {count} dine-in tray spawn points for this test.");

        Recipe product = MenuCatalog.Default != null
            ? MenuCatalog.Default.Products.FirstOrDefault(item => item != null && item.category == MenuProductCategory.Food)
            : null;
        Assert(product != null, "MenuCatalog contains no food product for the test order.");
        TrayPickupQueue queue = kitchen.GetComponent<TrayPickupQueue>();
        Assert(queue != null, "KitchenManager has no TrayPickupQueue.");

        for (int i = 0; i < count; i++)
        {
            Booth booth = booths[i];
            booth.ClearCurrentGroup();
            booth.CleanMess();

            GameObject groupObject = new GameObject($"TrolleySmokeGroup_{firstOrderNumber + i}");
            CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
            group.currentOrderNumber = firstOrderNumber + i;
            group.currentOrder.SetProducts(new[] { product }, product.DisplayName, product.EffectiveSellPrice);
            group.ConfirmOrder(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke);
            group.assignedBooth = booth;
            group.state = CustomerGroup.GroupState.OrderTaken;
            // Keep the deterministic customer in Eating long enough to prove
            // cart return before the normal bill workflow can take priority.
            group.minEatSeconds = 300f;
            group.maxEatSeconds = 300f;
            group.transform.position = booth.tableLookTarget != null
                ? booth.tableLookTarget.position
                : booth.transform.position;
            booth.SetCurrentGroup(group);

            Transform spawn = kitchen.traySpawnPoints[i];
            FoodTray tray = UnityEngine.Object.Instantiate(
                kitchen.foodTrayPrefab, spawn.position, spawn.rotation, spawn);
            tray.name = $"TrolleySmokeTray_{firstOrderNumber + i}";
            tray.Init(group);
            FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
            Assert(interactable != null, tray.name + " has no FoodTrayInteractable.");
            interactable.SetDeliveryPickable(queue);

            groups.Add(group);
            trays.Add(tray);
        }
    }

    private static void SpawnForecastDeliveryPair(int firstOrderNumber)
    {
        SpawnDeliveryBatch(1, firstOrderNumber);

        Booth firstBooth = groups[0].assignedBooth;
        Booth secondBooth = UnityEngine.Object.FindObjectsByType<Booth>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(booth => booth != null && booth != firstBooth &&
                                     booth.approachPoint != null && FindFoodDrop(booth) != null);
        Assert(secondBooth != null, "No second booth is available for the forecast test.");
        secondBooth.ClearCurrentGroup();
        secondBooth.CleanMess();

        Recipe product = MenuCatalog.Default != null
            ? MenuCatalog.Default.Products.FirstOrDefault(
                item => item != null && item.category == MenuProductCategory.Food)
            : null;
        Assert(product != null, "MenuCatalog contains no food product for the forecast order.");

        GameObject groupObject = new GameObject($"TrolleyForecastGroup_{firstOrderNumber + 1}");
        CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
        group.currentOrderNumber = firstOrderNumber + 1;
        group.currentOrder.SetProducts(new[] { product }, product.DisplayName, product.EffectiveSellPrice);
        group.ConfirmOrder(CustomerGroup.FoodType.Chicken, CustomerGroup.DrinkType.Coke);
        group.assignedBooth = secondBooth;
        group.state = CustomerGroup.GroupState.OrderTaken;
        group.minEatSeconds = 300f;
        group.maxEatSeconds = 300f;
        group.transform.position = secondBooth.tableLookTarget != null
            ? secondBooth.tableLookTarget.position
            : secondBooth.transform.position;
        secondBooth.SetCurrentGroup(group);
        groups.Add(group);

        kitchen.cookSeconds = 0.45f;
        SetField(kitchen, "preparationDelaySeconds", 0.1f);
        Assert(kitchen.ProcessOrder(group), "Kitchen rejected the deterministic forecast order.");
        Assert(kitchen.TryGetForecast(group.currentOrderNumber, out KitchenManager.OrderForecast forecast),
            "Kitchen did not publish an active forecast for the cooking order.");
        Assert(forecast.HasReliableReadyTime && forecast.RemainingSeconds <= 3f,
            "Kitchen forecast is not a reliable near-ready dine-in prediction.");
        forecastObserved = true;
    }

    private static void CaptureForecastTray()
    {
        if (groups.Count < 2 || trays.Count >= 2 || groups[1] == null)
            return;

        FoodTray forecastTray = UnityEngine.Object.FindObjectsByType<FoodTray>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(tray => tray != null && tray.TargetGroup == groups[1]);
        if (forecastTray != null)
            trays.Add(forecastTray);
    }

    private static void PrepareDeliveredTraysForCleanup()
    {
        for (int i = 0; i < trays.Count; i++)
        {
            FoodTray tray = trays[i];
            CustomerGroup group = groups[i];
            Assert(tray != null && group != null, "Delivered waiter batch lost a tray or group.");
            Assert(FindFoodDrop(group.assignedBooth).GetComponentInChildren<FoodTray>(true) == tray,
                tray.name + " was not placed on its assigned booth.");

            group.state = CustomerGroup.GroupState.Leaving;
            FoodTrayInteractable interactable = tray.GetComponent<FoodTrayInteractable>();
            Assert(interactable != null, tray.name + " lost FoodTrayInteractable after delivery.");
            interactable.SetCleanupPickable(true);

            if (i == 0)
            {
                group.assignedBooth.ClearCurrentGroup();
                group.assignedBooth.ForceDirtyForTest();
                bundledDirtyBooth = group.assignedBooth;
            }
        }
    }

    private static void TrackTrolleyUse(
        BotTrolleyCarrier trolley,
        AutonomousStaffBot bot,
        ref int maximumLoad,
        ref bool gripObserved,
        ref bool boostObserved)
    {
        if (trolley == null)
            return;

        maximumLoad = Mathf.Max(maximumLoad, trolley.Count);
        if (!trolley.IsInUse || bot == null)
            return;

        Assert(Mathf.Approximately(
                bot.TrolleyMovementSpeedMultiplier,
                trolley.MovementSpeedMultiplier),
            $"{trolley.name} did not apply its configured speed multiplier to {bot.name}.");
        Assert(bot.EffectiveMovementSpeed >
               bot.BaseMovementSpeed * bot.EmployeeMovementSpeedMultiplier,
            $"{trolley.name} did not increase {bot.name}'s effective movement speed.");
        boostObserved = true;

        Transform grip = null;
        WaiterHands waiterHands = bot.GetComponent<WaiterHands>();
        BusserHands busserHands = bot.GetComponent<BusserHands>();
        if (waiterHands != null)
            waiterHands.TryGetTrolleyGripPoint(out grip);
        else if (busserHands != null)
            busserHands.TryGetTrolleyGripPoint(out grip);

        if (grip != null && trolley.HoldingPoint != null)
        {
            float distance = Vector3.Distance(grip.position, trolley.HoldingPoint.position);
            Assert(distance <= 0.12f,
                $"{trolley.name} handle drifted {distance:0.000}m from the bot grip.");
            gripObserved = true;
        }
    }

    private static void ResolveRuntimeActors()
    {
        BotTrolleyCarrier[] trolleyCandidates = UnityEngine.Object.FindObjectsByType<BotTrolleyCarrier>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        waiterTrolley = trolleyCandidates.FirstOrDefault(
            trolley => trolley != null && trolley.Effect == EquipmentUpgradeEffect.WaiterTrolley);
        busserTrolley = trolleyCandidates.FirstOrDefault(
            trolley => trolley != null && trolley.Effect == EquipmentUpgradeEffect.BusserTrolley);

        AutonomousStaffBot[] bots = UnityEngine.Object.FindObjectsByType<AutonomousStaffBot>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        waiterBot = bots.FirstOrDefault(bot => bot != null && bot.GetComponent<WaiterHands>() != null);
        busserBot = bots.FirstOrDefault(bot => bot != null && bot.GetComponent<BusserHands>() != null);
    }

    private static void EnsureEveryRoleAssigned(EmployeeManager manager)
    {
        manager.ResetDailyAssignments();
        manager.EnsureEmployeesGenerated();

        List<EmployeeRole> required = new List<EmployeeRole>();
        required.AddRange(EmployeeRoleCatalog.LobbyRoles);
        required.AddRange(EmployeeRoleCatalog.KitchenRoles);
        for (int i = 0; i < required.Count; i++)
        {
            EmployeeRole role = required[i];
            EmployeeData employee = manager.allEmployees.FirstOrDefault(
                candidate => candidate != null && candidate.role == role && candidate.hired)
                ?? manager.allEmployees.FirstOrDefault(
                    candidate => candidate != null && candidate.role == role);
            Assert(employee != null, "No generated employee exists for " + role + ".");
            if (!employee.hired)
                Assert(manager.HireApplicant(employee), "Could not hire the " + role + " test employee.");
            Assert(manager.AssignEmployeeForDay(employee), "Could not assign the " + role + " test employee.");
        }
    }

    private static void ConfigureServiceForTest(LobbyAutonomousService target)
    {
        SetField(target, "managerReactionSeconds", 0f);
        SetField(target, "tableServiceSeconds", 0.05f);
        SetField(target, "counterServiceSeconds", 0.05f);
        SetField(target, "cleaningSeconds", 0.05f);
        SetField(target, "waiterTrolleyBatchGraceSeconds", 0.1f);
        SetField(target, "waiterTrolleyNearReadySeconds", 3f);
        SetField(target, "waiterTrolleyMaximumWaitSeconds", 3f);
        SetField(target, "waiterReadyTrayUrgencySeconds", 8f);
        SetField(target, "busserTrolleyBatchGraceSeconds", 0f);
        SetField(target, "busserBundleSingleDirtyBooth", true);
        SetField(target, "sceneQueryRefreshSeconds", 0.2f);
    }

    private static void ConfigureFastBot(AutonomousStaffBot bot)
    {
        NavMeshAgent agent = bot != null ? bot.GetComponent<NavMeshAgent>() : null;
        Assert(agent != null, (bot != null ? bot.name : "Missing bot") + " has no NavMeshAgent.");
        SetField(bot, "baseAgentSpeed", Mathf.Max(bot.BaseMovementSpeed, 9f));
        SetField(bot, "baseAgentAcceleration", Mathf.Max(agent.acceleration, 30f));
        bot.ClearTrolleyMovementModifier();
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, 720f);
    }

    private static void StopNormalCustomerSpawning(GameDayManager day)
    {
        FieldInfo field = day.GetType().GetField("spawnRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
        Coroutine routine = field?.GetValue(day) as Coroutine;
        if (routine != null)
            day.StopCoroutine(routine);
        field?.SetValue(day, null);
    }

    private static void RemoveExistingCustomers()
    {
        CustomerGroup[] existing = UnityEngine.Object.FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
        }
    }

    private static Transform FindFoodDrop(Booth booth)
    {
        if (booth == null)
            return null;
        Transform[] children = booth.GetComponentsInChildren<Transform>(true);
        return Array.Find(children, child => child != null && child.name == "TableFoodSpawn");
    }

    private static int GetTrayCleanedCount()
    {
        GameDayManager manager = GameDayManager.Instance;
        if (manager == null)
            return 0;
        FieldInfo field = manager.GetType().GetField("traysCleaned", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(manager) : 0;
    }

    private static void FailOnRecovery(BotTrolleyCarrier trolley, string label)
    {
        if (trolley != null && trolley.CurrentState == BotTrolleyCarrier.TrolleyState.Recovery)
            Fail($"The {label} trolley entered Recovery: {trolley.LastFailureReason}");
    }

    private static string Describe(BotTrolleyCarrier trolley, int maximumLoad)
    {
        return trolley == null
            ? "Trolley missing."
            : $"state={trolley.CurrentState}, inUse={trolley.IsInUse}, currentLoad={trolley.Count}, " +
              $"maximumLoad={maximumLoad}, failure='{trolley.LastFailureReason}'.";
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, target.GetType().Name + "." + fieldName + " was not found.");
        field.SetValue(target, value);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - phaseStartedAt;

    private static void SetPhase(Phase next)
    {
        phase = next;
        phaseStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void Pass()
    {
        string message =
            "PASS: waiter loaded/delivered four trays, busser collected four before one sink cleanup, " +
            "both carts used and restored their speed boosts, the waiter used a kitchen forecast for a " +
            "same-day two-tray batch, an isolated tray used the normal fallback, and the busser combined " +
            "one tray plus booth cleaning into one contextual trolley route.";
        Debug.Log("[TrolleyGameplaySmokeTest] " + message);
        Finish(message);
    }

    private static void Fail(string reason)
    {
        string message = "FAIL: " + reason;
        Debug.LogError("[TrolleyGameplaySmokeTest] " + message);
        Finish(message);
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        WriteResult(result);
        SessionState.SetBool(RunningKey, false);
        phase = Phase.None;
        DestroyTestObjects();
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void DestroyTestObjects()
    {
        for (int i = 0; i < trays.Count; i++)
            if (trays[i] != null)
                UnityEngine.Object.DestroyImmediate(trays[i].gameObject);
        DestroyTestGroups();
        trays.Clear();
    }

    private static void DestroyTestGroups()
    {
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null)
                continue;
            group.assignedBooth?.ClearCurrentGroup();
            UnityEngine.Object.DestroyImmediate(group.gameObject);
        }
        groups.Clear();
    }

    private static void ResetRuntimeFields()
    {
        phase = Phase.None;
        service = null;
        kitchen = null;
        waiterTrolley = null;
        busserTrolley = null;
        waiterBot = null;
        busserBot = null;
        groups.Clear();
        trays.Clear();
        waiterMaximumLoad = 0;
        busserMaximumLoad = 0;
        waiterGripObserved = false;
        busserGripObserved = false;
        waiterBoostObserved = false;
        busserBoostObserved = false;
        forecastObserved = false;
        singleWaiterUsedTrolley = false;
        bundledDirtyBooth = null;
        initialCleanedCount = 0;
    }

    private static string ProjectRoot =>
        Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

    private static void WriteResult(string result)
    {
        string directory = Path.Combine(ProjectRoot, "Temp");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ResultFileName), result);
    }
}
#endif
