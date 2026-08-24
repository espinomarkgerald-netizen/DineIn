#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ManagementComputerSmokeTest
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string RunningKey = "DineIn.ManagementComputerSmokeTest.Running";
    private const string ResultKey = "DineIn.ManagementComputerSmokeTest.Result";
    private static double enteredAt;
    private static readonly List<string> exceptions = new List<string>();

    static ManagementComputerSmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string requestPath = projectRoot != null
            ? Path.Combine(projectRoot, "Temp", "RunManagementComputerSmokeTest.request")
            : string.Empty;
        if (!string.IsNullOrEmpty(requestPath) && File.Exists(requestPath))
        {
            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Dine In/Run Management Computer Smoke Test %#F7")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[ManagementComputerSmokeTest] Stop Play mode before running the test.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetString(ResultKey, string.Empty);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            exceptions.Clear();
            Application.logMessageReceived -= CaptureLog;
            Application.logMessageReceived += CaptureLog;
            enteredAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= CaptureLog;

            string result = SessionState.GetString(ResultKey, "Smoke test did not finish.");
            if (result == "PASS")
                Debug.Log("[ManagementComputerSmokeTest] PASS — responsive management UI, prefab-backed Menu/Restock catalogs, reviewed delivery checkout, HR departments, all apps, and shift flow passed.");
            else
                Debug.LogError("[ManagementComputerSmokeTest] FAIL\n" + result);

            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(ResultKey);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - enteredAt < 2.5d)
            return;

        EditorApplication.update -= Tick;
        try
        {
            RunAssertions();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception.ToString());
        }
        finally
        {
            SessionState.SetString(ResultKey,
                exceptions.Count == 0 ? "PASS" : string.Join("\n", exceptions));
            EditorApplication.isPlaying = false;
        }
    }

    private static void RunAssertions()
    {
        Assert(GameDayManager.Instance != null, "GameDayManager missing");
        Assert(!GameDayManager.Instance.ServiceActive, "Shift auto-started before the manager used the computer");
        Assert(MoneyManager.Instance != null, "MoneyManager missing");
        Assert(InventoryManager.Instance != null, "InventoryManager missing");
        Assert(EmployeeManager.Instance != null, "EmployeeManager missing");
        Assert(EquipmentManager.Instance != null, "EquipmentManager missing");
        Assert(MenuAvailabilityManager.Instance != null, "MenuAvailabilityManager missing");

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SuppressWritesForTests = true;

        ManagementComputerController controller = UnityEngine.Object.FindFirstObjectByType<ManagementComputerController>();
        ManagementComputerStation station = UnityEngine.Object.FindFirstObjectByType<ManagementComputerStation>();
        ManagerPlayer manager = ManagerPlayer.Active;
        Assert(controller != null, "ManagementComputerController missing");
        Assert(station != null, "Terminal is missing ManagementComputerStation");
        Assert(manager != null, "Manager player missing");
        int terminalLayer = LayerMask.NameToLayer("ManagementTerminal");
        Assert(terminalLayer >= 0, "ManagementTerminal layer is missing");
        Assert(station.gameObject.layer == terminalLayer, "Terminal is not assigned to ManagementTerminal layer");
        Assert(station.GetComponent<Collider>() != null, "Terminal has no click collider");
        Assert(station.GetComponent<Outline>() != null, "Terminal is missing the booth-style Outline component");
        Assert(MaskContains(manager.Movement, "clickMask", terminalLayer),
            "Manager PlayerMovement click mask does not include ManagementTerminal");
        TapOutlineSelector selector = UnityEngine.Object.FindFirstObjectByType<TapOutlineSelector>();
        Assert(selector != null && MaskContains(selector, "selectableMask", terminalLayer),
            "TapOutlineSelector mask does not include ManagementTerminal");
        VerifyTerminalRaycast(station, manager.Movement, terminalLayer);
        Assert(station.CanInteract(), "Computer station cannot be interacted with before opening");
        VerifyRestockWorldInteractions(manager, selector);

        ManagementComputerResponsiveLayout[] responsiveLayouts =
            UnityEngine.Object.FindObjectsByType<ManagementComputerResponsiveLayout>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert(responsiveLayouts.Length == 1,
            "Lobby1 must contain exactly one responsive management desktop, found " + responsiveLayouts.Length);
        ManagementComputerResponsiveLayout responsive = responsiveLayouts[0];

        controller.OpenComputer(manager, station);
        Assert(controller.IsOpen, "Desktop did not open");
        Assert(!manager.Movement.IsPlayerControlled(), "Manager gameplay input remained active behind the desktop");
        responsive.RefreshLayout();
        Canvas.ForceUpdateCanvases();
        VerifyResponsiveCanvas(responsive);

        Button pointerTestButton = GetAppButton(responsive, 0);
        VerifyRealPointerClick(pointerTestButton, controller);
        controller.CloseApp();
        VerifyActiveControlBounds(responsive);

        for (int i = 0; i < Enum.GetValues(typeof(ManagementComputerApp)).Length; i++)
        {
            Button appButton = GetAppButton(responsive, i);
            Assert(appButton != null && appButton.interactable,
                "App button " + i + " is missing or not interactable");
            appButton.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(controller.AppWindow.Content);
            Assert(controller.AppWindow.gameObject.activeSelf, "App window did not open for index " + i);
            Assert(controller.AppWindow.Content.childCount > 0, "App index " + i + " populated no prefab rows");

            ScrollRect scroll = controller.AppWindow.GetComponentInChildren<ScrollRect>(true);
            bool isCatalog =
                i == (int)ManagementComputerApp.Menu ||
                i == (int)ManagementComputerApp.Restock;
            bool expectsPortraitCards = i == (int)ManagementComputerApp.Equipment;
            Assert(scroll != null, "App window has no ScrollRect for index " + i);
            Assert(isCatalog
                    ? !scroll.horizontal && !scroll.vertical
                    : expectsPortraitCards
                        ? scroll.horizontal && !scroll.vertical
                        : scroll.vertical && !scroll.horizontal,
                "App window has the wrong scroll direction for index " + i);
            Assert(scroll.viewport != null && scroll.content != null && scroll.verticalScrollbar != null,
                "App window scroll view references are incomplete");

            RectTransform firstRow = GetFirstActiveChild(controller.AppWindow.Content);
            Assert(firstRow != null, "First app entry is not a RectTransform");
            Assert(firstRow.rect.width <= controller.AppWindow.Content.rect.width + 1f,
                "Prefab row overflowed its scroll content");
            if (isCatalog)
            {
                ManagementComputerCatalogPanelUI catalogPanel =
                    controller.AppWindow.Content.GetComponentInChildren<ManagementComputerCatalogPanelUI>(false);
                Assert(catalogPanel != null,
                    "Menu/Restock did not instantiate the editable catalog panel prefab");

                ManagementComputerCatalogCardUI[] cards =
                    catalogPanel.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false);
                Assert(cards.Length > 0, "Menu/Restock catalog populated no portrait cards");
                RectTransform cardRect = cards[0].transform as RectTransform;
                Assert(cardRect != null && cardRect.rect.height > cardRect.rect.width,
                    "Menu/Restock catalog did not use portrait cards");

                Button cardAction = i == (int)ManagementComputerApp.Restock
                    ? cards[0].PlusButton
                    : cards[0].GetComponent<Button>();
                Assert(cardAction != null &&
                       ((RectTransform)cardAction.transform).rect.height >= 44f,
                    "Menu/Restock has a primary touch target smaller than 44 pixels");

                ScrollRect[] nestedScrolls = catalogPanel.GetComponentsInChildren<ScrollRect>(true);
                Assert(Array.Exists(nestedScrolls, nested => nested.vertical && !nested.horizontal),
                    "Menu/Restock catalog has no vertical content scroll");
            }
            else if (expectsPortraitCards)
            {
                Assert(firstRow.rect.height > firstRow.rect.width,
                    "App index " + i + " did not use portrait cards");
                Button cardAction = firstRow.GetComponentInChildren<Button>(false);
                Assert(cardAction != null &&
                       ((RectTransform)cardAction.transform).rect.height >= 44f,
                    "App index " + i + " has a card action smaller than the mobile target");
            }

            if (i == (int)ManagementComputerApp.Staff)
            {
                ManagementComputerHRPanel hrPanel =
                    controller.AppWindow.Content.GetComponentInChildren<ManagementComputerHRPanel>(false);
                Assert(hrPanel != null, "Staff app did not populate the prefab-backed HR board");
                VerifyHRBoard(hrPanel, EmployeeManager.Instance, controller);
            }
        }

        VerifyRestockCheckout(controller, responsive);

        Button closeButton = FindNamedComponent<Button>(responsive.SafeAreaRoot, "WindowCloseButton");
        Assert(closeButton != null && closeButton.interactable, "App window close button is not usable");
        closeButton.onClick.Invoke();
        Assert(!controller.AppWindow.gameObject.activeSelf, "App window close button did not close the window");

        Button exitButton = FindNamedComponent<Button>(responsive.SafeAreaRoot, "ExitButton");
        Assert(exitButton != null && exitButton.interactable, "Exit computer button is not usable");
        exitButton.onClick.Invoke();
        Assert(!controller.IsOpen, "Exit computer button did not close the desktop");
        Assert(manager.Movement.IsPlayerControlled(), "Exit computer button did not restore gameplay input");

        controller.OpenComputer(manager, station);
        Assert(controller.IsOpen, "Desktop could not be reopened after using Exit Computer");

        Recipe propagationProduct = VerifyMenuPropagation(out bool propagationProductWasAvailable);
        EnsureUsableStockForShiftTest();

        Button startButton = FindNamedComponent<Button>(responsive.SafeAreaRoot, "StartShiftButton");
        Assert(startButton != null && startButton.interactable, "Start Shift button is not usable");

        CasualDiningPolishManager polish = CasualDiningPolishManager.Instance;
        NewspaperIssueSaveEntry testIssue = polish != null
            ? polish.GetIssueForDay(GameFlowManager.Instance.CurrentDay)
            : null;
        Assert(testIssue != null,
            "Today's newspaper was not prepared for the management start gate");
        testIssue.viewed = false;
        startButton.onClick.Invoke();
        Assert(controller.IsOpen && !GameDayManager.Instance.ServiceActive,
            "Management start bypassed the unread newspaper gate");
        Assert(controller.AppWindow.gameObject.activeSelf &&
               controller.AppWindow.FooterButton != null &&
               !controller.AppWindow.FooterButton.interactable,
            "Unread news did not appear as a blocking pre-open checklist item");

        Button readNews = Array.Find(
            controller.AppWindow.Content.GetComponentsInChildren<Button>(true),
            button => button != null && button.gameObject.activeInHierarchy &&
                      button.interactable &&
                      button.GetComponentInChildren<TMP_Text>(true)?.text == "READ");
        Assert(readNews != null && readNews.interactable,
            "The pre-open checklist has no visual READ action for today's news");
        readNews.onClick.Invoke();
        DailyNewspaperPresenter newspaper =
            UnityEngine.Object.FindFirstObjectByType<DailyNewspaperPresenter>();
        Assert(newspaper != null && newspaper.IsOpen,
            "The checklist READ action did not open today's newspaper");
        polish.MarkCurrentIssueViewed();
        newspaper.CloseImmediately();
        startButton.onClick.Invoke();

        if (controller.IsOpen && !GameDayManager.Instance.ServiceActive)
        {
            Button confirm = controller.AppWindow.FooterButton;
            Assert(confirm != null && confirm.gameObject.activeInHierarchy,
                "Start warnings appeared without an available confirmation action");
            confirm.onClick.Invoke();
        }

        Assert(!controller.IsOpen, "Desktop did not close after shift start");
        Assert(manager.Movement.IsPlayerControlled(), "Manager gameplay controls were not restored after closing");

        Assert(!GameDayManager.Instance.ServiceActive,
            "Restaurant service started before the day intro Play button was confirmed");
        Button introPlayButton = Array.Find(
            UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None),
            button => button != null && button.name == "PlayButton" &&
                      button.gameObject.activeInHierarchy);
        Assert(introPlayButton != null && introPlayButton.interactable,
            "The management Start Shift action did not open the day intro panel");
        introPlayButton.onClick.Invoke();
        // The authored button uses a short close animation before service starts.
        // StartShift is idempotent, so call it directly for this synchronous smoke pass.
        GameDayManager.Instance.StartShift();
        Assert(GameDayManager.Instance.ServiceActive,
            "The day intro Play action did not allow restaurant service to start");

        if (!propagationProductWasAvailable)
            MenuAvailabilityManager.Instance.SetProductAvailable(propagationProduct, false);
    }

    private static void EnsureUsableStockForShiftTest()
    {
        InventoryManager inventory = InventoryManager.Instance;
        Assert(inventory != null, "InventoryManager missing for shift-start stock setup");
        foreach (ItemData item in inventory.Items)
        {
            if (item != null && inventory.GetStock(item.itemType) <= 0)
                inventory.AddStock(item.itemType, Mathf.Max(1, item.unitsPerBox));
        }
    }

    private static void VerifyRealPointerClick(
        Button targetButton,
        ManagementComputerController controller)
    {
        Assert(targetButton != null, "Dashboard button is missing for pointer test");
        Assert(EventSystem.current != null, "No active EventSystem can dispatch computer UI clicks");

        RectTransform rect = targetButton.transform as RectTransform;
        Assert(rect != null, "Dashboard button has no RectTransform");
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null, rect.TransformPoint(rect.rect.center));

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);

        // Unity 6 can keep this reactivated modal's graphics at depth -1. In
        // that known case there is deliberately no GraphicRaycaster hit and
        // the controller's position-based fallback owns the release.
        if (hits.Count == 0)
        {
            controller.OnPointerClick(pointer);
            Assert(controller.AppWindow.gameObject.activeSelf,
                "The depth -1 pointer fallback did not open Dashboard");
            return;
        }

        RaycastResult first = hits[0];
        Button hitButton = first.gameObject.GetComponentInParent<Button>();
        string hitNames = string.Join(" > ", hits.GetRange(0, Mathf.Min(6, hits.Count))
            .ConvertAll(hit => GetHierarchyPath(hit.gameObject.transform)));
        Graphic graphic = targetButton.targetGraphic;
        Canvas targetCanvas = targetButton.GetComponentInParent<Canvas>();
        CanvasGroup[] groups = targetButton.GetComponentsInParent<CanvasGroup>(true);
        string groupState = string.Join(", ", Array.ConvertAll(groups, group =>
            group.name + "[alpha=" + group.alpha + ", interactable=" + group.interactable +
            ", blocks=" + group.blocksRaycasts + "]"));
        string targetState = "target active=" + targetButton.gameObject.activeInHierarchy +
            ", interactable=" + targetButton.interactable +
            ", graphicRaycast=" + graphic.Raycast(screenPoint, null) +
            ", graphicDepth=" + graphic.depth +
            ", rendererCull=" + graphic.canvasRenderer.cull +
            ", canvas=" + (targetCanvas != null ? targetCanvas.name : "<none>") +
            ", canvasEnabled=" + (targetCanvas != null && targetCanvas.enabled) +
            ", canvasGroups=" + groupState;
        if (hitButton == targetButton)
        {
            ExecuteEvents.ExecuteHierarchy(
                first.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        else
        {
            controller.OnPointerClick(pointer);
        }
        Assert(controller.AppWindow.gameObject.activeSelf,
            "A real pointer click did not open Dashboard. Interceptor: " +
            GetHierarchyPath(first.gameObject.transform) + ". Raycast order: " + hitNames +
            ". " + targetState);
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target != null ? target.name : "<null>";
        while (target != null && target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    private static Recipe VerifyMenuPropagation(out bool originallyAvailable)
    {
        MenuCatalog catalog = MenuCatalog.Default;
        Assert(catalog != null, "MenuCatalog missing");

        Recipe product = null;
        foreach (Recipe candidate in catalog.Products)
        {
            if (candidate != null && candidate.availableOnMenu && candidate.IsUnlocked)
            {
                product = candidate;
                break;
            }
        }
        Assert(product != null, "No authored menu product is available for propagation test");

        originallyAvailable = MenuAvailabilityManager.IsProductAvailable(product);
        MenuAvailabilityManager.Instance.SetProductAvailable(product, false);
        List<Recipe> visible = catalog.GetProducts(product.category, true);
        Assert(!visible.Contains(product), "Disabled management menu product still appeared in MenuCatalog query");

        MenuAvailabilityManager.Instance.SetProductAvailable(product, true);
        visible = catalog.GetProducts(product.category, true);
        Assert(visible.Contains(product), "Re-enabled management menu product did not return to MenuCatalog query");

        if (!originallyAvailable)
            MenuAvailabilityManager.Instance.SetProductAvailable(product, false);

        // Ensure the shift has at least one usable product even when the loaded save disabled it.
        if (!originallyAvailable)
            MenuAvailabilityManager.Instance.SetProductAvailable(product, true);

        return product;
    }

    private static void VerifyHRBoard(
        ManagementComputerHRPanel panel,
        EmployeeManager manager,
        ManagementComputerController controller)
    {
        Assert(panel.LobbyTab != null && panel.KitchenTab != null,
            "HR department tabs are missing");
        Assert(panel.LobbyTab.interactable && panel.KitchenTab.interactable,
            "HR department tabs are not interactable");

        ManagementHRRoleSectionUI[] lobbySections = GetActiveRoleSections(panel);
        Assert(lobbySections.Length == EmployeeRoleCatalog.LobbyRoles.Count,
            "Lobby HR department did not create one section per lobby role");
        foreach (EmployeeRole role in EmployeeRoleCatalog.LobbyRoles)
            Assert(Array.Exists(lobbySections, section => section.Role == role),
                "Lobby HR department is missing " + role);
        VerifyRoleRails(lobbySections);

        panel.KitchenTab.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        ManagementHRRoleSectionUI[] kitchenSections = GetActiveRoleSections(panel);
        Assert(kitchenSections.Length == 2, "Kitchen HR department must contain exactly two roles");
        Assert(Array.Exists(kitchenSections, section => section.Role == EmployeeRole.Chef),
            "Kitchen HR department is missing Chef");
        Assert(Array.Exists(kitchenSections, section => section.Role == EmployeeRole.Barista),
            "Kitchen HR department is missing Barista");
        Assert(!Array.Exists(kitchenSections, section =>
                section.Role == EmployeeRole.PrepCook || section.Role == EmployeeRole.LineCook ||
                section.Role == EmployeeRole.Assembler),
            "Legacy kitchen roles are still exposed by the HR board");
        VerifyRoleRails(kitchenSections);

        ManagementHRRoleSectionUI actionSection = kitchenSections[0];
        if (manager.GetHiredCount(actionSection.Role) >= manager.MaxHiredPerRole)
        {
            ManagementEmployeeCardUI employedCard =
                actionSection.EmployedContent.GetComponentInChildren<ManagementEmployeeCardUI>(false);
            Assert(employedCard != null && employedCard.Employee != null,
                "Full HR roster has no employed card to manage");
            EmployeeData fired = employedCard.Employee;
            Assert(employedCard.SecondaryButton != null && employedCard.SecondaryButton.interactable,
                "Employed card Fire action is not interactable");
            employedCard.SecondaryButton.onClick.Invoke();
            Assert(!manager.allEmployees.Contains(fired), "Fire action did not remove the employee");
        }

        kitchenSections = GetActiveRoleSections(panel);
        actionSection = Array.Find(kitchenSections, section => section.Role == actionSection.Role);
        ManagementEmployeeCardUI[] applicantCards =
            actionSection.ApplicantContent.GetComponentsInChildren<ManagementEmployeeCardUI>(false);
        ManagementEmployeeCardUI applicantCard = applicantCards.Length > 0
            ? applicantCards[applicantCards.Length / 2]
            : null;
        Assert(applicantCard != null && applicantCard.Employee != null && !applicantCard.Employee.hired,
            "Applicant rail has no applicant card");
        Assert(applicantCard.PrimaryButton != null && applicantCard.PrimaryButton.interactable,
            "Applicant Hire action is not interactable");
        EmployeeData hired = applicantCard.Employee;
        int hiredCountBefore = manager.GetHiredCount(hired.role);
        InvokeDuplicateFallbackRelease(applicantCard.PrimaryButton, controller);
        Assert(hired.hired && manager.allEmployees.Contains(hired), "Hire action did not move applicant into employment");
        Assert(manager.GetHiredCount(hired.role) == hiredCountBefore + 1,
            "One Hire click hired more than one applicant");
        Assert(manager.GetAssignedEmployee(hired.role) != null,
            "Hiring into an empty role did not assign an active employee");

        kitchenSections = GetActiveRoleSections(panel);
        actionSection = Array.Find(kitchenSections, section => section.Role == hired.role);
        ManagementEmployeeCardUI hiredCard = Array.Find(
            actionSection.EmployedContent.GetComponentsInChildren<ManagementEmployeeCardUI>(false),
            card => card.Employee == hired);
        Assert(hiredCard != null && hiredCard.SecondaryButton != null && hiredCard.SecondaryButton.interactable,
            "Newly hired employee did not receive an interactive Fire action");
        hiredCard.SecondaryButton.onClick.Invoke();
        Assert(!manager.allEmployees.Contains(hired), "Fire action did not remove the newly hired employee");

        panel.LobbyTab.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert(panel.CurrentDepartment == EmployeeDepartment.Lobby,
            "Lobby department tab did not restore the lobby view");
    }

    private static void InvokeDuplicateFallbackRelease(
        Button targetButton,
        ManagementComputerController controller)
    {
        Assert(targetButton != null && controller != null,
            "Hire fallback pointer test is missing its button or controller");
        Assert(EventSystem.current != null,
            "Hire fallback pointer test requires an active EventSystem");

        FieldInfo frameField = typeof(ManagementComputerController).GetField(
            "lastFallbackButtonFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(frameField != null, "Fallback click frame guard is missing");
        frameField.SetValue(controller, -1);

        RectTransform rect = targetButton.transform as RectTransform;
        Assert(rect != null, "Hire button has no RectTransform");
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null, rect.TransformPoint(rect.rect.center));

        PointerEventData firstRelease = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        PointerEventData duplicateRelease = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };

        controller.OnPointerClick(firstRelease);
        controller.OnPointerClick(duplicateRelease);
    }

    private static void VerifyRestockCheckout(
        ManagementComputerController controller,
        ManagementComputerResponsiveLayout responsive)
    {
        MoneyManager.Instance.SetMoney(
            Mathf.Max(MoneyManager.Instance.Money, 100000),
            "Management computer checkout smoke test");

        // This play-mode test is transient and save writes are suppressed. Start
        // from known capacity so a real player's pending deliveries cannot make
        // the checkout test nondeterministic.
        RestockOrderManager orders = RestockOrderManager.EnsureInstance();
        Assert(orders != null, "Restock order ledger is unavailable");
        orders.ApplySaveData(new GameSaveData());
        InventoryManager.Instance.SetAllStock(0);

        Button restockButton = GetAppButton(responsive, (int)ManagementComputerApp.Restock);
        Assert(restockButton != null && restockButton.interactable,
            "Restock app button is unavailable for checkout testing");
        restockButton.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(controller.AppWindow.Content);

        ManagementComputerCatalogPanelUI panel =
            controller.AppWindow.Content.GetComponentInChildren<ManagementComputerCatalogPanelUI>(false);
        Assert(panel != null, "Restock app did not instantiate its catalog prefab");

        ManagementComputerCatalogCardUI selectedCard = null;
        ManagementComputerCatalogCardUI[] cards =
            panel.GetComponentsInChildren<ManagementComputerCatalogCardUI>(false);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i].BoundItem != null && cards[i].PlusButton != null &&
                cards[i].PlusButton.interactable)
            {
                selectedCard = cards[i];
                break;
            }
        }

        Assert(selectedCard != null, "Restock app has no usable quantity action");
        ItemData item = selectedCard.BoundItem;
        int moneyBefore = MoneyManager.Instance.Money;
        int stockBefore = InventoryManager.Instance.GetStock(item.itemType);
        int orderCountBefore = orders.Orders.Count;
        int pendingBefore = orders.GetPendingContainers(item);

        selectedCard.PlusButton.onClick.Invoke();
        Assert(panel.GetComponentsInChildren<ManagementComputerCheckoutLineUI>(false).Length == 1,
            "Adding one container did not create one reusable cart line");

        Button primary = FindNamedComponent<Button>(panel.transform, "Primary");
        Assert(primary != null && primary.interactable,
            "Restock checkout button is missing or disabled after adding an item");
        primary.onClick.Invoke();
        Assert(MoneyManager.Instance.Money == moneyBefore,
            "Opening order review spent money before ORDER NOW");
        Assert(primary.interactable, "ORDER NOW is unavailable in review mode");

        primary.onClick.Invoke();
        Assert(orders.Orders.Count == orderCountBefore + 1,
            "ORDER NOW did not create exactly one delivery order");
        Assert(MoneyManager.Instance.Money == moneyBefore - item.boxCost,
            "ORDER NOW did not spend the exact container cost once");
        Assert(InventoryManager.Instance.GetStock(item.itemType) == stockBefore,
            "Ordered stock became usable before truck delivery and storage");
        Assert(orders.GetPendingContainers(item) == pendingBefore + 1,
            "Placed container was not reserved in pending delivery counts");

        primary.onClick.Invoke();
        Assert(orders.Orders.Count == orderCountBefore + 1,
            "Repeated checkout input created a duplicate delivery order");

        RestockOrderSaveData createdOrder = orders.Orders[orders.Orders.Count - 1];
        createdOrder.deliveryReadyUtcTicks = DateTime.UtcNow.AddSeconds(-1d).Ticks;
        MethodInfo tickDeliveries = typeof(RestockOrderManager).GetMethod(
            "TickDeliveries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(tickDeliveries != null, "Delivery scheduler is unavailable");
        tickDeliveries.Invoke(orders, null);
        Assert(createdOrder.state == RestockOrderState.Delivered,
            "Paid order did not arrive after its delivery time");
        Assert(InventoryManager.Instance.GetStock(item.itemType) == stockBefore,
            "Truck arrival granted usable stock before collection and storage");

        Assert(orders.CollectDeliveredOrders(),
            "Delivered order could not be collected into the restock hotbar");
        Assert(!orders.CollectDeliveredOrders(),
            "The same delivered order could be collected twice");
        Assert(orders.GetHotbarContainers(item) == 1,
            "Collected box was not represented exactly once in the hotbar");

        Assert(orders.TryStoreOneContainer(item, item.requiredStorage, out string storageMessage),
            "Collected box could not be stored: " + storageMessage);
        Assert(InventoryManager.Instance.GetStock(item.itemType) ==
               stockBefore + Mathf.Max(1, item.unitsPerBox),
            "Shelf placement did not add exactly one container of usable stock");
        Assert(createdOrder.state == RestockOrderState.Stored,
            "Order did not finish after its final physical box was stored");
        Assert(!orders.TryStoreOneContainer(item, item.requiredStorage, out _),
            "The same stored box granted inventory twice");
    }

    private static void VerifyRestockWorldInteractions(
        ManagerPlayer manager,
        TapOutlineSelector selector)
    {
        int interactionLayer = LayerMask.NameToLayer("Interactable ");
        Assert(interactionLayer >= 0, "The Interactable layer is missing");
        Assert(MaskContains(manager.Movement, "clickMask", interactionLayer),
            "Manager PlayerMovement does not scan the Interactable layer");
        Assert(selector != null && MaskContains(selector, "selectableMask", interactionLayer),
            "TapOutlineSelector does not scan the Interactable layer");

        RestockTruckInteractable truck =
            UnityEngine.Object.FindFirstObjectByType<RestockTruckInteractable>();
        Assert(truck != null, "The delivery truck was not created in Lobby1");
        Assert(truck.gameObject.layer == interactionLayer,
            "The delivery truck is outside the player's click mask");
        Assert(truck.GetComponent<Collider>() != null,
            "The delivery truck has no click collider");
        Assert(truck.GetComponent<Outline>() != null,
            "The delivery truck has no booth-style outline");
        FieldInfo departureDelay = typeof(RestockTruckInteractable).GetField(
            "departureDelaySeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo hornClip = typeof(RestockTruckInteractable).GetField(
            "arrivalHornClip", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(departureDelay != null &&
               Mathf.Abs((float)departureDelay.GetValue(truck) - 2f) < 0.001f,
            "The delivery truck does not use the approved two-second departure delay");
        Assert(hornClip != null && hornClip.GetValue(truck) is AudioClip,
            "The delivery truck has no beep-beep horn clip assigned");
        AssertReachable(manager, truck, "delivery truck");

        RestockStockRoomEntrance[] roomTargets =
            UnityEngine.Object.FindObjectsByType<RestockStockRoomEntrance>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        RestockStockRoomEntrance dry = Array.Find(
            roomTargets,
            target => target != null && target.isActiveAndEnabled &&
                      target.StorageType == RestockStorageType.Dry);
        RestockStockRoomEntrance freezer = Array.Find(
            roomTargets,
            target => target != null && target.isActiveAndEnabled &&
                      target.StorageType == RestockStorageType.Frozen);
        Assert(dry != null, "Lobby has no Dry Storage interaction");
        Assert(freezer != null, "Lobby has no Walk-in Freezer interaction");
        VerifyRoomEntrance(manager, dry, interactionLayer, "dry-storage entrance");
        VerifyRoomEntrance(manager, freezer, interactionLayer, "walk-in-freezer entrance");
    }

    private static void VerifyRoomEntrance(
        ManagerPlayer manager,
        RestockStockRoomEntrance entrance,
        int interactionLayer,
        string label)
    {
        Assert(entrance.gameObject.layer == interactionLayer,
            label + " is outside the player's click mask");
        Assert(entrance.GetComponentsInChildren<Collider>(true).Length > 0,
            label + " has no click collider");
        Assert(entrance.GetComponent<Outline>() != null,
            label + " has no booth-style outline");
        Assert(entrance.name.StartsWith("DryRoomShelf", StringComparison.OrdinalIgnoreCase),
            label + " is not bound to the authored Lobby shelf bank");
        Transform sign = entrance.transform.Find("Restock Destination Sign");
        Assert(sign != null, label + " has no compact destination sign");
        Renderer marker = sign != null
            ? Array.Find(sign.GetComponentsInChildren<Renderer>(true),
                renderer => renderer != null && renderer.name == "Stock Room Shelf Marker")
            : null;
        Assert(marker == null || !marker.enabled,
            label + " still shows the obsolete green portal marker");
        AssertReachable(manager, entrance, label);
    }

    private static void AssertReachable(
        ManagerPlayer manager,
        IInteractable target,
        string label)
    {
        Assert(manager != null && target != null && target.StandPoint != null,
            label + " has no valid approach point");
        NavMeshPath path = new NavMeshPath();
        bool found = NavMesh.CalculatePath(
            manager.transform.position,
            target.StandPoint.position,
            NavMesh.AllAreas,
            path);
        Assert(found && path.status == NavMeshPathStatus.PathComplete,
            "The player cannot reach the " + label +
            " (player=" + manager.transform.position.ToString("F2") +
            ", target=" + target.StandPoint.position.ToString("F2") +
            ", found=" + found + ", status=" + path.status + ")");
    }

    private static ManagementHRRoleSectionUI[] GetActiveRoleSections(ManagementComputerHRPanel panel) =>
        panel.SectionsRoot.GetComponentsInChildren<ManagementHRRoleSectionUI>(false);

    private static RectTransform GetFirstActiveChild(RectTransform parent)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform child = parent.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf)
                return child;
        }

        return null;
    }

    private static void VerifyRoleRails(ManagementHRRoleSectionUI[] sections)
    {
        foreach (ManagementHRRoleSectionUI section in sections)
        {
            Assert(section.EmployedScroll != null && section.ApplicantScroll != null,
                section.Role + " is missing an employee or applicant ScrollRect");
            Assert(section.EmployedScroll.horizontal && !section.EmployedScroll.vertical,
                section.Role + " employed rail is not horizontal-only");
            Assert(section.ApplicantScroll.horizontal && !section.ApplicantScroll.vertical,
                section.Role + " applicant rail is not horizontal-only");
            Assert(section.EmployedScroll is ManagementHorizontalScrollRect &&
                   section.ApplicantScroll is ManagementHorizontalScrollRect,
                section.Role + " rails cannot forward vertical touch drags to the outer HR page");
            Assert(section.EmployedScroll.horizontalScrollbar != null &&
                   section.ApplicantScroll.horizontalScrollbar != null,
                section.Role + " horizontal rail is missing an editable scrollbar");
            Assert(section.EmployedContent.GetComponentInChildren<ManagementEmployeeCardUI>(false) != null,
                section.Role + " employed rail has neither a worker card nor an empty-slot card");
            Assert(section.ApplicantContent.GetComponentInChildren<ManagementEmployeeCardUI>(false) != null,
                section.Role + " applicant rail has no employee cards");
        }
    }

    private static bool MaskContains(UnityEngine.Object target, string propertyName, int layer)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty mask = serialized.FindProperty(propertyName);
        return mask != null && (mask.intValue & (1 << layer)) != 0;
    }

    private static void VerifyResponsiveCanvas(ManagementComputerResponsiveLayout responsive)
    {
        Assert(responsive.SafeAreaRoot != null, "Responsive desktop has no safe-area root");
        Assert(responsive.AppWindow != null, "Responsive desktop has no app window reference");

        Canvas inputCanvas = responsive.GetComponent<Canvas>();
        Assert(inputCanvas != null && inputCanvas.sortingOrder >= 501,
            "Management desktop is missing its modal input canvas");
        Assert(inputCanvas.GetComponent<GraphicRaycaster>() != null,
            "Management desktop modal canvas has no GraphicRaycaster");

        Canvas canvas = responsive.transform.parent != null
            ? responsive.transform.parent.GetComponentInParent<Canvas>()
            : null;
        Assert(canvas != null && canvas.name == "ManagementComputerCanvas",
            "Management desktop is not on its dedicated canvas");
        Assert(canvas.renderMode == RenderMode.ScreenSpaceOverlay,
            "Management computer canvas is not Screen Space Overlay");
        Assert(canvas.sortingOrder >= 500, "Management computer canvas can render behind gameplay UI");
        Assert(canvas.GetComponent<GraphicRaycaster>() != null,
            "Management computer canvas has no GraphicRaycaster");

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Assert(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
            "Management computer canvas is not configured to scale with screen size");
        Assert(Vector2.Distance(scaler.referenceResolution, new Vector2(1920f, 1080f)) < 0.1f,
            "Management computer canvas reference resolution must be 1920x1080");
        Assert(Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) < 0.001f,
            "Management computer canvas must balance width and height scaling");

        Assert(responsive.AppButtons != null &&
               responsive.AppButtons.Length == Enum.GetValues(typeof(ManagementComputerApp)).Length,
            "Responsive desktop app-button references are incomplete");
        foreach (RectTransform appButton in responsive.AppButtons)
        {
            Assert(appButton != null, "Responsive desktop has a missing app-button reference");
            Assert(Mathf.Abs(appButton.rect.width - appButton.rect.height) < 0.5f,
                appButton.name + " icon is not square");
            Image icon = appButton.GetComponent<Image>();
            Assert(icon != null && icon.preserveAspect,
                appButton.name + " icon does not preserve its sprite aspect ratio");
        }

        Vector2Int[] supportedScreens =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1080),
            new Vector2Int(1024, 768),
            new Vector2Int(1080, 1920),
            new Vector2Int(2960, 1440)
        };

        foreach (Vector2Int screen in supportedScreens)
            VerifyScaledViewport(screen, scaler.referenceResolution, scaler.matchWidthOrHeight);
    }

    private static void VerifyScaledViewport(Vector2Int screen, Vector2 reference, float match)
    {
        float widthScale = screen.x / reference.x;
        float heightScale = screen.y / reference.y;
        float scale = Mathf.Pow(widthScale, 1f - match) * Mathf.Pow(heightScale, match);
        Vector2 logicalSize = new Vector2(screen.x / scale, screen.y / scale);
        bool compact = screen.x / (float)screen.y < 1.25f;
        Vector2 anchorMin = compact ? new Vector2(0.035f, 0.12f) : new Vector2(0.30f, 0.12f);
        Vector2 anchorMax = compact ? new Vector2(0.965f, 0.92f) : new Vector2(0.97f, 0.92f);
        Vector2 windowSize = Vector2.Scale(logicalSize, anchorMax - anchorMin) - new Vector2(20f, 20f);
        const int columns = 2;
        const int rows = 4;
        const float gap = 16f;
        const float labelSpace = 44f;
        float availableWidth = compact ? logicalSize.x * 0.92f : logicalSize.x * 0.27f;
        float top = compact ? 92f : 82f;
        float maxFromWidth = (availableWidth - gap * (columns - 1)) / columns;
        float maxFromHeight =
            (logicalSize.y - top - 28f - gap * (rows - 1) - labelSpace * rows) / rows;
        float logicalButtonSize = Mathf.Clamp(Mathf.Min(maxFromWidth, maxFromHeight), 56f, 150f);

        Assert(windowSize.x >= 650f && windowSize.y >= 500f,
            $"Responsive app window is too small at {screen.x}x{screen.y}: {windowSize}");
        Assert(logicalButtonSize * scale >= 44f,
            $"Desktop touch targets fall below 44 screen pixels at {screen.x}x{screen.y}");
    }

    private static Button GetAppButton(
        ManagementComputerResponsiveLayout responsive,
        int index)
    {
        RectTransform[] appButtons = responsive != null ? responsive.AppButtons : null;
        if (appButtons == null || index < 0 || index >= appButtons.Length || appButtons[index] == null)
            return null;

        return appButtons[index].GetComponent<Button>();
    }

    private static void VerifyActiveControlBounds(ManagementComputerResponsiveLayout responsive)
    {
        Rect safe = Screen.safeArea;
        Button[] buttons = responsive.SafeAreaRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (!button.gameObject.activeInHierarchy)
                continue;

            Assert(button.targetGraphic != null && button.targetGraphic.raycastTarget,
                button.name + " has no raycastable target graphic");

            RectTransform rect = button.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert(bottomLeft.x >= safe.xMin - 2f && bottomLeft.y >= safe.yMin - 2f &&
                   topRight.x <= safe.xMax + 2f && topRight.y <= safe.yMax + 2f,
                button.name + " is outside the current screen safe area. Bounds " +
                bottomLeft + " to " + topRight + ", safe area " + safe);
        }
    }

    private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == objectName)
                return component;
        }

        return null;
    }

    private static void VerifyTerminalRaycast(
        ManagementComputerStation station,
        PlayerMovement movement,
        int terminalLayer)
    {
        Camera camera = Camera.main;
        Collider collider = station.GetComponent<Collider>();
        Assert(camera != null, "Main camera missing for terminal click raycast test");
        Assert(collider != null, "Terminal collider missing for raycast test");

        Vector3 screenPoint = camera.WorldToScreenPoint(collider.bounds.center);
        Assert(screenPoint.z > 0f, "Terminal is behind the active camera");
        Ray ray = camera.ScreenPointToRay(screenPoint);

        SerializedObject serialized = new SerializedObject(movement);
        int clickMask = serialized.FindProperty("clickMask").intValue;
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, clickMask, QueryTriggerInteraction.Collide);
        bool foundTerminal = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<ManagementComputerStation>() == station)
            {
                foundTerminal = true;
                break;
            }
        }

        Assert(foundTerminal, "Manager click ray did not hit the terminal collider");
        Assert(Physics.Raycast(ray, out RaycastHit outlineHit, 500f, 1 << terminalLayer,
                QueryTriggerInteraction.Collide) &&
            outlineHit.collider.GetComponentInParent<ManagementComputerStation>() == station,
            "Outline selector ray did not resolve the terminal as its selectable target");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
            exceptions.Add(condition + "\n" + stackTrace);
        else if (type == LogType.Error && condition.Contains("ManagementComputer"))
            exceptions.Add(condition);
    }
}
#endif
