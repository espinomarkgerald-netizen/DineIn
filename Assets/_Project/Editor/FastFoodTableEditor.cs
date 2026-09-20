#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FastFoodTable))]
public sealed class FastFoodTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var table = (FastFoodTable)target;
        var booth = table.GetComponent<Booth>();
        EditorGUILayout.HelpBox("Edit Furniture and service anchors in Prefab Mode. The Booth Seats list is the seating capacity. " +
            "Scene instances may override placement; keep each anchor inside its own table. Rebake/validate navigation after moving furniture.", MessageType.Info);
        if (!table.ValidateAuthoring(out var problem)) EditorGUILayout.HelpBox(problem, MessageType.Error);
        if (!Application.isPlaying && !FastFoodSeatingOutlineTools.ValidateBake(table, out var outlineProblem))
            EditorGUILayout.HelpBox(outlineProblem + " Use Dine In > Fast Food > Bake Seating Outlines after editing furniture.", MessageType.Warning);
        if (booth == null) return;
        EditorGUILayout.LabelField("Seat capacity", (booth.seats?.Count ?? 0).ToString());
        SelectButton("Select approach point", booth.approachPoint);
        SelectButton("Select facing point", booth.tableLookTarget);
        SelectButton("Select tray point", booth.NetworkTrayPoint);
        SelectButton("Select number anchor", booth.tableNumberAnchor);
    }

    private static void SelectButton(string label, Transform point)
    {
        using (new EditorGUI.DisabledScope(point == null))
            if (GUILayout.Button(label)) { Selection.activeTransform = point; SceneView.lastActiveSceneView?.FrameSelected(); }
    }
}
// Explicit prefab authoring only: no scene opening, import hooks or Play mode bootstrap.
public static class FastFoodSeatingOutlineTools
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Booth.prefab",
        "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Long Table.prefab",
        "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Round Table.prefab"
    };

    [MenuItem("Dine In/Fast Food/Bake Seating Outlines")]
    public static void BakeAll()
    {
        RequireEditMode();
        // Check all sources before saving any prefab. Model import settings are edited
        // explicitly in the Inspector; this tool never changes unrelated model assets.
        foreach (string path in PrefabPaths) ValidateSource(path);
        foreach (string path in PrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var table = root.GetComponent<FastFoodTable>();
                var outline = root.GetComponent<Outline>();
                var meshes = Meshes(outline);
                var data = new SerializedObject(outline);
                data.FindProperty("precomputeOutline").boolValue = true;
                var keys = data.FindProperty("bakeKeys");
                var values = data.FindProperty("bakeValues");
                keys.arraySize = values.arraySize = meshes.Length;
                for (int i = 0; i < meshes.Length; i++)
                {
                    Mesh mesh = meshes[i];
                    keys.GetArrayElementAtIndex(i).objectReferenceValue = mesh;
                    // Match QuickOutline's bake without modifying the imported mesh,
                    // appending submeshes, or enabling the outline in the editor.
                    var normals = mesh.normals;
                    foreach (var group in mesh.vertices.Select((vertex, index) => new { vertex, index })
                        .GroupBy(entry => entry.vertex).Where(group => group.Count() > 1))
                    {
                        Vector3 normal = Vector3.zero;
                        foreach (var entry in group) normal += normals[entry.index];
                        normal.Normalize();
                        foreach (var entry in group) normals[entry.index] = normal;
                    }
                    var vectors = values.GetArrayElementAtIndex(i).FindPropertyRelative("data");
                    vectors.arraySize = normals.Length;
                    for (int vertex = 0; vertex < normals.Length; vertex++)
                        vectors.GetArrayElementAtIndex(vertex).vector3Value = normals[vertex];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!ValidateBake(table, out string problem)) throw new InvalidOperationException(path + ": " + problem);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save outline bake: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ValidateAll();
        Debug.Log("[FastFood] Baked all three seating outlines. Check full furniture coverage in an external playtest.");
    }

    [MenuItem("Dine In/Fast Food/Validate Seating Outlines")]
    public static void ValidateAll()
    {
        RequireEditMode();
        foreach (string path in PrefabPaths)
        {
            var table = ValidateSource(path);
            if (!ValidateBake(table, out string problem)) throw new InvalidOperationException(path + ": " + problem);
        }
        Debug.Log("[FastFood] Seating outline sources and per-vertex bake coverage passed.");
    }

    public static bool ValidateBake(FastFoodTable table, out string problem)
    {
        problem = null;
        var outline = table.GetComponent<Outline>();
        if (outline == null) { problem = "Missing root Outline."; return false; }
        var meshes = Meshes(outline);
        var data = new SerializedObject(outline);
        var keys = data.FindProperty("bakeKeys");
        var values = data.FindProperty("bakeValues");
        if (!data.FindProperty("precomputeOutline").boolValue || meshes.Length == 0 ||
            keys.arraySize != meshes.Length || values.arraySize != meshes.Length)
        { problem = "Seating outline bake is missing or does not cover the furniture meshes."; return false; }
        foreach (Mesh mesh in meshes)
        {
            int index = -1;
            for (int i = 0; i < keys.arraySize; i++)
                if (keys.GetArrayElementAtIndex(i).objectReferenceValue == mesh) { index = i; break; }
            if (!mesh.isReadable || index < 0 ||
                values.GetArrayElementAtIndex(index).FindPropertyRelative("data").arraySize != mesh.vertexCount)
            { problem = "Outline bake is unreadable or stale for " + mesh.name + "."; return false; }
        }
        return true;
    }

    private static FastFoodTable ValidateSource(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var table = root != null ? root.GetComponent<FastFoodTable>() : null;
        if (table == null) throw new InvalidOperationException("Missing seating prefab: " + path);
        if (!table.ValidateAuthoring(out string problem)) throw new InvalidOperationException(path + ": " + problem);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<Renderer>();
            if (mesh == null || !mesh.isReadable || renderer == null || mesh.normals.Length != mesh.vertexCount ||
                mesh.subMeshCount > renderer.sharedMaterials.Length)
                throw new InvalidOperationException(path + ": " + filter.name + " needs readable mesh data, normals and a material for each submesh.");
            // Editor mesh access can be available even when Read/Write is off for builds.
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) is ModelImporter importer && !importer.isReadable)
                throw new InvalidOperationException(path + ": Enable Read/Write on " + importer.assetPath);
        }
        return table;
    }

    private static Mesh[] Meshes(Outline outline) => outline.GetComponentsInChildren<MeshFilter>()
        .Select(filter => filter.sharedMesh).Where(mesh => mesh != null).Distinct().ToArray();

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use seating outline authoring outside Play mode.");
    }
}
#endif
