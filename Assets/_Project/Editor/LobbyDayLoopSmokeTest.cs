#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LobbyDayLoopSmokeTest
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string RunKey = "DineIn.LobbyDayLoopSmokeTest.Running";
    private const string ResultPath = "Temp/LobbyDayLoopSmokeTest.result";

    private enum Phase
    {
        None,
        WaitingForLobby,
        WaitingForShift,
        WaitingForResults,
        WaitingForNextDay
    }

    private static Phase phase;
    private static double phaseStartedAt;
    private static int startingDay;

    static LobbyDayLoopSmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Dine In/Run Lobby1 Day Loop Smoke Test %#F8")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[LobbyDayLoopSmokeTest] Stop Play mode before running the test.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunKey, true);
        WriteResult("RUNNING");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunKey, false))
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
            SessionState.SetBool(RunKey, false);
        }
    }

    private static void Tick()
    {
        try
        {
            switch (phase)
            {
                case Phase.WaitingForLobby:
                    if (Elapsed >= 2.5d)
                        ValidateOpeningAndStart();
                    break;

                case Phase.WaitingForShift:
                    if (Elapsed >= 0.6d)
                        ValidateShiftAndEnd();
                    break;

                case Phase.WaitingForResults:
                    if (Elapsed >= 1.6d)
                        ValidateResultsAndContinue();
                    break;

                case Phase.WaitingForNextDay:
                    if (Elapsed >= 2.2d)
                        ValidateNextDay();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static void ValidateOpeningAndStart()
    {
        Assert(SceneManager.GetActiveScene().name == "Lobby1", "Lobby1 is not active.");
        Assert(GameDayManager.Instance != null, "GameDayManager is missing.");
        Assert(GameFlowManager.Instance != null && GameFlowManager.Instance.UsesSingleRestaurantFlow,
            "The single-scene restaurant game loop is not active.");
        Assert(InventoryManager.Instance != null, "InventoryManager is missing.");
        Assert(LobbyStockBridge.Instance != null, "LobbyStockBridge is missing.");

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SuppressWritesForTests = true;

        MoneyManager.Instance?.SetMoney(6000, "Day loop smoke test");
        AlienApprovalManager.Instance?.ResetApproval();
        GameFlowManager.Instance.TrySetCurrentDayDebug(1);
        startingDay = GameFlowManager.Instance.CurrentDay;

        ManagementComputerController controller =
            UnityEngine.Object.FindFirstObjectByType<ManagementComputerController>();
        Assert(controller != null, "Management computer controller is missing.");
        Assert(controller.gameObject.name == "ManagementComputerCanvas",
            "Management computer controller is not scene-local, so next-day UI would be stale.");

        Assert(GameDayManager.Instance.FormattedGameTime == "10:00 AM",
            "The pre-opening clock did not begin at 10:00 AM.");
        Assert(Mathf.Approximately(GameDayManager.Instance.ShiftLengthSeconds, 480f),
            "The 10 AM-6 PM day is not eight real minutes long.");

        ValidateFiniteStock();
        ValidateButtonAnimations();

        GameDayManager.Instance.ShowShiftIntro();
        GameObject introPanel = GetField<GameObject>(GameDayManager.Instance, "dayIntroPanel");
        Assert(GetPresentationRoot(introPanel).activeInHierarchy,
            "The day intro wrapper did not become visible.");

        Button playButton = GetField<Button>(GameDayManager.Instance, "playButton");
        Assert(playButton != null && playButton.gameObject.activeInHierarchy && playButton.interactable,
            "The day intro Play button is not usable.");
        playButton.onClick.Invoke();
        SetPhase(Phase.WaitingForShift);
    }

    private static void ValidateFiniteStock()
    {
        Recipe product = MenuCatalog.Default != null
            ? MenuCatalog.Default.FindByKitchenItem(ItemTypeKitchen.Fries)
            : null;
        Assert(product != null && product.ingredients != null && product.ingredients.Count > 0,
            "No ingredient-backed Fries product was found for the stock test.");

        MenuAvailabilityManager.Instance?.SetProductAvailable(product, true);
        InventoryManager inventory = InventoryManager.Instance;
        inventory.ResetStock();

        RecipeIngredient ingredient = product.ingredients.FirstOrDefault(entry =>
            entry != null && entry.item != null && entry.amount > 0 &&
            inventory.IsTracked(entry.item.itemType));
        Assert(ingredient != null, "The Fries product has no tracked inventory ingredient.");
        inventory.AddStock(ingredient.item.itemType, ingredient.amount * 2);

        LobbyStockBridge bridge = LobbyStockBridge.Instance;
        bool reachedZero = false;
        Action<Recipe, int> listener = (changedProduct, stock) =>
        {
            if (changedProduct == product && stock == 0)
                reachedZero = true;
        };
        bridge.OnProductStockChanged += listener;

        Assert(bridge.GetProductStock(product) == 2,
            "Product availability did not reflect two stocked servings.");
        Assert(bridge.TryUseProductStock(product), "The first stocked serving could not be sold.");
        Assert(bridge.GetProductStock(product) == 1,
            "Selling one serving did not decrement product availability from 2 to 1.");
        Assert(bridge.TryUseProductStock(product), "The final stocked serving could not be sold.");
        Assert(bridge.GetProductStock(product) == 0 && !bridge.HasProductStock(product),
            "The product did not become unavailable at zero stock.");
        Assert(reachedZero, "The product stock UI event did not report zero stock.");
        bridge.OnProductStockChanged -= listener;

        WarningSlideUI warning = WarningSlideUI.Instance;
        Assert(warning != null, "The out-of-stock warning UI is missing.");
        TMP_Text warningText = warning.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text.text.IndexOf("out of stock", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert(warningText != null, "Reaching zero stock did not notify the player.");
    }

    private static void ValidateButtonAnimations()
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert(buttons.Length > 0, "Lobby1 contains no UI buttons.");
        int missing = buttons.Count(button =>
            button != null && button.GetComponent<ButtonAnimator>() == null);
        Assert(missing == 0,
            missing + " Lobby1 buttons are missing the shared ButtonAnimator.");
    }

    private static void ValidateShiftAndEnd()
    {
        Assert(GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive,
            "The intro Play button did not start restaurant service.");
        Assert(GameDayManager.Instance.FormattedGameTime.StartsWith("10:"),
            "The service clock did not start in the 10 AM hour.");

        GameDayManager.Instance.RegisterHappyCustomer();
        GameDayManager.Instance.RegisterHappyCustomer();
        GameDayManager.Instance.RegisterHappyCustomer();
        GameDayManager.Instance.EndShift();
        SetPhase(Phase.WaitingForResults);
    }

    private static void ValidateResultsAndContinue()
    {
        Assert(GameDayManager.Instance != null, "GameDayManager disappeared before results.");
        GameObject resultsPanel = GetField<GameObject>(GameDayManager.Instance, "resultsPanel");
        Assert(GetPresentationRoot(resultsPanel).activeInHierarchy,
            "The daily results wrapper did not become visible.");

        Image[] stars =
        {
            GetField<Image>(GameDayManager.Instance, "star1"),
            GetField<Image>(GameDayManager.Instance, "star2"),
            GetField<Image>(GameDayManager.Instance, "star3")
        };
        for (int i = 0; i < stars.Length; i++)
        {
            Assert(stars[i] != null && stars[i].gameObject.activeInHierarchy,
                "Earned results star " + (i + 1) + " is not visible.");
            string path = AssetDatabase.GetAssetPath(stars[i].sprite).Replace('\\', '/');
            Assert(path.EndsWith("/Yellow/Default/star.png", StringComparison.OrdinalIgnoreCase),
                "Results star " + (i + 1) + " is not using the yellow star asset.");
            Assert(stars[i].transform.localScale.x > 0.95f,
                "Results star " + (i + 1) + " did not finish its entrance animation.");
        }

        Button nextDayButton = GetField<Button>(GameDayManager.Instance, "resultsActionButton");
        Assert(nextDayButton != null && nextDayButton.gameObject.activeInHierarchy && nextDayButton.interactable,
            "The results Start Next Day button is not usable.");
        // Mark the phase first because SceneManager.LoadScene can pump an editor
        // update synchronously while the button callback is still on the stack.
        SetPhase(Phase.WaitingForNextDay);
        nextDayButton.onClick.Invoke();
    }

    private static void ValidateNextDay()
    {
        Assert(SceneManager.GetActiveScene().name == "Lobby1", "Next day did not reload Lobby1.");
        Assert(GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentDay == startingDay + 1,
            "The results action did not advance to the next day.");
        Assert(GameDayManager.Instance != null && !GameDayManager.Instance.ServiceActive,
            "The next day started service before management/intro confirmation.");
        Assert(GameDayManager.Instance.FormattedGameTime == "10:00 AM",
            "The next day clock did not reset to 10:00 AM.");

        ManagementComputerController controller =
            UnityEngine.Object.FindFirstObjectByType<ManagementComputerController>();
        Assert(controller != null && controller.gameObject.name == "ManagementComputerCanvas",
            "The next-day management computer did not reconnect to the reloaded Lobby1 UI.");
        Assert(UnityEngine.Object.FindObjectsByType<LobbyStockBridge>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "The next-day reload created duplicate finite-stock bridges.");

        ValidateButtonAnimations();
        Pass();
    }

    private static T GetField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Missing test field: " + fieldName + ".");
        return field.GetValue(target) as T;
    }

    private static GameObject GetPresentationRoot(GameObject panel)
    {
        Assert(panel != null, "Panel reference is missing.");
        Transform current = panel.transform;
        while (current.parent != null && current.parent.GetComponent<Canvas>() == null)
            current = current.parent;
        return current.gameObject;
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - phaseStartedAt;

    private static void SetPhase(Phase next)
    {
        phase = next;
        phaseStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass()
    {
        Finish("PASS: finite stock decrement and zero warning, shared button animations, " +
               "10 AM-6 PM clock, intro start, animated yellow-star results, and next-day Lobby1 reload passed.", false);
    }

    private static void Fail(string reason)
    {
        Finish("FAIL: " + reason, true);
    }

    private static void Finish(string result, bool failed)
    {
        EditorApplication.update -= Tick;
        SetPhase(Phase.None);
        WriteResult(result);
        if (failed)
            Debug.LogError("[LobbyDayLoopSmokeTest] " + result);
        else
            Debug.Log("[LobbyDayLoopSmokeTest] " + result);

        SessionState.SetBool(RunKey, false);
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void WriteResult(string result)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string path = Path.Combine(projectRoot, ResultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
        File.WriteAllText(path, result);
    }
}
#endif
