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
        Assert(AlienApprovalManager.Instance != null && AlienApprovalManager.Instance.Approval == 30,
            "A new restaurant run did not start at the configured 30% approval.");
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
        ValidateResponsiveResultsLayout(false);

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
            Assert(stars[i].transform.localScale.sqrMagnitude > 0.0001f,
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
        ValidateNotepadCoverage();
        ValidateDynamicGameOverResults();
        Pass();
    }

    private static void ValidateNotepadCoverage()
    {
        OrderChecklistUI notepad = UnityEngine.Object.FindFirstObjectByType<OrderChecklistUI>(
            FindObjectsInactive.Include);
        Assert(notepad != null, "Order notepad is missing from Lobby1.");

        RectTransform root = notepad.transform as RectTransform;
        RectTransform parent = root != null ? root.parent as RectTransform : null;
        Assert(root != null && parent != null, "Order notepad has no responsive parent rectangle.");
        Assert(root.anchorMin == Vector2.zero && root.anchorMax == Vector2.one,
            "Order notepad root is not stretched to all four canvas edges.");

        Vector2 renderedSize = new Vector2(
            root.rect.width * Mathf.Abs(root.localScale.x),
            root.rect.height * Mathf.Abs(root.localScale.y));
        Assert(renderedSize.x + 0.5f >= parent.rect.width &&
               renderedSize.y + 0.5f >= parent.rect.height,
            "Order notepad background does not cover its full parent canvas.");
    }

    private static void ValidateDynamicGameOverResults()
    {
        Assert(GameDayManager.Instance != null && GameFlowManager.Instance != null,
            "Managers disappeared before the dynamic game-over panel check.");

        DevSettingsConsole console = UnityEngine.Object.FindFirstObjectByType<DevSettingsConsole>(
            FindObjectsInactive.Include);
        Assert(console != null, "Developer console is missing from Lobby1.");
        Assert(console.TryExecuteCode("help()") && console.TryExecuteCode("status()"),
            "Argument-free developer console commands did not parse.");

        Assert(console.TryExecuteCode("fillStocks(3)"),
            "fillStocks(value) developer command failed.");
        ItemData trackedItem = InventoryManager.Instance != null && InventoryManager.Instance.Items != null
            ? InventoryManager.Instance.Items.Find(item => item != null && InventoryManager.Instance.IsTracked(item.itemType))
            : null;
        Assert(trackedItem != null && InventoryManager.Instance.GetStock(trackedItem.itemType) == 3,
            "fillStocks(value) did not update tracked inventory.");
        Assert(console.TryExecuteCode("zeroStocks()") &&
               InventoryManager.Instance.GetStock(trackedItem.itemType) == 0,
            "zeroStocks() did not empty tracked inventory.");
        Assert(console.TryExecuteCode("fillStocks(1)"),
            "Could not restore smoke-test stock after zeroStocks().");

        MoneyManager.Instance?.SetMoney(6000, "Dynamic game-over smoke test");
        Assert(console.TryExecuteCode("gameOver()"),
            "gameOver() developer command did not open the results outcome.");

        Assert(GameFlowManager.Instance.TryGetRestaurantDayOutcome(out GameOverReason reason) &&
               reason == GameOverReason.ApprovalCollapsed,
            "Approval collapse was not recognized as the final restaurant outcome.");

        TMP_Text title = GetField<TMP_Text>(GameDayManager.Instance, "resultsTitleText");
        TMP_Text restartLabel = GetField<TMP_Text>(GameDayManager.Instance, "resultsActionButtonText");
        TMP_Text recoveryCopy = GetField<TMP_Text>(GameDayManager.Instance, "resultsCashText");
        Button paidContinue = GetField<Button>(GameDayManager.Instance, "resultsContinueButton");
        TMP_Text paidContinueLabel = GetField<TMP_Text>(GameDayManager.Instance, "resultsContinueButtonText");

        Assert(title != null && title.text == "Game Over",
            "The Day Report did not transform into the Game Over presentation.");
        Assert(restartLabel != null && restartLabel.text == "Restart Day 1",
            "The free game-over action does not clearly restart at Day 1.");
        Assert(recoveryCopy != null && recoveryCopy.text.Contains("Your restaurant itself stays"),
            "The game-over copy does not explain what the free restart preserves.");
        Assert(paidContinue != null && paidContinue.gameObject.activeInHierarchy &&
               paidContinueLabel != null && paidContinueLabel.text.Contains("500"),
            "The 500 GC continue option is not visible on the transformed panel.");
        ValidateResponsiveResultsLayout(true);

        Assert(console.TryExecuteCode("reputation(30)"),
            "reputation(value) developer command failed.");
    }

    private static void ValidateResponsiveResultsLayout(bool expectPaidContinue)
    {
        GameDayManager manager = GameDayManager.Instance;
        Assert(manager != null, "GameDayManager is missing during results layout validation.");

        GameObject resultsPanel = GetField<GameObject>(manager, "resultsPanel");
        TMP_Text title = GetField<TMP_Text>(manager, "resultsTitleText");
        TMP_Text status = GetField<TMP_Text>(manager, "resultsStarsText");
        TMP_Text summary = GetField<TMP_Text>(manager, "resultsSummaryText");
        TMP_Text customers = GetField<TMP_Text>(manager, "resultsCustomersText");
        TMP_Text cash = GetField<TMP_Text>(manager, "resultsCashText");
        Button action = GetField<Button>(manager, "resultsActionButton");
        Button paidContinue = GetField<Button>(manager, "resultsContinueButton");
        Image firstStar = GetField<Image>(manager, "star1");

        Assert(resultsPanel != null && title != null && status != null &&
               summary != null && customers != null && cash != null &&
               action != null && firstStar != null,
            "The responsive results screen is missing a required visual region.");

        Canvas.ForceUpdateCanvases();
        TMP_Text[] texts = { title, status, summary, customers, cash,
            action.GetComponentInChildren<TMP_Text>(true) };
        foreach (TMP_Text text in texts)
        {
            Assert(text != null && text.enableAutoSizing,
                "Results text is not configured to resize inside its region.");
            text.ForceMeshUpdate(true, true);
            Assert(!text.isTextOverflowing,
                "Results text still overflows its reserved region: " + text.gameObject.name + ".");
        }

        RectTransform presentation = GetPresentationRoot(resultsPanel).transform as RectTransform;
        RectTransform background = title.rectTransform.parent as RectTransform;
        Assert(presentation != null && background != null,
            "The results screen has no measurable responsive panel.");
        Assert(background.rect.width >= presentation.rect.width * 0.84f &&
               background.rect.height >= presentation.rect.height * 0.84f,
            "The Results/Game Over panel is not using the enlarged safe-area layout.");

        RectTransform starsRoot = firstStar.rectTransform.parent as RectTransform;
        RectTransform columnsRoot = summary.rectTransform.parent as RectTransform;
        AssertSeparated(title.rectTransform, status.rectTransform,
            "The results title overlaps the status text.");
        AssertSeparated(status.rectTransform, starsRoot,
            "The results status overlaps the star row.");
        AssertSeparated(starsRoot, columnsRoot,
            "The results stars overlap the report columns.");
        AssertSeparated(columnsRoot, action.transform as RectTransform,
            "The report columns overlap the primary results action.");
        AssertSeparated(summary.rectTransform, customers.rectTransform,
            "The revenue and customer report text regions overlap.");
        AssertSeparated(customers.rectTransform, cash.rectTransform,
            "The customer and cash report text regions overlap.");

        if (expectPaidContinue)
        {
            Assert(paidContinue != null && paidContinue.gameObject.activeInHierarchy,
                "The paid continue action is missing during responsive game-over validation.");
            TMP_Text paidLabel = paidContinue.GetComponentInChildren<TMP_Text>(true);
            Assert(paidLabel != null && paidLabel.enableAutoSizing,
                "The paid continue label does not resize dynamically.");
            paidLabel.ForceMeshUpdate(true, true);
            Assert(!paidLabel.isTextOverflowing,
                "The paid continue label overflows its button.");
            AssertSeparated(action.transform as RectTransform,
                paidContinue.transform as RectTransform,
                "The two game-over actions overlap.");
            AssertSeparated(columnsRoot, paidContinue.transform as RectTransform,
                "The game-over explanation overlaps the paid continue action.");
        }
    }

    private static void AssertSeparated(RectTransform first, RectTransform second, string message)
    {
        Assert(first != null && second != null, message + " A region is missing.");
        Assert(!GetScreenRect(first).Overlaps(GetScreenRect(second)), message);
    }

    private static Rect GetScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
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
               "10 AM-6 PM clock, full-screen notepad, enlarged non-overlapping responsive results, " +
               "next-day reload, dynamic game-over copy, " +
               "the 500 GC recovery option, and the new developer commands passed.", false);
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
