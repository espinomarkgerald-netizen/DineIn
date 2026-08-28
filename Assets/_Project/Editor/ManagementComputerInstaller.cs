#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ManagementComputerInstaller
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string PrefabFolder = "Assets/_Project/ManagementComputer/Prefabs";
    private const string RowPrefabPath = PrefabFolder + "/ManagementComputerRow.prefab";
    private const string WindowPrefabPath = PrefabFolder + "/ManagementComputerAppWindow.prefab";
    private const string DesktopPrefabPath = PrefabFolder + "/ManagementComputerDesktop.prefab";
    private const string EmployeeCardPrefabPath = PrefabFolder + "/ManagementEmployeeCard.prefab";
    private const string HRRoleSectionPrefabPath = PrefabFolder + "/ManagementHRRoleSection.prefab";
    private const string HRPanelPrefabPath = PrefabFolder + "/ManagementHRPanel.prefab";
    private const string TerminalLayerName = "ManagementTerminal";

    private static readonly Color DesktopBlue = new Color(0.035f, 0.16f, 0.29f, 1f);
    private static readonly Color PanelBlue = new Color(0.055f, 0.25f, 0.43f, 1f);
    private static readonly Color AccentBlue = new Color(0.08f, 0.55f, 0.88f, 1f);
    private static readonly Color LightPanel = new Color(0.94f, 0.97f, 1f, 1f);
    private static readonly Color DarkText = new Color(0.08f, 0.14f, 0.22f, 1f);

    private static TMP_FontAsset gameFont;
    private static Sprite uiSprite;

    [MenuItem("Tools/Dine In/Install Management Computer in Lobby1 %#F6")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[ManagementComputerInstaller] Exit Play mode before installing.");
            return;
        }

        EnsureFolder("Assets/_Project/ManagementComputer");
        EnsureFolder(PrefabFolder);
        gameFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset");
        if (gameFont == null)
            gameFont = TMP_Settings.defaultFontAsset;
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        ManagementComputerRowUI rowPrefab = EnsureRowPrefab();
        ManagementComputerWindow windowPrefab = EnsureWindowPrefab();
        ManagementEmployeeCardUI employeeCardPrefab = EnsureEmployeeCardPrefab();
        ManagementHRRoleSectionUI roleSectionPrefab = EnsureHRRoleSectionPrefab();
        ManagementComputerHRPanel hrPanelPrefab = EnsureHRPanelPrefab(roleSectionPrefab, employeeCardPrefab);
        GameObject desktopPrefab = EnsureDesktopPrefab(windowPrefab.gameObject);
        int terminalLayer = EnsureLayer(TerminalLayerName);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("ManagementSystems");
        if (systems == null)
        {
            systems = new GameObject("ManagementSystems");
            Undo.RegisterCreatedObjectUndo(systems, "Create management systems");
        }

        ConfigureManagers(systems);

        DestroyAllSceneObjectsNamed(scene, "ManagementComputerDesktop");
        Canvas targetCanvas = EnsureManagementCanvas(scene);

        GameObject desktop = (GameObject)PrefabUtility.InstantiatePrefab(desktopPrefab, targetCanvas.transform);
        desktop.name = "ManagementComputerDesktop";
        Stretch((RectTransform)desktop.transform);

        ManagementComputerController legacyController = systems.GetComponent<ManagementComputerController>();
        if (legacyController != null)
            Undo.DestroyObjectImmediate(legacyController);

        // The controller owns scene UI references, so it belongs on the
        // scene-local canvas rather than the persistent data-manager object.
        ManagementComputerController controller = targetCanvas.GetComponent<ManagementComputerController>();
        if (controller == null)
            controller = Undo.AddComponent<ManagementComputerController>(targetCanvas.gameObject);

        ConfigureController(controller, desktop, rowPrefab, hrPanelPrefab);
        ConfigureStation(controller, terminalLayer);
        ConfigureLobbyClickMasks(terminalLayer);

        GameDayManager dayManager = UnityEngine.Object.FindFirstObjectByType<GameDayManager>();
        if (dayManager != null)
            ConfigureDayLoop(dayManager);

        desktop.SetActive(false);
        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = GameObject.Find("Terminal");
        Debug.Log("[ManagementComputerInstaller] Installed computer interaction, management systems, and editable prefabs in Lobby1.");
    }

    private static void ConfigureManagers(GameObject systems)
    {
        MoneyManager money = EnsureComponent<MoneyManager>(systems);
        SerializedObject moneySerialized = new SerializedObject(money);
        moneySerialized.FindProperty("startingMoney").intValue = 5000;
        moneySerialized.ApplyModifiedPropertiesWithoutUndo();

        UnlockManager unlocks = EnsureComponent<UnlockManager>(systems);
        InventoryManager inventory = EnsureComponent<InventoryManager>(systems);
        List<ItemData> items = LoadAllAssets<ItemData>()
            .OrderBy(item => item.dayToUnlock)
            .ThenBy(item => item.displayName)
            .ToList();
        inventory.ConfigureItems(items);

        EmployeeGenerator generator = EnsureComponent<EmployeeGenerator>(systems);
        EmployeeManager employees = EnsureComponent<EmployeeManager>(systems);
        employees.generator = generator;
        employees.salaryConfig = AssetDatabase.LoadAssetAtPath<SalaryConfig>("Assets/_Project/Office/HR/SalaryConfig.asset");

        EquipmentManager equipment = EnsureComponent<EquipmentManager>(systems);
        equipment.Configure(LoadAllAssets<Equipment>()
            .OrderBy(item => item.dayToUnlock)
            .ThenBy(item => item.displayName)
            .ToList());

        EnsureComponent<FinanceManager>(systems);
        EnsureComponent<DailyFinanceBridge>(systems);
        EnsureComponent<DailyRevenueTracker>(systems);
        DailyObjectiveManager objectives = EnsureComponent<DailyObjectiveManager>(systems);
        objectives.EnsureDefaultObjectives();
        EnsureComponent<AlienApprovalManager>(systems);
        EnsureComponent<MenuAvailabilityManager>(systems);
        EnsureComponent<LobbyStockBridge>(systems);
        EnsureComponent<CoreManagersBridge>(systems);
        EnsureComponent<GameSaveManager>(systems);
        EnsureComponent<ShiftScaler>(systems);

        EditorUtility.SetDirty(unlocks);
        EditorUtility.SetDirty(inventory);
        EditorUtility.SetDirty(employees);
        EditorUtility.SetDirty(equipment);
        EditorUtility.SetDirty(objectives);
    }

    private static void ConfigureDayLoop(GameDayManager dayManager)
    {
        LobbyToManagement[] legacyResultsHandlers =
            UnityEngine.Object.FindObjectsByType<LobbyToManagement>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < legacyResultsHandlers.Length; i++)
            Undo.DestroyObjectImmediate(legacyResultsHandlers[i]);

        WarningSlideUI warning = UnityEngine.Object.FindFirstObjectByType<WarningSlideUI>(
            FindObjectsInactive.Include);
        if (warning != null)
        {
            // WarningSlideUI must be active during Awake so its singleton can
            // receive clock, stock, and service notifications.
            warning.gameObject.SetActive(true);
            EditorUtility.SetDirty(warning.gameObject);
        }

        SerializedObject serialized = new SerializedObject(dayManager);
        serialized.FindProperty("useManagementComputerForDayStart").boolValue = true;
        serialized.FindProperty("openingHour").intValue = 10;
        serialized.FindProperty("closingHour").intValue = 18;
        serialized.FindProperty("realSecondsPerGameHour").floatValue = 60f;
        serialized.FindProperty("maxClosingGraceSeconds").floatValue = 120f;

        GameObject introPanel = serialized.FindProperty("dayIntroPanel").objectReferenceValue as GameObject;
        if (introPanel != null)
        {
            Transform taskTransform = FindChild(introPanel.transform, "TodaysTask");
            TMP_Text taskText = taskTransform != null ? taskTransform.GetComponent<TMP_Text>() : null;
            serialized.FindProperty("dayIntroSummaryLeftText").objectReferenceValue = taskText;
            serialized.FindProperty("dayIntroSummaryMiddleText").objectReferenceValue = null;
            serialized.FindProperty("dayIntroSummaryRightText").objectReferenceValue = null;

            if (taskText != null)
            {
                RectTransform taskRect = taskText.rectTransform;
                taskRect.anchoredPosition = new Vector2(0f, 4f);
                taskRect.sizeDelta = new Vector2(430f, 170f);
                taskText.fontSize = 20f;
                taskText.alignment = TextAlignmentOptions.Center;
                taskText.textWrappingMode = TextWrappingModes.Normal;
                EditorUtility.SetDirty(taskText);
            }

            if (introPanel.transform.parent != null)
                introPanel.transform.parent.gameObject.SetActive(false);
        }

        GameObject resultsPanel = serialized.FindProperty("resultsPanel").objectReferenceValue as GameObject;
        if (resultsPanel != null)
        {
            Transform summaryRoot = FindChild(resultsPanel.transform, "Summary");
            TMP_Text[] columns = summaryRoot != null
                ? summaryRoot.GetComponentsInChildren<TMP_Text>(true)
                : Array.Empty<TMP_Text>();
            if (columns.Length > 0)
                serialized.FindProperty("resultsSummaryText").objectReferenceValue = columns[0];
            if (columns.Length > 1)
                serialized.FindProperty("resultsCustomersText").objectReferenceValue = columns[1];
            if (columns.Length > 2)
                serialized.FindProperty("resultsCashText").objectReferenceValue = columns[2];

            for (int i = 0; i < columns.Length; i++)
            {
                columns[i].fontSize = 17f;
                columns[i].alignment = TextAlignmentOptions.TopLeft;
                columns[i].textWrappingMode = TextWrappingModes.Normal;
                EditorUtility.SetDirty(columns[i]);
            }

            if (resultsPanel.transform.parent != null)
                resultsPanel.transform.parent.gameObject.SetActive(false);
        }

        Sprite yellowStar = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Yellow/Default/star.png");
        string[] starProperties = { "star1", "star2", "star3" };
        for (int i = 0; i < starProperties.Length; i++)
        {
            Image star = serialized.FindProperty(starProperties[i]).objectReferenceValue as Image;
            if (star == null)
                continue;

            star.sprite = yellowStar;
            star.preserveAspect = true;
            star.raycastTarget = false;
            EditorUtility.SetDirty(star);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dayManager);
    }

    private static void ConfigureController(
        ManagementComputerController controller,
        GameObject desktop,
        ManagementComputerRowUI rowPrefab,
        ManagementComputerHRPanel hrPanelPrefab)
    {
        Button[] apps = new Button[Enum.GetValues(typeof(ManagementComputerApp)).Length];
        for (int i = 0; i < apps.Length; i++)
            apps[i] = FindChild(desktop.transform, "AppButton_" + i).GetComponent<Button>();

        ManagementComputerWindow window = desktop.GetComponentInChildren<ManagementComputerWindow>(true);
        controller.ConfigureReferences(
            desktop,
            apps,
            FindChild(desktop.transform, "StartShiftButton").GetComponent<Button>(),
            FindChild(desktop.transform, "StartShiftLabel").GetComponent<TMP_Text>(),
            FindChild(desktop.transform, "ExitButton").GetComponent<Button>(),
            FindChild(desktop.transform, "DayStatus").GetComponent<TMP_Text>(),
            FindChild(desktop.transform, "MoneyStatus").GetComponent<TMP_Text>(),
            FindChild(desktop.transform, "ApprovalStatus").GetComponent<TMP_Text>(),
            FindChild(desktop.transform, "ClockStatus").GetComponent<TMP_Text>(),
            FindChild(desktop.transform, "DesktopHint").GetComponent<TMP_Text>(),
            window,
            rowPrefab,
            hrPanelPrefab);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureStation(ManagementComputerController controller, int terminalLayer)
    {
        GameObject terminal = GameObject.Find("Terminal");
        if (terminal == null)
            throw new InvalidOperationException("The Lobby1 Terminal model was not found.");

        terminal.layer = terminalLayer;

        BoxCollider collider = terminal.GetComponent<BoxCollider>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(terminal);

        ManagementComputerStation station = terminal.GetComponent<ManagementComputerStation>();
        if (station == null)
            station = Undo.AddComponent<ManagementComputerStation>(terminal);

        Outline outline = terminal.GetComponent<Outline>();
        if (outline == null)
            outline = Undo.AddComponent<Outline>(terminal);
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 4f;
        outline.enabled = false;

        GameObject standPointObject = GameObject.Find("ManagementComputerStandPoint");
        if (standPointObject == null)
        {
            standPointObject = new GameObject("ManagementComputerStandPoint");
            Undo.RegisterCreatedObjectUndo(standPointObject, "Create computer stand point");
        }

        Renderer renderer = terminal.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : terminal.transform.position;
        Vector3 away = Vector3.ProjectOnPlane(-terminal.transform.forward, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.1f)
            away = Vector3.back;
        standPointObject.transform.position = new Vector3(center.x, 0f, center.z) + away * 1.35f;
        standPointObject.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);

        station.Configure(controller, standPointObject.transform);
        EditorUtility.SetDirty(station);
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(outline);
        EditorUtility.SetDirty(terminal);
    }

    private static void ConfigureLobbyClickMasks(int terminalLayer)
    {
        int terminalBit = 1 << terminalLayer;

        ManagerPlayer manager = UnityEngine.Object.FindObjectsByType<ManagerPlayer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        PlayerMovement playerMovement = manager != null ? manager.GetComponent<PlayerMovement>() : null;
        if (manager == null || playerMovement == null)
            throw new InvalidOperationException("Lobby1 ManagerPlayer or PlayerMovement was not found.");

        SerializedObject movement = new SerializedObject(playerMovement);
        SerializedProperty clickMask = movement.FindProperty("clickMask");
        clickMask.intValue |= terminalBit;
        movement.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerMovement);

        TapOutlineSelector selector = UnityEngine.Object.FindObjectsByType<TapOutlineSelector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (selector == null)
            throw new InvalidOperationException("Lobby1 TapOutlineSelector was not found.");

        SerializedObject outlineSelector = new SerializedObject(selector);
        SerializedProperty selectableMask = outlineSelector.FindProperty("selectableMask");
        selectableMask.intValue |= terminalBit;
        outlineSelector.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(selector);
    }

    private static ManagementComputerRowUI EnsureRowPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (existing != null)
        {
            UpgradeResponsiveRowPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath).GetComponent<ManagementComputerRowUI>();
        }

        GameObject root = CreateUIObject("ManagementComputerRow", null);
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(0f, 94f);
        Image background = root.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 94f;
        layout.minHeight = 82f;
        layout.flexibleWidth = 1f;

        Image icon = CreateImage("Icon", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(14f, 12f), new Vector2(82f, -12f), Color.white);
        icon.preserveAspect = true;

        TMP_Text title = CreateText("Title", root.transform, 25f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.48f), new Vector2(1f, 1f),
            new Vector2(94f, 0f), new Vector2(-310f, -4f));
        TMP_Text details = CreateText("Details", root.transform, 17f, FontStyles.Normal,
            new Color(0.3f, 0.38f, 0.48f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Vector2(94f, 5f), new Vector2(-310f, 0f));
        TMP_Text value = CreateText("Value", root.transform, 21f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.Center, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-300f, 9f), new Vector2(-142f, -9f));
        Button action = CreateButton("ActionButton", root.transform, "ACTION", AccentBlue,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-74f, 0f), new Vector2(124f, 54f));
        TMP_Text actionLabel = action.GetComponentInChildren<TMP_Text>();
        actionLabel.gameObject.name = "ActionLabel";

        ManagementComputerRowUI row = root.AddComponent<ManagementComputerRowUI>();
        row.ConfigureReferences(icon, title, details, value, action, actionLabel);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        UpgradeResponsiveRowPrefab();
        return AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath).GetComponent<ManagementComputerRowUI>();
    }

    private static ManagementEmployeeCardUI EnsureEmployeeCardPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EmployeeCardPrefabPath);
        if (existing != null)
            return existing.GetComponent<ManagementEmployeeCardUI>();

        GameObject root = CreateUIObject("ManagementEmployeeCard", null);
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(304f, 270f);
        Image background = root.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = 304f;
        layout.minWidth = 280f;
        layout.preferredHeight = 270f;
        layout.minHeight = 250f;

        Image accent = CreateImage("RoleAccent", root.transform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -10f), Vector2.zero, AccentBlue);
        Image avatar = CreateImage("Avatar", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -91f), new Vector2(88f, -17f), new Color(0.60f, 0.84f, 0.96f));
        TMP_Text initial = CreateText("AvatarInitial", avatar.transform, 42f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text name = CreateText("EmployeeName", root.transform, 26f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(100f, -57f), new Vector2(-10f, -16f));
        TMP_Text role = CreateText("Role", root.transform, 14f, FontStyles.Normal,
            new Color(0.22f, 0.38f, 0.52f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), Vector2.one, new Vector2(100f, -86f), new Vector2(-10f, -56f));
        TMP_Text status = CreateText("EmploymentStatus", root.transform, 13f, FontStyles.Normal, AccentBlue,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(100f, -110f), new Vector2(-10f, -84f));
        TMP_Text stars = CreateText("Stars", root.transform, 20f, FontStyles.Normal,
            new Color(0.96f, 0.66f, 0.08f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -119f), new Vector2(-14f, -95f));
        TMP_Text stats = CreateText("Stats", root.transform, 14f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(14f, -164f), new Vector2(-14f, -119f));
        stats.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text pro = CreateText("Pro", root.transform, 14f, FontStyles.Normal,
            new Color(0.08f, 0.56f, 0.28f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -184f), new Vector2(-14f, -164f));
        TMP_Text con = CreateText("Con", root.transform, 14f, FontStyles.Normal,
            new Color(0.78f, 0.20f, 0.20f), TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -204f), new Vector2(-14f, -184f));
        TMP_Text salary = CreateText("Salary", root.transform, 15f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(14f, 43f), new Vector2(-14f, 67f));

        Button secondary = CreateButton("SecondaryButton", root.transform, "FIRE", new Color(0.78f, 0.20f, 0.20f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(78f, 24f), new Vector2(132f, 42f));
        TMP_Text secondaryLabel = secondary.GetComponentInChildren<TMP_Text>();
        secondaryLabel.gameObject.name = "SecondaryLabel";
        secondaryLabel.fontSize = 16f;
        Button primary = CreateButton("PrimaryButton", root.transform, "HIRE", new Color(0.10f, 0.62f, 0.30f),
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-78f, 24f), new Vector2(132f, 42f));
        TMP_Text primaryLabel = primary.GetComponentInChildren<TMP_Text>();
        primaryLabel.gameObject.name = "PrimaryLabel";
        primaryLabel.fontSize = 16f;

        ManagementEmployeeCardUI card = root.AddComponent<ManagementEmployeeCardUI>();
        card.ConfigureReferences(accent, avatar, initial, name, role, stars, stats, pro, con, salary, status,
            primary, primaryLabel, secondary, secondaryLabel);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EmployeeCardPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<ManagementEmployeeCardUI>();
    }

    private static ManagementHRRoleSectionUI EnsureHRRoleSectionPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HRRoleSectionPrefabPath);
        if (existing != null)
            return existing.GetComponent<ManagementHRRoleSectionUI>();

        GameObject root = CreateUIObject("ManagementHRRoleSection", null);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(0f, 760f);
        Image background = root.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.88f, 0.93f, 0.98f, 1f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 760f;
        layout.minHeight = 730f;
        layout.flexibleWidth = 1f;

        TMP_Text title = CreateText("RoleTitle", root.transform, 28f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(18f, -52f), new Vector2(-300f, -12f));
        TMP_Text summary = CreateText("RoleSummary", root.transform, 16f, FontStyles.Normal,
            new Color(0.28f, 0.39f, 0.50f), TextAlignmentOptions.MidlineRight,
            new Vector2(0f, 1f), Vector2.one, new Vector2(300f, -50f), new Vector2(-18f, -14f));
        CreateText("EmployedLabel", root.transform, 18f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(18f, -82f), new Vector2(-18f, -54f)).text = "YOUR EMPLOYEES";

        CreateHorizontalCardRail("EmployedScroll", root.transform,
            new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -414f), new Vector2(-14f, -84f),
            out ScrollRect employedScroll, out RectTransform employedContent);

        CreateText("ApplicantsLabel", root.transform, 18f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one,
            new Vector2(18f, -446f), new Vector2(-18f, -418f)).text = "APPLICANTS";

        CreateHorizontalCardRail("ApplicantScroll", root.transform,
            new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -754f), new Vector2(-14f, -448f),
            out ScrollRect applicantScroll, out RectTransform applicantContent);

        ManagementHRRoleSectionUI section = root.AddComponent<ManagementHRRoleSectionUI>();
        section.ConfigureReferences(title, summary, employedContent, applicantContent, employedScroll, applicantScroll);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HRRoleSectionPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<ManagementHRRoleSectionUI>();
    }

    private static ManagementComputerHRPanel EnsureHRPanelPrefab(
        ManagementHRRoleSectionUI sectionPrefab,
        ManagementEmployeeCardUI cardPrefab)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HRPanelPrefabPath);
        if (existing != null)
            return existing.GetComponent<ManagementComputerHRPanel>();

        GameObject root = CreateUIObject("ManagementHRPanel", null);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(0f, 3208f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 3208f;
        layout.flexibleWidth = 1f;

        Button lobbyTab = CreateButton("LobbyDepartmentTab", root.transform, "LOBBY", AccentBlue,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -38f), new Vector2(216f, 60f));
        TMP_Text lobbyLabel = lobbyTab.GetComponentInChildren<TMP_Text>();
        lobbyLabel.gameObject.name = "LobbyTabLabel";
        Button kitchenTab = CreateButton("KitchenDepartmentTab", root.transform, "KITCHEN", PanelBlue,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(350f, -38f), new Vector2(216f, 60f));
        TMP_Text kitchenLabel = kitchenTab.GetComponentInChildren<TMP_Text>();
        kitchenLabel.gameObject.name = "KitchenTabLabel";
        TMP_Text departmentTitle = CreateText("DepartmentTitle", root.transform, 28f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineRight, new Vector2(0f, 1f), Vector2.one,
            new Vector2(470f, -48f), new Vector2(-18f, -12f));
        TMP_Text description = CreateText("DepartmentDescription", root.transform, 15f, FontStyles.Normal,
            new Color(0.28f, 0.39f, 0.50f), TextAlignmentOptions.MidlineRight,
            new Vector2(0f, 1f), Vector2.one, new Vector2(470f, -75f), new Vector2(-18f, -48f));

        GameObject sectionsObject = CreateUIObject("RoleSections", root.transform);
        RectTransform sections = (RectTransform)sectionsObject.transform;
        sections.anchorMin = new Vector2(0f, 1f);
        sections.anchorMax = Vector2.one;
        sections.pivot = new Vector2(0.5f, 1f);
        sections.anchoredPosition = new Vector2(0f, -92f);
        sections.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vertical = sectionsObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.spacing = 14f;
        vertical.childControlWidth = true;
        vertical.childForceExpandWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter fitter = sectionsObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ManagementComputerHRPanel panel = root.AddComponent<ManagementComputerHRPanel>();
        panel.ConfigureReferences(departmentTitle, description, lobbyTab, lobbyLabel, kitchenTab, kitchenLabel,
            sections, sectionPrefab, cardPrefab);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HRPanelPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<ManagementComputerHRPanel>();
    }

    private static void CreateHorizontalCardRail(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        out ScrollRect scrollRect,
        out RectTransform content)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.offsetMin = offsetMin;
        rootRect.offsetMax = offsetMax;
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.78f, 0.86f, 0.93f, 1f);
        scrollRect = root.AddComponent<ManagementHorizontalScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.scrollSensitivity = 34f;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        GameObject viewportObject = CreateUIObject("Viewport", root.transform);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        Stretch(viewport);
        viewport.offsetMin = new Vector2(4f, 22f);
        viewport.offsetMax = new Vector2(-4f, -4f);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewport.transform);
        content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup horizontal = contentObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(8, 8, 4, 4);
        horizontal.spacing = 10f;
        horizontal.childControlWidth = false;
        horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject scrollbarObject = CreateUIObject("HorizontalScrollbar", root.transform);
        RectTransform scrollbarRect = (RectTransform)scrollbarObject.transform;
        scrollbarRect.anchorMin = Vector2.zero;
        scrollbarRect.anchorMax = new Vector2(1f, 0f);
        scrollbarRect.offsetMin = new Vector2(6f, 3f);
        scrollbarRect.offsetMax = new Vector2(-6f, 19f);
        Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
        scrollbarBackground.color = new Color(0.58f, 0.67f, 0.76f, 1f);

        GameObject slidingObject = CreateUIObject("SlidingArea", scrollbarObject.transform);
        Stretch((RectTransform)slidingObject.transform);
        GameObject handleObject = CreateUIObject("Handle", slidingObject.transform);
        Stretch((RectTransform)handleObject.transform);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.sprite = uiSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = AccentBlue;
        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = (RectTransform)handleObject.transform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontalScrollbar = scrollbar;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    private static ManagementComputerWindow EnsureWindowPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (existing != null)
        {
            UpgradeResponsiveWindowPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath).GetComponent<ManagementComputerWindow>();
        }

        GameObject root = CreateUIObject("ManagementComputerAppWindow", null);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1080f, 700f);
        Image background = root.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = LightPanel;

        Image titleBar = CreateImage("TitleBar", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -64f), Vector2.zero, PanelBlue);
        TMP_Text title = CreateText("WindowTitle", titleBar.transform, 30f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-85f, 0f));
        Button close = CreateButton("WindowCloseButton", titleBar.transform, "×", new Color(0.78f, 0.18f, 0.2f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-35f, 0f), new Vector2(54f, 46f));

        GameObject scrollObject = CreateUIObject("ScrollView", root.transform);
        RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(18f, 94f);
        scrollRectTransform.offsetMax = new Vector2(-18f, -78f);
        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.86f, 0.91f, 0.97f, 1f);
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();

        GameObject viewport = CreateUIObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect);
        viewportRect.offsetMax = new Vector2(-24f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewport.transform);
        RectTransform content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(8f, 0f);
        content.offsetMax = new Vector2(-8f, 0f);
        VerticalLayoutGroup vertical = contentObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(8, 8, 8, 8);
        vertical.spacing = 8f;
        vertical.childControlHeight = false;
        vertical.childControlWidth = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(scrollObject.transform);
        scrollRect.viewport = viewportRect;
        scrollRect.content = content;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 28f;

        TMP_Text message = CreateText("WindowMessage", root.transform, 18f, FontStyles.Normal, DarkText,
            TextAlignmentOptions.MidlineLeft, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(22f, 12f), new Vector2(-230f, 84f));
        message.textWrappingMode = TextWrappingModes.Normal;
        Button footer = CreateButton("WindowFooterButton", root.transform, "CONTINUE", AccentBlue,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-116f, 46f), new Vector2(200f, 58f));
        TMP_Text footerLabel = footer.GetComponentInChildren<TMP_Text>();
        footerLabel.gameObject.name = "WindowFooterLabel";

        ManagementComputerWindow window = root.AddComponent<ManagementComputerWindow>();
        window.ConfigureReferences(title, close, scrollRect, content, message, footer, footerLabel);
        footer.gameObject.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        UpgradeResponsiveWindowPrefab();
        return AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath).GetComponent<ManagementComputerWindow>();
    }

    private static void UpgradeResponsiveRowPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RowPrefabPath);
        try
        {
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 104f);

            LayoutElement layout = root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = 104f;
                layout.minHeight = 92f;
                layout.flexibleWidth = 1f;
            }

            RectTransform action = FindChild(root.transform, "ActionButton").GetComponent<RectTransform>();
            action.sizeDelta = new Vector2(150f, 68f);
            action.anchoredPosition = new Vector2(-88f, 0f);

            RectTransform value = FindChild(root.transform, "Value").GetComponent<RectTransform>();
            value.offsetMin = new Vector2(-330f, 9f);
            value.offsetMax = new Vector2(-172f, -9f);

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeResponsiveWindowPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WindowPrefabPath);
        try
        {
            RectTransform titleBar = FindChild(root.transform, "TitleBar").GetComponent<RectTransform>();
            titleBar.offsetMin = new Vector2(0f, -72f);

            RectTransform close = FindChild(root.transform, "WindowCloseButton").GetComponent<RectTransform>();
            close.sizeDelta = new Vector2(68f, 64f);
            close.anchoredPosition = new Vector2(-40f, 0f);

            RectTransform scroll = FindChild(root.transform, "ScrollView").GetComponent<RectTransform>();
            scroll.offsetMin = new Vector2(18f, 106f);
            scroll.offsetMax = new Vector2(-18f, -86f);

            RectTransform viewport = FindChild(scroll, "Viewport").GetComponent<RectTransform>();
            viewport.offsetMax = new Vector2(-40f, 0f);

            RectTransform scrollbar = FindChild(scroll, "Scrollbar").GetComponent<RectTransform>();
            scrollbar.offsetMin = new Vector2(-34f, 4f);
            scrollbar.offsetMax = new Vector2(-4f, -4f);

            RectTransform footer = FindChild(root.transform, "WindowFooterButton").GetComponent<RectTransform>();
            footer.sizeDelta = new Vector2(220f, 68f);
            footer.anchoredPosition = new Vector2(-126f, 54f);

            RectTransform message = FindChild(root.transform, "WindowMessage").GetComponent<RectTransform>();
            message.offsetMin = new Vector2(22f, 14f);
            message.offsetMax = new Vector2(-256f, 96f);

            PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureDesktopPrefab(GameObject windowPrefab)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabPath);
        if (existing != null)
        {
            UpgradeResponsiveDesktopPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabPath);
        }

        GameObject root = CreateUIObject("ManagementComputerDesktop", null);
        Stretch((RectTransform)root.transform);
        Image background = root.AddComponent<Image>();
        background.color = DesktopBlue;

        TMP_Text brand = CreateText("DesktopBrand", root.transform, 50f, FontStyles.Normal,
            new Color(1f, 1f, 1f, 0.13f), TextAlignmentOptions.BottomRight,
            Vector2.zero, Vector2.one, new Vector2(260f, 90f), new Vector2(-35f, -80f));
        brand.text = "DINE IN\nMANAGEMENT OS";

        TMP_Text hint = CreateText("DesktopHint", root.transform, 21f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(24f, 62f), new Vector2(-310f, 105f));
        hint.text = "Choose an app to prepare the restaurant, then start the shift.";

        string[] appNames = { "DASHBOARD", "STAFF", "MENU", "RESTOCK", "EQUIPMENT", "FINANCES", "OBJECTIVES" };
        Color[] colors =
        {
            new Color(0.12f, 0.52f, 0.86f), new Color(0.27f, 0.67f, 0.54f),
            new Color(0.92f, 0.50f, 0.20f), new Color(0.61f, 0.43f, 0.83f),
            new Color(0.89f, 0.66f, 0.18f), new Color(0.14f, 0.64f, 0.72f),
            new Color(0.79f, 0.30f, 0.39f)
        };
        for (int i = 0; i < appNames.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            Button button = CreateButton("AppButton_" + i, root.transform, appNames[i], colors[i],
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(135f + column * 225f, -120f - row * 100f), new Vector2(200f, 78f));
            button.GetComponentInChildren<TMP_Text>().fontSize = 23f;
        }

        Image taskbar = CreateImage("Taskbar", root.transform, Vector2.zero, new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, 58f), new Color(0.025f, 0.08f, 0.14f, 0.98f));
        TMP_Text day = CreateStatusText("DayStatus", taskbar.transform, "DAY 1", 15f, 135f);
        TMP_Text money = CreateStatusText("MoneyStatus", taskbar.transform, "₱0", 155f, 280f);
        TMP_Text approval = CreateStatusText("ApprovalStatus", taskbar.transform, "APPROVAL 50%", 300f, 490f);
        TMP_Text clock = CreateStatusText("ClockStatus", taskbar.transform, "12:00 PM", 500f, 650f);

        Button start = CreateButton("StartShiftButton", taskbar.transform, "START SHIFT", AccentBlue,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-155f, 0f), new Vector2(230f, 44f));
        TMP_Text startLabel = start.GetComponentInChildren<TMP_Text>();
        startLabel.gameObject.name = "StartShiftLabel";
        Button exit = CreateButton("ExitButton", root.transform, "EXIT COMPUTER", new Color(0.68f, 0.18f, 0.2f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -35f), new Vector2(184f, 50f));

        GameObject window = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, root.transform);
        window.name = "ManagementComputerAppWindow";
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchoredPosition = new Vector2(120f, 15f);
        window.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DesktopPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        UpgradeResponsiveDesktopPrefab();
        return prefab;
    }

    private static void UpgradeResponsiveDesktopPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DesktopPrefabPath);
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            // Keep input on a canvas that is enabled together with the modal.
            // The dedicated parent canvas stays active while the desktop is
            // hidden, which can leave newly enabled Graphics at depth -1 in
            // Unity 6 and make every click fall through to CanvasGameMenu.
            Canvas inputCanvas = root.GetComponent<Canvas>();
            if (inputCanvas == null)
                inputCanvas = root.AddComponent<Canvas>();
            inputCanvas.overrideSorting = true;
            inputCanvas.sortingOrder = 501;
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();

            Transform safeTransform = root.transform.Find("SafeAreaContent");
            GameObject safeObject;
            if (safeTransform == null)
            {
                safeObject = CreateUIObject("SafeAreaContent", root.transform);
                safeObject.layer = 5;
                safeTransform = safeObject.transform;
                safeTransform.SetAsFirstSibling();
            }
            else
            {
                safeObject = safeTransform.gameObject;
            }

            RectTransform safeRect = (RectTransform)safeTransform;
            Stretch(safeRect);

            List<Transform> directChildren = new List<Transform>();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child != safeTransform)
                    directChildren.Add(child);
            }

            foreach (Transform child in directChildren)
                child.SetParent(safeTransform, false);

            ManagementComputerWindow window = safeObject.GetComponentInChildren<ManagementComputerWindow>(true);
            if (window == null)
                throw new InvalidOperationException("Management computer app window prefab instance is missing.");

            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.30f, 0.12f);
            windowRect.anchorMax = new Vector2(0.97f, 0.92f);
            windowRect.offsetMin = new Vector2(10f, 10f);
            windowRect.offsetMax = new Vector2(-10f, -10f);
            windowRect.localScale = Vector3.one;

            RectTransform taskbar = FindChild(safeTransform, "Taskbar").GetComponent<RectTransform>();
            taskbar.offsetMin = Vector2.zero;
            taskbar.offsetMax = new Vector2(0f, 82f);

            RectTransform start = FindChild(safeTransform, "StartShiftButton").GetComponent<RectTransform>();
            start.sizeDelta = new Vector2(240f, 64f);
            start.anchoredPosition = new Vector2(-160f, 0f);

            RectTransform exit = FindChild(safeTransform, "ExitButton").GetComponent<RectTransform>();
            exit.sizeDelta = new Vector2(200f, 64f);
            exit.anchoredPosition = new Vector2(-112f, -42f);

            RectTransform hint = FindChild(safeTransform, "DesktopHint").GetComponent<RectTransform>();
            hint.offsetMin = new Vector2(24f, 90f);
            hint.offsetMax = new Vector2(-330f, 138f);

            RectTransform[] buttons = new RectTransform[Enum.GetValues(typeof(ManagementComputerApp)).Length];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = FindChild(safeTransform, "AppButton_" + i).GetComponent<RectTransform>();

            ManagementComputerResponsiveLayout responsive = root.GetComponent<ManagementComputerResponsiveLayout>();
            if (responsive == null)
                responsive = root.AddComponent<ManagementComputerResponsiveLayout>();
            responsive.ConfigureReferences(safeRect, windowRect, buttons);

            PrefabUtility.SaveAsPrefabAsset(root, DesktopPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Canvas EnsureManagementCanvas(Scene scene)
    {
        GameObject canvasObject = FindSceneObject(scene, "ManagementComputerCanvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("ManagementComputerCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create management computer canvas");
        }

        canvasObject.layer = 5;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        Stretch(rect);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasObject);

        EditorUtility.SetDirty(canvasObject);
        return canvas;
    }

    private static void DestroyAllSceneObjectsNamed(Scene scene, string objectName)
    {
        List<GameObject> matches = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    matches.Add(transform.gameObject);
            }
        }

        foreach (GameObject match in matches)
            Undo.DestroyObjectImmediate(match);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    private static TMP_Text CreateStatusText(string name, Transform parent, string value, float left, float right)
    {
        TMP_Text text = CreateText(name, parent, 20f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(left, 0f), new Vector2(right, 0f));
        text.text = value;
        return text;
    }

    private static Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject root = CreateUIObject("Scrollbar", parent);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-20f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.65f, 0.72f, 0.8f, 1f);

        GameObject sliding = CreateUIObject("SlidingArea", root.transform);
        Stretch((RectTransform)sliding.transform);
        GameObject handle = CreateUIObject("Handle", sliding.transform);
        Stretch((RectTransform)handle.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.sprite = uiSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = AccentBlue;

        Scrollbar scrollbar = root.AddComponent<Scrollbar>();
        scrollbar.handleRect = (RectTransform)handle.transform;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = root.AddComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", root.transform, 21f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(5f, 3f), new Vector2(-5f, -3f));
        text.text = label;
        text.raycastTarget = false;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = root.AddComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style, Color color,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TMP_Text text = root.AddComponent<TextMeshProUGUI>();
        text.font = gameFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == name)
                return candidate;
        }

        throw new InvalidOperationException("Missing generated UI child: " + name);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static IEnumerable<T> LoadAllAssets<T>() where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" }))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                yield return asset;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static int EnsureLayer(string layerName)
    {
        UnityEngine.Object tagManagerAsset =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject tagManager = new SerializedObject(tagManagerAsset);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                return i;
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrWhiteSpace(layer.stringValue))
                continue;

            layer.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return i;
        }

        throw new InvalidOperationException("No free Unity layer is available for " + layerName + ".");
    }
}
#endif
