#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Copies the saved restaurant and authors its final tutorial through Editor APIs.</summary>
public static class FineDiningTutorialAuthoring
{
    public const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby3Tutorial.unity";
    private const string SourcePath = "Assets/_Project/Scenes/RoleBased/Lobby3.unity";
    private const string PresentationPath = "Assets/_Project/Tutorials/FastFoodTutorialPresentation.prefab";
    private const string RecipeFolder = "Assets/_Project/Office/Manager/Recipe/Fine Dining/";
    private const string ArtFolder = "Assets/_Project/Art/Icons/GameIcons/WinePouring/";
    private static string AppliedKey => "DineIn.FineDiningTutorial.Authoring.1." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (EditorPrefs.GetBool(AppliedKey, false) && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
        EditorApplication.update -= AuthorWhenIdle;
        EditorApplication.update += AuthorWhenIdle;
    }

    private static void AuthorWhenIdle()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= AuthorWhenIdle;
        try { CreatePreparedTutorial(); }
        catch (Exception exception)
        {
            Debug.LogError("[Fine Dining Tutorial] Preparation did not finish. The restaurant was not replaced. " +
                "After resolving the reported dependency, use Dine In/Tutorial/Create Prepared Fine Dining Tutorial.\n" + exception);
        }
    }

    [MenuItem("Dine In/Tutorial/Create Prepared Fine Dining Tutorial")]
    public static void CreatePreparedTutorial()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Tutorial authoring requires the idle Editor outside Play Mode.");

        Scene previous = SceneManager.GetActiveScene();
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException("Lobby3Tutorial has unsaved edits. Save it before validating the prepared tutorial.");
            bool opened = !existing.IsValid() || !existing.isLoaded;
            if (opened) existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ApplyPresentationCorrections(existing);
                Validate(existing);
                if (existing.isDirty && !EditorSceneManager.SaveScene(existing))
                    throw new InvalidOperationException("Unity could not save the tutorial corrections.");
                RegisterBuildScene();
                EditorPrefs.SetBool(AppliedKey, true);
                Debug.Log("[Fine Dining Tutorial] Existing tutorial validated; its authored steps and scene edits were preserved.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(existing, true);
                RestoreActive(previous);
            }
            return;
        }

        Scene source = SceneManager.GetSceneByPath(SourcePath);
        if (source.IsValid() && source.isLoaded && source.isDirty)
            throw new InvalidOperationException("Lobby3 has unsaved edits. Save Lobby3 first so its tutorial copies the latest restaurant.");
        RequireAsset<SceneAsset>(SourcePath);
        RequireAsset<GameObject>(PresentationPath);

        // A failed generation never leaves a partly configured final tutorial in the level list.
        string stagedPath = "Assets/_Project/Scenes/RoleBased/__Lobby3Tutorial_" + Guid.NewGuid().ToString("N") + ".unity";
        Scene staged = default;
        bool copied = false;
        bool finalized = false;
        try
        {
            copied = AssetDatabase.CopyAsset(SourcePath, stagedPath);
            if (!copied) throw new InvalidOperationException("Unity could not copy the saved Lobby3 scene.");
            staged = EditorSceneManager.OpenScene(stagedPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(staged);
            Configure(staged);
            Validate(staged);
            EditorSceneManager.MarkSceneDirty(staged);
            if (!EditorSceneManager.SaveScene(staged)) throw new InvalidOperationException("Unity could not save the authored tutorial.");
            EditorSceneManager.CloseScene(staged, true);
            staged = default;
            string error = AssetDatabase.MoveAsset(stagedPath, ScenePath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            finalized = true;
            RegisterBuildScene();
            EditorPrefs.SetBool(AppliedKey, true);
            Debug.Log("[Fine Dining Tutorial] Created Lobby3Tutorial from Lobby3 with the existing Big Boss presentation, " +
                "reservation, extra drink and pouring lessons, and no practice section. Build entry is enabled.");
        }
        finally
        {
            if (staged.IsValid() && staged.isLoaded) EditorSceneManager.CloseScene(staged, true);
            if (copied && !finalized && !AssetDatabase.DeleteAsset(stagedPath))
                Debug.LogWarning("[Fine Dining Tutorial] Could not remove incomplete staging asset: " + stagedPath);
            RestoreActive(previous);
        }
    }

    private static void Configure(Scene scene)
    {
        if (FindAll<TutorialSystem>(scene).Any() || FindAll<FineDiningTutorialBridge>(scene).Any())
            throw new InvalidOperationException("The source Lobby3 already contains tutorial components; refusing to stack tutorials.");

        var reservations = Require<FineDiningReservations>(scene);
        var kitchen = Require<KitchenManager>(scene);
        var reception = FindAll<ManagementComputerStation>(scene).Single(s => s.Purpose == RestaurantComputerPurpose.Reservations);
        var cashier = FindAll<CashierBoothInteractable>(scene).Single(station => station.name == "Bar Cashier Target");
        var navigation = FindAll<NavMeshSurface>(scene).FirstOrDefault(s => s.agentTypeID == 0 && s.navMeshData != null && s.name == "NavMesh")
            ?? FindAll<NavMeshSurface>(scene).FirstOrDefault(s => s.agentTypeID == 0 && s.navMeshData != null);
        if (navigation == null)
        {
            // Repair only the tutorial copy when the saved restaurant has lost its asset binding.
            var surface = FindAll<NavMeshSurface>(scene).SingleOrDefault(s => s.agentTypeID == 0
                && s.isActiveAndEnabled && s.name == "Fine Dining Navigation");
            var baked = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(
                "Assets/_Project/Scenes/RoleBased/Lobby3-NavMesh.asset");
            if (surface != null && baked != null)
            {
                surface.navMeshData = baked;
                EditorUtility.SetDirty(surface);
                navigation = surface;
            }
        }
        if (navigation == null) throw new InvalidOperationException("Lobby3's baked customer navigation is missing.");
        if (!kitchen.HasDrinkServiceOutput) throw new InvalidOperationException("Lobby3's bar drink pickup/output references are missing.");

        var presentation = (GameObject)PrefabUtility.InstantiatePrefab(RequireAsset<GameObject>(PresentationPath), scene);
        PrefabUtility.UnpackPrefabInstance(presentation, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        presentation.name = "Fine Dining Tutorial Presentation";
        presentation.SetActive(true);
        var dialogue = presentation.GetComponentInChildren<TutorialDialogueUI>(true);
        var hand = presentation.GetComponentInChildren<TutorialHandIndicator>(true);
        var indicator = presentation.GetComponentInChildren<TutorialTargetIndicator>(true);
        var mask = presentation.GetComponentInChildren<TutorialUIFocusMask>(true);
        if (dialogue == null || hand == null || indicator == null || mask == null)
            throw new InvalidOperationException("The existing tutorial presentation is missing its dialogue or guidance components.");
        ClearCamera(hand);
        ClearCamera(indicator);
        var dialogueSO = new SerializedObject(dialogue);
        Property(dialogueSO, "useTutorialPresentation").boolValue = true;
        Property(dialogueSO, "animatePortraitOnEveryLine").boolValue = true;
        Property(dialogueSO, "avoidFocusOverlap").boolValue = true;
        dialogueSO.ApplyModifiedPropertiesWithoutUndo();

        var buttons = presentation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        UnityEngine.UI.Button Button(string name) => buttons.Single(b => b.name == name);
        foreach (var button in buttons) button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        var retry = Button("RETRY CHAPTER");
        var skip = Button("SKIP TRAINING");
        var finish = Button("START DAY 1");
        var yes = Button("SKIP");
        var no = Button("CONTINUE");
        var confirmation = presentation.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Skip Confirmation").gameObject;
        var recovery = presentation.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Recovery message");
        var status = recovery.GetComponentInChildren<TMP_Text>(true);
        var confirmationText = confirmation.GetComponentsInChildren<TMP_Text>(true).Single(t => t.GetComponentInParent<UnityEngine.UI.Button>(true) == null);
        confirmationText.text = "Skip Fine Dining training?";
        confirmation.SetActive(false);
        retry.gameObject.SetActive(false);
        skip.gameObject.SetActive(false);
        finish.gameObject.SetActive(false);
        status.text = "";

        var root = new GameObject("FineDiningTutorial");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<TutorialSceneRuntimeMarker>();
        root.AddComponent<TutorialRestaurantCatalogContext>();
        var day = root.AddComponent<TutorialDayContext>();
        var tutorial = root.AddComponent<TutorialSystem>();
        var bindings = root.AddComponent<TutorialSceneBindings>();
        var bridge = root.AddComponent<FineDiningTutorialBridge>();
        var proxy = new GameObject("Fine Dining tutorial world focus", typeof(RectTransform)).GetComponent<RectTransform>();
        proxy.SetParent(root.transform, false);
        proxy.sizeDelta = Vector2.one; // Geometry only; the shared focus mask owns rendering and input.

        var ts = new SerializedObject(tutorial);
        Set(ts, "dialogueUI", dialogue); Set(ts, "handIndicator", hand); Set(ts, "targetIndicator", indicator);
        Set(ts, "uiFocusMask", mask); Set(ts, "sceneBindings", bindings);
        Set(ts, "cameraController", Require<MainCameraController>(scene)); Set(ts, "tapSelector", Require<TapOutlineSelector>(scene));
        Set(ts, "groupSpawner", Require<GroupSpawner>(scene));
        Set(ts, "lobbyNavigationSurface", navigation); Set(ts, "lobbyNavigationData", navigation.navMeshData);
        Property(ts, "startAutomatically").boolValue = false;
        Property(ts, "restoreAutomaticSpawningAfterOpening").boolValue = false;
        Property(ts, "debugStartEnabled").boolValue = false;
        Property(ts, "explainWithFocus").boolValue = true;
        Property(ts, "focusTransitionDuration").floatValue = .24f;

        var bs = new SerializedObject(bridge);
        Set(bs, "tutorial", tutorial); Set(bs, "day", day); Set(bs, "worldFocusProxy", proxy);
        Set(bs, "controlsCanvas", retry.GetComponentInParent<Canvas>());
        Set(bs, "restart", retry); Set(bs, "skip", skip); Set(bs, "finish", finish);
        Set(bs, "confirmation", confirmation); Set(bs, "confirmSkip", yes); Set(bs, "cancelSkip", no); Set(bs, "status", status);
        Set(bs, "lobbyHUD", Require<LobbyHUDRedesign>(scene)); Set(bs, "reservations", reservations);
        Set(bs, "receptionistDesk", reception); Set(bs, "cashier", cashier); Set(bs, "barPickup", kitchen.drinkPickupApproach);
        Set(bs, "food", RequireAsset<Recipe>(RecipeFolder + "Pumpkin Soup.asset"));
        Set(bs, "drink", RequireAsset<Recipe>(RecipeFolder + "Grape Juice.asset"));
        Set(bs, "water", RequireAsset<Recipe>(RecipeFolder + "Sparkling Water.asset"));
        WriteLessons(ts, bs);
        ts.ApplyModifiedPropertiesWithoutUndo();
        bs.ApplyModifiedPropertiesWithoutUndo();

        // The copy receives the prepared pitcher bindings even if the separate Lobby3 update has not run yet.
        reservations.waterPourProduct = RequireAsset<Recipe>(RecipeFolder + "Sparkling Water.asset");
        reservations.waterPitcherIcon = RequireAsset<Sprite>(ArtFolder + "WaterPitcher.png");
        var pourUI = Require<WinePouringUI>(scene);
        pourUI.waterPitcherSprite = reservations.waterPitcherIcon;
        pourUI.waterFillSprite = RequireAsset<Sprite>(ArtFolder + "WaterFill.png");
        PrefabUtility.RecordPrefabInstancePropertyModifications(pourUI);
        ApplyPresentationCorrections(scene);
    }

    private static void ApplyPresentationCorrections(Scene scene)
    {
        var tutorial = Require<TutorialSystem>(scene);
        var bridge = Require<FineDiningTutorialBridge>(scene);
        var ts = new SerializedObject(tutorial);
        var bs = new SerializedObject(bridge);
        var steps = Property(ts, "steps");
        var lessons = Property(bs, "lessons");
        if (steps.arraySize != lessons.arraySize)
            throw new InvalidOperationException("Tutorial step/lesson count mismatch; refusing to remove a lesson from only one array.");
        for (int i = steps.arraySize - 1; i >= 0; i--)
        {
            if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue != "fd_camera_hint") continue;
            if (lessons.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue != "fd_camera_hint")
                throw new InvalidOperationException("Camera lesson identity does not match its step.");
            steps.DeleteArrayElementAtIndex(i);
            lessons.DeleteArrayElementAtIndex(i);
        }
        bool hasPreciseDrinkWalkthrough = false;
        for (int i = 0; i < steps.arraySize; i++)
            hasPreciseDrinkWalkthrough |= steps.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "fd_congratulations";
        if (!hasPreciseDrinkWalkthrough) WriteLessons(ts, bs);
        BindWaitingTimer(scene, bs);
        var status = Property(bs, "status").objectReferenceValue as TMP_Text;
        status.color = Color.white;
        status.raycastTarget = false;
        EditorUtility.SetDirty(status);
        var furniture = Require<FineDiningSceneBindings>(scene);
        if (furniture.importedModel == null) throw new InvalidOperationException("Fine Dining tutorial model reference is missing.");
        var nodes = furniture.importedModel.GetComponentsInChildren<Transform>(true);
        Set(bs, "receptionFocus", nodes.Single(t => t.name == "Reception"));
        Set(bs, "barFocus", nodes.Single(t => t.name == "Bar"));
        Set(bs, "cashierFocus", FindAll<Transform>(scene).Single(t => t.name == "Fine Dining Cashier Station"));
        Set(bs, "bartenderFocus", FindAll<KitchenWorkerBot>(scene).Single(t => t.EmployeeRole == EmployeeRole.Bartender).transform);
        foreach (string field in new[] { "restart", "skip", "finish", "confirmation" })
        {
            var value = Property(bs, field).objectReferenceValue;
            if (value is Component control) control.gameObject.SetActive(false);
            else if (value is GameObject panel) panel.SetActive(false);
        }
        ts.ApplyModifiedPropertiesWithoutUndo();
        bs.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void BindWaitingTimer(Scene scene, SerializedObject bridge)
    {
        if (Property(bridge, "waitingTimerTemplate").objectReferenceValue != null) return;
        const string blueprint = "Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity";
        Scene previous = SceneManager.GetActiveScene();
        Scene source = SceneManager.GetSceneByPath(blueprint);
        bool opened = !source.IsValid() || !source.isLoaded;
        if (opened) source = EditorSceneManager.OpenScene(blueprint, OpenSceneMode.Additive);
        try
        {
            var sourceBridge = new SerializedObject(Require<FastFoodTutorialBridge>(source));
            var template = Property(sourceBridge, "waitingTimerTemplate").objectReferenceValue as FastFoodCookingTimer;
            if (template == null || template.ring == null || template.seconds == null || template.caption == null)
                throw new InvalidOperationException("The existing tutorial waiting timer is incomplete.");
            var status = Property(bridge, "status").objectReferenceValue as TMP_Text;
            var copy = UnityEngine.Object.Instantiate(template.gameObject, status.transform.parent, false);
            copy.name = "Fine Dining tutorial waiting template";
            copy.SetActive(false);
            Set(bridge, "waitingTimerTemplate", copy.GetComponent<FastFoodCookingTimer>());
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(source, true);
            RestoreActive(previous);
        }
    }

    private sealed class AuthoredLesson
    {
        public string id, message, objective, control;
        public FineDiningTutorialBridge.Chapter chapter;
        public FineDiningTutorialBridge.ActionKind action;
    }

    private static void WriteLessons(SerializedObject tutorial, SerializedObject bridge)
    {
        var lessons = new List<AuthoredLesson>();
        var chapter = FineDiningTutorialBridge.Chapter.Briefing;
        void Add(string id, string message, FineDiningTutorialBridge.ActionKind action = FineDiningTutorialBridge.ActionKind.Explain,
            string control = "", string objective = "") => lessons.Add(new AuthoredLesson
            { id = id, message = message, objective = objective.Length > 0 ? objective : message, control = control, chapter = chapter, action = action });

        Add("fd_welcome", "Welcome to Fine Dining, our final restaurant! You know the basics. Let's focus on reservations and personal drink service.");
        Add("fd_reception_tour", "The receptionist desk handles reservations. Check each party's time and size before choosing its table.", control: "World:Reception");
        Add("fd_cashier_tour", "Your cashier works beside the bar. This is your payment station; use the payment steps you've already learned.", control: "World:Cashier");
        Add("fd_bar_tour", "The bartender prepares extra drink orders. Those trays are collected here at the bar.", control: "World:Bar");

        chapter = FineDiningTutorialBridge.Chapter.Reservation;
        Add("fd_reservation_open", "A reservation request is waiting. Open the receptionist desk.", FineDiningTutorialBridge.ActionKind.OpenReception,
            "World:Reception", "Open reservations at the receptionist desk.");
        Add("fd_reservation_requests", "Requests holds parties waiting for your decision. The number on this tab counts pending requests.", control: "Reservation:RequestsTab");
        Add("fd_reservation_party", "Choose a party from this list to review its visit. The check mark shows the party you're reviewing.", control: "Reservation:Party");
        Add("fd_reservation_review", "Here are the party's name, arrival time and guest count. The table you choose must fit everyone.", control: "Reservation:SelectedParty");
        Add("fd_reservation_status", "REQUESTED means the booking still needs a decision. Its status changes after you confirm it.", control: "Reservation:SelectedPartyStatus");
        Add("fd_reservation_decline", "Decline opens a confirmation when you cannot accommodate a party. Keep this request for our seating lesson.", control: "Reservation:Decline");
        Add("fd_reservation_tables", "These are the restaurant's tables. Compare their pictures, names and seating capacities before choosing.", control: "Reservation:TableCard");
        Add("fd_reservation_capacity", "Seats tells you how many guests fit. Choose a table with room for the whole party.", control: "Reservation:TableCapacity");
        Add("fd_reservation_availability", "This status tells you whether the table can take this booking. An unavailable table cannot be selected.", control: "Reservation:TableStatus");
        Add("fd_reservation_summary", "This summary counts booked guests and expected walk-ins, so you can prepare for tonight's demand.", control: "Reservation:Summary");
        Add("fd_reservation_select", "Use this table's Select button. Selection chooses the table; it does not confirm the reservation yet.", FineDiningTutorialBridge.ActionKind.SelectReservationTable,
            "Reservation:SelectTable", "Select a suitable table for this party.");
        Add("fd_reservation_accept", "Confirm Reservation accepts the request and holds the selected table for the booked visit.", FineDiningTutorialBridge.ActionKind.AcceptReservation,
            "Reservation:Confirm", "Confirm this reservation at the selected table.");
        Add("fd_reservation_tonight_open", "The accepted booking is now in Tonight. Open that tab to review it.", FineDiningTutorialBridge.ActionKind.ViewTonight,
            "Reservation:TonightTab", "Open Tonight to view the accepted booking.");
        Add("fd_reservation_tonight_party", "Tonight lists accepted visits. Select a party here to follow its booking.", control: "Reservation:Party");
        Add("fd_reservation_protection", "Here are its arrival time, guest count and assigned table. That table is held for this visit.", control: "Reservation:PartyDetails");
        Add("fd_reservation_tonight_status", "This status follows the party's arrival and seating. Use Tonight to keep track of booked visits during service.", control: "Reservation:PartyStatus");
        Add("fd_reservation_close", "Close reservations. Let's welcome the booked party.", FineDiningTutorialBridge.ActionKind.CloseReception,
            "Reservation:Back", "Close reservations to return to the restaurant.");
        Add("fd_reserved_arrival", "Match the arriving party to its booking, then seat it at the reserved table.", FineDiningTutorialBridge.ActionKind.SeatReservation,
            "World:Party", "Seat the booked party at its reserved table.");
        Add("fd_reservation_done", "Good! Their reservation is seated. A seating prompt only appears when a suitable table is available.", control: "World:Party");

        chapter = FineDiningTutorialBridge.Chapter.Drinks;
        Add("fd_extra_drink_intro", "This table wants another drink during its meal. The DRINK bubble marks an extra request.", control: "World:Party");
        Add("fd_extra_drink_open", "Select the DRINK bubble to hear this table's extra request.", FineDiningTutorialBridge.ActionKind.OpenDrinkOrder,
            "Drink:Order", "Open this table's DRINK request.");
        Add("fd_extra_drink_quantity", "Add the requested drink with this plus button. Match the quantity shown beside the customer's request.", FineDiningTutorialBridge.ActionKind.SelectDrinkQuantity,
            "Drink:Quantity", "Match the customer's requested drink quantity.");
        Add("fd_extra_drink_check", "Use CHECK ORDER to review the drink before sending it to the bar.", FineDiningTutorialBridge.ActionKind.ReviewDrinkOrder,
            "Drink:Check", "Check this extra drink order.");
        Add("fd_extra_drink_order", "Confirm the checked drink order. Your bartender will prepare it at the bar.", FineDiningTutorialBridge.ActionKind.TakeDrinkOrder,
            "Drink:Confirm", "Confirm the extra DRINK order for the bartender.");
        Add("fd_extra_drink_pickup", "Collect this table's ready drink from the bar.", FineDiningTutorialBridge.ActionKind.PickUpDrink,
            "World:Bar", "Collect the prepared drink at the bar.");
        Add("fd_extra_drink_deliver", "Click this table's number bubble to carry the drink to the correct guests. Match it to the number on your tray.", FineDiningTutorialBridge.ActionKind.DeliverDrink,
            "World:TableNumber", "Click the table number to deliver its extra drink.");
        Add("fd_extra_drink_done", "Exactly. Extra drink orders use the bar, and the drink is added to that table's bill.", control: "World:Party");

        chapter = FineDiningTutorialBridge.Chapter.Pour;
        Add("fd_pour_intro", "A different table wants a personal pour. This request is served at the table, without a new bar ticket.", control: "World:Party");
        Add("fd_pour_open", "Select POUR and walk over to serve this table.", FineDiningTutorialBridge.ActionKind.OpenPour,
            "World:Party", "Open the table's POUR request.");
        Add("fd_pour_pitcher", "This pitcher is the control. Press and hold it, then drag it toward the glass.", control: "Pour:Pitcher");
        Add("fd_pour_glass", "Place the pitcher's spout above this glass. The pitcher tilts and the stream starts when it is in position.", control: "Pour:Glass");
        Add("fd_pour_line", "This mark is the target fill. Keep holding the pitcher in pouring position until the water reaches this line.", control: "Pour:Line");
        Add("fd_pour_fill", "", FineDiningTutorialBridge.ActionKind.Pour, "Pour:Glass", "Drag the pitcher as shown, then hold it steady until the glass reaches the marked line.");
        Add("fd_pour_done", "Nicely poured! Personal service can earn a 500 to 1,000 peso tip. Wine refills use the same pouring gesture.", control: "World:Party");
        Add("fd_congratulations", "Congratulations! You've completed Fine Dining training and learned every restaurant. You're ready to welcome booked parties, serve extra drinks and pour at the table.");

        chapter = FineDiningTutorialBridge.Chapter.Complete;
        Add("fd_complete", "Good luck with your final restaurant! Continue when you're ready to begin your Fine Dining shift.");

        Sprite welcome = RequireAsset<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Welcome  Greeting Pose.png");
        Sprite explaining = RequireAsset<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
        Sprite success = RequireAsset<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Success  Thumbs Up Pose.png");
        var steps = Property(tutorial, "steps");
        var data = Property(bridge, "lessons");
        steps.arraySize = data.arraySize = lessons.Count;
        for (int i = 0; i < lessons.Count; i++)
        {
            var lesson = lessons[i];
            var step = steps.GetArrayElementAtIndex(i);
            var item = data.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("id").stringValue = lesson.id;
            item.FindPropertyRelative("chapter").enumValueIndex = (int)lesson.chapter;
            item.FindPropertyRelative("action").enumValueIndex = (int)lesson.action;
            item.FindPropertyRelative("control").stringValue = lesson.control;
            step.FindPropertyRelative("id").stringValue = lesson.id;
            step.FindPropertyRelative("speaker").stringValue = "Big Boss";
            step.FindPropertyRelative("message").stringValue = lesson.message;
            step.FindPropertyRelative("objective").stringValue = lesson.objective;
            step.FindPropertyRelative("portrait").objectReferenceValue = i == 0 ? welcome :
                lesson.id.EndsWith("_done", StringComparison.Ordinal) || lesson.id == "fd_complete" || lesson.id == "fd_congratulations" ? success : explaining;
            step.FindPropertyRelative("phase").enumValueIndex = (int)PhaseFor(lesson.chapter);
            bool action = lesson.action != FineDiningTutorialBridge.ActionKind.Explain;
            bool pour = lesson.action == FineDiningTutorialBridge.ActionKind.Pour;
            bool target = !string.IsNullOrEmpty(lesson.control);
            step.FindPropertyRelative("stepType").enumValueIndex = (int)(action ? TutorialSystem.TutorialStepType.WaitForGameplayAction : TutorialSystem.TutorialStepType.ManualContinue);
            step.FindPropertyRelative("actionKey").stringValue = action ? lesson.id : "";
            step.FindPropertyRelative("restrictUnrelatedInteractions").boolValue = target;
            step.FindPropertyRelative("explainsAction").boolValue = true;
            step.FindPropertyRelative("hintMode").enumValueIndex = (int)(pour ? TutorialSystem.TutorialHintMode.Drag : action ? TutorialSystem.TutorialHintMode.Tap : TutorialSystem.TutorialHintMode.None);
            step.FindPropertyRelative("uiTargetKey").stringValue = pour ? "FD.Source" : target ? "FD.Target" : "";
            step.FindPropertyRelative("worldTargetKey").stringValue = target ? "FD.Target" : "";
            step.FindPropertyRelative("uiFocusTarget").objectReferenceValue = null;
            step.FindPropertyRelative("highlightTarget").objectReferenceValue = null;
            step.FindPropertyRelative("requiredContext").objectReferenceValue = null;
            step.FindPropertyRelative("requiredAction").enumValueIndex = (int)TutorialSystem.TutorialAction.None;
            step.FindPropertyRelative("isPlaceholder").boolValue = false;
            step.FindPropertyRelative("enableStaffSpawningOnComplete").boolValue = false;
            step.FindPropertyRelative("enableCustomerSpawningOnComplete").boolValue = false;
        }
    }

    private static TutorialSystem.TutorialPhase PhaseFor(FineDiningTutorialBridge.Chapter chapter) => chapter switch
    {
        FineDiningTutorialBridge.Chapter.Reservation => TutorialSystem.TutorialPhase.FineDiningReservations,
        FineDiningTutorialBridge.Chapter.Drinks => TutorialSystem.TutorialPhase.FineDiningDrinks,
        FineDiningTutorialBridge.Chapter.Pour => TutorialSystem.TutorialPhase.FineDiningPour,
        FineDiningTutorialBridge.Chapter.Complete => TutorialSystem.TutorialPhase.Completed,
        _ => TutorialSystem.TutorialPhase.FineDiningBriefing
    };

    private static void Validate(Scene scene)
    {
        var tutorial = FindAll<TutorialSystem>(scene).Single();
        var bridge = FindAll<FineDiningTutorialBridge>(scene).Single();
        var ts = new SerializedObject(tutorial); var bs = new SerializedObject(bridge);
        foreach (string key in new[] { "dialogueUI", "handIndicator", "targetIndicator", "uiFocusMask", "sceneBindings", "cameraController", "tapSelector", "groupSpawner", "lobbyNavigationSurface", "lobbyNavigationData" })
            RequireReference(ts, key);
        foreach (string key in new[] { "tutorial", "day", "controlsCanvas", "restart", "skip", "finish", "confirmation", "confirmSkip", "cancelSkip", "status", "waitingTimerTemplate", "lobbyHUD", "worldFocusProxy", "receptionistDesk", "cashier", "barPickup", "receptionFocus", "barFocus", "cashierFocus", "bartenderFocus", "reservations", "food", "drink", "water" })
            RequireReference(bs, key);
        var timer = Property(bs, "waitingTimerTemplate").objectReferenceValue as FastFoodCookingTimer;
        if (timer == null || timer.gameObject.scene != scene || timer.ring == null || timer.seconds == null || timer.caption == null)
            throw new InvalidOperationException("Tutorial waiting timer bindings are incomplete.");
        var cashier = Property(bs, "cashier").objectReferenceValue as CashierBoothInteractable;
        if (cashier == null || cashier.gameObject.scene != scene || cashier.name != "Bar Cashier Target")
            throw new InvalidOperationException("Tutorial cashier guidance must reference this scene's Bar Cashier Target.");
        var steps = Property(ts, "steps"); var lessons = Property(bs, "lessons");
        if (steps.arraySize == 0 || steps.arraySize != lessons.arraySize) throw new InvalidOperationException("Tutorial step/lesson count mismatch.");
        var ids = new HashSet<string>();
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i); var lesson = lessons.GetArrayElementAtIndex(i);
            string id = step.FindPropertyRelative("id").stringValue;
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || id != lesson.FindPropertyRelative("id").stringValue)
                throw new InvalidOperationException("Invalid tutorial step identity: " + id);
            var chapter = (FineDiningTutorialBridge.Chapter)lesson.FindPropertyRelative("chapter").enumValueIndex;
            if (step.FindPropertyRelative("phase").enumValueIndex != (int)PhaseFor(chapter) || step.FindPropertyRelative("portrait").objectReferenceValue == null)
                throw new InvalidOperationException("Tutorial phase or Big Boss portrait is missing: " + id);
            bool action = lesson.FindPropertyRelative("action").enumValueIndex != (int)FineDiningTutorialBridge.ActionKind.Explain;
            if (action && step.FindPropertyRelative("actionKey").stringValue != id)
                throw new InvalidOperationException("Gameplay action is not bound: " + id);
        }
        var canvas = ((TutorialDialogueUI)Property(ts, "dialogueUI").objectReferenceValue).GetComponentInParent<Canvas>().rootCanvas;
        if (canvas.GetComponent<CanvasScaler>() == null || canvas.GetComponent<GraphicRaycaster>() == null || canvas.GetComponentInChildren<UIScreenSafeArea>(true) == null)
            throw new InvalidOperationException("Tutorial presentation lost its responsive Canvas or safe area.");
        var kitchen = Require<KitchenManager>(scene);
        var reservations = Require<FineDiningReservations>(scene);
        int partySize = Mathf.Max(1, Property(bs, "partySize").intValue);
        if (!Require<WinePouringUI>(scene).SupportsWaterService || !kitchen.HasDrinkServiceOutput || kitchen.foodTrayPrefab == null)
            throw new InvalidOperationException("Tutorial drink service is missing its real pouring or bar bindings.");
        if (reservations.dayManager == null || reservations.receptionLine == null || reservations.waitingArea == null ||
            reservations.waitingArea.reservations != reservations || reservations.waitingArea.receptionLine != reservations.receptionLine ||
            reservations.waitingArea.seats == null || reservations.waitingArea.seats.Count(s => s != null && s.seat != null && s.approach != null) < partySize)
            throw new InvalidOperationException("Tutorial reservation arrival is missing its day, line or waiting-seat bindings.");
        if (reservations.tables == null || !reservations.tables.Any(t => t != null && t.unlockDay <= 1 &&
            (t.equipment == null || !string.IsNullOrWhiteSpace(t.equipment.itemID)) && t.Capacity >= partySize &&
            t.booth.NetworkTrayPoint != null && (t.booth.customerApproachPoint != null || t.booth.approachPoint != null)))
            throw new InvalidOperationException("Tutorial diners need a complete opening-day table with equipment identity, seats, customer approach and tray position.");
        if (FindAll<TutorialManager>(scene).Any(manager => manager.enabled && manager.gameObject.activeInHierarchy))
            throw new InvalidOperationException("The copied restaurant contains an active legacy tutorial; refusing to run two tutorial flows.");
    }

    private static void RegisterBuildScene()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(s => s.path == ScenePath);
        if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
        else return;
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void RestoreActive(Scene scene) { if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene); }
    private static IEnumerable<T> FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static T Require<T>(Scene scene) where T : Component => FindAll<T>(scene).FirstOrDefault() ?? throw new InvalidOperationException("Lobby3 is missing " + typeof(T).Name + ".");
    private static T RequireAsset<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Required tutorial asset missing: " + path);
    private static SerializedProperty Property(SerializedObject owner, string key) => owner.FindProperty(key) ?? throw new InvalidOperationException(owner.targetObject.GetType().Name + " is missing field " + key);
    private static void Set(SerializedObject owner, string key, UnityEngine.Object value) => Property(owner, key).objectReferenceValue = value;
    private static void RequireReference(SerializedObject owner, string key)
    {
        if (Property(owner, key).objectReferenceValue == null) throw new InvalidOperationException("Tutorial reference missing: " + key);
    }
    private static void ClearCamera(Component owner)
    {
        var serialized = new SerializedObject(owner);
        var camera = serialized.FindProperty("worldCamera");
        if (camera == null) return;
        camera.objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
