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
        Set(bso,"waitingTimerTemplate",Find<FastFoodCookingTimer>(scene));
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
            Set(bso,"waitingTimerTemplate",Find<FastFoodCookingTimer>(scene));
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
        for(int i=0;i<n;i++) Add("ff_"+chapter+"_load_"+i,i==0&&chapter==FastFoodTutorialBridge.Chapter.Burger?"Let's put a raw patty on the grill.":"",
            FastFoodTutorialBridge.ActionKind.Load,recipe,i,i,objective:"Load cooking spot "+(i+1)+" with the highlighted raw ingredient.");
        for(int i=0;i<n;i++)
        {
            Add("ff_"+chapter+"_wait_"+i,burn?"We'll deliberately leave this patty on the heat too long, so you can recognize burnt food.":i==0&&chapter==FastFoodTutorialBridge.Chapter.Burger?"The timer shows cooking progress. Wait until the patty is ready; then we'll collect it.":"",
                burn?FastFoodTutorialBridge.ActionKind.Burn:FastFoodTutorialBridge.ActionKind.Ready,recipe,i,i,control:"World:Food",objective:burn?"Watch what happens if we leave it on the heat.":recipe==fries?"The fries are cooking…":"The patty is cooking…");
            if(!burn) Add("ff_"+chapter+"_collect_"+i,i==0?(FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry?"The raised basket is ready. Collect the fries for service.":"That patty is ready. Collect it before it burns."):"",
                FastFoodTutorialBridge.ActionKind.Collect,recipe,i,i,objective:FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry?"Collect the ready fries.":"Collect the ready patty.");
        }
    }
    private static void Assemble(Recipe recipe,int portion,int slot,int start=0)
    {
        var steps=FastFoodCookingState.AssemblySteps(recipe);
        for(int i=start;i<steps.Count;i++) Add("ff_"+chapter+"_"+recipe.ProductId+"_"+portion+"_ingredient_"+i,
            chapter==FastFoodTutorialBridge.Chapter.Burger?"Add "+steps[i].label+" to the highlighted prep position.":"",
            FastFoodTutorialBridge.ActionKind.Assemble,recipe,portion,slot,i,
            objective:"Add "+steps[i].label+" ("+(i+1)+"/"+steps.Count+").");
    }
    private static void BuildLessons()
    {
        lessons.Clear();messages.Clear();objectives.Clear();
        chapter=FastFoodTutorialBridge.Chapter.Briefing;
        Add("ff_welcome","Welcome to Fast Food! Let's learn how this kitchen works.");
        Add("ff_lobby_recap","You know the restaurant basics. Here we'll focus on cooking, preparing food, and serving complete kitchen orders.");
        Add("ff_hud_pause","Use Pause whenever you need a break. Resume brings you back to work.",control:"Pause");
        Add("ff_hud_task","TASK shows your next instruction: what to do, when to wait, and where to go next.",control:"Task");
        Add("ff_enter","Kitchen is where you'll help the team prepare orders. Let's head in.",FastFoodTutorialBridge.ActionKind.Open,control:"Enter",objective:"Open Kitchen.");
        Add("ff_kitchen_welcome","Welcome to your kitchen! This station list lets you choose where to help. We'll start with Grill.",control:"Selection");
        chapter=FastFoodTutorialBridge.Chapter.Burger;Station(FastFoodStationMode.Grill);
        Add("ff_hud_header","The blue panel names your station and current food. Its count and progress bar show your progress.",control:"Header");
        Add("ff_hotbar","Your ingredients are lined up here. Each keeps its place so you can find it quickly.",control:"Hotbar");
        Add("ff_hotbar_counts","This is a raw patty. The number shows how many you have available.",recipe:burger,control:"Raw");
        Add("ff_hotbar_empty","Cooked ingredients have a separate READY cell. A dim cell with zero cannot be used yet.",recipe:burger,control:"ProteinCell");
        Add("ff_grill_area","Each cooking spot can hold one patty. Let's cook one first, then prepare its burger.",control:"World:Cooking");
        Cook(burger,1);
        Add("ff_prep_area","Here's the prep table. Build the burger here in recipe order.",control:"World:Prep");
        Add("ff_ready_ingredient","Your collected patty is now available in the READY cell. Use it after the bottom bun.",recipe:burger,control:"ProteinCell");
        Assemble(burger,0,0);
        Add("ff_burger_done","Good! The burger is assembled. Its pickup transfers it to ready supply so the prep position can be used again.");
        chapter=FastFoodTutorialBridge.Chapter.Burn;
        Add("ff_burn_intro","Now let's learn to recover burnt food. Load another patty; we'll deliberately leave this one too long.",control:"World:Cooking");
        Cook(burger,1,true);
        Add("ff_discard","Burnt food can't be served. This patty belongs in the discard area.",FastFoodTutorialBridge.ActionKind.Discard,burger,objective:"Discard the burnt patty.");
        Add("ff_reload","",FastFoodTutorialBridge.ActionKind.Load,burger,objective:"Load a fresh patty onto the highlighted cooking spot.");
        Add("ff_retry_wait","",FastFoodTutorialBridge.ActionKind.Ready,burger,objective:"The replacement patty is cooking…");
        Add("ff_retry_collect","",FastFoodTutorialBridge.ActionKind.Collect,burger,objective:"Collect the replacement patty.");
        chapter=FastFoodTutorialBridge.Chapter.Sandwiches;
        foreach(var r in new[]{chicken,fish})
        {
            Add("ff_protein_"+r.ProductId,r==chicken?"Stay at the prep table. Fryer staff cook the chicken for you; we'll assemble it when the READY ingredient arrives.":"Next is the Fish Fillet Sandwich. Staff are cooking the fish; wait here at the prep table.",FastFoodTutorialBridge.ActionKind.Protein,r,control:"World:Prep",objective:r==chicken?"The fryer team is preparing the chicken…":"The fryer team is preparing the fish…");
            Add("ff_sandwich_ready_"+r.ProductId,r==chicken?"The cooked chicken is ready. Add bottom bun, cooked chicken, then top bun.":"The cooked fish is ready. Add bottom bun, cooked fish, then top bun.",recipe:r,control:"ProteinCell");
            Assemble(r,0,0);
        }
        Add("ff_sandwich_done","That's it: staff fry the protein, and you assemble the sandwich here.");
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
        Add("ff_assembler_hotbar","These ready foods and drinks are available for the order. Each belongs on the tray.",control:"Hotbar");
        foreach(var r in new[]{burger,fries,drink})Add("ff_place_"+r.ProductId,"Add "+r.DisplayName+" to the tray.",FastFoodTutorialBridge.ActionKind.Place,r);
        Add("ff_serve","Everything is on the tray. Serve sends the complete order out to the customer.",FastFoodTutorialBridge.ActionKind.Serve,control:"Serve",objective:"Serve the complete order.");
        Add("ff_served","Nice work. That order is ready for the customer.");
        chapter=FastFoodTutorialBridge.Chapter.Practice;
        Add("ff_practice_intro","Let's put that into practice. Prepare and serve 2 Burgers, 1 Fries, and 1 Coke, then the chicken and fish sandwich order. Follow TASK; there's no deadline.");
        Add("ff_practice_navigation","Use the arrows to switch directly, or open Stations to choose your next workstation.",control:"Stations");
        Add("ff_practice_exit","Exit Kitchen returns to the restaurant. You can reopen Kitchen to continue your practice.",control:"Exit Kitchen");
        Add("ff_practice","",FastFoodTutorialBridge.ActionKind.Practice,objective:"Let's finish this order. Go to Grill and prepare 2 burgers. 0/2 ready.");
        Add("ff_practice_done","Well done! You cooked, prepared food, and served both complete tickets. Let's finish with kitchen maintenance.");
        chapter=FastFoodTutorialBridge.Chapter.Hygiene;
        Add("ff_hygiene_intro","The kitchen needs cleaning. Clean Now gives the team time to make it safe for food again.",control:"Clean Now");
        Add("ff_hygiene_clean","Choose Clean Now, then wait for cleaning to finish.",FastFoodTutorialBridge.ActionKind.Clean,control:"Clean Now");
        chapter=FastFoodTutorialBridge.Chapter.Restock;
        Add("ff_stock_intro","We're low on fries. Let's order a box at the computer.",control:"Restock");
        Add("ff_stock_open","Go to Restock will take us to the computer to arrange the delivery.",FastFoodTutorialBridge.ActionKind.RestockOpen,control:"Go to Restock",objective:"Go to the computer.");
        Add("ff_stock_app","Choose Restock on the computer.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Restock",objective:"Open Restock.");
        Add("ff_stock_food","The Food tab lists ingredients for the kitchen. We'll find French Fries there.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Food",objective:"Choose Food.");
        Add("ff_stock_fries","Order one box of French Fries. The card shows the price and how much comes in a box.",control:"Computer:FriesCard");
        Add("ff_stock_add_fries","",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Fries",objective:"Order 1 box of fries.");
        Add("ff_stock_checkout","Checkout lets us review the quantities and total before paying.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Checkout",objective:"Review the order at Checkout.");
        Add("ff_stock_order","One box of fries is all we need. Order Now confirms the purchase and pays the supplier.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Order",objective:"Confirm the purchase with Order Now.");
        Add("ff_stock_close_app","The delivery is on its way. Close Restock and we'll collect it outside.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:CloseApp",objective:"Close Restock.");
        Add("ff_stock_close_computer","We're finished at the computer. Exit returns us to the restaurant.",FastFoodTutorialBridge.ActionKind.Purchase,control:"Computer:Exit",objective:"Leave the computer.");
        Add("ff_stock_delivery","",FastFoodTutorialBridge.ActionKind.Delivery,objective:"Waiting for the delivery…");
        Add("ff_stock_truck","Our delivery has arrived. Meet the truck to collect it.",FastFoodTutorialBridge.ActionKind.Truck,control:"World:Truck",objective:"Meet the delivery truck.");
        Add("ff_stock_collect","HOLD TO COLLECT brings the boxes off the truck. Let's collect our delivery.",FastFoodTutorialBridge.ActionKind.CollectDelivery,control:"Get Orders",objective:"Collect the delivery.");
        Add("ff_stock_freezer","Fries belong in frozen storage. Take this box to the freezer.",FastFoodTutorialBridge.ActionKind.Freezer,control:"World:Freezer",objective:"Take the fries to the freezer.");
        Add("ff_stock_store","Put this box of fries on the empty freezer shelf.",FastFoodTutorialBridge.ActionKind.Store,objective:"Drag the fries box onto the empty freezer shelf.");
        chapter=FastFoodTutorialBridge.Chapter.Complete;
        Add("ff_complete","Good work. The fries are stored. Good luck with your shift!");
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
            s.FindPropertyRelative("portrait").objectReferenceValue=i==0?welcome:l.id.EndsWith("done")||l.id=="ff_complete"?success:explaining;
            s.FindPropertyRelative("phase").enumValueIndex=(int)PhaseFor(l.chapter);
            bool action=l.action!=FastFoodTutorialBridge.ActionKind.Explain;
            bool drag=l.action==FastFoodTutorialBridge.ActionKind.Load||l.action==FastFoodTutorialBridge.ActionKind.Assemble||l.action==FastFoodTutorialBridge.ActionKind.Place||l.action==FastFoodTutorialBridge.ActionKind.Discard||l.action==FastFoodTutorialBridge.ActionKind.Store;
            s.FindPropertyRelative("stepType").enumValueIndex=action?1:0;
            s.FindPropertyRelative("actionKey").stringValue=action?l.id:"";
            s.FindPropertyRelative("restrictUnrelatedInteractions").boolValue=l.action!=FastFoodTutorialBridge.ActionKind.Practice &&
                !(l.action==FastFoodTutorialBridge.ActionKind.Station && l.control=="StationArrow" && l.id!="ff_Fries_station");
            s.FindPropertyRelative("explainsAction").boolValue=true;
            bool passive=l.action==FastFoodTutorialBridge.ActionKind.Ready||l.action==FastFoodTutorialBridge.ActionKind.Protein||l.action==FastFoodTutorialBridge.ActionKind.Burn||l.action==FastFoodTutorialBridge.ActionKind.Practice||l.action==FastFoodTutorialBridge.ActionKind.Delivery;
            s.FindPropertyRelative("hintMode").enumValueIndex=drag?(int)TutorialSystem.TutorialHintMode.Drag:l.action==FastFoodTutorialBridge.ActionKind.CollectDelivery?(int)TutorialSystem.TutorialHintMode.Hold:action&&!passive?(int)TutorialSystem.TutorialHintMode.Tap:0;
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
        foreach (string key in new[]{"kitchen","view","tutorial","day","controlsCanvas","restart","skip","finish","confirmation","confirmSkip","cancelSkip","status","waitingTimerTemplate","burger","fries","chicken","fish","drink"})
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
