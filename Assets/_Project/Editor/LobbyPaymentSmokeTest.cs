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
        {
            Debug.LogError("[LobbyPaymentSmokeTest] Open Lobby1 before running the payment test.");
            WriteResult("FAIL: Lobby1 was not open.");
            return;
        }

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
        LobbyAutonomousService service = UnityEngine.Object.FindFirstObjectByType<LobbyAutonomousService>();
        GameDayManager dayManager = GameDayManager.Instance;
        staffHands = WaiterHands.Instance;

        CashierRegisterUI register = CashierRegisterUI.Instance != null
            ? CashierRegisterUI.Instance
            : UnityEngine.Object.FindFirstObjectByType<CashierRegisterUI>(FindObjectsInactive.Include);

        if (service == null || dayManager == null || staffHands == null || register == null)
        {
            if (ElapsedInPhase > 12d)
            {
                Fail(
                    "Lobby initialization timed out: " +
                    $"service={(service != null)}, waiterHands={(staffHands != null)}, " +
                    $"gameDayManager={(dayManager != null)}, cashierRegister={(register != null)}.");
            }
            return;
        }

        if (!dayManager.ServiceActive)
            dayManager.StartShift();

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

        phase = Phase.WaitingForBotClaim;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        Debug.Log("[LobbyPaymentSmokeTest] Player ownership passed; waiting for autonomous waiter claim.");
    }

    private static void TrackBotClaim()
    {
        if (payment == null)
        {
            Fail("The payment disappeared before a waiter owned it.");
            return;
        }

        if (RestaurantTaskClaim.IsClaimedByBot(payment))
        {
            Assert(bubbleObject == null || !bubbleObject.activeSelf,
                "The payment bubble stayed visible after the waiter claimed it.");
            phase = Phase.WaitingForCompletion;
            phaseStartedAt = EditorApplication.timeSinceStartup;
            Debug.Log("[LobbyPaymentSmokeTest] Waiter claimed payment; checking for flicker and cashier completion.");
            return;
        }

        Assert(bubbleObject != null && bubbleObject.activeSelf,
            "The payment bubble disappeared while nobody owned the task.");

        if (ElapsedInPhase > 8d)
            Fail("The autonomous waiter did not claim the available payment within 8 seconds.");
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

        if (EditorApplication.isPlaying)
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
