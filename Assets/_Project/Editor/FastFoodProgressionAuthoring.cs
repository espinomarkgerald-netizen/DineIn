using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>Explicit, repeatable authoring for Lobby2. Never runs on import or alters player saves.</summary>
public static class FastFoodProgressionAuthoring
{
    private const string Data = "Assets/_Project/Resources/FastFood";
    private const string Food = "Assets/_Project/Restaurant/Prefabs/FastFood/Food";
    private const string Art = "Assets/_Project/UI/Assets/FoodIcons/Fast Food";
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby2.unity";

    [MenuItem("Dine In/Fast Food/Install Progression and Polish")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before authoring progression.");
        Scene restaurantScene = SceneManager.GetSceneByPath(ScenePath);
        if (!restaurantScene.IsValid() || !restaurantScene.isLoaded)
            throw new InvalidOperationException("Open Lobby2 before installing progression.");
        if (restaurantScene.isDirty)
            throw new InvalidOperationException("Save or discard your current Lobby2 edits before installing progression.");
        Folder(Data); Folder(Data + "/Equipment"); Folder(Data + "/Ingredients"); Folder(Data + "/Recipes"); Folder(Food);
        var config = Asset<FastFoodProgressionSettings>(Data + "/Progression.asset");
        var restaurant = restaurantScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FastFoodRestaurant>(true)).Single();
        Scene original = SceneManager.GetActiveScene();
        Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(temporary);
            AuthorMenu();
            AuthorBossUI();
            config.equipment.Clear();
            AuthorEquipment(config, restaurant);
            Save(config);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(temporary, true);
        }
        EditorSceneManager.MarkSceneDirty(restaurantScene);
        EditorSceneManager.SaveScene(restaurantScene);
        MenuCatalog.ClearCachedDefault();
        Debug.Log("[FastFoodProgression] Authored progression, six foods, five starter seats, cashier two and shared Big Boss tips.");
        Validate();
    }

    private static void AuthorEquipment(FastFoodProgressionSettings config, FastFoodRestaurant restaurant)
    {
        var source = new SerializedObject(restaurant);
        var tableArray = source.FindProperty("diningTables");
        var tables = Enumerable.Range(0, tableArray.arraySize)
            .Select(i => (Booth)tableArray.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        AuthorSeating(config, tables);
        var kiosk1 = Equipment(config, "ff_kiosk_1", "Kiosk 1", 4, 650,
            "Open the first self-service ordering and payment queue.", EquipmentCatalogSection.Upgrades);
        var cashier = Equipment(config, FastFoodProgressionSettings.SecondCashierID, "Second Cashier Station", 6, 750,
            "Open counter 2. Hire a second cashier from Day 15 in Computer > Staff to staff both registers.", EquipmentCatalogSection.Upgrades);
        var kiosk2 = Equipment(config, "ff_kiosk_2", "Kiosk 2", 11, 1100,
            "Open the second self-service queue for the busiest days.", EquipmentCatalogSection.Upgrades);
        int counters = 0, kiosks = 0;
        foreach (var station in restaurant.ServiceStations)
        {
            var serialized = new SerializedObject(station);
            serialized.FindProperty("stationUpgrade").objectReferenceValue = station.IsKiosk
                ? (++kiosks == 1 ? kiosk1 : kiosk2) : (++counters == 1 ? null : cashier);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
        }
        Upgrade(config, "Card Payment", 8, 850, "Accept card payments at cashier counters.");
        Upgrade(config, "Busser Trolley", 9, 950, "Carry several used trays per cleanup trip.");
        Upgrade(config, "Waiter Trolley", 14, 1400, "Lobby Person 2 carries several table-delivery meals per trip. Hire a second Lobby Person in Staff.");

        var roles = restaurant.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoleManager>(true)).Single();
        var bindings = restaurant.GetComponent<FastFoodLobbyAuthoring>();
        var bindingData = new SerializedObject(bindings);
        if (bindingData.FindProperty("secondCashier").objectReferenceValue == null)
        {
            var station = restaurant.ServiceStations.Where(s => !s.IsKiosk).Skip(1).First();
            var home = new GameObject("Second Cashier Home");
            SceneManager.MoveGameObjectToScene(home, restaurant.gameObject.scene);
            home.transform.SetParent(restaurant.transform, true);
            home.transform.position = station.StaffApproach.position;
            var clone = UnityEngine.Object.Instantiate(roles.cashier);
            clone.name = "Second Cashier";
            SceneManager.MoveGameObjectToScene(clone, restaurant.gameObject.scene);
            clone.transform.SetParent(roles.cashier.transform.parent, true);
            clone.transform.position = home.transform.position;
            var movement = clone.GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = false;
            clone.SetActive(false);
            bindingData.FindProperty("secondCashier").objectReferenceValue = clone;
            bindingData.FindProperty("secondCashierHome").objectReferenceValue = home.transform;
            bindingData.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AuthorSeating(FastFoodProgressionSettings config, Booth[] tables)
    {
        // Stable scene names identify physical tables; array/Hierarchy order is not progression.
        string[] order = {
            "Dining Back Stool Table 1", "Dining Back Stool Table 2",
            "Dining 14 - Round Table", "Dining 15 - Round Table 2",
            "Dining 7 - Booth.010", "Dining 2 - Booth.004",
            "Dining 8 - Booth.011", "Dining 1 - Booth.003",
            "Dining 6 - Booth.009", "Dining 3 - Booth.005",
            "Dining 5 - Booth.008", "Dining 4 - Booth.006",
            "Dining 13 - Long Table.006", "Dining 12 - Long Table.005",
            "Dining 11 - Long Table.003", "Dining 10 - Long Table.001", "Dining 9 - Long Table"
        };
        if (tables.Length != order.Length || order.Any(n => tables.Count(b => b != null && b.name == n) != 1))
            throw new InvalidOperationException("Lobby2 table registry no longer matches the authored layout. Review the seating order first.");
        int[] stableIds = { 6, 17, 3, 15, 10, 1, 16, 14, 8, 9, 4, 12, 13, 7, 5, 2, 11 };
        for (int rank = 0; rank < order.Length; rank++)
        {
            Booth booth = tables.Single(b => b.name == order[rank]);
            var table = booth.GetComponent<FastFoodTable>();
            Undo.RecordObject(table, "Plan Fast Food seating");
            var data = new SerializedObject(table);
            var upgrade = data.FindProperty("seatingUpgrade").objectReferenceValue as Equipment;
            // Retain every existing physical-table purchase ID, including older saves.
            if (upgrade == null)
                upgrade = Equipment(config, "ff_seating_" + stableIds[rank], "Seating", 8,
                    booth.seats.Count == 2 ? 300 : 500,
                    "Make all seats at this table available.", EquipmentCatalogSection.BoothsAndSeating);
            if (!config.equipment.Contains(upgrade)) config.equipment.Add(upgrade);
            upgrade.dayToUnlock = rank < 4 ? rank + 2 : Mathf.Min(30, 6 + (rank - 4) * 2);
            upgrade.catalogSortOrder = rank;
            upgrade.displayName = rank < 2 ? "Back Stool Table " + (rank + 1)
                : rank < 4 ? "Round Table " + (rank - 1)
                : rank < 12 ? "Booth " + (rank - 3) : "Window Table " + (rank - 11);
            upgrade.description = "Make all " + booth.seats.Count + " seats at this table available.";
            Save(upgrade);
            data.FindProperty("seatingUpgrade").objectReferenceValue = upgrade;
            data.FindProperty("starterSeats").intValue = rank == 0 ? 2 : rank < 4 ? 1 : 0;
            data.FindProperty("layoutId").stringValue = upgrade.itemID;
            data.FindProperty("layoutPriority").intValue = rank;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(table);
        }
    }

    [MenuItem("Dine In/Fast Food/Apply Planned Seating Layout")]
    public static void ApplyPlannedSeatingLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before applying the layout.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open Lobby2 first.");
        var restaurant = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FastFoodRestaurant>(true)).Single();
        var source = new SerializedObject(restaurant).FindProperty("diningTables");
        var tables = Enumerable.Range(0, source.arraySize).Select(i => (Booth)source.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var config = Require<FastFoodProgressionSettings>(Data + "/Progression.asset");
        AuthorSeating(config, tables);
        foreach (var booth in tables)
        {
            var table = booth.GetComponent<FastFoodTable>();
            Vector3 position = booth.transform.position;
            switch (table.LayoutPriority)
            {
                case 0: position.z = 3.3f; break;
                case 1: position.z = 23.3f; break;
                case 2: position.x = -15.7f; position.z = 32f; break;
                case 3: position.x = -15.7f; position.z = 22f; break;
            }
            Undo.RecordObject(booth.transform, "Align starter seating");
            booth.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(booth.transform);
            var tableData = new SerializedObject(table);
            string dividerName = table.LayoutPriority >= 4 && table.LayoutPriority < 8 ? "Cube.005"
                : table.LayoutPriority >= 8 && table.LayoutPriority < 12 ? "Cube.006" : null;
            var divider = dividerName == null ? null : scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(t => t.name == dividerName && t.parent != null && t.parent.name == "Fast Food Wall Separated");
            tableData.FindProperty("sharedDivider").objectReferenceValue = divider != null ? divider.gameObject : null;
            tableData.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(table);
        }
        foreach (var recipe in Require<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset").Products)
            if (recipe.category == MenuProductCategory.Food && recipe.servingPrefab != null &&
                AssetDatabase.GetAssetPath(recipe.servingPrefab).StartsWith(Food + "/", StringComparison.Ordinal))
            { recipe.normalizedServingTransform = true; Save(recipe); }
        Save(config);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FastFood] Planned four starter tables / five seats, adjacent expansions, stable purchases and normalized servings. Bake navigation and save Lobby2.");
    }

    [MenuItem("Dine In/Fast Food/Validate Tray Serving Transforms")]
    public static void ValidateTrayServingTransforms()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = Require<GameObject>("Assets/_Project/Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab");
            var trayObject = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(trayObject, preview);
            var tray = trayObject.GetComponentInChildren<FoodTray>(true);
            var data = new SerializedObject(tray);
            var apply = typeof(FoodTray).GetMethod("ApplyServingTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            int checks = 0;
            foreach (var recipe in Require<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset").Products)
            {
                if (recipe.category != MenuProductCategory.Food) continue;
                if (!recipe.normalizedServingTransform) throw new InvalidOperationException(recipe.name + ": serving normalization is off.");
                foreach (string anchorName in new[] { "foodAnchor1", "foodAnchor2" })
                {
                    var anchor = (Transform)data.FindProperty(anchorName).objectReferenceValue;
                    var visual = UnityEngine.Object.Instantiate(recipe.servingPrefab, anchor);
                    try
                    {
                        apply.Invoke(null, new object[] { visual.transform, recipe });
                        var renderers = visual.GetComponentsInChildren<Renderer>(true);
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        if (Mathf.Max(bounds.size.x, bounds.size.z) > .6f || bounds.size.magnitude < .1f ||
                            Vector3.Dot(visual.transform.up, Vector3.up) < .99f || Mathf.Abs(bounds.min.y - anchor.position.y) > .02f)
                            throw new InvalidOperationException(recipe.name + " / " + anchorName + ": incorrect serving size, orientation or placement: " + bounds);
                        checks++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(visual); }
                }
            }
            if (checks != 12) throw new InvalidOperationException("Expected six foods on both tray anchors.");
            Debug.Log("[FastFood] PASS: 12 actual-tray serving checks (size, upright orientation and base placement).");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static Equipment Equipment(FastFoodProgressionSettings config, string id, string title, int day, int price,
        string description, EquipmentCatalogSection section)
    {
        var item = Asset<Equipment>(Data + "/Equipment/" + id + ".asset");
        item.itemID = id; item.displayName = title; item.dayToUnlock = day; item.cost = price;
        item.description = description; item.catalogSection = section;
        item.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ComputerIcons/EquipmentIcon.png");
        Save(item); config.equipment.Add(item); return item;
    }

    private static void Upgrade(FastFoodProgressionSettings config, string name, int day, int price, string description)
    {
        var original = Require<EquipmentUpgrade>("Assets/_Project/Office/Manager/Equipment/Upgrades/" + name + ".asset");
        string path = Data + "/Equipment/" + name + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<EquipmentUpgrade>(path);
        if (item == null) { item = UnityEngine.Object.Instantiate(original); AssetDatabase.CreateAsset(item, path); }
        item.dayToUnlock = day; item.cost = price; item.description = description;
        if (name == "Busser Trolley") item.displayName = "Lobby Trolley";
        if (name == "Waiter Trolley") item.displayName = "Delivery Trolley";
        Save(item); config.equipment.Add(item);
    }

    private static void AuthorMenu()
    {
        var catalog = Require<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset");
        var products = catalog.Products.ToList();
        var ingredients = catalog.Ingredients.ToList();
        UpdateIngredientIcons();
        var buns = ingredients.Single(i => i.itemType == ItemType.Bun);
        var chicken = ingredients.Single(i => i.itemType == ItemType.Drumsticks);
        var nuggets = Ingredient(chicken, "Frozen Nuggets", ItemType.FrozenNuggets, "ff-frozen-nuggets", 3, 150);
        var patty = Ingredient(chicken, "Chicken Patty", ItemType.ChickenPatty, "ff-chicken-patty", 5, 220);
        var fish = Ingredient(chicken, "Frozen Fish Fillet", ItemType.FrozenFishFillet, "ff-frozen-fish-fillet", 10, 260);
        foreach (var ingredient in new[] { nuggets, patty, fish })
            if (!ingredients.Contains(ingredient)) ingredients.Add(ingredient);
        Product(products.Single(r => r.recipeID == "02"), "Burger", "Burger/Burger.fbx", 1, 119, 0);
        Product(products.Single(r => r.recipeID == "03"), "Fries", "Fries/Fries.fbx", 1, 79, 1);
        Product(products.Single(r => r.recipeID == "01"), "Fried Chicken", "FriedChicken/FriedChicken.fbx", 7, 149, 4);
        AddProduct(products, "ff-chicken-nuggets", "Chicken Nuggets", "ChickenNuggets/ChickeNuggets.fbx",
            ItemTypeKitchen.ChickenNuggets, 3, 99, 2, nuggets);
        AddProduct(products, "ff-chicken-sandwich", "Chicken Sandwich", "ChickenSandwich/ChickenSandwich.fbx",
            ItemTypeKitchen.ChickenSandwich, 5, 159, 3, patty, buns);
        AddProduct(products, "ff-fish-fillet-sandwich", "Fish Fillet Sandwich", "FishFilletSandwich/FishFilletSandwich.fbx",
            ItemTypeKitchen.FishFilletSandwich, 10, 179, 5, fish, buns);
        var data = new SerializedObject(catalog);
        SetArray(data.FindProperty("products"), products.Cast<UnityEngine.Object>().ToArray());
        SetArray(data.FindProperty("ingredients"), ingredients.Cast<UnityEngine.Object>().ToArray());
        data.ApplyModifiedPropertiesWithoutUndo();
        // Preserve bundle IDs and references; use the new menu's current individual prices.
        foreach (var bundle in catalog.FoodBundles)
            bundle.customPrice = Mathf.Max(1, bundle.products.Sum(p => p.sellPrice) - 19);
        Save(catalog);
        foreach (var item in ingredients)
        {
            int unlockDay = products.Where(p => p.ingredients.Any(i => i.item == item)).Select(p => p.dayToUnlock).DefaultIfEmpty(item.dayToUnlock).Min();
            item.dayToUnlock = unlockDay;
            Save(item);
        }
    }

    [MenuItem("Dine In/Fast Food/Update Ingredient Icons")]
    public static void UpdateIngredientIcons()
    {
        var catalog = Require<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset");
        var icons = new System.Collections.Generic.Dictionary<ItemType, string>
        {
            { ItemType.Patty, "Beef Patty" }, { ItemType.Bun, "Burger Bun" },
            { ItemType.Drumsticks, "Chicken Pieces" }, { ItemType.FrenchFryBag, "Frozen Fries" },
            { ItemType.FrozenNuggets, "Frozen Nuggets" }, { ItemType.ChickenPatty, "Chicken Patty" },
            { ItemType.FrozenFishFillet, "Frozen Fish Fillet" }
        };
        foreach (var item in catalog.Ingredients)
            if (item != null && icons.TryGetValue(item.itemType, out var icon))
            {
                item.sprite = Require<Sprite>(Art + "/Ingredients/" + icon + ".png");
                Save(item);
            }
    }

    private static ItemData Ingredient(ItemData template, string icon, ItemType type, string id, int day, int cost)
    {
        string path = Data + "/Ingredients/" + icon + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null) { item = UnityEngine.Object.Instantiate(template); AssetDatabase.CreateAsset(item, path); }
        item.itemID = id; item.itemType = type; item.restaurantType = RestaurantType.FastFood;
        item.displayName = icon + " x20"; item.unitsPerBox = 20; item.boxCost = cost; item.dayToUnlock = day;
        item.requiredStorage = RestockStorageType.Frozen; item.containerType = RestockContainerType.FreezerBox;
        item.sprite = Require<Sprite>(Art + "/Ingredients/" + icon + ".png");
        Save(item); return item;
    }

    private static void AddProduct(List<Recipe> products, string id, string name, string model, ItemTypeKitchen type,
        int day, int price, int order, params ItemData[] ingredients)
    {
        var recipe = Asset<Recipe>(Data + "/Recipes/" + name + ".asset");
        recipe.recipeID = id; recipe.kitchenItemType = type; recipe.restaurantType = RestaurantType.FastFood;
        recipe.ingredients = ingredients.Select(item => new RecipeIngredient { item = item, amount = 1 }).ToList();
        Product(recipe, name, model, day, price, order);
        if (!products.Contains(recipe)) products.Add(recipe);
    }

    private static void Product(Recipe recipe, string name, string modelPath, int day, int price, int order)
    {
        recipe.recipeName = name; recipe.dayToUnlock = day; recipe.sellPrice = price; recipe.menuSortOrder = order;
        recipe.sprite = Require<Sprite>(Art + "/Menu/" + name + ".png");
        recipe.descriptionText = name + " prepared fresh for Fast Food service.";
        recipe.servingPositionOffset = Vector3.zero; recipe.servingRotation = Vector3.zero; recipe.servingScale = Vector3.one;
        recipe.normalizedServingTransform = true;
        var root = new GameObject(name + " Serving");
        try
        {
            var model = UnityEngine.Object.Instantiate(Require<GameObject>(Art + "/Models/" + modelPath), root.transform);
            model.name = "Model";
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(name + " has no food meshes.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width <= .00001f) throw new InvalidOperationException(name + " has invalid bounds.");
            float factor = .48f / width;
            model.transform.localScale *= factor;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            recipe.servingPrefab = PrefabUtility.SaveAsPrefabAsset(root, Food + "/" + name + ".prefab");
            Save(recipe);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void AuthorBossUI()
    {
        const string path = "Assets/_Project/Resources/UI/RestaurantBossTips.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject("RestaurantBossTips", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(RestaurantBossTips), typeof(TutorialDialogueUI));
        try
        {
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("TipPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            var rect = (RectTransform)panel.transform; rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 45); rect.sizeDelta = new Vector2(850, 180);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.055f, .105f, .16f, .96f);
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var group = panel.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var speaker = Text(rect, "Speaker", new Vector2(145, -10), new Vector2(670, 38), 26);
            speaker.text = "BIG BOSS"; speaker.color = new Color(1, .8f, .3f);
            var body = Text(rect, "Body", new Vector2(145, -50), new Vector2(670, 115), 24);
            body.enableAutoSizing = true; body.fontSizeMin = 20; body.fontSizeMax = 24;
            var portrait = new GameObject("Big Boss", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var pr = (RectTransform)portrait.transform; pr.SetParent(rect, false);
            pr.anchorMin = pr.anchorMax = new Vector2(0, .5f); pr.pivot = new Vector2(0, .5f);
            pr.anchoredPosition = new Vector2(8, 0); pr.sizeDelta = new Vector2(130, 180);
            var image = portrait.GetComponent<UnityEngine.UI.Image>(); image.preserveAspect = true; image.raycastTarget = false;
            image.sprite = Require<Sprite>("Assets/_Project/UI/Assets/Tutorial Images/Tutorial  Explaining Pose.png");
            var dialogue = root.GetComponent<TutorialDialogueUI>();
            var serialized = new SerializedObject(dialogue);
            serialized.FindProperty("root").objectReferenceValue = panel;
            serialized.FindProperty("speakerText").objectReferenceValue = speaker;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("portraitImage").objectReferenceValue = image;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var tips = new SerializedObject(root.GetComponent<RestaurantBossTips>());
            tips.FindProperty("dialogue").objectReferenceValue = dialogue;
            tips.FindProperty("portrait").objectReferenceValue = image.sprite;
            tips.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
        return text;
    }
    private static T Require<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);
    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    private static void Save(UnityEngine.Object value) { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
    private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    [MenuItem("Dine In/Fast Food/Validate Progression")]
    public static void Validate()
    {
        var config = Require<FastFoodProgressionSettings>(Data + "/Progression.asset");
        if (config.equipment.Count == 0 || config.equipment.Any(e => e == null) ||
            config.equipment.Select(e => e.itemID).Distinct().Count() != config.equipment.Count)
            throw new InvalidOperationException("Equipment IDs must be unique.");
        var catalog = Require<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset");
        var foods = catalog.Products.Where(p => p.category == MenuProductCategory.Food).ToArray();
        if (foods.Length != 6 || foods.Any(p => p.servingPrefab == null || p.sprite == null || p.ingredients.Count == 0))
            throw new InvalidOperationException("Six foods must have models, icons and ingredients.");
        if (foods.Count(p => p.dayToUnlock == 1) != 2) throw new InvalidOperationException("Day 1 must offer burger and fries only.");
        if (catalog.Ingredients.Select(i => i.itemType).Distinct().Count() != catalog.Ingredients.Count)
            throw new InvalidOperationException("Ingredient types must not collide.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var restaurant = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FastFoodRestaurant>(true)).Single();
        var tableArray = new SerializedObject(restaurant).FindProperty("diningTables");
        int starterSeats = 0;
        for (int i = 0; i < tableArray.arraySize; i++)
        {
            var table = ((Booth)tableArray.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<FastFoodTable>();
            var data = new SerializedObject(table);
            starterSeats += data.FindProperty("starterSeats").intValue;
        }
        if (starterSeats != 5) throw new InvalidOperationException("Expected exactly five starter seats.");
        if (restaurant.ServiceStations.Count(s => new SerializedObject(s).FindProperty("stationUpgrade").objectReferenceValue == null) != 1)
            throw new InvalidOperationException("Exactly one station must start available.");
        var bindings = restaurant.GetComponent<FastFoodLobbyAuthoring>();
        if (bindings.SecondCashier == null || bindings.SecondCashierHome == null)
            throw new InvalidOperationException("Second cashier authoring missing.");
        for (int day = 1; day <= 30; day++)
            if (config.Evaluate(config.groups, day) < 1 || config.Evaluate(config.patienceSeconds, day) < 30)
                throw new InvalidOperationException("Invalid difficulty curve.");
        Require<GameObject>("Assets/_Project/Resources/UI/RestaurantBossTips.prefab");
        Debug.Log("[FastFoodProgression] PASS: six modeled foods, unique ingredients/equipment, five starter seats, one starter counter, cashier two, day curves and shared Boss UI.");
    }
}
