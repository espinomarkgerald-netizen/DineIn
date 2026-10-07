#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit Lobby3 authoring checks; nothing bakes during gameplay or import.</summary>
public static class FineDiningSceneTools
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby3.unity";
    private const string ModelPath = "Assets/_Project/Restaurant/Assets/Fine Dining.fbx";
    private const string NavigationPath = "Assets/_Project/Scenes/RoleBased/Lobby3-NavMesh.asset";

    private static Scene Scene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Fine Dining authoring tools run in edit mode only.");
        var scene = SceneManager.GetSceneByName("Lobby3");
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            throw new InvalidOperationException("Open Lobby3 before running this tool.");
        return scene;
    }

    private static NavMeshSurface Surface(Scene scene)
    {
        var active = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>())
            .Where(surface => surface.isActiveAndEnabled).ToArray();
        if (active.Length != 1 || active[0].name != "Fine Dining Navigation")
            throw new InvalidOperationException("Lobby3 needs exactly one active Fine Dining Navigation surface.");
        return active[0];
    }

    [MenuItem("Dine In/Fine Dining/Validate Lobby3 Foundation")]
    public static void ValidateFoundation()
    {
        var scene = Scene();
        var roots = scene.GetRootGameObjects();
        var model = roots.SingleOrDefault(root => root.name == "Fine Dining");
        if (model == null || !model.activeInHierarchy || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model) != ModelPath)
            throw new InvalidOperationException("Lobby3 must contain the supplied Fine Dining.fbx model instance.");
        if (model.GetComponentsInChildren<MeshCollider>().Length == 0)
            throw new InvalidOperationException("Reimport the Fine Dining FBX with model colliders enabled.");
        var manager = roots.SelectMany(root => root.GetComponentsInChildren<GameDayManager>()).Single();
        var data = new SerializedObject(manager);
        if (data.FindProperty("restaurantSceneName").stringValue != "Lobby3" ||
            data.FindProperty("openingHour").intValue != 17 || data.FindProperty("closingHour").intValue != 22)
            throw new InvalidOperationException("Lobby3 scene identity or 17:00–22:00 shift is missing.");
        if (MenuCatalog.ForScene("Lobby3") == null || MenuCatalog.ForScene("Lobby3").RestaurantType != RestaurantType.FineDining)
            throw new InvalidOperationException("Lobby3 is not bound to the Fine Dining menu catalog.");
        ValidateServiceBindings(scene);
        Surface(scene);
        Debug.Log("[FineDining] Lobby3 foundation references passed. Navigation and gameplay still need validation.", manager);
    }

    [MenuItem("Dine In/Fine Dining/Apply Lobby3 Furniture Bindings")]
    public static void ApplyFurnitureBindings()
    {
        var scene = Scene();
        var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FineDiningSceneBindings>()).Single();
        foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Bind Fine Dining furniture");
        binding.ApplyBindings();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FineDining] Furniture interaction anchors applied. Bake navigation and save Lobby3.", binding);
    }

    [MenuItem("Dine In/Fine Dining/Bake Lobby3 Navigation")]
    public static void BakeNavigation()
    {
        // Authored layouts must not be overwritten merely by baking navigation.
        var binding = Scene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FineDiningSceneBindings>()).Single();
        if (binding.applyBindingsOnAwake) ApplyFurnitureBindings();
        ValidateFoundation();
        var scene = Scene();
        var surface = Surface(scene);
        // Never overwrite Lobby1's serialized NavMesh or another restaurant's asset.
        var existingPath = AssetDatabase.GetAssetPath(surface.navMeshData);
        var destination = existingPath == NavigationPath ? existingPath : NavigationPath;
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("Lobby3 navigation bake returned no data.");
        var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(destination);
        if (asset == null) AssetDatabase.CreateAsset(surface.navMeshData, destination);
        else
        {
            surface.RemoveData();
            EditorUtility.CopySerialized(surface.navMeshData, asset);
            surface.navMeshData = asset;
            surface.AddData();
        }
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssetIfDirty(surface.navMeshData);
        ValidateNavigation();
        Debug.Log("[FineDining] Navigation baked. Save Lobby3 to keep this asset reference.", surface);
    }

    private static void ValidateServiceBindings(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var errors = new List<string>();
        var binding = roots.SelectMany(r => r.GetComponentsInChildren<FineDiningSceneBindings>()).SingleOrDefault();
        var reservations = roots.SelectMany(r => r.GetComponentsInChildren<FineDiningReservations>()).SingleOrDefault();
        if (binding == null || reservations == null)
            throw new InvalidOperationException("Lobby3 needs one furniture binding and one reservation component.");
        if (binding.dryStorage == null || binding.freezer == null || binding.dryStorage == binding.freezer)
            errors.Add("Dry storage and freezer must have separate assigned entrances.");
        if (binding.receptionMonitor == null || binding.receptionMonitor.GetComponent<MeshFilter>()?.sharedMesh == null)
            errors.Add("Reception monitor mesh is missing.");
        if (reservations.dayManager == null || reservations.receptionLine == null)
            errors.Add("Reservation clock or reception queue is missing.");
        var ids = new HashSet<string>();
        var booths = new HashSet<Booth>();
        var furnitureTargets = new HashSet<string>();
        foreach (var table in binding.tables)
        {
            if (table == null || table.binding == null || table.binding.booth == null)
            { errors.Add("Missing table/booth binding."); continue; }
            var t = table.binding;
            if (string.IsNullOrWhiteSpace(t.tableId) || !ids.Add(t.tableId)) errors.Add("Missing/duplicate persistent table ID: " + t.tableId);
            if (!booths.Add(t.booth)) errors.Add("Duplicate table interaction: " + t.displayName);
            if (!furnitureTargets.Add(table.furniture)) errors.Add("Duplicate furniture interaction: " + table.furniture);
            if (reservations.tables == null || !reservations.tables.Contains(t)) errors.Add(t.displayName + " is missing from reservations.");
            if (table.seatOffsets == null || table.seatOffsets.Length == 0 || t.Capacity != table.seatOffsets.Length ||
                t.booth.seats.Distinct().Count() != t.booth.seats.Count)
                errors.Add(t.displayName + " has invalid seats. Apply Furniture Bindings before validating.");
            if (t.booth.approachPoint == null || t.booth.menuSpawnPoint == null || t.booth.tableLookTarget == null ||
                t.booth.GetComponent<BoothMoneySpawner>() == null || t.booth.GetComponent<CustomerDeliverInteractable>() == null)
                errors.Add(t.displayName + " is missing service or payment references.");
            try { binding.FindFurniture(table.furniture); } catch (Exception e) { errors.Add(e.Message); }
        }
        foreach (var anchor in binding.anchors)
        {
            if (anchor.target == null) errors.Add("Missing scene anchor: " + anchor.furniture);
            try { binding.FindFurniture(anchor.furniture); } catch (Exception e) { errors.Add(e.Message); }
        }
        var stations = roots.SelectMany(r => r.GetComponentsInChildren<ManagementComputerStation>()).ToArray();
        var controller = roots.SelectMany(r => r.GetComponentsInChildren<ManagementComputerController>(true)).SingleOrDefault();
        if (stations.Length != 2 || stations.Count(s => s.Purpose == RestaurantComputerPurpose.Management) != 1 ||
            stations.Count(s => s.Purpose == RestaurantComputerPurpose.Reservations) != 1)
            errors.Add("Expected one office computer and one reception reservation computer.");
        foreach (var station in stations)
        {
            var data = new SerializedObject(station);
            if (controller == null || data.FindProperty("controller").objectReferenceValue != controller ||
                data.FindProperty("standPoint").objectReferenceValue == null || station.GetComponent<Collider>() == null)
                errors.Add(station.name + " is missing its controller, approach or click target.");
        }
        var targets = stations.Select(s => s.GetComponent<Collider>()).Concat(
            roots.SelectMany(r => r.GetComponentsInChildren<CashierBoothInteractable>()).Select(c => c.GetComponent<Collider>())).ToArray();
        for (int a = 0; a < targets.Length; a++)
            for (int b = a + 1; b < targets.Length; b++)
                if (targets[a] != null && targets[b] != null && targets[a].bounds.Intersects(targets[b].bounds))
                    errors.Add("Overlapping computer/cashier click targets: " + targets[a].name + " / " + targets[b].name);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
    }

    [MenuItem("Dine In/Fine Dining/Validate Lobby3 Navigation")]
    public static void ValidateNavigation()
    {
        var scene = Scene();
        var surface = Surface(scene);
        if (surface.navMeshData == null)
            throw new InvalidOperationException("Lobby3 navigation is not baked. Use Dine In > Fine Dining > Bake Lobby3 Navigation.");
        var roots = scene.GetRootGameObjects();
        string[] required = { "CustomerSpawnPoint", "CustomerWaitingSpot", "CustomerExitPoint",
            "HostHomePoint", "WaiterHomePoint", "CashierHomePoint", "BusserHomePoint" };
        foreach (string name in required)
        {
            var points = roots.SelectMany(root => root.GetComponentsInChildren<Transform>())
                .Where(point => point.name == name).ToArray();
            if (points.Length != 1 || !NavMesh.SamplePosition(points[0].position, out _, 1.5f, NavMesh.AllAreas))
                throw new InvalidOperationException(name + " is missing or outside the baked Lobby3 NavMesh.");
        }
        foreach (var booth in roots.SelectMany(root => root.GetComponentsInChildren<Booth>()))
            if (booth.approachPoint == null || !NavMesh.SamplePosition(booth.approachPoint.position, out _, 1.5f, NavMesh.AllAreas))
                throw new InvalidOperationException(booth.name + " approach point is outside the baked Lobby3 NavMesh.");
        var binding = roots.SelectMany(r => r.GetComponentsInChildren<FineDiningSceneBindings>()).Single();
        var spawn = roots.SelectMany(r => r.GetComponentsInChildren<Transform>()).Single(t => t.name == "CustomerSpawnPoint");
        if (!NavMesh.SamplePosition(spawn.position, out var origin, .75f, NavMesh.AllAreas))
            throw new InvalidOperationException("Customer spawn is not on the baked navigation.");
        var errors = new List<string>();
        foreach (var target in binding.navigationApproaches)
        {
            if (target == null) { errors.Add("Missing navigation approach."); continue; }
            if (!target.gameObject.activeInHierarchy) { errors.Add(target.name + ": approach is under an inactive object."); continue; }
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(target.position, out var hit, .75f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(origin.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                errors.Add(target.name + ": no complete route from the customer entrance.");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("[FineDining] All authored approaches have complete routes from the entrance. Runtime crowding and interactions still need manual checks.", surface);
    }
}
#endif