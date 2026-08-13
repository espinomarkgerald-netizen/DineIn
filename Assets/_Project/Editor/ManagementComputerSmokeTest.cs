#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
                Debug.Log("[ManagementComputerSmokeTest] PASS — responsive management UI, HR departments, horizontal role rails, employee cards, Hire/Fire actions, all apps, and shift flow passed.");
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

        ManagementComputerResponsiveLayout[] responsiveLayouts =
            UnityEngine.Object.FindObjectsByType<ManagementComputerResponsiveLayout>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        Assert(responsiveLayouts.Length == 1,
            "Lobby1 must contain exactly one responsive management desktop, found " + responsiveLayouts.Length);
        ManagementComputerResponsiveLayout responsive = responsiveLayouts[0];
        VerifyResponsiveCanvas(responsive);

        controller.OpenComputer(manager, station);
        Assert(controller.IsOpen, "Desktop did not open");
        Assert(!manager.Movement.IsPlayerControlled(), "Manager gameplay input remained active behind the desktop");
        responsive.RefreshLayout();
        Canvas.ForceUpdateCanvases();

        Button pointerTestButton = FindNamedComponent<Button>(
            responsive.SafeAreaRoot, "AppButton_0");
        VerifyRealPointerClick(pointerTestButton, controller);
        controller.CloseApp();
        VerifyActiveControlBounds(responsive);

        for (int i = 0; i < Enum.GetValues(typeof(ManagementComputerApp)).Length; i++)
        {
            Button appButton = FindNamedComponent<Button>(responsive.SafeAreaRoot, "AppButton_" + i);
            Assert(appButton != null && appButton.interactable,
                "App button " + i + " is missing or not interactable");
            appButton.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(controller.AppWindow.Content);
            Assert(controller.AppWindow.gameObject.activeSelf, "App window did not open for index " + i);
            Assert(controller.AppWindow.Content.childCount > 0, "App index " + i + " populated no prefab rows");

            ScrollRect scroll = controller.AppWindow.GetComponentInChildren<ScrollRect>(true);
            Assert(scroll != null && scroll.vertical && !scroll.horizontal,
                "App window does not have a usable vertical scroll view");
            Assert(scroll.viewport != null && scroll.content != null && scroll.verticalScrollbar != null,
                "App window scroll view references are incomplete");

            RectTransform firstRow = controller.AppWindow.Content.GetChild(0) as RectTransform;
            Assert(firstRow != null, "First app entry is not a RectTransform");
            Assert(firstRow.rect.width <= controller.AppWindow.Content.rect.width + 1f,
                "Prefab row overflowed its scroll content");

            if (i == (int)ManagementComputerApp.Staff)
            {
                ManagementComputerHRPanel hrPanel =
                    controller.AppWindow.Content.GetComponentInChildren<ManagementComputerHRPanel>(false);
                Assert(hrPanel != null, "Staff app did not populate the prefab-backed HR board");
                VerifyHRBoard(hrPanel, EmployeeManager.Instance);
            }
        }

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
        startButton.onClick.Invoke();

        if (!GameDayManager.Instance.ServiceActive)
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
        Assert(hits.Count > 0,
            "The computer canvas returned no UI raycast at the Dashboard button");

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

    private static void VerifyHRBoard(ManagementComputerHRPanel panel, EmployeeManager manager)
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
        ManagementEmployeeCardUI applicantCard =
            actionSection.ApplicantContent.GetComponentInChildren<ManagementEmployeeCardUI>(false);
        Assert(applicantCard != null && applicantCard.Employee != null && !applicantCard.Employee.hired,
            "Applicant rail has no applicant card");
        Assert(applicantCard.PrimaryButton != null && applicantCard.PrimaryButton.interactable,
            "Applicant Hire action is not interactable");
        EmployeeData hired = applicantCard.Employee;
        applicantCard.PrimaryButton.onClick.Invoke();
        Assert(hired.hired && manager.allEmployees.Contains(hired), "Hire action did not move applicant into employment");
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

    private static ManagementHRRoleSectionUI[] GetActiveRoleSections(ManagementComputerHRPanel panel) =>
        panel.SectionsRoot.GetComponentsInChildren<ManagementHRRoleSectionUI>(false);

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
        float logicalButtonHeight = Mathf.Clamp(logicalSize.y * 0.08f, 68f, 88f);

        Assert(windowSize.x >= 650f && windowSize.y >= 500f,
            $"Responsive app window is too small at {screen.x}x{screen.y}: {windowSize}");
        Assert(logicalButtonHeight * scale >= 44f,
            $"Desktop touch targets fall below 44 screen pixels at {screen.x}x{screen.y}");
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
