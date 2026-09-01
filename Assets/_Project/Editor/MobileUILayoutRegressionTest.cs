#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using PlayFab.ClientModels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MobileUILayoutRegressionTest
{
    [MenuItem("Tools/Dine In/Validate Mobile UI Scaling")]
    public static void Run()
    {
        ValidateReferenceResolution(new Vector2(1920f, 1080f));
        ValidateReferenceResolution(new Vector2(800f, 450f));
        ValidateReferenceResolution(new Vector2(800f, 600f));
        ValidatePersistentHudScaling("PlayerTaskHUD(Clone)");
        ValidatePersistentHudScaling("CasualDiningProgressHUD(Clone)");
        ValidatePersistentHudScaling("LobbyPauseMenu(Clone)");
        ValidateManagementComputerScaling();
        ValidateManagementComputerMobileAuthoring();
        ValidateRealme8SizingEnvelope();
        ValidateCashierLandscapeResponsiveness();
        ValidateManagementLandscapeResponsiveness();
        ValidateAuthoredNewGameMenuScene();
        ValidateLoadingCanvasProtection();
        ValidateDevConsoleAuthorizationBoundary();
        ValidateInspectorEditableMobileUI();
        ValidateSmoothScrollPolicy();
        ValidateRestockContainerQuantityLifecycle();
        ValidateRestockInteractionPrefabs();
        ValidateCasualDiningFeedbackFeatures();
        Debug.Log(
            "[MobileUILayoutRegressionTest] PASS — authored canvas coordinates are preserved " +
            "while full-screen panels, persistent HUD sizing, and physical touch targets " +
            "match the 1280 x 576 Android policy.");
    }

    private static void ValidateInspectorEditableMobileUI()
    {
        Assert(IsInspectorField<OrderChecklistUI>("mobileRootScaleMultiplier") &&
               IsInspectorField<OrderChecklistUI>("mobileCustomerMessagePosition"),
            "Notepad mobile scale/header layout is no longer Inspector-editable.");
        Assert(IsInspectorField<CashierRegisterUI>("mobileCompactItemsWidth") &&
               IsInspectorField<CashierRegisterUI>("desktopCompactItemsWidth"),
            "Cashier mobile layout is no longer Inspector-editable.");
        Assert(IsInspectorField<ManagementComputerResponsiveLayout>("mobileLandscapeWindowMin") &&
               IsInspectorField<ManagementComputerResponsiveLayout>("appButtonColumns") &&
               IsInspectorField<ManagementComputerResponsiveLayout>("mobileReferenceResolution") &&
               IsInspectorField<ManagementComputerResponsiveLayout>("minimumTouchTarget") &&
               IsInspectorField<ManagementComputerResponsiveLayout>("wideButtonSprite") &&
               IsInspectorField<ManagementComputerResponsiveLayout>("squareButtonSprite"),
            "Management-computer mobile scale, touch targets, or Blue/Double theme are no longer Inspector-editable.");
        Assert(IsInspectorField<ManagementComputerWindow>("mobileBodyMinimum") &&
               IsInspectorField<ManagementComputerWindow>("mobileCloseButtonSize") &&
               IsInspectorField<ManagementComputerWindow>("mobileCardSizeRange"),
            "Management-computer app-window sizing is no longer Inspector-editable.");
        Assert(IsInspectorField<ManagementComputerCatalogPanelUI>("mobilePreferredCardSize") &&
               IsInspectorField<ManagementComputerCatalogPanelUI>("mobileControlHeight") &&
               IsInspectorField<ManagementComputerCatalogPanelUI>("mobileRightRailWidthRange"),
            "Management-computer catalog sizing is no longer Inspector-editable.");
        Assert(IsInspectorField<DevSettingsConsole>("androidOpenButton") &&
               IsInspectorField<DevSettingsConsole>("positionAndroidButtonInSafeArea"),
            "Authorized Android dev button can no longer be authored in a scene or prefab.");
        Assert(IsInspectorField<RestockStorageContainer>("quantityTexts") &&
               IsInspectorField<RestockStorageContainer>("quantityPrefix"),
            "Restock quantity presentation is no longer Inspector-editable.");
    }

    private static bool IsInspectorField<T>(string fieldName)
    {
        FieldInfo field = typeof(T).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null &&
               (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null);
    }

    private static void ValidateSmoothScrollPolicy()
    {
        GameObject root = new GameObject("Smooth Scroll Regression", typeof(RectTransform));
        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(root.transform, false);
        try
        {
            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.content = contentObject.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 32f;

            SmoothScrollRectInput smooth = root.AddComponent<SmoothScrollRectInput>();
            smooth.ApplyNowForValidation();
            Assert(Mathf.Approximately(scroll.scrollSensitivity, 0f),
                "Built-in immediate wheel jumps were not disabled by the smooth-scroll policy.");
            Assert(scroll.inertia && Mathf.Approximately(scroll.decelerationRate, 0.12f),
                "Touch/drag inertia is not using the shared moderate scrolling policy.");
            Assert(smooth.NormalizedStep > 0.05f && smooth.NormalizedStep < 0.12f &&
                   smooth.SmoothTime >= 0.08f && smooth.SmoothTime <= 0.16f,
                "Default wheel scrolling is outside the comfortable moderate range.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateRestockContainerQuantityLifecycle()
    {
        InventoryManager previousInventory = InventoryManager.Instance;
        GameObject inventoryObject = new GameObject("Restock Quantity Test Inventory");
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        GameObject containerObject = null;
        try
        {
            InventoryManager inventory = inventoryObject.AddComponent<InventoryManager>();
            InventoryManager.Instance = inventory;
            item.itemID = "regression-restock-item";
            item.itemType = default;
            item.displayName = "Regression Buns";
            item.unitsPerBox = 20;
            item.shelfLifeDays = 7f;
            inventory.ConfigureItems(new List<ItemData> { item });
            inventory.SetAllStock(0);
            inventory.AddStockBatch(item, 20, 1, out string batchID, out int expiresDay);

            containerObject = new GameObject("Restock Quantity Test Container");
            GameObject nameObject = new GameObject(
                "Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            GameObject quantityObject = new GameObject(
                "Quantity", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            GameObject iconObject = new GameObject(
                "Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            nameObject.transform.SetParent(containerObject.transform, false);
            quantityObject.transform.SetParent(containerObject.transform, false);
            iconObject.transform.SetParent(containerObject.transform, false);

            TMP_Text nameText = nameObject.GetComponent<TMP_Text>();
            TMP_Text quantityText = quantityObject.GetComponent<TMP_Text>();
            RestockStorageContainer container =
                containerObject.AddComponent<RestockStorageContainer>();
            container.ConfigureLabels(
                new[] { nameText },
                new[] { iconObject.GetComponent<Image>() });
            container.ConfigureQuantityLabels(new[] { quantityText });
            container.Bind(item, batchID, expiresDay);

            Assert(container.CurrentRemainingQuantity == 20 && quantityText.text == "x20",
                "A new RestockScene crate does not show its real full quantity.");
            Assert(inventory.UseStock(item.itemType, 1),
                "Regression inventory could not consume one crate item.");
            Assert(container.CurrentRemainingQuantity == 19 && quantityText.text == "x19",
                "RestockScene crate quantity did not update immediately after one item was consumed.");
            Assert(inventory.UseStock(item.itemType, 19),
                "Regression inventory could not consume the remaining crate items.");
            Assert(container == null && containerObject == null,
                "The zero-quantity RestockScene crate was not removed.");

            // A physical crate can be restored before its saved inventory batch.
            // That temporary ordering must never delete or disable the crate.
            containerObject = new GameObject("Restock Delayed Batch Container");
            RestockStorageContainer delayed =
                containerObject.AddComponent<RestockStorageContainer>();
            delayed.ConfigureLabels(Array.Empty<TMP_Text>(), Array.Empty<Image>());
            delayed.Bind(item, "batch-not-restored-yet", expiresDay);
            delayed.RefreshExpiryState();
            Assert(delayed != null && containerObject != null &&
                   delayed.CurrentRemainingQuantity == item.unitsPerBox,
                "A not-yet-restored stock batch was incorrectly treated as an empty crate.");
        }
        finally
        {
            InventoryManager.Instance = previousInventory;
            if (containerObject != null)
                UnityEngine.Object.DestroyImmediate(containerObject);
            UnityEngine.Object.DestroyImmediate(inventoryObject);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void ValidateRestockInteractionPrefabs()
    {
        string[] prefabPaths =
        {
            "Assets/_Project/Restaurant/RestockRoom/Prefabs/CardboardBox.prefab",
            "Assets/_Project/Restaurant/RestockRoom/Prefabs/crate.prefab"
        };

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert(prefab != null, "Restock container prefab is missing: " + path);
            DraggableStorageBox draggable = prefab.GetComponent<DraggableStorageBox>();
            Assert(draggable != null && prefab.GetComponentInChildren<Collider>(true) != null,
                "Restock container lost its drag script or click collider: " + path);

            SerializedObject serialized = new SerializedObject(draggable);
            GameObject interactionRoot = serialized.FindProperty("interactionUIRoot")
                ?.objectReferenceValue as GameObject;
            Button keep = serialized.FindProperty("keepButton")?.objectReferenceValue as Button;
            Button throwAway = serialized.FindProperty("throwAwayButton")
                ?.objectReferenceValue as Button;
            Assert(interactionRoot != null && keep != null && throwAway != null,
                "Restock container lost its editable Keep/Throw interaction references: " + path);

            RestockStorageContainer container = prefab.GetComponent<RestockStorageContainer>();
            Assert(container != null,
                "Restock container lost its live quantity/spoilage identity: " + path);
            SerializedObject containerSerialized = new SerializedObject(container);
            Assert(containerSerialized.FindProperty("appendQuantityToItemName")?.boolValue == true &&
                   containerSerialized.FindProperty("removeContainerWhenEmpty")?.boolValue == true &&
                   containerSerialized.FindProperty("quantityPrefix")?.stringValue == "x",
                "Restock container quantity display/removal settings are not serialized: " + path);
        }
    }

    private static void ValidateLoadingCanvasProtection()
    {
        const string prefabPath =
            "Assets/_Project/MainMenu/NewDesign/LoadingScreens/NormalLoadingScreen/LoadingScreen.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert(prefab != null, "Loading screen prefab is missing.");

        CanvasScaler scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
        BurgerLoadingAnimation burger = prefab.GetComponentInChildren<BurgerLoadingAnimation>(true);
        Slider slider = prefab.GetComponentInChildren<Slider>(true);
        Assert(scaler != null && burger != null && slider != null,
            "Loading screen lost its canvas, burger animation, or progress bar.");

        LoadingScreenUI presenter = prefab.GetComponentInChildren<LoadingScreenUI>(true);
        RectTransform tipSafeArea = FindNamedTransform(prefab.transform, "FlavorTipSafeArea") as RectTransform;
        TMP_Text tipText = FindNamedTransform(prefab.transform, "FlavorTipText")?.GetComponent<TMP_Text>();
        SerializedObject loadingPresenter = presenter != null ? new SerializedObject(presenter) : null;
        CanvasGroup serializedTipGroup = loadingPresenter != null
            ? loadingPresenter.FindProperty("tipsCanvasGroup").objectReferenceValue as CanvasGroup
            : null;
        Assert(presenter != null && tipSafeArea != null && tipText != null &&
               serializedTipGroup != null &&
               loadingPresenter.FindProperty("tipsText").objectReferenceValue == tipText &&
               loadingPresenter.FindProperty("tipsSafeAreaRoot").objectReferenceValue == tipSafeArea,
            "Loading screen lost its editable, safe-area-aware flavor-tip strip.");
        Assert(tipSafeArea.anchorMin == Vector2.zero && tipSafeArea.anchorMax == Vector2.one,
            "Loading flavor text no longer starts from a full safe-area root.");
        Assert(tipText.transform.parent.GetComponent<Image>() == null,
            "Loading flavor text regained an unwanted framed progress-style background.");

        MethodInfo progressLookup = typeof(SceneLoader).GetMethod(
            "FindDedicatedProgressText",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(progressLookup != null && progressLookup.Invoke(null, new object[] { prefab }) == null,
            "The scene loader can still mistake the loading tip for percentage text.");

        CanvasScaler.ScreenMatchMode authoredMode = scaler.screenMatchMode;
        float authoredMatch = scaler.matchWidthOrHeight;
        Vector3 authoredBurgerScale = burger.transform.localScale;
        MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

        Assert(scaler.screenMatchMode == authoredMode &&
               Mathf.Approximately(scaler.matchWidthOrHeight, authoredMatch),
            "Loading screen CanvasScaler was changed by the global mobile pass.");
        Assert(burger.gameObject.activeSelf && burger.enabled &&
               burger.transform.localScale == authoredBurgerScale,
            "Loading burger artwork was hidden or visually rescaled by the global mobile pass.");
    }

    private static void ValidateDevConsoleAuthorizationBoundary()
    {
        const string sessionPlayFabId = "TEST-OWNER-ID";
        UserAccountInfo owner = new UserAccountInfo
        {
            PlayFabId = sessionPlayFabId,
            Username = "Kali"
        };

        Assert(DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "The authenticated Kali test account was rejected by the dev-console policy.");
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, "DIFFERENT-SESSION-ID", "Kali"),
            "A PlayFab account response from a different login session was accepted.");

        owner.Username = "kali";
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "The dev-console policy stopped requiring the exact unique PlayFab username.");

        owner.Username = "RegularPlayer";
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "A non-owner PlayFab account was allowed to use dev commands.");
    }

    private static void ValidatePersistentHudScaling(string canvasName)
    {
        GameObject root = CreateCanvas(canvasName, new Vector2(1920f, 1080f));
        try
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                $"{canvasName} did not use the persistent-HUD mobile policy.");
            Assert(Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                $"{canvasName} was not width-scaled for mobile readability.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateManagementComputerScaling()
    {
        GameObject root = CreateCanvas("ManagementComputerCanvas", new Vector2(1920f, 1080f));
        try
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                "Management computer did not use its mobile readability policy.");
            Assert(Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                "Management computer mobile scaling became too large or too small.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateManagementComputerMobileAuthoring()
    {
        const string desktopPath =
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerDesktop.prefab";
        const string windowPath =
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerAppWindow.prefab";
        const string catalogPath =
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogPanel.prefab";
        const string equipmentCardPath =
            "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentCard.prefab";

        GameObject desktopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(desktopPath);
        Assert(desktopPrefab != null, "Management-computer desktop prefab is missing.");
        ManagementComputerResponsiveLayout responsive =
            desktopPrefab.GetComponentInChildren<ManagementComputerResponsiveLayout>(true);
        Assert(responsive != null, "Management-computer desktop lost its responsive layout.");
        Assert(responsive.AppButtons != null &&
               responsive.AppButtons.Length == Enum.GetValues(typeof(ManagementComputerApp)).Length,
            "Management-computer desktop no longer covers every app button.");

        SerializedObject desktop = new SerializedObject(responsive);
        Vector2 mobileReference = desktop.FindProperty("mobileReferenceResolution").vector2Value;
        float touchTarget = desktop.FindProperty("minimumTouchTarget").floatValue;
        Sprite wideSprite = desktop.FindProperty("wideButtonSprite").objectReferenceValue as Sprite;
        Sprite squareSprite = desktop.FindProperty("squareButtonSprite").objectReferenceValue as Sprite;
        Assert(Vector2.Distance(mobileReference, new Vector2(1600f, 900f)) < 0.1f,
            $"Management-computer phone reference changed unexpectedly ({mobileReference}).");
        Assert(touchTarget >= 64f && touchTarget <= 80f,
            $"Management-computer touch target escaped the readable phone range ({touchTarget:0}px).");
        Assert(IsBlueDoubleSprite(wideSprite) && IsBlueDoubleSprite(squareSprite),
            "Management-computer buttons no longer use the existing Blue/Double UI family.");

        GameObject windowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(windowPath);
        ManagementComputerWindow window = windowPrefab != null
            ? windowPrefab.GetComponent<ManagementComputerWindow>()
            : null;
        Assert(window != null, "Management-computer app-window prefab is missing or invalid.");
        SerializedObject windowSettings = new SerializedObject(window);
        Assert(windowSettings.FindProperty("mobileBodyMinimum").floatValue >= 18f &&
               windowSettings.FindProperty("mobileCloseButtonSize").vector2Value.x >= 88f,
            "Management-computer app-window text or close target became too small for phones.");

        GameObject catalogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPath);
        ManagementComputerCatalogPanelUI catalog = catalogPrefab != null
            ? catalogPrefab.GetComponent<ManagementComputerCatalogPanelUI>()
            : null;
        Assert(catalog != null, "Management-computer catalog prefab is missing or invalid.");
        SerializedObject catalogSettings = new SerializedObject(catalog);
        Vector2 mobileCard = catalogSettings.FindProperty("mobilePreferredCardSize").vector2Value;
        Assert(mobileCard.x >= 248f && mobileCard.y >= 292f &&
               catalogSettings.FindProperty("mobileControlHeight").floatValue >= 64f,
            "Menu/Restock cards or controls became too small for phones.");

        GameObject equipmentCard = AssetDatabase.LoadAssetAtPath<GameObject>(equipmentCardPath);
        Assert(equipmentCard != null, "Management equipment-card prefab is missing.");
        Button[] equipmentButtons = equipmentCard.GetComponentsInChildren<Button>(true);
        Assert(Array.Exists(equipmentButtons, button =>
                button != null && button.targetGraphic is Image image && IsBlueDoubleSprite(image.sprite)),
            "Equipment cards no longer carry the shared Blue/Double action styling.");

        const string hrPath =
            "Assets/_Project/ManagementComputer/Prefabs/ManagementHRPanel.prefab";
        GameObject hrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hrPath);
        ManagementComputerHRPanel hrPanel = hrPrefab != null
            ? hrPrefab.GetComponent<ManagementComputerHRPanel>()
            : null;
        Assert(hrPanel != null && hrPanel.ApplicantsTab != null && hrPanel.BodyScroll != null,
            "Staff prefab lost its editable Applicants tab or dedicated body scroll.");
        Assert(hrPanel.BodyScroll.vertical && hrPanel.BodyScroll.content == hrPanel.SectionsRoot,
            "Staff role sections are no longer owned by the smooth vertical body scroll.");
        Assert(!hrPanel.ApplicantsTab.transform.IsChildOf(hrPanel.BodyScroll.transform) &&
               !hrPanel.LobbyTab.transform.IsChildOf(hrPanel.BodyScroll.transform) &&
               !hrPanel.KitchenTab.transform.IsChildOf(hrPanel.BodyScroll.transform),
            "Staff navigation no longer stays sticky while employee cards scroll.");

        Transform staffButton = FindNamedTransform(desktopPrefab.transform, "STAFF");
        Assert(staffButton != null && staffButton.Find("NewApplicantBadge") != null,
            "Staff desktop button lost its new-applicant notification badge.");

        string[] animatedPrefabs =
        {
            windowPath,
            "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerRow.prefab",
            hrPath,
            "Assets/_Project/ManagementComputer/Prefabs/ManagementHRRoleSection.prefab",
            "Assets/_Project/ManagementComputer/Prefabs/ManagementEmployeeCard.prefab"
        };
        foreach (string animatedPath in animatedPrefabs)
        {
            GameObject animated = AssetDatabase.LoadAssetAtPath<GameObject>(animatedPath);
            Assert(animated != null && animated.GetComponent<UIRevealAnimation>() != null,
                "Management reveal transition is missing from " + animatedPath);
        }
    }

    private static void ValidateCasualDiningFeedbackFeatures()
    {
        ValidateApplicantLifecycleAndSoleHire();
        ValidateFinanceLedgerPersistence();

        const string complaintSettingsPath =
            "Assets/_Project/Resources/ManagerComplaints/ManagerComplaintSettings.asset";
        ManagerComplaintSettings complaintSettings =
            AssetDatabase.LoadAssetAtPath<ManagerComplaintSettings>(complaintSettingsPath);
        Assert(complaintSettings != null, "Manager complaint settings asset is missing.");
        Assert(complaintSettings.RollDailyComplaintAllowance(0f) == 0 &&
               complaintSettings.RollDailyComplaintAllowance(0.2499f) == 0 &&
               complaintSettings.RollDailyComplaintAllowance(0.25f) == 1 &&
               complaintSettings.RollDailyComplaintAllowance(0.7499f) == 1 &&
               complaintSettings.RollDailyComplaintAllowance(0.75f) == 2 &&
               complaintSettings.RollDailyComplaintAllowance(0.9499f) == 2 &&
               complaintSettings.RollDailyComplaintAllowance(0.95f) == 3,
            "Manager complaint weights no longer implement the approved 25/50/20/5 distribution.");

        int[] complaintCounts = new int[4];
        int complaintTotal = 0;
        const int deterministicRolls = 10000;
        for (int i = 0; i < deterministicRolls; i++)
        {
            float roll = (i + 0.5f) / deterministicRolls;
            int allowance = complaintSettings.RollDailyComplaintAllowance(roll);
            Assert(allowance >= 0 && allowance <= 3,
                "Manager complaint allowance escaped the supported 0-3 range.");
            complaintCounts[allowance]++;
            complaintTotal += allowance;
        }
        Assert(complaintCounts[0] == 2500 &&
               complaintCounts[1] == 5000 &&
               complaintCounts[2] == 2000 &&
               complaintCounts[3] == 500,
            "Manager complaint sampling no longer produces exact 25/50/20/5 buckets.");
        Assert(Mathf.Abs((float)complaintTotal / deterministicRolls - 1.05f) < 0.0001f,
            "Manager complaint distribution no longer averages 1.05 encounters per day.");
        Assert(complaintSettings.firstComplaintDelaySeconds >= 90f &&
               complaintSettings.minimumSecondsBetweenComplaints >= 120f &&
               complaintSettings.stopNewComplaintsBeforeCloseSeconds >= 60f,
            "Manager complaint pacing guardrails became too aggressive.");
    }

    private static void ValidateApplicantLifecycleAndSoleHire()
    {
        GameObject managerObject = new GameObject("Applicant Lifecycle Regression");
        GameObject generatorObject = new GameObject("Applicant Generator Regression");
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(73419);
            EmployeeManager manager = managerObject.AddComponent<EmployeeManager>();
            EmployeeGenerator generator = generatorObject.AddComponent<EmployeeGenerator>();
            manager.generator = generator;

            EmployeeData soleHost = new EmployeeData("Solo Host", 3, EmployeeRole.Host)
            {
                hired = true,
                assigned = false
            };
            EmployeeData firstWaiter = new EmployeeData("Waiter One", 3, EmployeeRole.Waiter)
            {
                hired = true,
                assigned = false
            };
            EmployeeData secondWaiter = new EmployeeData("Waiter Two", 4, EmployeeRole.Waiter)
            {
                hired = true,
                assigned = false
            };
            EmployeeData expiringApplicant = new EmployeeData("One Day Applicant", 2, EmployeeRole.Host)
            {
                hired = false,
                applicantAvailableUntilDay = 1
            };
            string expiringID = expiringApplicant.EmployeeID;
            manager.allEmployees.AddRange(new[]
            {
                soleHost, firstWaiter, secondWaiter, expiringApplicant
            });

            manager.AutoAssignSoleHires();
            Assert(soleHost.assigned,
                "A role with exactly one hired employee was not made active automatically.");
            Assert(!firstWaiter.assigned && !secondWaiter.assigned,
                "A role with multiple hired employees bypassed the player's active-worker choice.");

            SetPrivateField(manager, "applicantLastProcessedDay", 1);
            SetPrivateField(manager, "applicantNextRefreshDay", 3);
            manager.RefreshApplicantsIfDue(2, 2);
            Assert(!manager.allEmployees.Exists(employee =>
                    employee != null && employee.EmployeeID == expiringID),
                "A one-day applicant remained after their availability expired.");
            Assert(!manager.HasUnseenApplicants,
                "Applicant removal incorrectly raised a new-applicant notification.");
            Assert(manager.allEmployees.FindAll(employee =>
                    employee != null && !employee.hired && employee.role == EmployeeRole.Host).Count == 0,
                "The expired applicant slot was refilled before the scheduled refresh.");

            manager.MarkApplicantsSeen();
            Assert(!manager.HasUnseenApplicants,
                "Opening Applicants did not clear its notification state.");
            HashSet<string> dayTwoApplicants = new HashSet<string>();
            foreach (EmployeeData employee in manager.allEmployees)
            {
                if (employee != null && !employee.hired)
                    dayTwoApplicants.Add(employee.EmployeeID);
            }

            manager.RefreshApplicantsIfDue(3, 2);
            Assert(manager.ApplicantNextRefreshDay == 5 && manager.HasUnseenApplicants,
                "Applicant pools did not perform their full every-other-day refresh.");
            Assert(!manager.allEmployees.Exists(employee =>
                    employee != null && !employee.hired && dayTwoApplicants.Contains(employee.EmployeeID)),
                "The scheduled applicant refresh reused an expired cohort.");

            manager.MarkApplicantsSeen();
            EmployeeData declined = manager.allEmployees.Find(employee =>
                employee != null && !employee.hired && employee.role == EmployeeRole.Host);
            int hostApplicantsBeforeDecline = manager.allEmployees.FindAll(employee =>
                employee != null && !employee.hired && employee.role == EmployeeRole.Host).Count;
            Assert(manager.DeclineApplicant(declined),
                "A valid applicant could not be declined.");
            Assert(manager.allEmployees.FindAll(employee =>
                    employee != null && !employee.hired && employee.role == EmployeeRole.Host).Count ==
                   hostApplicantsBeforeDecline - 1,
                "Declining an applicant generated an immediate replacement.");
            Assert(!manager.HasUnseenApplicants,
                "Declining an applicant incorrectly raised a new-applicant notification.");

            EmployeeData remainingHost;
            while ((remainingHost = manager.allEmployees.Find(employee =>
                       employee != null && !employee.hired && employee.role == EmployeeRole.Host)) != null)
            {
                Assert(manager.DeclineApplicant(remainingHost),
                    "A remaining Host applicant could not be declined.");
            }

            GameSaveData save = new GameSaveData();
            manager.FillSaveData(save);
            Assert(save.employeeApplicantNextRefreshDay == 5 &&
                   save.employeeApplicantLastProcessedDay == 3 &&
                   !save.employeeApplicantsUnseen,
                "Applicant refresh/notification state did not persist to the save model.");

            save.currentDay = 3;
            save.employeeApplicantNextRefreshDay = 99;
            manager.ApplySaveData(save);
            Assert(manager.ApplicantNextRefreshDay == 5,
                "A legacy weekly applicant schedule was not migrated to every other day.");
            Assert(manager.allEmployees.FindAll(employee =>
                    employee != null && !employee.hired && employee.role == EmployeeRole.Host).Count == 0,
                "Loading the save refilled a deliberately depleted applicant role.");
        }
        finally
        {
            UnityEngine.Random.state = previousRandom;
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(generatorObject);
        }
    }

    private static void ValidateFinanceLedgerPersistence()
    {
        MoneyManager previous = MoneyManager.Instance;
        GameObject sourceObject = new GameObject("Finance Ledger Regression Source");
        GameObject restoredObject = new GameObject("Finance Ledger Regression Restored");
        try
        {
            MoneyManager source = sourceObject.AddComponent<MoneyManager>();
            MoneyManager.Instance = source;
            source.SetMoney(1000, "Regression starting balance");
            Assert(source.Spend(175, "Equipment purchase"),
                "Finance regression could not record a normal expense.");
            source.ForceSpend(225, "Payroll");
            Assert(source.Money == 600 && source.DailyTransactions.Count >= 3,
                "Finance ledger did not retain the current-day transactions.");
            Assert(ContainsTransaction(source.DailyTransactions, "Equipment purchase", -175) &&
                   ContainsTransaction(source.DailyTransactions, "Payroll", -225),
                "Finance ledger does not itemize purchases and automatic payroll.");

            GameSaveData save = new GameSaveData();
            source.FillSaveData(save);
            MoneyManager restored = restoredObject.AddComponent<MoneyManager>();
            restored.ApplySaveData(save);
            Assert(restored.Money == 600 &&
                   ContainsTransaction(restored.DailyTransactions, "Payroll", -225),
                "Finance ledger did not survive save/load.");
        }
        finally
        {
            MoneyManager.Instance = previous;
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(restoredObject);
            MoneyManager.Instance = previous;
        }
    }

    private static bool ContainsTransaction(
        IReadOnlyList<MoneyTransactionSaveEntry> transactions,
        string description,
        int amount)
    {
        for (int i = 0; i < transactions.Count; i++)
        {
            MoneyTransactionSaveEntry entry = transactions[i];
            if (entry != null && entry.description == description && entry.amountDelta == amount)
                return true;
        }
        return false;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Regression field is missing: " + fieldName);
        field.SetValue(target, value);
    }

    private static Transform FindNamedTransform(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindNamedTransform(child, objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static bool IsBlueDoubleSprite(Sprite sprite)
    {
        if (sprite == null)
            return false;

        string path = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
        return path.IndexOf(
                   "/NewDesign/UI Elements/PNG/Blue/Double/",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ValidateFullScreenPanelCoverage()
    {
        GameObject parent = new GameObject("Canvas Frame", typeof(RectTransform));
        GameObject panelObject = new GameObject("GamemodePopUpUI", typeof(RectTransform));
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform));
        GameObject dialogueObject = new GameObject("Dialogue", typeof(RectTransform));
        GameObject titleObject = new GameObject("TitleFrame", typeof(RectTransform));
        GameObject campaignObject = new GameObject("CampaignButton", typeof(RectTransform));
        GameObject multiplayerObject = new GameObject("MultiplayerButton", typeof(RectTransform));
        GameObject closeObject = new GameObject("CancelButton ", typeof(RectTransform));

        try
        {
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent.transform, false);
            panel.anchorMin = new Vector2(0.2f, 0.1f);
            panel.anchorMax = new Vector2(0.8f, 0.9f);
            panel.sizeDelta = new Vector2(200f, 100f);

            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            background.SetParent(panel, false);
            background.anchorMin = background.anchorMax = Vector2.zero;
            background.sizeDelta = new Vector2(1920f, 1080f);

            RectTransform dialogue = dialogueObject.GetComponent<RectTransform>();
            dialogue.SetParent(panel, false);
            dialogue.sizeDelta = new Vector2(450f, 260f);

            RectTransform title = titleObject.GetComponent<RectTransform>();
            title.SetParent(dialogue, false);
            title.sizeDelta = new Vector2(450f, 100f);
            RectTransform campaign = campaignObject.GetComponent<RectTransform>();
            campaign.SetParent(dialogue, false);
            campaign.sizeDelta = new Vector2(200f, 80f);
            RectTransform multiplayer = multiplayerObject.GetComponent<RectTransform>();
            multiplayer.SetParent(dialogue, false);
            multiplayer.sizeDelta = new Vector2(200f, 80f);
            RectTransform close = closeObject.GetComponent<RectTransform>();
            close.SetParent(dialogue, false);
            close.sizeDelta = new Vector2(50f, 50f);

            MobileUIAccessibility.ConfigureFullScreenPanelForMobile(panel);

            Assert(panel.anchorMin == Vector2.zero && panel.anchorMax == Vector2.one &&
                   panel.sizeDelta == Vector2.zero,
                "Full-screen popup root no longer covers the complete phone canvas.");
            Assert(background.anchorMin == new Vector2(0.5f, 0.5f) &&
                   background.anchorMax == new Vector2(0.5f, 0.5f) &&
                   background.sizeDelta == new Vector2(1920f, 1080f),
                "Popup backdrop no longer preserves its authored motif scale.");
            Assert(dialogue.sizeDelta == new Vector2(450f, 260f),
                "Full-screen coverage unexpectedly resized modal content.");
            Assert(title.localScale == new Vector3(2.1f, 2.1f, 1f) &&
                   title.anchoredPosition == new Vector2(0f, 80f),
                "Game-mode title escaped its phone content frame.");
            Assert(campaign.localScale == new Vector3(2.1f, 2.1f, 1f) &&
                   multiplayer.localScale == new Vector3(2.1f, 2.1f, 1f),
                "Game-mode choices are no longer sized for phone input.");
            Assert(close.sizeDelta == new Vector2(72f, 72f),
                "Game-mode close control is no longer visibly touch-sized.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    private static void ValidateRealme8SizingEnvelope()
    {
        const float screenWidth = 1280f;
        const float screenHeight = 576f;
        float hudScale = screenWidth / 1920f;
        float pausePixels = Mathf.Max(MobileUIAccessibility.MinimumPersistentHudPixels, 82f * hudScale);
        float taskPixels = Mathf.Max(MobileUIAccessibility.MinimumPersistentHudPixels, 76f * hudScale);
        float touchPixels = MobileUIAccessibility.MinimumCanvasTouchSizeForScale(
            screenHeight / 1080f) * (screenHeight / 1080f);
        float lobbyCanvasScale = screenHeight / 450f;
        float notepadScale = 0.5f * OrderChecklistUI.MobileRootScaleMultiplier * lobbyCanvasScale;
        Vector2 notepadCardPixels = new Vector2(174f, 218f) * notepadScale;
        Vector2 cashierPanelPixels = new Vector2(630f, 407.4194f) * lobbyCanvasScale;
        float computerCanvasScale = screenWidth / 1920f;
        Vector2 computerLogicalScreen = new Vector2(
            screenWidth / computerCanvasScale,
            screenHeight / computerCanvasScale);
        Vector2 computerWindowPixels = Vector2.Scale(
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMax -
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMin,
            computerLogicalScreen) * computerCanvasScale - Vector2.one * (16f * computerCanvasScale);

        Assert(pausePixels >= 70f && pausePixels <= 76f,
            $"Pause HUD escaped its readable mobile size envelope ({pausePixels:0.0}px).");
        Assert(taskPixels >= 70f && taskPixels <= 76f,
            $"Task HUD escaped its readable mobile size envelope ({taskPixels:0.0}px).");
        Assert(Mathf.Approximately(touchPixels, 72f),
            $"Physical touch target is no longer 72px ({touchPixels:0.0}px).");
        Assert(notepadCardPixels.x >= 140f && notepadCardPixels.x <= 150f &&
               notepadCardPixels.y >= 175f && notepadCardPixels.y <= 185f,
            $"Notepad choices escaped their mobile readability envelope ({notepadCardPixels.x:0.0} x {notepadCardPixels.y:0.0}px).");
        Assert(cashierPanelPixels.x >= 800f && cashierPanelPixels.x <= 820f &&
               cashierPanelPixels.y >= 510f && cashierPanelPixels.y <= 530f &&
               cashierPanelPixels.x < screenWidth && cashierPanelPixels.y < screenHeight,
            $"Cashier panel no longer fits the phone ({cashierPanelPixels.x:0.0} x {cashierPanelPixels.y:0.0}px).");
        Assert(computerWindowPixels.x >= 1200f && computerWindowPixels.x < screenWidth &&
               computerWindowPixels.y >= 500f && computerWindowPixels.y < screenHeight,
            $"Management workspace escaped the phone safe frame ({computerWindowPixels.x:0.0} x {computerWindowPixels.y:0.0}px).");
    }

    private static void ValidateCashierLandscapeResponsiveness()
    {
        Vector2 panelSize = new Vector2(630f, 407.4194f);
        Vector2 referenceResolution = new Vector2(800f, 450f);
        Vector2Int[] landscapeSizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(2160, 1080),
            new Vector2Int(2340, 1080),
            new Vector2Int(2560, 1440)
        };

        for (int i = 0; i < landscapeSizes.Length; i++)
        {
            Vector2Int screen = landscapeSizes[i];
            // CanvasMainHUD uses CanvasScaler Expand: choose the smaller axis
            // scale so the complete authored panel remains visible.
            float canvasScale = Mathf.Min(
                screen.x / referenceResolution.x,
                screen.y / referenceResolution.y);
            Vector2 panelPixels = panelSize * canvasScale;
            float horizontalMargin = (screen.x - panelPixels.x) * 0.5f;
            float verticalMargin = (screen.y - panelPixels.y) * 0.5f;

            Assert(horizontalMargin >= 0f && verticalMargin >= 0f,
                $"Cashier panel escapes {screen.x} x {screen.y} " +
                $"({panelPixels.x:0.0} x {panelPixels.y:0.0}px).");
            Assert(Mathf.Approximately(panelPixels.x / panelPixels.y,
                                       panelSize.x / panelSize.y),
                $"Cashier panel proportions changed at {screen.x} x {screen.y}.");
        }
    }

    private static void ValidateManagementLandscapeResponsiveness()
    {
        Vector2 referenceResolution = new Vector2(1600f, 900f);
        Vector2 anchorSpan =
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMax -
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMin;
        Vector2Int[] landscapeSizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(2160, 1080),
            new Vector2Int(2340, 1080),
            new Vector2Int(2560, 1440)
        };

        for (int i = 0; i < landscapeSizes.Length; i++)
        {
            Vector2Int screen = landscapeSizes[i];
            // MatchWidthOrHeight at 0.5 uses the geometric mean. The management
            // window then fills proportional safe-area anchors and its grids reflow.
            float canvasScale = Mathf.Sqrt(
                (screen.x / referenceResolution.x) *
                (screen.y / referenceResolution.y));
            Vector2 logicalScreen = new Vector2(
                screen.x / canvasScale,
                screen.y / canvasScale);
            Vector2 windowPixels =
                (Vector2.Scale(anchorSpan, logicalScreen) - Vector2.one * 16f) *
                canvasScale;

            Assert(windowPixels.x > 0f && windowPixels.y > 0f &&
                   windowPixels.x < screen.x && windowPixels.y < screen.y,
                $"Management window escapes {screen.x} x {screen.y} " +
                $"({windowPixels.x:0.0} x {windowPixels.y:0.0}px).");
        }
    }

    private static void ValidateAuthoredNewGameMenuScene()
    {
        // This check intentionally mutates an additive scene in memory and then
        // closes it without saving. Keep interactive editor scenes completely
        // untouched; CI/batch validation is the authoritative authored-scene run.
        if (!Application.isBatchMode)
            return;

        const string scenePath = "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
        Scene existing = SceneManager.GetSceneByPath(scenePath);
        if (existing.IsValid() && existing.isLoaded)
        {
            Debug.LogWarning(
                "[MobileUILayoutRegressionTest] NewGameMenu is already open; " +
                "skipping the non-destructive authored-scene check.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        GameObject validatorObject = null;
        try
        {
            validatorObject = new GameObject("[Mobile UI Scene Validator]");
            SceneManager.MoveGameObjectToScene(validatorObject, scene);
            MobileUIAccessibility validator = validatorObject.AddComponent<MobileUIAccessibility>();
            validator.ApplyNowForValidation();

            RectTransform gameCanvas = FindSceneRect(scene, "GameCanvas");
            Assert(gameCanvas != null, "NewGameMenu lost its GameCanvas.");
            CanvasScaler scaler = gameCanvas.GetComponent<CanvasScaler>();
            Assert(scaler != null &&
                   scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                   Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                "NewGameMenu authored CanvasScaler was changed by the global mobile pass.");

            RectTransform modePanel = FindSceneRect(scene, "GamemodePopUpUI");
            RectTransform shopPanel = FindSceneRect(scene, "ShopPanelUI");
            Assert(modePanel != null && shopPanel != null,
                "NewGameMenu lost a required full-screen panel.");

            RectTransform title = FindDescendant(modePanel, "TitleFrame");
            RectTransform campaign = FindDescendant(modePanel, "CampaignButton");
            RectTransform multiplayer = FindDescendant(modePanel, "MultiplayerButton");
            RectTransform backdrop = FindDescendant(modePanel, "Background");
            Assert(title != null && campaign != null && multiplayer != null && backdrop != null,
                "Game-mode popup hierarchy changed without updating its mobile layout.");
            Assert(title.localScale == Vector3.one && campaign.localScale == Vector3.one &&
                   multiplayer.localScale == Vector3.one,
                "Global mobile scaling changed the authored game-mode popup visuals.");

            RectTransform shopScroll = FindDescendant(shopPanel, "Vertical Scroll");
            Assert(shopScroll != null && shopScroll.localScale == Vector3.one,
                "Global mobile scaling changed the authored shop card layout.");

            RectTransform selector = FindSceneRect(scene, "RestaurantSelectorButtonsUI");
            RectTransform play = FindSceneRect(scene, "PlayButton");
            RectTransform selectorButtons = FindDirectChild(selector, "Buttons");
            RectTransform money = FindSceneRect(scene, "MoneyUI");
            RectTransform back = FindDirectChild(gameCanvas, "BackButton");
            RectTransform shop = FindDirectChild(gameCanvas, "ShopButton");
            Assert(selector != null && selector.gameObject.activeSelf && selector.localScale == Vector3.one,
                "Restaurant selector was hidden or visually rescaled on mobile.");
            Assert(play != null && play.gameObject.activeSelf && play.localScale == Vector3.one &&
                   play.rect.size == new Vector2(160f, 50f),
                "NewGameMenu Play button is missing or no longer uses its working authored layout.");
            Assert(selectorButtons != null && selectorButtons.gameObject.activeSelf,
                "NewGameMenu restaurant controls container is inactive.");

            // With the authored 800-wide CanvasScaler, a 1280 x 576 phone has a
            // 360-unit logical height. Verify the complete Play button stays inside it.
            const float realmeLogicalHeight = 576f / (1280f / 800f);
            float playCenterY = selector.anchoredPosition.y +
                                selectorButtons.anchoredPosition.y +
                                play.anchoredPosition.y;
            Assert(playCenterY - play.rect.height * 0.5f >= 0f &&
                   playCenterY + play.rect.height * 0.5f <= realmeLogicalHeight,
                "NewGameMenu Play button falls outside the Realme 8 viewport.");
            Assert(money != null && Mathf.Approximately(money.anchoredPosition.y, -75f),
                "Global mobile scaling moved the authored wallet controls.");
            Assert(back != null && back.rect.size == new Vector2(50f, 50f) &&
                   shop != null && shop.rect.size == new Vector2(50f, 50f),
                "Global mobile scaling changed the authored NewGameMenu corner controls.");
        }
        finally
        {
            if (validatorObject != null)
                UnityEngine.Object.DestroyImmediate(validatorObject);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static RectTransform FindSceneRect(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            RectTransform[] rects = roots[i].GetComponentsInChildren<RectTransform>(true);
            for (int r = 0; r < rects.Length; r++)
            {
                if (rects[r].name == objectName)
                    return rects[r];
            }
        }

        return null;
    }

    private static RectTransform FindDescendant(RectTransform root, string objectName)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == objectName)
                return rects[i];
        }

        return null;
    }

    private static RectTransform FindDirectChild(RectTransform parent, string objectName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i) is RectTransform child && child.name == objectName)
                return child;
        }

        return null;
    }

    private static GameObject CreateCanvas(string name, Vector2 referenceResolution)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        return root;
    }

    private static void ValidateReferenceResolution(Vector2 authoredReference)
    {
        GameObject root = new GameObject(
            "Mobile UI Scale Test",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = authoredReference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.referenceResolution == authoredReference,
                $"Mobile policy changed {authoredReference.x} x {authoredReference.y} canvas coordinates.");
            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                "Mobile policy did not select non-cropping Expand scaling.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
