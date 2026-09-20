#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit edit-time tools. Nothing runs automatically on import or in Play mode.</summary>
public static class FastFoodSceneTools
{
    private const string NavigationPath = "Assets/_Project/Scenes/RoleBased/Lobby2/NavMesh-Fast Food Revamp.asset";

    private static FastFoodRestaurant Restaurant()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use Fast Food authoring tools outside Play mode.");
        var scene = SceneManager.GetSceneByName("Lobby2");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open Lobby2 first.");
        var services = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FastFoodRestaurant>(true)).ToArray();
        if (services.Length != 1) throw new InvalidOperationException("Lobby2 needs exactly one authored Fast Food Services object.");
        return services[0];
    }

    [MenuItem("Dine In/Fast Food/Validate Lobby2 Structure")]
    public static void Validate()
    {
        FastFoodRestaurant restaurant = Restaurant();
        Require(restaurant.ValidateServiceSetup(out var setupProblem), setupProblem);
        var data = new SerializedObject(restaurant);
        foreach (string field in new[] { "counterQueue", "counterFlow", "kitchen", "customerPickupPoint", "cashierApproach", "customerExit", "cardPaymentPrefab" })
            Require(data.FindProperty(field).objectReferenceValue != null, "Missing Fast Food reference: " + field);
        Require(data.FindProperty("waitingPoints").arraySize > 0, "Author paid waiting points.");
        var waiting = data.FindProperty("waitingPoints");
        for (int i = 0; i < waiting.arraySize; i++)
            Require(waiting.GetArrayElementAtIndex(i).objectReferenceValue != null, "Missing waiting point " + i);
        var tables = data.FindProperty("diningTables");
        Require(tables.arraySize > 0, "Author at least one dining table.");
        var registered = new HashSet<Booth>();
        for (int i = 0; i < tables.arraySize; i++)
        {
            var booth = tables.GetArrayElementAtIndex(i).objectReferenceValue as Booth;
            Require(booth != null && booth.gameObject.activeInHierarchy, "Inactive/missing dining table " + i);
            Require(registered.Add(booth), booth.name + ": registered more than once.");
            Require(PrefabUtility.IsPartOfPrefabInstance(booth), booth.name + ": use an authored Fast Food table prefab.");
            Require(booth.approachPoint != null && booth.NetworkTrayPoint != null && booth.seats.Count > 0 && booth.seats.All(seat => seat != null), booth.name + ": missing service anchors.");
            Require(booth.seats.Distinct().Count() == booth.seats.Count && booth.seats.All(seat => seat.IsChildOf(booth.transform)), booth.name + ": duplicate or foreign seats.");
            Require(booth.seats.Select(seat => seat.position).Distinct().Count() == booth.seats.Count, booth.name + ": overlapping seats.");
            Require(booth.approachPoint.IsChildOf(booth.transform) && booth.NetworkTrayPoint.IsChildOf(booth.transform), booth.name + ": service points belong to another object.");
            Require(booth.GetComponentsInChildren<BoothDeliverInteractable>(true).Length == 1, booth.name + ": needs exactly one tray delivery interaction.");
            Require(booth.GetComponent<BoothDeliverInteractable>().DeliveryPoint == booth.NetworkTrayPoint, booth.name + ": delivery and tray points disagree.");
            Require(booth.GetComponentsInChildren<CustomerDeliverInteractable>(true).Length == 0, booth.name + ": duplicate legacy delivery interaction.");
            Require(booth.GetComponents<Collider>().Any(collider => collider.enabled), booth.name + ": no enabled click collider.");
            Require(booth.gameObject.layer == LayerMask.NameToLayer("Booth"), booth.name + ": incorrect interaction layer.");
            Require(booth.GetComponentsInChildren<BoothMessCleanUI>(true).Length == 1, booth.name + ": missing or duplicate cleaning UI.");
            var table = booth.GetComponent<FastFoodTable>();
            Require(table != null && table.Furniture != null && table.Furniture.transform.IsChildOf(booth.transform), booth.name + ": furniture must be inside its prefab.");
            Require(table.ValidateAuthoring(out var tableProblem), booth.name + ": " + tableProblem);
            var hygiene = booth.GetComponent<HygieneSurface>();
            Require(hygiene != null && !hygiene.exclude && hygiene.area == HygieneArea.Lobby && hygiene.kind == HygieneSurfaceKind.Dining, booth.name + ": missing dining hygiene settings.");
            var boothData = new SerializedObject(booth);
            foreach (string field in new[] { "tableLookTarget", "tableNumberAnchor", "cleanUIRoot", "cleanUI" })
                Require(boothData.FindProperty(field).objectReferenceValue != null, booth.name + ": missing " + field);
            Require(boothData.FindProperty("menuBookPrefab").objectReferenceValue == null && booth.CurrentGroup == null && !booth.IsDirty, booth.name + ": prefab contains legacy menu or runtime state.");
            Require(!booth.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component != null &&
                (component.GetType().Name == "BoothMoneySpawner" || component.GetType().Name == "PaymentPickupInteractable" || component.GetType().Name == "BoothPuddleSpawner")), booth.name + ": legacy table billing/spill component.");
        }
        Require(SceneComponents<FastFoodTable>(restaurant).All(table => registered.Contains(table.GetComponent<Booth>())), "A Fast Food table is missing from diningTables.");
        foreach (var transform in SceneComponents<Transform>(restaurant))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, transform.name + ": missing script.");
        var kitchen = data.FindProperty("kitchen").objectReferenceValue as KitchenManager;
        var kitchenData = new SerializedObject(kitchen);
        Require(kitchen.foodTrayPrefab != null && kitchen.traySpawnPoints.Length > 0, "Missing dine-in kitchen output.");
        var carryPose = kitchen.foodTrayPrefab.GetComponent<FoodTrayCarryPose>();
        Require(carryPose != null && carryPose.IsValid, "Author tray carry origin and both hand grips on Food Tray.");
        var previews = SceneComponents<FastFoodShelfPreview>(restaurant);
        Require(previews.Length == 2, "Author one stock preview on each storage bank.");
        foreach (var preview in previews)
        {
            var slots = new SerializedObject(preview).FindProperty("slots");
            Require(slots.arraySize > 0, preview.name + ": missing preview slots.");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                Require(slot != null && slot.transform.IsChildOf(preview.transform), "Preview slot must belong to its storage bank.");
                Require(slot.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    slot.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null && c.GetType().Namespace != null &&
                        (c.GetType().Namespace.StartsWith("TMPro") || c.GetType().Namespace.StartsWith("UnityEngine.UI"))),
                    slot.name + ": previews must contain art/UI only, without inventory or interaction components.");
            }
        }
        Require(kitchenData.FindProperty("takeoutBagPrefab").objectReferenceValue != null && kitchenData.FindProperty("takeoutSpawnPoints").arraySize > 0, "Missing takeaway kitchen output.");
        var services = restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyAutonomousService>(true)).ToArray();
        Require(services.Length == 1 && services[0].GetComponent<GameDayManager>() != null,
            "Author one staff service on GameManager so its existing bootstrap does not add a second instance.");
        Require(restaurant.GetComponentInChildren<SinkInteractable>() != null, "Missing active Fast Food sink.");
        ValidateStations(restaurant);
        var bindings = restaurant.GetComponent<FastFoodLobbyAuthoring>();
        Require(bindings != null, "Add the authored Lobby2 room and storage bindings.");
        Require(bindings.ValidateAuthoring(out var bindingProblem), bindingProblem);
        Require(SceneComponents<ManagementComputerStation>(restaurant).Single() == bindings.RoomComputer,
            "Only the computer in the office room may be active.");
        var entrances = SceneComponents<RestockStockRoomEntrance>(restaurant);
        Require(entrances.Length == 2 && entrances.Contains(bindings.DryStorageEntrance) && entrances.Contains(bindings.FreezerEntrance),
            "Author exactly two shelf entrances into the shared RestockScene.");
        Require(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.EndsWith("/RestockScene.unity")),
            "Enable the shared RestockScene in Build Settings.");
        foreach (var entrance in entrances)
            Require(entrance.GetComponent<Collider>().enabled && entrance.gameObject.layer == LayerMask.NameToLayer("Interactable "),
                entrance.name + ": shelf collider is not clickable.");
        foreach (var station in SceneComponents<FastFoodCounter>(restaurant).Cast<Component>()
            .Concat(SceneComponents<SinkInteractable>(restaurant)).Concat(SceneComponents<ManagementComputerStation>(restaurant)))
            Require(station.GetComponent<Outline>() != null, station.name + ": missing authored outline.");
        Require(SceneComponents<TapOutlineSelector>(restaurant).Length == 1, "Author exactly one outline selector in Lobby2.");
        foreach (string path in new[] {
            "Assets/_Project/Art/Models/Customer/Old Alien Models/GreenCustomer.prefab",
            "Assets/_Project/Art/Models/Customer/Old Alien Models/BlueCustomer.prefab",
            "Assets/_Project/Art/Models/Customer/Old Alien Models/PinkCustomer.prefab",
            "Assets/_Project/Restaurant/Assets/Level1/GameMechanics/Customer.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Missing customer prefab: " + path);
            var agent = prefab.GetComponentInChildren<CustomerAgent>(true);
            Require(agent != null, path + ": missing CustomerAgent.");
            var agentData = new SerializedObject(agent);
            foreach (string field in new[] { "trayCarryAnchor", "trayLeftGrip", "trayRightGrip" })
            {
                var anchor = agentData.FindProperty(field).objectReferenceValue as Transform;
                Require(anchor != null && anchor.IsChildOf(agent.transform), path + ": missing authored " + field);
            }
            Require(Vector3.Dot(agent.TrayCarryAnchor.forward, agent.transform.up) > .99f,
                path + ": tray carry frame must keep the tray horizontal.");
        }
        ValidateHUD();
        Require(restaurant.NavigationSurface != null && restaurant.NavigationSurface.gameObject.scene == restaurant.gameObject.scene
            && restaurant.NavigationSurface.isActiveAndEnabled, "Assign the active Lobby2 navigation surface to Fast Food Services.");
        Debug.Log("[FastFood] Lobby2 structure passed. This does not validate gameplay or navigation.", restaurant);
    }

    private static T[] SceneComponents<T>(FastFoodRestaurant restaurant) where T : Component =>
        restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
            .Where(component => component.gameObject.activeInHierarchy).ToArray();

    private static void ValidateStations(FastFoodRestaurant restaurant)
    {
        var computers = SceneComponents<ManagementComputerStation>(restaurant);
        var counters = SceneComponents<FastFoodCounter>(restaurant);
        var sinks = SceneComponents<SinkInteractable>(restaurant);
        Require(computers.Length == 1 && counters.Length == 2 && sinks.Length == 1, "Author one computer, two Fast Food cashiers and one sink.");
        var serviceStations = SceneComponents<FastFoodServiceStation>(restaurant);
        Require(serviceStations.Length == 4 && serviceStations.Count(s => s.IsKiosk) == 2,
            "Author two independent counters and two kiosks.");
        Require(serviceStations.Select(s => s.Queue).Distinct().Count() == 4 &&
            serviceStations.Select(s => s.Flow).Distinct().Count() == 4, "Each service station needs its own queue and flow.");
        foreach (var service in serviceStations)
        {
            Require(service.Queue != null && service.Flow != null && service.StaffApproach != null,
                service.name + ": incomplete station references.");
            Require(service.CompanionPoints.Length == 3 && service.CompanionPoints.All(p => p != null && p.IsChildOf(service.Queue.OrderPoint)) &&
                service.CompanionPoints.Distinct().Count() == 3, service.name + ": author three distinct companions under the order point.");
            Require(service.Queue.ValidateAuthoring(out var problem), service.name + ": " + problem);
        }
        foreach (Component component in new Component[] { computers[0], counters[0], counters[1], sinks[0] })
        {
            var station = (IInteractable)component;
            var stationData = new SerializedObject(component);
            Require(component.GetComponents<Collider>().Any(collider => collider.enabled), component.name + ": missing authored click collider.");
            Require(stationData.FindProperty("standPoint").objectReferenceValue != null && station.StandPoint.IsChildOf(component.transform), component.name + ": author a stand point inside the station.");
        }
        Require(new SerializedObject(computers[0]).FindProperty("controller").objectReferenceValue != null, "Connect the computer to ManagementComputerCanvas.");
        foreach (var counter in counters)
        {
            var serialized = new SerializedObject(counter);
            Require(serialized.FindProperty("restaurant").objectReferenceValue == restaurant &&
                serialized.FindProperty("station").objectReferenceValue != null, "Connect each cashier to its restaurant and station.");
        }
        Require(restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CashierRegisterUI>(true)).Count() == 1, "Missing/duplicate cashier UI.");
    }

    private static void ValidateHUD()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/LobbyHUD.prefab");
        Require(root != null && root.GetComponent<LobbyHUDRoot>() != null, "Missing complete LobbyHUD prefab.");
        var controls = root.GetComponentInChildren<LobbyHUDRedesign>(true);
        Require(controls != null && root.GetComponentInChildren<CasualDiningProgressHUD>(true) != null
            && root.GetComponentInChildren<PlayerTaskHUD>(true) != null && root.GetComponentInChildren<LobbyPauseMenuView>(true) != null, "Incomplete combined HUD.");
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            Require(canvas.transform.localScale.x > 0f && canvas.transform.localScale.y > 0f, canvas.name + ": zero HUD canvas scale.");
        foreach (string button in new[] { "CameraButton", "ComputerButton", "NewspaperButton", "TaskButton" })
        {
            var transform = controls.transform.Find("SafeArea/" + button);
            Require(transform != null && transform.gameObject.activeSelf && transform.GetComponent<UnityEngine.UI.Button>() != null, "Missing/inactive HUD button: " + button);
        }
    }

    [MenuItem("Dine In/Fast Food/Bake Lobby2 Navigation")]
    public static void BakeNavigation()
    {
        FastFoodRestaurant restaurant = Restaurant();
        Validate();
        NavMeshSurface surface = restaurant.NavigationSurface;
        Require(surface != null, "Fast Food Services needs its authored NavMeshSurface.");
        // Never overwrite a shared or other restaurant's navigation asset.
        string existingPath = AssetDatabase.GetAssetPath(surface.navMeshData);
        string destination = existingPath.StartsWith("Assets/_Project/Scenes/RoleBased/Lobby2/", StringComparison.Ordinal)
            ? existingPath : NavigationPath;
        surface.BuildNavMesh();
        Require(surface.navMeshData != null, "Lobby2 navigation bake returned no data.");
        var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(destination);
        if (asset == null) AssetDatabase.CreateAsset(surface.navMeshData, destination);
        else
        {
            surface.RemoveData();
            EditorUtility.CopySerialized(surface.navMeshData, asset);
            surface.navMeshData = asset;
            surface.AddData();
        }
        // Old scene surfaces may point at a shared Lobby1 asset. Never rebake or
        // overwrite those assets; disable their Lobby2 instances only.
        foreach (var other in restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)))
            if (other != surface) { Undo.RecordObject(other, "Use Lobby2 navigation"); other.RemoveData(); other.enabled = false; EditorUtility.SetDirty(other); }
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(restaurant.gameObject.scene);
        AssetDatabase.SaveAssets();
        ValidateNavigation();
        Debug.Log("[FastFood] Navigation baked. Save Lobby2 to keep its surface reference and overrides.", restaurant);
    }

    [MenuItem("Dine In/Fast Food/Validate Lobby2 Navigation")]
    public static void ValidateNavigation()
    {
        var restaurant = Restaurant();
        var surface = restaurant.NavigationSurface;
        Require(surface != null && surface.navMeshData != null, "Lobby2 navigation has not been baked. Use Dine In > Fast Food > Bake Lobby2 Navigation.");
        var data = new SerializedObject(restaurant);
        var start = (Transform)data.FindProperty("cashierApproach").objectReferenceValue;
        Require(NavMesh.SamplePosition(start.position, out var origin, 1f, NavMesh.AllAreas), "Cashier approach is outside the navigation mesh.");
        var points = restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
            .Where(t => t.name == "ApproachPoint" && t.GetComponentInParent<FastFoodTable>() != null
                || t.name.StartsWith("Paid Waiting ") || t.name.StartsWith("Customer Pickup ") || t.name.StartsWith("Outside Group ") || t.name.StartsWith("Companion ")
                || t.name == "Customer Order Point" || t.name == "Sink Approach" || t.name == "ManagementComputerStandPoint"
                || t.name.StartsWith("Customer Spawn") || t.name.StartsWith("Left Entrance") || t.name == "Customer Exit"
                || t.name.StartsWith("QueuePoint_") || t.name == "OverflowRoot" || t.name == "Overflow Root" || t.name == "Cashier Approach"
                || t.name.EndsWith("HomePoint") || t.name.EndsWith("TrolleyParkingPoint")
                || t.name == "ChefPrepPoint" || t.name == "ChefCookPoint"
                || t.name == "BaristaServePoint" || t.name == "BaristaDrinkPoint");
        var bindings = restaurant.GetComponent<FastFoodLobbyAuthoring>();
        Require(bindings != null && bindings.ValidateAuthoring(out _), "Assign the Lobby2 room and storage references first.");
        points = points.Concat(new[] { bindings.RoomComputer.StandPoint, bindings.DryStorageEntrance.StandPoint,
            bindings.FreezerEntrance.StandPoint, bindings.CashierHome, bindings.BusserHome }).Distinct();
        foreach (var point in points)
        {
            Require(NavMesh.SamplePosition(point.position, out var destination, 1f, NavMesh.AllAreas), point.name + " is outside the navigation mesh.");
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(origin.position, destination.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "No complete route to " + point.parent.name + "/" + point.name);
        }
        Debug.Log("[FastFood] Authored service routes are connected.", restaurant);
    }

    [MenuItem("Dine In/Fast Food/Check Live Service Invariants")]
    public static void CheckLiveService()
    {
        Require(Application.isPlaying, "Run this check during an external Lobby2 playtest.");
        var restaurant = UnityEngine.Object.FindFirstObjectByType<FastFoodRestaurant>();
        Require(restaurant != null && restaurant.Operational, "A live Lobby2 restaurant is required.");
        var groups = UnityEngine.Object.FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None)
            .Where(group => group.FastFood == restaurant).ToArray();
        var trays = UnityEngine.Object.FindObjectsByType<FoodTray>(FindObjectsSortMode.None);
        var bags = UnityEngine.Object.FindObjectsByType<TakeoutBagInteractable>(FindObjectsSortMode.None);
        foreach (var group in groups)
        {
            Require(group.FastFoodPaid || group.assignedBooth == null, group.name + ": seated before payment.");
            Require(group.FastFoodDineIn || group.assignedBooth == null, group.name + ": takeaway customer seated.");
            if (group.FastFoodPaid && group.assignedBooth != null)
                Require(group.assignedBooth.CurrentGroup == group, group.name + ": table reservation disagrees with customer.");
            Require(trays.Count(tray => tray.TargetGroup == group) <= 1, group.name + ": duplicate trays.");
            Require(bags.Count(bag => bag.TargetGroup == group) <= 1, group.name + ": duplicate takeaway bags.");
            Require(!group.FastFoodPaid || TakeoutQueueManager.For(group)?.CurrentFront != group,
                group.name + ": paid customer still blocking counter.");
        }
        Require(groups.Where(group => group.assignedBooth != null)
            .GroupBy(group => group.assignedBooth).All(table => table.Count() == 1), "A table is reserved by multiple groups.");
        Debug.Log("[FastFood] Live service invariants passed for " + groups.Length + " groups. Continue the human-control regression checklist.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
