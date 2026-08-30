using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LobbyPaymentSmokeTest
{
    private const string RunKey = "DineIn.LobbyPaymentSmokeTest.Running";
    private const string MenuPath = "Tools/Dine In/Run Lobby1 Payment Smoke Test %#F9";
    private const string BubblePrefabPath = "Assets/_Project/Restaurant/Assets/Level1/UI/Money.prefab";
    private const string LobbyScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";

    private enum Phase
    {
        None,
        WaitingForLobby,
        WaitingForBotClaim,
        WaitingForCompletion
    }

    private static Phase phase;
    private static double phaseStartedAt;
    private static CustomerGroup group;
    private static Booth booth;
    private static MoneyPickup payment;
    private static GameObject bubbleObject;
    private static WaiterHands staffHands;
    private static LobbyAutonomousService autonomousService;
    private static bool forcedWaiterPoll;

    static LobbyPaymentSmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (SessionState.GetBool(RunKey, false) && EditorApplication.isPlaying)
            BeginRuntimeTest();
    }

    [MenuItem(MenuPath)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[LobbyPaymentSmokeTest] Stop Play Mode before running the test.");
            return;
        }

        if (SceneManager.GetActiveScene().name != "Lobby1")
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

        SessionState.SetBool(RunKey, true);
        WriteResult("RUNNING");
        Debug.Log("[LobbyPaymentSmokeTest] Starting Lobby1 player/bot payment test.");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
            BeginRuntimeTest();
        else if (state == PlayModeStateChange.EnteredEditMode)
            SessionState.SetBool(RunKey, false);
    }

    private static void BeginRuntimeTest()
    {
        EditorApplication.update -= Tick;
        phase = Phase.WaitingForLobby;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            switch (phase)
            {
                case Phase.WaitingForLobby:
                    TrySetUpPayment();
                    break;

                case Phase.WaitingForBotClaim:
                    TrackBotClaim();
                    break;

                case Phase.WaitingForCompletion:
                    TrackCompletion();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail("Unhandled test exception: " + exception);
        }
    }

    private static void TrySetUpPayment()
    {
        autonomousService = UnityEngine.Object.FindFirstObjectByType<LobbyAutonomousService>();
        GameDayManager dayManager = GameDayManager.Instance;
        staffHands = WaiterHands.Instance;

        CashierRegisterUI register = CashierRegisterUI.Instance != null
            ? CashierRegisterUI.Instance
            : UnityEngine.Object.FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);

        if (autonomousService == null || dayManager == null || staffHands == null || register == null)
        {
            if (ElapsedInPhase > 12d)
            {
                Fail(
                    "Lobby initialization timed out: " +
                    $"service={(autonomousService != null)}, waiterHands={(staffHands != null)}, " +
                    $"gameDayManager={(dayManager != null)}, cashierRegister={(register != null)}.");
            }
            return;
        }

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SuppressWritesForTests = true;

        EnsureRequiredTestStaff();

        if (!dayManager.ServiceActive)
            dayManager.StartShift();
        Assert(dayManager.ServiceActive,
            "The payment smoke test could not start service with its temporary staff roster.");
        Time.timeScale = 1f;

        // EditorApplication.timeSinceStartup advances independently of Play Mode's
        // Time.time in headless batch runs. Remove only this test's player-reaction
        // grace so the production ownership path is deterministic; normal gameplay
        // keeps LobbyAutonomousService's authored one-second grace.
        FieldInfo reactionGrace = typeof(LobbyAutonomousService).GetField(
            "managerReactionSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(reactionGrace != null,
            "Lobby autonomous service lost its player-reaction grace setting.");
        reactionGrace.SetValue(autonomousService, 0f);

        FieldInfo spawnLimit = typeof(GameDayManager).GetField(
            "maxCustomersToSpawn", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(spawnLimit != null,
            "GameDayManager lost its customer spawn limit used to isolate payment tests.");
        spawnLimit.SetValue(dayManager, 0);

        Booth[] booths = UnityEngine.Object.FindObjectsByType<Booth>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < booths.Length; i++)
        {
            if (booths[i] != null && booths[i].CurrentGroup == null &&
                booths[i].GetComponent<BoothMoneySpawner>() != null)
            {
                booth = booths[i];
                break;
            }
        }

        if (booth == null)
        {
            Fail("Lobby1 has no free booth with a BoothMoneySpawner.");
            return;
        }

        GameObject groupObject = new GameObject("PaymentSmokeTestGroup");
        group = groupObject.AddComponent<CustomerGroup>();
        group.transform.position = booth.tableLookTarget != null
            ? booth.tableLookTarget.position
            : booth.transform.position;
        group.state = CustomerGroup.GroupState.NeedsBill;
        group.assignedBooth = booth;
        group.currentOrderNumber = 999;
        group.currentOrder.Clear();
        group.currentOrder.lines.Add(new CustomerGroup.OrderLine
        {
            itemId = "payment-smoke-test-item",
            displayName = "Payment Smoke Test Item",
            quantity = 1,
            unitPrice = 100
        });
        booth.SetCurrentGroup(group);

        BoothMoneySpawner spawner = booth.GetComponent<BoothMoneySpawner>();
        payment = spawner.SpawnMoney(group, 100, booth.approachPoint);
        if (payment == null)
        {
            Fail("BoothMoneySpawner did not create a MoneyPickup.");
            return;
        }

        GameObject bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
        if (bubblePrefab == null)
        {
            Fail("Money bubble prefab was not found at " + BubblePrefabPath + ".");
            return;
        }

        bubbleObject = UnityEngine.Object.Instantiate(bubblePrefab);
        MoneyBubbleUI bubble = bubbleObject.GetComponentInChildren<MoneyBubbleUI>(true);
        if (bubble == null)
        {
            Fail("Money bubble prefab has no MoneyBubbleUI.");
            return;
        }

        bubble.Init(100, payment);
        Assert(bubbleObject.activeSelf, "An unclaimed payment bubble must be visible.");

        Button collectButton = bubbleObject.GetComponentInChildren<Button>(true);
        Assert(collectButton != null && collectButton.interactable,
            "The unclaimed payment button must be fully interactable.");

        Assert(RestaurantTaskClaim.TryClaimPlayer(payment),
            "The player could not claim an unowned payment.");
        payment.SetClaimedByStaff(true);
        Assert(!bubbleObject.activeSelf,
            "The bubble must hide immediately when the player owns the payment.");
        Assert(collectButton.interactable,
            "Claiming a payment must not use the Button disabled/fade state.");

        RestaurantTaskClaim.ReleasePlayer(payment);
        payment.SetClaimedByStaff(false);
        Assert(bubbleObject.activeSelf,
            "The bubble must return after the player genuinely cancels the payment task.");
        Assert(collectButton.interactable,
            "The returned bubble must remain fully interactable with no fade.");

        MethodInfo refreshPayments = typeof(LobbyAutonomousService).GetMethod(
            "RefreshSceneQueryCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(refreshPayments != null,
            "Lobby autonomous service lost its dynamic task-cache refresh.");
        refreshPayments.Invoke(autonomousService, new object[] { true });

        FieldInfo cachedPaymentsField = typeof(LobbyAutonomousService).GetField(
            "cachedPayments", BindingFlags.Instance | BindingFlags.NonPublic);
        MoneyPickup[] refreshedPayments = cachedPaymentsField?.GetValue(autonomousService) as MoneyPickup[];
        Assert(refreshedPayments != null && Array.Exists(refreshedPayments, entry => entry == payment),
            "The active synthetic payment was missing from the autonomous service cache " +
            $"(activeSelf={payment.gameObject.activeSelf}, activeInHierarchy={payment.gameObject.activeInHierarchy}, " +
            $"scene={payment.gameObject.scene.name}).");

        phase = Phase.WaitingForBotClaim;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        forcedWaiterPoll = false;
        Debug.Log("[LobbyPaymentSmokeTest] Player ownership passed; waiting for autonomous waiter claim.");
    }

    private static void EnsureAssignedTestEmployee(EmployeeRole role)
    {
        EmployeeManager employees = EmployeeManager.Instance;
        if (employees == null || employees.GetAssignedEmployee(role) != null)
            return;

        EmployeeData employee = new EmployeeData("Payment Smoke " + role, 3, role)
        {
            hired = true
        };
        employees.allEmployees.Add(employee);
        Assert(employees.AssignEmployeeForDay(employee),
            "The payment smoke test could not schedule a " + role + ".");
    }

    private static void EnsureRequiredTestStaff()
    {
        for (int i = 0; i < EmployeeRoleCatalog.LobbyRoles.Count; i++)
            EnsureAssignedTestEmployee(EmployeeRoleCatalog.LobbyRoles[i]);
        for (int i = 0; i < EmployeeRoleCatalog.KitchenRoles.Count; i++)
            EnsureAssignedTestEmployee(EmployeeRoleCatalog.KitchenRoles[i]);
    }

    private static void TrackBotClaim()
    {
        if (payment == null)
        {
            Fail("The payment disappeared before a waiter owned it.");
            return;
        }

        if (BeginCompletionIfBotClaimed())
            return;

        Assert(bubbleObject != null && bubbleObject.activeSelf,
            "The payment bubble disappeared while nobody owned the task.");

        // The editor's headless coroutine timing is not deterministic. Once the
        // same player-reaction grace used in production has elapsed, invoke the
        // service's real task poll once; all ownership and task code remains the
        // production path exercised by the test.
        if (!forcedWaiterPoll && ElapsedInPhase >= 0.25d)
        {
            forcedWaiterPoll = true;
            MethodInfo waiterPoll = typeof(LobbyAutonomousService).GetMethod(
                "TryStartWaiterTask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(waiterPoll != null,
                "Lobby autonomous service lost its waiter task poll.");
            waiterPoll.Invoke(autonomousService, null);

            // Headless Editor updates can be sparse enough that this invocation
            // and the timeout boundary occur in the same tick. Re-check the
            // ownership we just triggered before evaluating the timeout.
            if (BeginCompletionIfBotClaimed())
                return;
        }

        if (ElapsedInPhase > 8d)
        {
            FieldInfo waiterField = typeof(LobbyAutonomousService).GetField(
                "waiter", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo handsField = typeof(LobbyAutonomousService).GetField(
                "waiterHands", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo paymentsField = typeof(LobbyAutonomousService).GetField(
                "cachedPayments", BindingFlags.Instance | BindingFlags.NonPublic);
            AutonomousStaffBot waiterBot = waiterField?.GetValue(autonomousService) as AutonomousStaffBot;
            WaiterHands serviceHands = handsField?.GetValue(autonomousService) as WaiterHands;
            MoneyPickup[] cachedPayments = paymentsField?.GetValue(autonomousService) as MoneyPickup[];
            Fail(
                "The autonomous waiter did not claim the available payment within 8 seconds. " +
                $"serviceActive={GameDayManager.Instance?.ServiceActive}, " +
                $"waiter={(waiterBot != null)}, busy={waiterBot?.IsBusy}, state={waiterBot?.CurrentState}, " +
                $"handsFree={(serviceHands != null && !serviceHands.HasMoney && !serviceHands.HasBill && !serviceHands.HasTray)}, " +
                $"cachedPayments={cachedPayments?.Length ?? -1}, available={payment.IsAvailableForBotCollection}, " +
                $"playerClaim={RestaurantTaskClaim.IsClaimedByPlayer(payment)}, botClaim={RestaurantTaskClaim.IsClaimedByBot(payment)}.");
        }
    }

    private static bool BeginCompletionIfBotClaimed()
    {
        if (payment == null || !RestaurantTaskClaim.IsClaimedByBot(payment))
            return false;

        Assert(bubbleObject == null || !bubbleObject.activeSelf,
            "The payment bubble stayed visible after the waiter claimed it.");
        phase = Phase.WaitingForCompletion;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        Debug.Log("[LobbyPaymentSmokeTest] Waiter claimed payment; checking for flicker and cashier completion.");
        return true;
    }

    private static void TrackCompletion()
    {
        Assert(bubbleObject == null || !bubbleObject.activeSelf,
            "The bubble became visible again after the waiter owned the task.");

        bool groupCompleted = group == null || group.state == CustomerGroup.GroupState.Leaving;
        bool handsCleared = staffHands == null || !staffHands.HasMoney;
        if (groupCompleted && handsCleared)
        {
            Pass();
            return;
        }

        if (ElapsedInPhase > 35d)
            Fail("The waiter claimed the payment but did not finish it at the cashier within 35 seconds.");
    }

    private static double ElapsedInPhase => EditorApplication.timeSinceStartup - phaseStartedAt;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass()
    {
        const string message =
            "PASS: Lobby1 payment stayed visible while unowned, hid without fading for player/waiter ownership, " +
            "never reappeared during waiter handling, and completed at the cashier.";
        Debug.Log("[LobbyPaymentSmokeTest] " + message);
        Finish(message);
    }

    private static void Fail(string reason)
    {
        string message = "FAIL: " + reason;
        Debug.LogError("[LobbyPaymentSmokeTest] " + message);
        Finish(message);
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        phase = Phase.None;
        WriteResult(result);
        SessionState.SetBool(RunKey, false);

        if (Application.isBatchMode)
            EditorApplication.Exit(result.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
        else if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void WriteResult(string result)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string tempDirectory = Path.Combine(projectRoot, "Temp");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "LobbyPaymentSmokeTest.result"), result);
    }
}
