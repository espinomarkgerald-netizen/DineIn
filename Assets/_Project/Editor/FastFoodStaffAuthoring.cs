using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit, repeatable upgrade of Lobby2's existing staff. Never modifies saves.</summary>
public static class FastFoodStaffAuthoring
{
    [MenuItem("Dine In/Fast Food/Configure Station Staff and Catalog")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/RoleBased/Lobby2.unity");
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || scene.isDirty)
            throw new InvalidOperationException("Open and save Lobby2 outside Play mode first.");
        var roots = scene.GetRootGameObjects();
        var workers = roots.SelectMany(r => r.GetComponentsInChildren<KitchenWorkerBot>(true)).ToArray();
        var grill = workers.Single(w => w.EmployeeRole == EmployeeRole.Chef || w.EmployeeRole == EmployeeRole.GrillStation);
        var assembler = workers.Single(w => w.EmployeeRole == EmployeeRole.Barista || w.EmployeeRole == EmployeeRole.FastFoodAssembler);
        var machines = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        // Validate both destinations before touching the scene. Use the already baked kitchen mesh.
        var grillPoint = Station(machines, "Grill", grill.transform.position);
        var fryPoint = Station(machines, "Fryer", grill.transform.position);
        var fry = workers.SingleOrDefault(w => w.EmployeeRole == EmployeeRole.FryStation);
        if (fry == null)
        {
            var clone = UnityEngine.Object.Instantiate(grill.gameObject, grill.transform.parent);
            Undo.RegisterCreatedObjectUndo(clone, "Add Fry Station worker");
            clone.name = "Fry Station";
            var view = clone.GetComponent<Photon.Pun.PhotonView>();
            if (view != null) view.ViewID = 0;
            fry = clone.GetComponent<KitchenWorkerBot>();
        }
        Configure(grill, EmployeeRole.GrillStation, Anchor(grillPoint, "GrillWorkPoint"),
            new[] { ItemTypeKitchen.Burger, ItemTypeKitchen.ChickenSandwich, ItemTypeKitchen.FishFilletSandwich }, 60);
        Configure(fry, EmployeeRole.FryStation, Anchor(fryPoint, "FryWorkPoint"),
            new[] { ItemTypeKitchen.Chicken, ItemTypeKitchen.Fries, ItemTypeKitchen.ChickenNuggets }, 65);
        var assemblyData = new SerializedObject(assembler);
        assemblyData.FindProperty("employeeRole").intValue = (int)EmployeeRole.FastFoodAssembler;
        assemblyData.ApplyModifiedProperties();
        Label(assembler.gameObject, "Assembler");
        PrefabUtility.RecordPrefabInstancePropertyModifications(assembler);

        var lobbyWorkers = roots.SelectMany(r => r.GetComponentsInChildren<BusserHands>(true))
            .Where(w => w.GetComponent<ManagerPlayer>() == null).OrderBy(w => w.name).ToArray();
        for (int i = 0; i < lobbyWorkers.Length; i++) Label(lobbyWorkers[i].gameObject, "Lobby #" + (i + 1));

        foreach (var table in roots.SelectMany(r => r.GetComponentsInChildren<FastFoodTable>(true)))
        {
            var item = new SerializedObject(table).FindProperty("seatingUpgrade").objectReferenceValue as Equipment;
            if (item == null) continue;
            item.catalogSortOrder = table.LayoutPriority;
            EditorUtility.SetDirty(item);
        }
        var config = AssetDatabase.LoadAssetAtPath<FastFoodProgressionSettings>("Assets/_Project/Resources/FastFood/Progression.asset");
        foreach (var item in config.equipment.Where(e => e != null))
        {
            if (item.displayName == "Busser Trolley") { item.displayName = "Lobby Trolley"; EditorUtility.SetDirty(item); }
        }
        config.equipment.Sort(Equipment.CompareProgression);
        EditorUtility.SetDirty(config);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FastFoodStaff] Configured Grill, Fry and Assembler, reachable station points, Lobby labels and ordered catalog.");
    }

    private static (Transform machine, Vector3 position) Station(MeshRenderer[] machines, string prefix, Vector3 from)
    {
        foreach (var machine in machines.Where(r => r.name.StartsWith(prefix, StringComparison.Ordinal) &&
                     Mathf.Abs(r.bounds.center.y - from.y) < 5f).OrderBy(r => (r.bounds.center - from).sqrMagnitude))
        {
            var away = from - machine.bounds.center; away.y = 0;
            var edge = machine.bounds.ClosestPoint(from) + away.normalized * .8f; edge.y = from.y;
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(edge, out var hit, 1f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                return (machine.transform, hit.position);
        }
        throw new InvalidOperationException("No reachable " + prefix + " station found. Check the kitchen NavMesh.");
    }

    private static Transform Anchor((Transform machine, Vector3 position) station, string name)
    {
        var anchor = station.machine.Find(name);
        if (anchor == null) { anchor = new GameObject(name).transform; anchor.SetParent(station.machine); }
        anchor.position = station.position;
        return anchor;
    }

    private static void Configure(KitchenWorkerBot worker, EmployeeRole role, Transform point, ItemTypeKitchen[] products, int priority)
    {
        var data = new SerializedObject(worker);
        data.FindProperty("employeeRole").intValue = (int)role;
        data.FindProperty("homePoint").objectReferenceValue = point;
        var points = data.FindProperty("workPoints"); points.arraySize = 1;
        points.GetArrayElementAtIndex(0).objectReferenceValue = point;
        var items = data.FindProperty("stationProducts"); items.arraySize = products.Length;
        for (int i = 0; i < products.Length; i++) items.GetArrayElementAtIndex(i).intValue = (int)products[i];
        data.FindProperty("avoidancePriority").intValue = priority;
        data.ApplyModifiedProperties();
        worker.transform.position = point.position;
        Label(worker.gameObject, EmployeeRoleCatalog.DisplayName(role));
        PrefabUtility.RecordPrefabInstancePropertyModifications(worker);
        PrefabUtility.RecordPrefabInstancePropertyModifications(worker.transform);
    }

    private static void Label(GameObject worker, string label)
    {
        foreach (var text in worker.GetComponentsInChildren<TMP_Text>(true))
        {
            string previous = text.text.Trim();
            if (previous != "Chef" && previous != "Barista" && previous != "Busser" &&
                previous != "Grill Station" && previous != "Fry Station" && previous != "Assembler" &&
                !previous.StartsWith("Lobby #", StringComparison.Ordinal)) continue;
            text.text = label;
            EditorUtility.SetDirty(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
    }
}
