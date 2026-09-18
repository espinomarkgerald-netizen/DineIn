#if UNITY_EDITOR
using System;
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
    private const string NavigationPath = "Assets/_Project/Scenes/RoleBased/Lobby2Navigation.asset";

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
        var data = new SerializedObject(restaurant);
        foreach (string field in new[] { "counterQueue", "counterFlow", "kitchen", "customerPickupPoint", "cashierApproach", "customerExit", "cardPaymentPrefab" })
            Require(data.FindProperty(field).objectReferenceValue != null, "Missing Fast Food reference: " + field);
        Require(data.FindProperty("waitingPoints").arraySize > 0, "Author paid waiting points.");
        var waiting = data.FindProperty("waitingPoints");
        for (int i = 0; i < waiting.arraySize; i++)
            Require(waiting.GetArrayElementAtIndex(i).objectReferenceValue != null, "Missing waiting point " + i);
        var tables = data.FindProperty("diningTables");
        Require(tables.arraySize > 0, "Author at least one dining table.");
        for (int i = 0; i < tables.arraySize; i++)
        {
            var booth = tables.GetArrayElementAtIndex(i).objectReferenceValue as Booth;
            Require(booth != null && booth.gameObject.activeInHierarchy, "Inactive/missing dining table " + i);
            Require(booth.approachPoint != null && booth.NetworkTrayPoint != null && booth.seats.Count > 0 && booth.seats.All(seat => seat != null), booth.name + ": missing service anchors.");
            Require(booth.GetComponent<BoothDeliverInteractable>() != null, booth.name + ": missing tray delivery interaction.");
            Require(booth.gameObject.layer == LayerMask.NameToLayer("Booth"), booth.name + ": incorrect interaction layer.");
            Require(booth.GetComponentInChildren<BoothMessCleanUI>(true) != null, booth.name + ": missing cleaning UI.");
            var table = booth.GetComponent<FastFoodTable>();
            Require(table != null && table.Furniture != null, booth.name + ": imported furniture could not be resolved.");
        }
        var kitchen = data.FindProperty("kitchen").objectReferenceValue as KitchenManager;
        var kitchenData = new SerializedObject(kitchen);
        Require(kitchen.foodTrayPrefab != null && kitchen.traySpawnPoints.Length > 0, "Missing dine-in kitchen output.");
        Require(kitchenData.FindProperty("takeoutBagPrefab").objectReferenceValue != null && kitchenData.FindProperty("takeoutSpawnPoints").arraySize > 0, "Missing takeaway kitchen output.");
        var services = restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyAutonomousService>(true)).ToArray();
        Require(services.Length == 1 && services[0].GetComponent<GameDayManager>() != null,
            "Author one staff service on GameManager so its existing bootstrap does not add a second instance.");
        Require(restaurant.GetComponentInChildren<SinkInteractable>() != null, "Missing active Fast Food sink.");
        Debug.Log("[FastFood] Lobby2 structure passed. This does not validate gameplay or navigation.", restaurant);
    }

    [MenuItem("Dine In/Fast Food/Bake Lobby2 Navigation")]
    public static void BakeNavigation()
    {
        FastFoodRestaurant restaurant = Restaurant();
        Validate();
        NavMeshSurface surface = restaurant.GetComponent<NavMeshSurface>();
        Require(surface != null, "Fast Food Services needs its authored NavMeshSurface.");
        surface.BuildNavMesh();
        Require(surface.navMeshData != null, "Lobby2 navigation bake returned no data.");
        var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath);
        if (asset == null) AssetDatabase.CreateAsset(surface.navMeshData, NavigationPath);
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
        var surface = restaurant.GetComponent<NavMeshSurface>();
        Require(surface != null && surface.navMeshData != null, "Lobby2 navigation has not been baked. Use Dine In > Fast Food > Bake Lobby2 Navigation.");
        var data = new SerializedObject(restaurant);
        var start = (Transform)data.FindProperty("cashierApproach").objectReferenceValue;
        Require(NavMesh.SamplePosition(start.position, out var origin, 1f, NavMesh.AllAreas), "Cashier approach is outside the navigation mesh.");
        var points = restaurant.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
            .Where(t => t.name == "ApproachPoint" && t.GetComponentInParent<FastFoodTable>() != null
                || t.name.StartsWith("Paid Waiting ") || t.name == "Customer Pickup Approach"
                || t.name == "Customer Order Point" || t.name == "Sink Approach"
                || t.name == "Customer Entrance" || t.name == "Customer Exit"
                || t.name.EndsWith("HomePoint") || t.name.EndsWith("TrolleyParkingPoint")
                || t.name == "ChefPrepPoint" || t.name == "ChefCookPoint"
                || t.name == "BaristaServePoint" || t.name == "BaristaDrinkPoint");
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
            Require(!group.FastFoodPaid || TakeoutQueueManager.Instance.CurrentFront != group,
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
