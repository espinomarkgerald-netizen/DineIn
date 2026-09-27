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

public static class FastFoodTutorialAuthoring
{
    public const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity";
    private const string SourcePath = "Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity";
    private static readonly List<FastFoodTutorialBridge.Lesson> lessons = new();
    private static readonly List<string> messages = new();
    private static readonly List<string> objectives = new();
    private static Recipe burger, fries, chicken, fish, drink;
    private static FastFoodTutorialBridge.Chapter chapter;
    private static Sprite welcome, explaining, success;

    [MenuItem("Dine In/Tutorial/Match Fast Food Big Boss Presentation")]
    public static string MatchBigBossPresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        const string path = "Assets/_Project/Tutorials/FastFoodTutorialPresentation.prefab";
        var source = EditorSceneManager.OpenPreviewScene(SourcePath);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MatchBigBossPresentation(root, source);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "Matched Big Boss dialogue scale and bottom anchoring to Casual Dining; lessons unchanged.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); EditorSceneManager.ClosePreviewScene(source); }
    }

    private static void MatchBigBossPresentation(GameObject root, Scene source)
    {
        var original = Find<TutorialDialogueUI>(source);
        var sourceScaler = original.GetComponentInParent<CanvasScaler>();
        var targetScaler = root.GetComponent<CanvasScaler>();
        // The original CanvasMainHUD is 800x450 Expand, independently of LobbyHUDRedesign.
        // Convert only the dialogue's authored units; do not enlarge recovery controls or hints.
        if (sourceScaler.screenMatchMode != targetScaler.screenMatchMode ||
            Mathf.Abs(sourceScaler.referenceResolution.x / sourceScaler.referenceResolution.y -
                targetScaler.referenceResolution.x / targetScaler.referenceResolution.y) > .001f)
            throw new InvalidOperationException("Dialogue canvases must use matching aspect and screen-match mode.");
        float units = targetScaler.referenceResolution.y / sourceScaler.referenceResolution.y;
        var from = (RectTransform)original.transform;
        var to = (RectTransform)root.GetComponentInChildren<TutorialDialogueUI>(true).transform;
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta;
        to.localScale = from.localScale * units;
        to.anchoredPosition = from.anchoredPosition * units;
    }

    [MenuItem("Dine In/Tutorial/Polish Existing Fast Food Tutorial")]
    public static string PolishExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) throw new InvalidOperationException("Open Lobby2Tutorial first.");
        var bridge = Find<FastFoodTutorialBridge>(scene);
        var tutorial = Find<TutorialSystem>(scene);
        if (bridge == null || tutorial == null) throw new InvalidOperationException("Author the tutorial first.");
        const string path = "Assets/_Project/Tutorials/FastFoodTutorialPresentation.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Controls use HUD units; the dialogue separately retains Casual Dining proportions.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var source = EditorSceneManager.OpenPreviewScene(SourcePath);
            try { MatchBigBossPresentation(root, source); }
            finally { EditorSceneManager.ClosePreviewScene(source); }
            var dialogue = new SerializedObject(root.GetComponentInChildren<TutorialDialogueUI>(true));
            dialogue.FindProperty("animatePortraitOnEveryLine").boolValue = true;
            dialogue.FindProperty("avoidFocusOverlap").boolValue = true;
            dialogue.FindProperty("focusClearance").floatValue = 16f;
            dialogue.ApplyModifiedPropertiesWithoutUndo();
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "ENTER KITCHEN") { UnityEngine.Object.DestroyImmediate(button.gameObject); continue; }
                if (button.name != "RETRY CHAPTER" && button.name != "SKIP TRAINING") continue;
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(button.name == "RETRY CHAPTER" ? .42f : .58f, .075f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(220,64);
                var label = button.GetComponentInChildren<TMP_Text>(); label.fontSizeMin = 18; label.fontSizeMax = 24;
                button.gameObject.SetActive(false);
            }
            var message = root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Recovery message");
            message.anchorMin = message.anchorMax = new Vector2(.5f,.17f);
            message.anchoredPosition = Vector2.zero; message.sizeDelta = new Vector2(620,70);
            var status = message.GetComponentInChildren<TMP_Text>(true);
            status.enableAutoSizing = true; status.fontSizeMin = 18; status.fontSizeMax = 24; status.text = "";
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var hud = Find<LobbyHUDRedesign>(scene);
        foreach (var basket in FindAll<FastFoodFryerBasket>(scene))
        {
            var mesh = basket.GetComponent<MeshFilter>()?.sharedMesh;
            var collider = basket.GetComponent<BoxCollider>();
            if (mesh == null || collider == null) continue;
            var scale = basket.transform.lossyScale;
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size + new Vector3(.05f / Mathf.Max(.0001f, Mathf.Abs(scale.x)), .1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), .05f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            collider.enabled = false; // The active Fryer view enables its own hit targets.
        }
        if (hud != null) hud.ConfigureKitchenButtonForEditor(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/GameIcons/HUD/Spatula.png"));
        var tso = new SerializedObject(tutorial); var bso = new SerializedObject(bridge);
        Set(bso,"lobbyHUD",hud);
        LoadRecipes();
        welcome=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Welcome  Greeting Pose.png");
        explaining=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
        success=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Success  Thumbs Up Pose.png");
        BuildLessons(); WriteLessons(tso,bso);
        tso.ApplyModifiedPropertiesWithoutUndo(); bso.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return ValidateScene();
    }

    [MenuItem("Dine In/Tutorial/Author Fast Food Tutorial")]
    public static string Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (Find<FastFoodTutorialBridge>(scene) != null) throw new InvalidOperationException("Tutorial already authored; edit its serialized steps instead of replacing it.");
        var view = Find<FastFoodCookingView>(scene);
        var cooking = Find<FastFoodCookingController>(scene);
        if (view == null || cooking == null || !view.IsAuthored) throw new InvalidOperationException("The existing kitchen must be authored.");
        Scene source = EditorSceneManager.OpenPreviewScene(SourcePath);
        try
        {
            var sourceTutorial = Find<TutorialSystem>(source);
            var sourceSO = new SerializedObject(sourceTutorial);
            var presentation = new GameObject("Fast Food Tutorial Presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(presentation, scene);
            var canvas = presentation.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32759;
            var scaler = presentation.GetComponent<CanvasScaler>();
            var sourceCanvas = Find<TutorialDialogueUI>(source).GetComponentInParent<Canvas>();
            var sourceScaler = sourceCanvas != null ? sourceCanvas.GetComponentInParent<CanvasScaler>() : null;
            if (sourceScaler != null) EditorUtility.CopySerialized(sourceScaler, scaler);
            else { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f; }
            var safe = Rect(presentation.transform, "SafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<UIScreenSafeArea>().Capture(new[] { safe });
            var dialogue = Copy<TutorialDialogueUI>(sourceSO, "dialogueUI", safe);
            var hand = Copy<TutorialHandIndicator>(sourceSO, "handIndicator", safe);
            var indicator = Copy<TutorialTargetIndicator>(sourceSO, "targetIndicator", safe);
            TutorialUIFocusMask mask;
            var maskSource = sourceSO.FindProperty("uiFocusMask").objectReferenceValue as TutorialUIFocusMask;
            if (maskSource != null) mask = UnityEngine.Object.Instantiate(maskSource.gameObject, safe).GetComponent<TutorialUIFocusMask>();
            else mask = TutorialUIFocusMask.Create(safe);
            mask.name = "TutorialUIFocusMask"; mask.gameObject.SetActive(true);
            var dso = new SerializedObject(dialogue); dso.FindProperty("useTutorialPresentation").boolValue = true; dso.ApplyModifiedPropertiesWithoutUndo();
            ClearCamera(hand); ClearCamera(indicator);
            var oldDialogue = FindAll<TutorialDialogueUI>(scene).Where(d => d != dialogue).ToArray();
            foreach (var old in oldDialogue) old.gameObject.SetActive(false);

            var system = new GameObject("FastFoodTutorial");
            SceneManager.MoveGameObjectToScene(system, scene);
            var context = system.AddComponent<TutorialRestaurantCatalogContext>();
            var day = system.AddComponent<TutorialDayContext>();
            var tutorial = system.AddComponent<TutorialSystem>();
            var bindings = system.AddComponent<TutorialSceneBindings>();
            var bridge = system.AddComponent<FastFoodTutorialBridge>();
            var tso = new SerializedObject(tutorial);
            Set(tso, "dialogueUI", dialogue); Set(tso, "handIndicator", hand); Set(tso, "targetIndicator", indicator);
            Set(tso, "uiFocusMask", mask); Set(tso, "sceneBindings", bindings);
            Set(tso, "cameraController", Find<MainCameraController>(scene));
            Set(tso, "tapSelector", Find<TapOutlineSelector>(scene));
            Set(tso, "groupSpawner", Find<GroupSpawner>(scene));
            tso.FindProperty("startAutomatically").boolValue = false;
            tso.FindProperty("restoreAutomaticSpawningAfterOpening").boolValue = false;
            tso.FindProperty("debugStartEnabled").boolValue = false;

            var controls = Rect(safe, "Training Controls", Vector2.zero, Vector2.one);
            var controlCanvas = controls.gameObject.AddComponent<Canvas>(); controlCanvas.overrideSorting = true; controlCanvas.sortingOrder = 32762;
            controls.gameObject.AddComponent<GraphicRaycaster>();
            var template = ((RectTransform)new SerializedObject(view).FindProperty("station").objectReferenceValue)
                .GetComponentsInChildren<Button>(true).First(b => b.name == "Stations");
            var retry = Button(template, controls, "RETRY CHAPTER", new Vector2(.13f,.03f), new Vector2(230,52));
            var skip = Button(template, controls, "SKIP TRAINING", new Vector2(.87f,.03f), new Vector2(230,52));
            var finish = Button(template, controls, "START DAY 1", new Vector2(.5f,.35f), new Vector2(300,64)); finish.gameObject.SetActive(false);
            var statusRoot = Rect(controls, "Recovery message", new Vector2(.25f,.09f), new Vector2(.75f,.16f));
            var text = UnityEngine.Object.Instantiate(template.GetComponentInChildren<TMP_Text>().gameObject, statusRoot).GetComponent<TMP_Text>();
            var textRect = (RectTransform)text.transform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            text.text = ""; text.fontSize = 24; text.raycastTarget = false;
            var confirm = Rect(controls, "Skip Confirmation", new Vector2(.3f,.35f), new Vector2(.7f,.58f));
            var background = confirm.gameObject.AddComponent<Image>(); EditorUtility.CopySerialized(template.GetComponent<Image>(), background); background.raycastTarget = true;
            var title = UnityEngine.Object.Instantiate(text.gameObject, confirm).GetComponent<TMP_Text>();
            title.text = "Skip kitchen training?"; ((RectTransform)title.transform).anchorMin = new Vector2(.05f,.55f); ((RectTransform)title.transform).anchorMax = new Vector2(.95f,.95f);
            var yes = Button(template, confirm, "SKIP", new Vector2(.72f,.25f), new Vector2(170,52));
            var no = Button(template, confirm, "CONTINUE", new Vector2(.28f,.25f), new Vector2(170,52));
            confirm.gameObject.SetActive(false);
            var bso = new SerializedObject(bridge);
            Set(bso,"kitchen",cooking); Set(bso,"view",view); Set(bso,"tutorial",tutorial); Set(bso,"day",day);
            Set(bso,"controlsCanvas",controlCanvas); Set(bso,"restart",retry); Set(bso,"skip",skip); Set(bso,"lobbyHUD",Find<LobbyHUDRedesign>(scene)); Set(bso,"finish",finish);
            Set(bso,"confirmation",confirm.gameObject); Set(bso,"confirmSkip",yes); Set(bso,"cancelSkip",no); Set(bso,"status",text);
            LoadRecipes();
            Set(bso,"burger",burger); Set(bso,"fries",fries); Set(bso,"chicken",chicken); Set(bso,"fish",fish); Set(bso,"drink",drink);
            welcome = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Welcome  Greeting Pose.png");
            explaining = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
            success = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Success  Thumbs Up Pose.png");
            BuildLessons();
            WriteLessons(tso,bso);
            tso.ApplyModifiedPropertiesWithoutUndo(); bso.ApplyModifiedPropertiesWithoutUndo();
            // Saved objects are immediately inspectable. Runtime binding never rebuilds this presentation.
            const string prefabPath = "Assets/_Project/Tutorials/FastFoodTutorialPresentation.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(presentation, prefabPath, InteractionMode.AutomatedAction);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Authored " + lessons.Count + " editable tutorial steps in " + ScenePath;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }
    private static void LoadRecipes()
    {
        var all = AssetDatabase.FindAssets("t:Recipe", new[] {"Assets/_Project"}).Select(g=>AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(r=>r!=null && r.restaurantType==RestaurantType.FastFood).ToArray();
        burger=all.First(r=>r.kitchenItemType==ItemTypeKitchen.Burger);
        fries=all.First(r=>r.ProductId=="03");
        chicken=all.First(r=>r.DisplayName.IndexOf("Chicken Sandwich",StringComparison.OrdinalIgnoreCase)>=0);
        fish=all.First(r=>r.DisplayName.IndexOf("Fish Fillet Sandwich",StringComparison.OrdinalIgnoreCase)>=0);
        drink=all.First(r=>r.category==MenuProductCategory.Drink && r.availableOnMenu && r.ingredients.Count>0);
    }
    private static void Add(string id, string message, FastFoodTutorialBridge.ActionKind action=FastFoodTutorialBridge.ActionKind.Explain,
        Recipe recipe=null, int portion=0, int slot=0, int ingredient=0, FastFoodStationMode station=FastFoodStationMode.None, string control="", string objective="")
    {
        lessons.Add(new FastFoodTutorialBridge.Lesson {id=id,chapter=chapter,action=action,recipe=recipe,portion=portion,slot=slot,ingredient=ingredient,station=station,control=control});
        messages.Add(message); objectives.Add(objective.Length>0?objective:message);
    }
    private static void Station(FastFoodStationMode mode, bool arrow = false) => Add("ff_"+chapter+"_station",
        arrow ? "Use the highlighted station arrow to go to "+(mode==FastFoodStationMode.Fry?"Fryer":mode.ToString())+"." : "Choose Grill.",
        FastFoodTutorialBridge.ActionKind.Station,station:mode,control:arrow?"StationArrow":"Enter "+mode);
    private static void Cook(Recipe recipe,int n,bool burn=false)
    {
        for(int i=0;i<n;i++) Add("ff_"+chapter+"_load_"+i,i==0?"Drag the raw ingredient from the hotbar onto the highlighted cooking spot.":"",
            FastFoodTutorialBridge.ActionKind.Load,recipe,i,i,objective:"Load cooking spot "+(i+1)+" with the highlighted raw ingredient.");
        for(int i=0;i<n;i++)
        {
            Add("ff_"+chapter+"_wait_"+i,burn?"This first burn is intentional. Leave this training patty on the heat to see what happens.":i==0&&chapter!=FastFoodTutorialBridge.Chapter.Parallel?"The timer shows the cooking progress. Wait until the food is ready.":"",
                burn?FastFoodTutorialBridge.ActionKind.Burn:FastFoodTutorialBridge.ActionKind.Ready,recipe,i,i,control:"World:Food",objective:burn?"Wait for this training patty to burn.":"Wait for cooking spot "+(i+1)+" to finish.");
            if(!burn) Add("ff_"+chapter+"_collect_"+i,i==0?"{INTERACT} the ready "+(FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry?"raised basket to collect the fries.":"patty to collect it."):"",
                FastFoodTutorialBridge.ActionKind.Collect,recipe,i,i,objective:"{INTERACT} the highlighted ready "+(FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry?"basket.":"patty."));
        }
    }
    private static void Assemble(Recipe recipe,int portion,int slot,int start=0)
    {
        var steps=FastFoodCookingState.AssemblySteps(recipe);
        for(int i=start;i<steps.Count;i++) Add("ff_"+chapter+"_"+recipe.ProductId+"_"+portion+"_ingredient_"+i,
            chapter==FastFoodTutorialBridge.Chapter.Burger?"Add "+steps[i].label+" to the highlighted prep position.":"",
            FastFoodTutorialBridge.ActionKind.Assemble,recipe,portion,slot,i,
            objective:"Prep position "+(slot+1)+": add "+steps[i].label+" ("+(i+1)+"/"+steps.Count+").");
    }
    private static void BuildLessons()
    {
        lessons.Clear();messages.Clear();objectives.Clear();
        chapter=FastFoodTutorialBridge.Chapter.Briefing;
        Add("ff_welcome","Welcome to Fast Food! Let's learn how this kitchen works.");
        Add("ff_lobby_recap","You know the restaurant basics. Here we'll focus on cooking, preparing food, and serving complete kitchen orders.");
        Add("ff_hud_pause","Use Pause whenever you need a break. Resume returns to the same lesson.",control:"Pause");
        Add("ff_hud_task","The TASK clipboard holds your current objective. Check it whenever you're unsure what comes next.",control:"Task");
        Add("ff_enter","The Kitchen button on your HUD opens the kitchen. Select it now.",FastFoodTutorialBridge.ActionKind.Open,control:"Enter");
        Add("ff_kitchen_welcome","Welcome to your kitchen! This station list lets you choose where to help. We'll start with Grill.",control:"Selection");
        chapter=FastFoodTutorialBridge.Chapter.Burger;Station(FastFoodStationMode.Grill);
        Add("ff_hud_header","The blue panel names your station and current food. Its count and progress bar show your progress.",control:"Header");
        Add("ff_hotbar","This hotbar holds your ingredients. Cells keep their positions so you can find the same ingredient quickly.",control:"Hotbar");
        Add("ff_hotbar_counts","This is a raw patty. The number shows how many you have available.",recipe:burger,control:"Raw");
        Add("ff_hotbar_empty","Cooked ingredients have a separate READY cell. A dim cell with zero cannot be used yet.",recipe:burger,control:"ProteinCell");
        Add("ff_grill_area","Each cooking spot can hold one patty. Let's cook one first, then prepare its burger.",control:"World:Cooking");
        Cook(burger,1);
        Add("ff_prep_area","The view now follows your food to the prep table. Build the burger here in recipe order.",control:"World:Prep");
        Add("ff_ready_ingredient","Your collected patty is now available in the READY cell. Use it after the bottom bun.",recipe:burger,control:"ProteinCell");
        Assemble(burger,0,0);
        Add("ff_burger_done","Good! The burger is assembled. Its pickup transfers it to ready supply so the prep position can be used again.");
        chapter=FastFoodTutorialBridge.Chapter.Parallel;
        Add("ff_parallel_intro","Load three cooking spots before collecting. Each patty cooks independently.",control:"World:Cooking");
        Cook(burger,3);
        Add("ff_three_slots","There are three independent prep positions. Start each with a bottom bun, then follow the pointer to finish each burger.",control:"World:Prep");
        for(int i=0;i<3;i++) Add("ff_parallel_bun_"+i,"",FastFoodTutorialBridge.ActionKind.Assemble,burger,i,i,0,objective:"Start a burger in prep position "+(i+1)+": bottom bun.");
        for(int i=0;i<3;i++)Assemble(burger,i,i,1);
        Add("ff_parallel_done","Each position keeps its own recipe progress. Completed food transfers to ready supply.");
        chapter=FastFoodTutorialBridge.Chapter.Sandwiches;
        foreach(var r in new[]{chicken,fish})
        {
            Add("ff_protein_"+r.ProductId,"Stay at Grill. Staff prepare the protein for "+r.DisplayName+". Wait here until it is ready.",FastFoodTutorialBridge.ActionKind.Protein,r,objective:"Wait here for the cooked sandwich ingredient.");
            Add("ff_sandwich_ready_"+r.ProductId,"The cooked protein is here. Follow the pointer: bottom bun, cooked protein, then top bun.",recipe:r,control:"ProteinCell");
            Assemble(r,0,r==chicken?0:1);
        }
        Add("ff_sandwich_done","That's the sandwich sequence: staff fry the protein, and you assemble it here.");
        chapter=FastFoodTutorialBridge.Chapter.Burn;Cook(burger,1,true);
        Add("ff_discard","Drag the burnt patty to the discard area.",FastFoodTutorialBridge.ActionKind.Discard,burger);
        Add("ff_reload","The cooking spot is free. Load a fresh patty.",FastFoodTutorialBridge.ActionKind.Load,burger);
        Add("ff_retry_wait","",FastFoodTutorialBridge.ActionKind.Ready,burger,objective:"Wait for the replacement patty to finish cooking.");
        Add("ff_retry_collect","{INTERACT} the ready patty. During guided training, it stays ready while you read.",FastFoodTutorialBridge.ActionKind.Collect,burger);
        Add("ff_recovery_prep","Finish the replacement burger: bottom bun, cooked patty, cheese, then top bun.",control:"World:Prep");
        Assemble(burger,0,0);
        chapter=FastFoodTutorialBridge.Chapter.Fries;
        Add("ff_hud_navigation","These SWITCH STATION arrows move directly between workstations. Use the highlighted arrow next to go to Fryer.",station:FastFoodStationMode.Fry,control:"StationArrow");
        Station(FastFoodStationMode.Fry,true);
        Add("ff_fryer_ui","Each basket holds food while it cooks. A ready basket rises out of the oil for collection.",control:"World:Basket");
        Add("ff_fryer_raw","Use these raw fries to load a basket.",recipe:fries,control:"Raw");
        Cook(fries,1);
        Add("ff_fries_done","Collected fries go onto this drying rack and become ready supply for the Assembler.",control:"World:Rack");
        chapter=FastFoodTutorialBridge.Chapter.Serving;Station(FastFoodStationMode.Assembler,true);
        Add("ff_ticket","This active order needs 1 Burger, 1 Fries, and 1 Coke. Each quantity shows how much is already on the tray.",control:"Ticket");
        Add("ff_tray_ui","This tray holds the order. Completing food, putting it on this tray, and serving are separate actions.",control:"World:Tray");
        Add("ff_assembler_hotbar","Use these ready-food and drink items to fill the order. Follow the pointer to the accepted tray position.",control:"Hotbar");
        foreach(var r in new[]{burger,fries,drink})Add("ff_place_"+r.ProductId,"Drag "+r.DisplayName+" onto the highlighted tray area.",FastFoodTutorialBridge.ActionKind.Place,r);
        Add("ff_serve","Everything is on the tray. This Serve button submits the complete order. {INTERACT} it now.",FastFoodTutorialBridge.ActionKind.Serve,control:"Serve");
        Add("ff_served","Nice work. The real Serve action submitted the ticket.");
        chapter=FastFoodTutorialBridge.Chapter.Practice;
        Add("ff_practice_intro","Your turn: first prepare and serve 2 Burgers, 1 Fries, and 1 Coke. Then serve the chicken and fish sandwich ticket. Follow TASK; there is no order deadline.");
        Add("ff_practice_navigation","Use the arrows to switch directly, or open Stations to choose your next workstation.",control:"Stations");
        Add("ff_practice_exit","Exit Kitchen returns to the restaurant. You can reopen Kitchen to continue your practice.",control:"Exit Kitchen");
        Add("ff_practice","",FastFoodTutorialBridge.ActionKind.Practice,objective:"Go to Grill and prepare 2 burgers. 0/2 ready.");
        Add("ff_practice_done","Well done! You cooked, prepared food, and served both complete tickets. Let's finish with kitchen maintenance.");
        chapter=FastFoodTutorialBridge.Chapter.Hygiene;
        Add("ff_hygiene_intro","This cleaning decision explains how cooking will be affected. We will use Clean Now for this exercise.",control:"Clean Now");
        Add("ff_hygiene_clean","Choose Clean Now, then wait for cleaning to finish.",FastFoodTutorialBridge.ActionKind.Clean,control:"Clean Now");
        chapter=FastFoodTutorialBridge.Chapter.Restock;
        Add("ff_stock_intro","This Restock notice shows the missing ingredient. A training supply box is ready for you in storage.",control:"Restock");
        Add("ff_stock_open","Choose Go to Restock to enter storage.",FastFoodTutorialBridge.ActionKind.RestockOpen,control:"Go to Restock");
        Add("ff_stock_store","Store the supplied box on an open shelf, just as you learned before.",FastFoodTutorialBridge.ActionKind.Store);
        Add("ff_stock_exit","The ingredient is available again. Exit storage to return to the restaurant.",FastFoodTutorialBridge.ActionKind.RestockExit,control:"Exit Storage");
        chapter=FastFoodTutorialBridge.Chapter.Complete;
        Add("ff_complete","Kitchen training complete.");
    }
    private static void WriteLessons(SerializedObject tso,SerializedObject bso)
    {
        tso.FindProperty("explainWithFocus").boolValue = true;
        tso.FindProperty("focusTransitionDuration").floatValue = .24f;
        if (bso.FindProperty("worldFocusProxy").objectReferenceValue == null)
        {
            // No Canvas or Graphic: the existing mask projects this in screen pixels.
            var proxy = new GameObject("Kitchen tutorial world focus", typeof(RectTransform)).GetComponent<RectTransform>();
            proxy.SetParent(((Component)bso.targetObject).transform, false);
            proxy.sizeDelta = Vector2.one;
            Set(bso, "worldFocusProxy", proxy);
        }
        var steps=tso.FindProperty("steps");steps.arraySize=lessons.Count;
        var data=bso.FindProperty("lessons");data.arraySize=lessons.Count;
        for(int i=0;i<lessons.Count;i++)
        {
            var l=lessons[i];var s=steps.GetArrayElementAtIndex(i);var d=data.GetArrayElementAtIndex(i);
            foreach(string f in new[]{"id","control"})d.FindPropertyRelative(f).stringValue=f=="id"?l.id:l.control;
            d.FindPropertyRelative("chapter").enumValueIndex=(int)l.chapter;d.FindPropertyRelative("action").enumValueIndex=(int)l.action;
            d.FindPropertyRelative("recipe").objectReferenceValue=l.recipe;d.FindPropertyRelative("station").enumValueIndex=(int)l.station;
            d.FindPropertyRelative("portion").intValue=l.portion;d.FindPropertyRelative("slot").intValue=l.slot;d.FindPropertyRelative("ingredient").intValue=l.ingredient;
            s.FindPropertyRelative("id").stringValue=l.id;s.FindPropertyRelative("speaker").stringValue="Big Boss";
            s.FindPropertyRelative("message").stringValue=messages[i];s.FindPropertyRelative("objective").stringValue=objectives[i];
            s.FindPropertyRelative("portrait").objectReferenceValue=i==0?welcome:l.id.EndsWith("done")?success:explaining;
            s.FindPropertyRelative("phase").enumValueIndex=(int)PhaseFor(l.chapter);
            bool action=l.action!=FastFoodTutorialBridge.ActionKind.Explain;
            bool drag=l.action==FastFoodTutorialBridge.ActionKind.Load||l.action==FastFoodTutorialBridge.ActionKind.Assemble||l.action==FastFoodTutorialBridge.ActionKind.Place||l.action==FastFoodTutorialBridge.ActionKind.Discard||l.action==FastFoodTutorialBridge.ActionKind.Store;
            s.FindPropertyRelative("stepType").enumValueIndex=action?1:0;
            s.FindPropertyRelative("actionKey").stringValue=action?l.id:"";
            s.FindPropertyRelative("restrictUnrelatedInteractions").boolValue=l.action!=FastFoodTutorialBridge.ActionKind.Practice &&
                !(l.action==FastFoodTutorialBridge.ActionKind.Station && l.control=="StationArrow" && l.id!="ff_Fries_station");
            s.FindPropertyRelative("explainsAction").boolValue=true;
            bool passive=l.action==FastFoodTutorialBridge.ActionKind.Ready||l.action==FastFoodTutorialBridge.ActionKind.Protein||l.action==FastFoodTutorialBridge.ActionKind.Burn||l.action==FastFoodTutorialBridge.ActionKind.Practice;
            s.FindPropertyRelative("hintMode").enumValueIndex=drag?(int)TutorialSystem.TutorialHintMode.Drag:action&&!passive?(int)TutorialSystem.TutorialHintMode.Tap:0;
            bool worldControl=l.control.StartsWith("World:",StringComparison.Ordinal);
            string ui=l.action==FastFoodTutorialBridge.ActionKind.Open?"FF.Enter":
                l.action==FastFoodTutorialBridge.ActionKind.Discard?"FF.Discard":worldControl?"":l.control.Length>0?"FF.Control":
                drag&&l.action!=FastFoodTutorialBridge.ActionKind.Discard?"FF.Source":"";
            s.FindPropertyRelative("uiTargetKey").stringValue=ui;
            bool target=drag||l.action==FastFoodTutorialBridge.ActionKind.Collect;
            s.FindPropertyRelative("worldTargetKey").stringValue=worldControl?"FF.Subject":target?"FF.Target":"";
            s.FindPropertyRelative("uiFocusTarget").objectReferenceValue=null;
            s.FindPropertyRelative("highlightTarget").objectReferenceValue=null;
            s.FindPropertyRelative("requiredContext").objectReferenceValue=null;
            s.FindPropertyRelative("requiredAction").enumValueIndex=0;
            s.FindPropertyRelative("isPlaceholder").boolValue=false;
            s.FindPropertyRelative("enableStaffSpawningOnComplete").boolValue=false;
            s.FindPropertyRelative("enableCustomerSpawningOnComplete").boolValue=false;
        }
    }
    private static TutorialSystem.TutorialPhase PhaseFor(FastFoodTutorialBridge.Chapter value) => value switch
    {
        FastFoodTutorialBridge.Chapter.Burger => TutorialSystem.TutorialPhase.FastFoodGrill,
        FastFoodTutorialBridge.Chapter.Fries => TutorialSystem.TutorialPhase.FastFoodFryer,
        FastFoodTutorialBridge.Chapter.Serving => TutorialSystem.TutorialPhase.FastFoodAssembler,
        FastFoodTutorialBridge.Chapter.Parallel => TutorialSystem.TutorialPhase.FastFoodParallelPrep,
        FastFoodTutorialBridge.Chapter.Sandwiches => TutorialSystem.TutorialPhase.FastFoodSandwiches,
        FastFoodTutorialBridge.Chapter.Burn or FastFoodTutorialBridge.Chapter.Hygiene => TutorialSystem.TutorialPhase.FastFoodRecovery,
        FastFoodTutorialBridge.Chapter.Restock => TutorialSystem.TutorialPhase.PhysicalRestocking,
        FastFoodTutorialBridge.Chapter.Practice => TutorialSystem.TutorialPhase.FastFoodPractice,
        FastFoodTutorialBridge.Chapter.Complete => TutorialSystem.TutorialPhase.Completed,
        _ => TutorialSystem.TutorialPhase.FastFoodBriefing
    };

    public static string ValidateScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) throw new InvalidOperationException("Open Lobby2Tutorial first.");
        var tutorial = FindAll<TutorialSystem>(scene).Single();
        var bridge = FindAll<FastFoodTutorialBridge>(scene).Single();
        var ts = new SerializedObject(tutorial); var bs = new SerializedObject(bridge);
        var steps = ts.FindProperty("steps"); var data = bs.FindProperty("lessons");
        if (steps.arraySize != data.arraySize || steps.arraySize == 0) throw new Exception("Lesson/step count mismatch.");
        var ids = new HashSet<string>();
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i); var lesson = data.GetArrayElementAtIndex(i);
            string id = step.FindPropertyRelative("id").stringValue;
            if (!ids.Add(id) || id != lesson.FindPropertyRelative("id").stringValue) throw new Exception("Invalid step identity: " + id);
            if (step.FindPropertyRelative("portrait").objectReferenceValue == null) throw new Exception("Missing Big Boss portrait: " + id);
            if (step.FindPropertyRelative("phase").enumValueIndex != (int)PhaseFor((FastFoodTutorialBridge.Chapter)lesson.FindPropertyRelative("chapter").enumValueIndex)) throw new Exception("Chapter phase mismatch: " + id);
        }
        foreach (string key in new[]{"kitchen","view","tutorial","day","controlsCanvas","restart","skip","finish","confirmation","confirmSkip","cancelSkip","status","burger","fries","chicken","fish","drink"})
            if (bs.FindProperty(key).objectReferenceValue == null) throw new Exception("Missing reference: " + key);
        foreach (string key in new[]{"dialogueUI","handIndicator","targetIndicator","uiFocusMask","sceneBindings","cameraController","groupSpawner"})
            if (ts.FindProperty(key).objectReferenceValue == null) throw new Exception("Missing presentation reference: " + key);
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath)) throw new Exception("Tutorial missing from build scenes.");
        return "PASS: " + ids.Count + " unique matched lessons; chapter phases, portraits, scene references and build entry.";
    }
    private static T Find<T>(Scene scene) where T:Component=>FindAll<T>(scene).FirstOrDefault();
    private static IEnumerable<T> FindAll<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    private static T Copy<T>(SerializedObject source,string field,Transform parent)where T:Component
    {
        var c=(T)source.FindProperty(field).objectReferenceValue;
        var copy=UnityEngine.Object.Instantiate(c.gameObject,parent);copy.name=c.name;copy.SetActive(true);return copy.GetComponent<T>();
    }
    private static void ClearCamera(Component c){var s=new SerializedObject(c);var p=s.FindProperty("worldCamera");if(p!=null){p.objectReferenceValue=null;s.ApplyModifiedPropertiesWithoutUndo();}}
    private static void Set(SerializedObject s,string key,UnityEngine.Object value)=>s.FindProperty(key).objectReferenceValue=value;
    private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    private static Button Button(Button source,Transform parent,string label,Vector2 anchor,Vector2 size)
    {
        var b=UnityEngine.Object.Instantiate(source,parent);b.name=label;b.onClick=new Button.ButtonClickedEvent();
        var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=anchor;r.pivot=Vector2.one*.5f;r.anchoredPosition=Vector2.zero;r.sizeDelta=size;
        var text=b.GetComponentInChildren<TMP_Text>();text.text=label;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=26;
        b.gameObject.SetActive(true);return b;
    }
}
#endif
