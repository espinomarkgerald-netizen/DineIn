#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit authoring operation; never runs on import or in Play mode.
public static class FastFoodBackStoolAuthoring
{
    [MenuItem("Dine In/Fast Food/Enable Back Stool Tables")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var restaurant = UnityEngine.Object.FindFirstObjectByType<FastFoodRestaurant>();
        if (restaurant == null || restaurant.gameObject.scene.name != "Lobby2") throw new InvalidOperationException("Open Lobby2.");
        var sources = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.name == "Long Stool Table" && r.GetComponentInParent<Booth>() == null)
            .OrderBy(r => r.bounds.center.z).ToArray();
        if (sources.Length == 0) return; // Already authored.
        if (sources.Length != 2) throw new InvalidOperationException("Expected the two back-wall stool tables.");
        var stools = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.name == "Stool" && r.GetComponentInParent<Booth>() == null).ToArray();
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Long Table.prefab");
        if (template == null) throw new InvalidOperationException("Missing authored table template.");
        foreach (var source in sources)
        {
            var bounds = source.bounds;
            var seats = stools.Where(s => s.bounds.center.z > bounds.min.z && s.bounds.center.z < bounds.max.z)
                .OrderBy(s => s.bounds.center.z).ToArray();
            if (seats.Length != 4) throw new InvalidOperationException("Each back table must have four stools.");
            if (source.GetComponent<MeshFilter>()?.sharedMesh.isReadable != true) throw new InvalidOperationException("Table mesh must be readable.");
        }
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Enable back-wall stool dining");
        int index = 0;
        foreach (var source in sources)
        {
            var bounds = source.bounds;
            var stoolRenderers = stools.Where(s => s.bounds.center.z > bounds.min.z && s.bounds.center.z < bounds.max.z)
                .OrderBy(s => s.bounds.center.z).ToArray();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(template, restaurant.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Create stool dining table");
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "Dining Back Stool Table " + (++index);
            root.transform.position = new Vector3(bounds.center.x, -8.81f, bounds.center.z);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var table = root.GetComponent<FastFoodTable>();
            var booth = root.GetComponent<Booth>();
            var oldFurniture = table.Furniture;
            var visual = CloneVisual(source, root.transform);
            visual.name = "Furniture";
            Undo.DestroyObjectImmediate(oldFurniture.gameObject);
            var data = new SerializedObject(table);
            data.FindProperty("furniture").objectReferenceValue = visual;
            data.ApplyModifiedProperties();
            foreach (var seat in booth.seats) Undo.DestroyObjectImmediate(seat.gameObject);
            booth.seats.Clear();
            booth.useAuthoredSeatRotation = true;
            foreach (var stool in stoolRenderers)
            {
                CloneVisual(stool, root.transform);
                var seat = new GameObject("SeatPoint_0" + (booth.seats.Count + 1)).transform;
                Undo.RegisterCreatedObjectUndo(seat.gameObject, "Author stool seat");
                seat.SetParent(root.transform, false);
                seat.position = new Vector3(stool.bounds.center.x, stool.bounds.max.y + .1f, stool.bounds.center.z);
                seat.rotation = Quaternion.LookRotation(Vector3.right);
                booth.seats.Add(seat);
                Undo.RecordObject(stool.gameObject, "Replace decorative stool");
                stool.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(stool.gameObject);
            }
            booth.approachPoint.position = new Vector3(43.9f, -8.81f, bounds.center.z);
            var customerApproach = new GameObject("CustomerApproachPoint").transform;
            Undo.RegisterCreatedObjectUndo(customerApproach.gameObject, "Author customer access");
            customerApproach.SetParent(root.transform, false);
            customerApproach.position = new Vector3(43.9f, -8.81f, bounds.center.z + (bounds.center.z > 10f ? -6f : 6f));
            booth.customerApproachPoint = customerApproach;
            booth.tableLookTarget.position = new Vector3(bounds.center.x, -6.3f, bounds.center.z);
            booth.NetworkTrayPoint.position = new Vector3(bounds.center.x, bounds.max.y + .03f, bounds.center.z);
            booth.tableNumberAnchor.position = booth.NetworkTrayPoint.position + Vector3.up * 1.3f;
            if (booth.menuSpawnPoint != null) booth.menuSpawnPoint.position = booth.NetworkTrayPoint.position;
            var collider = root.GetComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            var obstacle = root.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null) { obstacle.center = collider.center; obstacle.size = collider.size; }
            BakeOutline(root.GetComponent<Outline>());
            Undo.RecordObject(source.gameObject, "Replace decorative table");
            source.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source.gameObject);
            if (!table.ValidateAuthoring(out var problem)) throw new InvalidOperationException(problem);
            EditorUtility.SetDirty(booth);
        }
        var settings = new SerializedObject(restaurant);
        var registered = settings.FindProperty("diningTables");
        var tables = UnityEngine.Object.FindObjectsByType<FastFoodTable>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == restaurant.gameObject.scene).Select(t => t.GetComponent<Booth>()).ToArray();
        registered.arraySize = tables.Length;
        for (int i = 0; i < tables.Length; i++) registered.GetArrayElementAtIndex(i).objectReferenceValue = tables[i];
        settings.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(restaurant.gameObject.scene);
        Debug.Log("[FastFood] Registered both back stool tables, four seats each. Validate navigation and save Lobby2.");
    }

    static MeshRenderer CloneVisual(MeshRenderer source, Transform parent)
    {
        var go = new GameObject(source.name);
        Undo.RegisterCreatedObjectUndo(go, "Copy dining furniture");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        EditorUtility.CopySerialized(source, renderer);
        return renderer;
    }

    static void BakeOutline(Outline outline)
    {
        var meshes = outline.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Distinct().ToArray();
        var data = new SerializedObject(outline);
        data.FindProperty("precomputeOutline").boolValue = true;
        var keys = data.FindProperty("bakeKeys"); var values = data.FindProperty("bakeValues");
        keys.arraySize = values.arraySize = meshes.Length;
        for (int i = 0; i < meshes.Length; i++)
        {
            var mesh = meshes[i]; var normals = mesh.normals;
            foreach (var group in mesh.vertices.Select((p, n) => new { p, n }).GroupBy(v => v.p).Where(g => g.Count() > 1))
            { var normal = group.Aggregate(Vector3.zero, (sum, v) => sum + normals[v.n]).normalized; foreach (var v in group) normals[v.n] = normal; }
            keys.GetArrayElementAtIndex(i).objectReferenceValue = mesh;
            var vectors = values.GetArrayElementAtIndex(i).FindPropertyRelative("data"); vectors.arraySize = normals.Length;
            for (int j = 0; j < normals.Length; j++) vectors.GetArrayElementAtIndex(j).vector3Value = normals[j];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
