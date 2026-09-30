using System;
using System.Linq;
using DineIn.Appearance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AlmanacContentAuthoring
{
    private const string PeopleProfiles = "Assets/_Project/Art/Models/Customer/New Alien Models/PlayerType/";
    private const string PeoplePreviews = "Assets/_Project/UI/Almanac/Previews/";
    private const string PeopleIdle = "Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim";

    private static void PopulatePeople()
    {
        PeopleFolder(PeoplePreviews.TrimEnd('/'));
        PeopleFolder(PeoplePreviews + "Materials");
        PeopleCustomer("customer-regular", "Regular Customer", "GreenCustomer", "The familiar green guest", 
            "A regular guest gives you a little time to get the meal right. Keep orders moving, serve the right dishes, and make their visit comfortable.",
            "Normal patience and eating speed.\nCasual Dining: arrives from Day 1.\nFast Food: arrives from Day 1.",
            "Assets/_Project/Art/Models/Customer/Old Alien Models/GreenCustomer.prefab", new[] { "restaurant-casual-dining", "staff-waiter" });
        PeopleCustomer("customer-vip", "VIP Customer", "PinkCustomer", "Pink guest · prompt service matters",
            "These pink guests expect prompt attention. A happy visit earns an extra tip, but their patience drains faster while waiting in line or for an order.",
            "Line and order patience drain 3× as fast.\nHappy payment adds a 100 bonus tip.\nCasual Dining: Day 5. Fast Food: Day 1.",
            "Assets/_Project/Art/Models/Customer/Old Alien Models/PinkCustomer.prefab", new[] { "staff-host", "staff-waiter", "staff-cashier" });
        PeopleCustomer("customer-messy", "Messy Customer", "BlueCustomer", "Blue guest · take time to tidy",
            "Blue guests take their time over a meal and tend to leave a mess. Keep a table available for the next party by sending the busser after they finish.",
            "Meals take 2× as long.\nNormal line and order patience.\nCasual Dining: Day 10. Fast Food: Day 1.",
            "Assets/_Project/Art/Models/Customer/Old Alien Models/BlueCustomer.prefab", new[] { "staff-busser", "staff-lobby-person" });
        PeopleCustomer("customer-purple", "Purple Customer", "PurpleCustomer", "Fast Food · a new face in the queue",
            "Purple guests begin dropping by as the Fast Food restaurant gets established. Their normal patience, ordering pace and eating speed make them a straightforward visit to handle well.",
            "Fast Food: arrives from Day 2.\nNormal patience, walking, ordering and eating speed.",
            "Assets/_Project/Art/Models/Customer/AdditionalAliens/PurpleAlien/PurpleAlienCustomer.prefab", new[] { "restaurant-fast-food", "staff-cashier" });
        PeopleCustomer("customer-orange", "Orange Customer", "OrangeCustomer", "Fast Food · a quick meal on the go",
            "Orange guests have busy schedules. Most take their food away, and those who sit down finish quickly. Keep their order moving so they can be on their way.",
            "Fast Food: arrives from Day 5.\n70% takeaway chance.\nOrdering takes 75% of normal time; eating takes 70%.",
            "Assets/_Project/Art/Models/Customer/AdditionalAliens/OrangeAlien/OrangeAlienCustomer.prefab", new[] { "restaurant-fast-food", "staff-cashier", "staff-fast-food-assembler" });
        PeopleCustomer("customer-elderly", "Elderly Customer", "ElderlyCustomer", "Fast Food · assisted service",
            "Older guests need more time to walk and choose a meal, but do not like being left waiting. Give them priority at the counter and bring dine-in meals to their table.",
            "Fast Food: arrives from Day 10 when assisted service is available.\nWalking speed: 70%. Ordering time: 1.4×.\nLine and order patience drain 3× as fast.",
            "Assets/_Project/Art/Models/Customer/AdditionalAliens/OldAlien/OldAlienCustomer.prefab", new[] { "staff-cashier", "staff-lobby-person" });
        PeopleCustomer("customer-family", "Family Customer", "YellowCustomer", "Yellow guests · room for the little ones",
            "Yellow families visit in groups of three or four. The children may leave their seats to play in the dining area, so give the group enough room and watch for footprints.",
            "Fast Food: arrives from Day 15.\nMeals take 1.5× as long.\nDine-in families need a table with at least three seats.",
            "Assets/_Project/Art/Models/Customer/AdditionalAliens/YellowAlien/YellowAlienCustomer.prefab", new[] { "restaurant-fast-food", "staff-lobby-person" });

        PeopleStaff("staff-host", "Host", EmployeeRole.Host, "Casual Dining · front of house",
            "Welcomes guests in the queue and helps the party get seated. A ready host keeps the arrival area moving while the rest of the team handles service.",
            "Keep tables ready and watch waiting guests.\nUniform: receptionist outfit.", false, true, new[] { "customer-vip", "staff-waiter" });
        PeopleStaff("staff-waiter", "Waiter", EmployeeRole.Waiter, "Casual Dining · table service",
            "Takes orders, delivers food and bills, and brings customer payments to the cashier. The waiter connects the dining room with the kitchen and payment station.",
            "Watch order, food and bill requests.\nUniform: waiter outfit.", false, true, new[] { "staff-chef", "staff-barista", "staff-cashier" });
        PeopleStaff("staff-cashier", "Cashier", EmployeeRole.Cashier, "Casual Dining & Fast Food · payment",
            "Processes payments in Casual Dining. In Fast Food, the cashier takes orders and payment at the counter before the kitchen completes the meal.",
            "Assign a cashier before service.\nFast Food can unlock a second cashier station.\nCompare restaurant uniforms beside the portrait.", false, false, new[] { "staff-waiter", "restaurant-fast-food" });
        PeopleStaff("staff-busser", "Busser", EmployeeRole.Busser, "Casual Dining · clean tables, ready seats",
            "Clears dirty tables and carries used trays away, helping the next group find a clean place to sit. A busy dining room needs someone keeping its tables ready.",
            "Clear tables after customers leave.\nUniform: waiter outfit.\nIn Fast Food this role is called Lobby Person.", false, true, new[] { "customer-messy", "staff-lobby-person" });
        PeopleStaff("staff-lobby-person", "Lobby Person", EmployeeRole.Busser, "Fast Food · dining room support",
            "Keeps the Fast Food dining room ready by clearing tables and trays. Assigned lobby staff also support meal delivery, including guests who need assisted table service.",
            "The Fast Food name for the Busser role.\nKeep tables clear and support dine-in service.\nUniform: Fast Food outfit.", true, true, new[] { "customer-elderly", "customer-family", "staff-busser" });
        PeopleStaff("staff-chef", "Chef", EmployeeRole.Chef, "Casual Dining · kitchen",
            "Works on food orders in the Casual Dining kitchen. Keeping a chef assigned helps prepared dishes reach the dining room while the service team looks after the guests.",
            "Assign kitchen staff through staff management.\nUniform: chef outfit and chef hat.", false, true, new[] { "staff-waiter", "staff-barista" });
        PeopleStaff("staff-barista", "Barista", EmployeeRole.Barista, "Casual Dining · drinks",
            "Handles the drinks side of the Casual Dining kitchen. Coordinate food and drinks so the service team can complete customers' orders.",
            "Assign kitchen staff through staff management.\nUniform: waiter outfit, without a chef hat.", false, true, new[] { "staff-chef", "staff-waiter" });
        PeopleStaff("staff-grill", "Grill Station", EmployeeRole.GrillStation, "Fast Food · kitchen role · grill and preparation",
            "The Grill Station employee cooks burger patties and works the preparation board. Chicken and fish sandwich proteins arrive from the fryer before the sandwiches are prepared here and sent to assembly.",
            "A separate role in Fast Food staff hiring.\nUniform: Fast Food outfit and chef hat.", true, true, new[] { "staff-fry", "staff-fast-food-assembler" });
        PeopleStaff("staff-fry", "Fry Station", EmployeeRole.FryStation, "Fast Food · kitchen role · fryer",
            "Works the fryer for chicken, fries and chicken nuggets, as well as chicken and fish sandwich proteins. Sandwich proteins then move to the grill preparation board; finished fried portions are ready for assembly.",
            "A separate role in Fast Food staff hiring.\nUniform: Fast Food outfit and chef hat.", true, true, new[] { "staff-grill", "staff-fast-food-assembler" });
        PeopleStaff("staff-fast-food-assembler", "Assembler", EmployeeRole.FastFoodAssembler, "Fast Food · kitchen role · order assembly",
            "Brings prepared portions together for Fast Food orders. The assembler handles the final order tickets after the grill and fry stations do their work.",
            "A separate role in Fast Food staff hiring.\nWorks across the order menu.\nUniform: Fast Food outfit and chef hat.", true, true, new[] { "staff-grill", "staff-fry", "staff-cashier" });

        PeopleRestaurant("restaurant-casual-dining", "Casual Dining", "Table service · campaign restaurant",
            "Welcome guests, seat their party, take their orders and bring meals to the table. Hosts, waiters, cashiers, bussers and kitchen staff work together through the full visit.",
            "Regular guests arrive from Day 1; VIP guests from Day 5; messy guests from Day 10.\nManage your staff, ingredients and equipment between shifts.",
            "Assets/_Project/Art/Models/3D Models/CasualDiningExterior/CasualDiningExterior (1).fbx", new[] { "staff-host", "staff-waiter", "staff-busser", "customer-regular" });
        PeopleRestaurant("restaurant-fast-food", "Fast Food", "Counter service · campaign restaurant",
            "Take orders and payment at the counter, prepare food at the grill and fry stations, then complete the ticket at assembly. Guests can take their meal away or dine in.",
            "Regular, VIP and messy guests are present from Day 1. Purple, orange, elderly and family visitors arrive as the campaign progresses.\nCashiers and lobby staff support the dining room.",
            "Assets/_Project/Art/Models/3D Models/FastFoodRestaurant_Exterior.fbx", new[] { "staff-cashier", "staff-grill", "staff-fry", "staff-fast-food-assembler" });
        PeopleRestaurant("restaurant-fine", "Fine Dining", "Coming soon · district map preview",
            "This is the building currently assigned to Fine Dining on the district map. It is a preview of an upcoming restaurant, not an available campaign.",
            "This restaurant does not have a playable campaign yet.",
            "Assets/_Project/MainMenu/NewDesign/Restaurant/Models/3D Models/Objects/Diner/diner.fbx", new[] { "restaurant-casual-dining", "restaurant-fast-food" });
    }

    private static void PeopleCustomer(string id, string name, string profileName, string subtitle, string description, string notes, string prefab, string[] related)
    {
        var profile = AssetDatabase.LoadAssetAtPath<CustomerTypeProfile>(PeopleProfiles + profileName + ".asset");
        if (profile == null) throw new InvalidOperationException("Missing customer profile: " + profileName);
        var entry = Entry(id, AlmanacCategory.Customers, name, subtitle, description, notes,
            PeopleProfiles + profileName + ".asset; Customers/Booth/GroupSpawner.CampaignCustomers.cs; Customers/Customer/CustomerGroup.CampaignVisit.cs; Lobby1/Lobby2 scene spawner references.",
            kind: AlmanacPreviewKind.Character, related: related);
        entry.icon = profile.customerImage;
        entry.previewPrefab = PeopleVisualPrefab(prefab, id);
        entry.idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PeopleIdle);
        entry.previewEuler = new Vector3(0, 160, 0);
        EditorUtility.SetDirty(entry);
    }

    private static void PeopleStaff(string id, string name, EmployeeRole role, string subtitle, string description, string notes,
        bool fastFood, bool singleRestaurant, string[] related)
    {
        var entry = Entry(id, AlmanacCategory.Staff, name, subtitle, description, notes,
            "Office/HR/EmployeeRoleCatalog.cs; Office/HR/EmployeeManager.cs:IsRoleUsedInCurrentRestaurant; ManagementComputer/ManagementComputerHRPanel.cs; Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs; Gameplay/AutonomousService/Kitchen/KitchenWorkerBot.cs; Restaurant/FastFoodCookingState.cs:Station/PreparationStation; Resources/CasualEmployeeUniforms.asset; Resources/FastFoodEmployeeUniforms.asset.",
            kind: AlmanacPreviewKind.Character, related: related);
        entry.idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PeopleIdle);
        entry.previewEuler = new Vector3(0, 160, 0);
        var first = PeopleStaffVariant(role, fastFood, "male");
        var second = PeopleStaffVariant(role, fastFood, "female");
        entry.previewPrefab = first.prefab;
        entry.introductionClip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Animations/PlayerAnimation/Happy Idle.fbx")
            .OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        entry.variants = singleRestaurant ? new[] { first, second } : new[]
        {
            first, second, PeopleStaffVariant(role, !fastFood, "male"), PeopleStaffVariant(role, !fastFood, "female")
        };
        // Role portraits are rendered from this entry's actual uniform preview, not an outfit-only swatch.
        entry.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/Almanac/Thumbnails/" + id + ".png");
        entry.listSection = !singleRestaurant ? "Shared roles" : fastFood ? "Fast Food" : "Casual Dining";
        entry.sectionOrder = !singleRestaurant ? 0 : fastFood ? 2 : 1;
        entry.searchAliases = role switch
        {
            EmployeeRole.Host => "receptionist greeter front of house",
            EmployeeRole.Waiter => "server table service front of house",
            EmployeeRole.Cashier => "counter payment register",
            EmployeeRole.Busser when fastFood => "lobby staff attendant busser cleaning table delivery",
            EmployeeRole.Busser => "table clearing cleaning",
            EmployeeRole.Chef => "cook kitchen food preparation",
            EmployeeRole.Barista => "drinks beverage kitchen",
            EmployeeRole.GrillStation => "grill cook kitchen staff burger sandwich preparation",
            EmployeeRole.FryStation => "fryer cook kitchen staff fried food protein",
            EmployeeRole.FastFoodAssembler => "assembly kitchen staff tickets",
            _ => ""
        };
        EditorUtility.SetDirty(entry);
    }

    private static AlmanacPreviewVariant PeopleStaffVariant(EmployeeRole role, bool fastFood, string body)
    {
        string key = "staff-" + (fastFood ? "fast-food-" : "casual-") + role.ToString().ToLowerInvariant() + "-" + body;
        string path = PeoplePreviews + key + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AppearanceCatalog>("Assets/Resources/AppearanceCatalog.asset");
            var uniform = AssetDatabase.LoadAssetAtPath<EmployeeUniforms>(fastFood ? "Assets/Resources/FastFoodEmployeeUniforms.asset" : "Assets/Resources/CasualEmployeeUniforms.asset");
            if (catalog == null || uniform == null) throw new InvalidOperationException("Employee appearance catalog or uniforms missing.");
            // Fixed representative traits, independent of the player's recipe, roster and UnityEngine.Random.
            var recipe = AppearanceCatalog.Find(catalog.bodies, body).defaults.Copy();
            recipe.bodyId = body;
            recipe.skinId = body == "male" ? "warm" : "tan";
            recipe.hairColorId = "brown";
            recipe = uniform.Apply(recipe, role, catalog);
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject source = null, copy = null;
            try
            {
                source = new GameObject(key);
                SceneManager.MoveGameObjectToScene(source, scene);
                source.AddComponent<Animator>();
                var appearance = source.AddComponent<CharacterAppearance>();
                appearance.Apply(recipe);
                PeoplePersistTints(source);
                copy = AlmanacPreviewStage.CreateVisualCopy(source, null);
                SceneManager.MoveGameObjectToScene(copy, scene);
                copy.name = key;
                copy.SetActive(true);
                asset = PrefabUtility.SaveAsPrefabAsset(copy, path);
                if (asset == null) throw new InvalidOperationException("Could not save staff preview: " + path);
            }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        return new AlmanacPreviewVariant { label = (fastFood ? "Fast Food" : "Casual Dining") + " · " + (body == "male" ? "Male" : "Female"), prefab = asset };
    }

    private static void PeoplePersistTints(GameObject source)
    {
        var block = new MaterialPropertyBlock();
        foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
        {
            renderer.GetPropertyBlock(block);
            if (block.isEmpty) continue;
            var color = block.GetColor("_BaseColor");
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                if (original == null) continue;
                var materialPath = AssetDatabase.GetAssetPath(original);
                // Property blocks are not serialized by prefabs. Persist the existing appearance tint.
                string key = AssetDatabase.AssetPathToGUID(materialPath) + "-" + ColorUtility.ToHtmlStringRGBA(color);
                string path = PeoplePreviews + "Materials/" + key + ".mat";
                var tinted = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (tinted == null)
                {
                    tinted = new Material(original) { name = original.name + " " + ColorUtility.ToHtmlStringRGB(color) };
                    if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", color);
                    if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", color);
                    AssetDatabase.CreateAsset(tinted, path);
                }
                materials[i] = tinted;
            }
            renderer.sharedMaterials = materials;
            renderer.SetPropertyBlock(null);
            block.Clear();
        }
    }

    private static void PeopleRestaurant(string id, string name, string subtitle, string description, string notes, string prefab, string[] related)
    {
        string sceneObject = id switch
        {
            "restaurant-casual-dining" => "CasualDiningExterior (1)",
            "restaurant-fast-food" => "FastFoodRestaurant_Exterior",
            "restaurant-fine" => "diner (2)",
            _ => throw new InvalidOperationException("No authored restaurant presentation for " + id)
        };
        var entry = Entry(id, AlmanacCategory.Restaurants, name, subtitle, description, notes,
            "MainMenu/GameMenu/Scripts/UI/GameModePopupController.cs: campaignRestaurantScenes contains Lobby1 and Lobby2 only; Save/CampaignSaveStore.cs; ProjectSettings/EditorBuildSettings.asset; NewGameMenu Restaurants/" + sceneObject + ".",
            kind: AlmanacPreviewKind.Model, related: related);
        // Copy the actual district instance, including its material overrides and imported basis.
        // A raw FBX here loses Casual Dining's scene-authored textured material.
        entry.previewPrefab = PeopleRestaurantVisual(sceneObject, prefab, id, out var presentationEvidence);
        entry.sourceNotes += "\n" + presentationEvidence;
        if (entry.icon != null) entry.previewKind = AlmanacPreviewKind.Image;
        entry.previewEuler = new Vector3(0, 45, 0);
        entry.searchAliases = id == "restaurant-fine" ? "coming soon district map preview" : "restaurant campaign " + name;
        EditorUtility.SetDirty(entry);
    }

    private static GameObject PeopleRestaurantVisual(string sceneObject, string expectedPrefab, string key, out string evidence)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Restaurant visual authoring requires Edit Mode.");
        const string scenePath = "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
        var sourceScene = SceneManager.GetSceneByPath(scenePath);
        bool ownsSourceScene = !sourceScene.IsValid() || !sourceScene.isLoaded;
        if (ownsSourceScene) sourceScene = EditorSceneManager.OpenPreviewScene(scenePath);
        var copyScene = EditorSceneManager.NewPreviewScene();
        GameObject copy = null;
        try
        {
            var restaurants = sourceScene.GetRootGameObjects().SingleOrDefault(root => root.name == "Restaurants");
            var source = restaurants != null ? restaurants.transform.Find(sceneObject)?.gameObject : null;
            if (source == null) throw new InvalidOperationException("Missing authored restaurant: " + scenePath + "/Restaurants/" + sceneObject);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source) != expectedPrefab)
                throw new InvalidOperationException("Restaurant model changed: inspect " + sceneObject + " before regenerating its preview.");
            var renderers = source.GetComponentsInChildren<Renderer>(true);
            if (!renderers.Any(renderer => renderer.enabled && renderer.sharedMaterials.Any(material => material != null)))
                throw new InvalidOperationException("Restaurant has no authored visible material: " + sceneObject);
            evidence = scenePath + "/Restaurants/" + sceneObject + "; source model: " + expectedPrefab + "; renderer materials: " +
                string.Join("; ", renderers.SelectMany(renderer => renderer.sharedMaterials.Where(material => material != null)
                    .Select(material => renderer.name + " = " + material.name + " [" + AssetDatabase.GetAssetPath(material) + "]")));
            copy = AlmanacPreviewStage.CreateVisualCopy(source, null);
            SceneManager.MoveGameObjectToScene(copy, copyScene);
            copy.name = key;
            copy.SetActive(true);
            // Always refresh this derived asset: old previews came from unconfigured model imports.
            var asset = PrefabUtility.SaveAsPrefabAsset(copy, PeoplePreviews + key + ".prefab");
            if (asset == null) throw new InvalidOperationException("Could not save restaurant preview: " + key);
            return asset;
        }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            EditorSceneManager.ClosePreviewScene(copyScene);
            if (ownsSourceScene) EditorSceneManager.ClosePreviewScene(sourceScene);
        }
    }
    private static GameObject PeopleVisualPrefab(string sourcePath, string key)
    {
        string path = PeoplePreviews + key + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("Missing almanac source visual: " + sourcePath);
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject copy = null;
        try
        {
            copy = AlmanacPreviewStage.CreateVisualCopy(source, null);
            SceneManager.MoveGameObjectToScene(copy, scene);
            copy.name = key;
            copy.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(copy, path);
        }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void PeopleFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        PeopleFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
