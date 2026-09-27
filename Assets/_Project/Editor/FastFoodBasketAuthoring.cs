using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FastFoodBasketAuthoring
{
    const string Folder = "Assets/_Project/Restaurant/CookingAssets/Batch/";
    [MenuItem("Dine In/Fast Food/Apply Basket Collection Polish")]
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring baskets.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Lobby2") throw new InvalidOperationException("Open Lobby2 first.");
        var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FastFoodCookingController>(true)).Single();
        var fry = controller.GetComponentsInChildren<FastFoodCookingStation>(true).Single(s => s.mode == FastFoodStationMode.Fry);
        Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Fryer basket collection");
        if (fry.baskets.Length != 4 || fry.baskets.Any(b => b == null))
        {
            var baskets = new List<FastFoodFryerBasket>();
            foreach (string applianceName in new[] { "Fryer.001", "Fryer.003" })
            {
                var appliance = GameObject.Find("Restaurant/Fast Food Revamp/" + applianceName);
                if (appliance == null) throw new InvalidOperationException("Missing fryer " + applianceName);
                Undo.RegisterFullObjectHierarchyUndo(appliance, "Separate liftable fryer baskets");
                var filter = appliance.GetComponent<MeshFilter>();
                var renderer = appliance.GetComponent<MeshRenderer>();
                var source = filter.sharedMesh;
                var materials = renderer.sharedMaterials.Take(6).ToArray();
                if (!source.isReadable || source.subMeshCount < 6 || materials.Length < 6)
                    throw new InvalidOperationException("Fryer mesh must have its original six readable material sections.");
                for (int side = 0; side < 2; side++)
                {
                    var mesh = Extract(source, new[] { 2 + side * 2, 3 + side * 2 });
                    Vector3 pivot = mesh.bounds.center;
                    mesh.vertices = mesh.vertices.Select(v => v - pivot).ToArray(); mesh.RecalculateBounds();
                    SaveMesh(mesh, applianceName + " Basket " + (side + 1));
                    var root = new GameObject("Lift Basket " + (baskets.Count + 1), typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
                    Undo.RegisterCreatedObjectUndo(root, "Liftable basket");
                    root.layer = fry.preparation.gameObject.layer;
                    root.transform.SetParent(appliance.transform, false); root.transform.localPosition = pivot;
                    root.GetComponent<MeshFilter>().sharedMesh = mesh;
                    root.GetComponent<MeshRenderer>().sharedMaterials = new[] { materials[2 + side * 2], materials[3 + side * 2] };
                    var collider = root.GetComponent<BoxCollider>(); collider.center = mesh.bounds.center;
                    var scale = root.transform.lossyScale;
                    collider.size = mesh.bounds.size + new Vector3(.05f / Mathf.Max(.0001f, Mathf.Abs(scale.x)), .1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), .05f / Mathf.Max(.0001f, Mathf.Abs(scale.z))); collider.isTrigger = true;
                    var basket = root.AddComponent<FastFoodFryerBasket>();
                    basket.firstSlot = baskets.Count * 2; basket.movingBasket = root.transform;
                    basket.loweredLocalPosition = root.transform.localPosition;
                    basket.interactionOutline = root.AddComponent<Outline>();
                    basket.interactionOutline.OutlineMode = Outline.Mode.OutlineVisible;
                    basket.interactionOutline.OutlineColor = Color.white; basket.interactionOutline.OutlineWidth = 2;
                    basket.interactionOutline.enabled = false;
                    foreach (var slot in fry.Slots.Where(s => s.slotIndex >= basket.firstSlot && s.slotIndex < basket.firstSlot + 2))
                        Undo.SetTransformParent(slot.foodAnchor, root.transform, "Food travels with basket");
                    baskets.Add(basket);
                }
                var body = Extract(source, new[] { 0, 1 }); SaveMesh(body, applianceName + " Body");
                filter.sharedMesh = body; renderer.sharedMaterials = materials.Take(2).ToArray();
                var outline = appliance.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            }
            fry.baskets = baskets.ToArray();
        }
        AuthorRack(fry);
        var grill = controller.GetComponentsInChildren<FastFoodCookingStation>(true).Single(s => s.mode == FastFoodStationMode.Grill);
        var surfaces = new List<FastFoodCookingDropTarget>();
        for (int i = 0; i < 2; i++)
        {
            var name = "Tap Grill Surface " + (i + 1);
            var child = grill.transform.Find(name);
            if (child == null)
            {
                var root = new GameObject(name, typeof(BoxCollider), typeof(FastFoodCookingDropTarget));
                Undo.RegisterCreatedObjectUndo(root, "Grill tap area"); child = root.transform; child.SetParent(grill.transform, false);
            }
            var points = grill.Slots.Where(s => s.slotIndex / 4 == i).Select(s => s.foodAnchor.position).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            child.position = bounds.center - Vector3.up * .10f; child.rotation = Quaternion.identity;
            child.gameObject.layer = grill.preparation.gameObject.layer;
            var box = child.GetComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(bounds.size.x + 1.1f, .04f, bounds.size.z + 1.1f);
            var target = child.GetComponent<FastFoodCookingDropTarget>(); target.kind = 0; target.slotIndex = i * 4; target.collectionSurface = true;
            surfaces.Add(target); EditorUtility.SetDirty(target); EditorUtility.SetDirty(box);
        }
        grill.collectionSurfaces = surfaces.ToArray(); EditorUtility.SetDirty(grill);
        var view = controller.GetComponent<FastFoodCookingView>();
        var data = new SerializedObject(view);
        data.FindProperty("equipmentOutlines").arraySize = 0;
        data.FindProperty("collectStepText").stringValue = "Tap the ready food to collect";
        data.ApplyModifiedProperties();
        var controllerData = new SerializedObject(controller);
        controllerData.FindProperty("collectGuidance").FindPropertyRelative("detail").stringValue = "Tap a raised fryer basket, or tap each ready grill patty.";
        controllerData.ApplyModifiedProperties();
        foreach (var outline in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Outline>(true)))
            if (outline.OutlineColor.maxColorComponent < .15f) { outline.enabled = false; EditorUtility.SetDirty(outline); }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var outline in root.GetComponentsInChildren<Outline>(true))
                    if (outline.OutlineColor.maxColorComponent < .15f && outline.enabled) { outline.enabled = false; changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        int renderers = AuthorWorldOutlines();
        EditorUtility.SetDirty(fry); EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Lobby2.");
        return "Saved 4 lifting baskets, 8 paired food positions, shared loose-food drying rack, and world outline passes in " + renderers + " renderers.";
    }
    static Mesh Extract(Mesh source, int[] sections)
    {
        var input = source.vertices; var normals = source.normals; var uv = source.uv;
        var remap = new Dictionary<int, int>(); var vertices = new List<Vector3>();
        var outputNormals = new List<Vector3>(); var outputUv = new List<Vector2>();
        var triangles = new List<int[]>();
        foreach (int section in sections)
        {
            var indices = source.GetTriangles(section);
            for (int i = 0; i < indices.Length; i++)
            {
                int old = indices[i];
                if (!remap.TryGetValue(old, out int next))
                {
                    next = vertices.Count; remap.Add(old, next); vertices.Add(input[old]);
                    outputNormals.Add(normals.Length == input.Length ? normals[old] : Vector3.up);
                    outputUv.Add(uv.Length == input.Length ? uv[old] : Vector2.zero);
                }
                indices[i] = next;
            }
            triangles.Add(indices);
        }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(outputNormals); mesh.SetUVs(0, outputUv);
        mesh.subMeshCount = sections.Length;
        for (int i = 0; i < sections.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateBounds(); return mesh;
    }
    static void SaveMesh(Mesh mesh, string name)
    {
        mesh.name = name; string path = Folder + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) throw new InvalidOperationException("Mesh already exists without basket wiring: " + path);
        AssetDatabase.CreateAsset(mesh, path);
    }
    static void AuthorRack(FastFoodCookingStation fry)
    {
        var tray = fry.preparation.transform.Find("Drying Rack/Tray visual") ?? fry.preparation.transform.Find("Tray visual");
        if (tray == null) throw new InvalidOperationException("Fryer prep tray is missing.");
        var renderer = tray.GetComponentInChildren<MeshRenderer>();
        Vector3 center = fry.completedFoodAnchors.Aggregate(Vector3.zero, (v, a) => v + a.position) / fry.completedFoodAnchors.Length;
        var rack = fry.preparation.transform.Find("Drying Rack");
        if (rack == null)
        {
            var root = new GameObject("Drying Rack"); Undo.RegisterCreatedObjectUndo(root, "Shared drying rack");
            rack = root.transform; rack.SetParent(fry.preparation.transform, false); rack.rotation = Quaternion.identity;
            Undo.SetTransformParent(tray, rack, "Tray inside drying rack");
        }
        var bounds = renderer.bounds;
        rack.localScale = Vector3.Scale(rack.localScale, new Vector3(3.65f / bounds.size.x, .07f / bounds.size.y, 1.9f / bounds.size.z));
        bounds = renderer.bounds;
        rack.position += new Vector3(center.x, fry.preparation.transform.position.y + .03f, center.z) - bounds.center;
        // Anchor all eight loose servings inside the shared tray, on top of its base.
        bounds = renderer.bounds;
        for (int i = 0; i < fry.completedFoodAnchors.Length; i++)
        {
            var anchor = fry.completedFoodAnchors[i];
            anchor.position = new Vector3(center.x + (i % 4 - 1.5f) * .82f, bounds.min.y + .022f, center.z + (i / 4 - .5f) * .78f);
            EditorUtility.SetDirty(anchor);
        }
        EditorUtility.SetDirty(tray); EditorUtility.SetDirty(rack);
    }
    static int AuthorWorldOutlines()
    {
        var shader = Shader.Find("Dine In/World Outline");
        if (shader == null) throw new InvalidOperationException("World outline shader has not imported.");
        string path = Folder + "World Outline.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.SetFloat("_Width", .9f); material.SetColor("_Color", Color.black); EditorUtility.SetDirty(material);
        var pipelines = new List<RenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline };
        for (int i = 0; i < QualitySettings.names.Length; i++) pipelines.Add(QualitySettings.GetRenderPipelineAssetAt(i));
        var rendererAssets = new HashSet<ScriptableRendererData>();
        foreach (var pipeline in pipelines.Where(p => p != null).Distinct())
        {
            var serialized = new SerializedObject(pipeline); var list = serialized.FindProperty("m_RendererDataList");
            if (list == null) continue;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData renderer) rendererAssets.Add(renderer);
        }
        foreach (var renderer in rendererAssets)
        {
            var old = renderer.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == "World Outline Opaque");
            if (old != null) { renderer.rendererFeatures.Remove(old); UnityEngine.Object.DestroyImmediate(old, true); }
            var edges = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "World Outline Edges");
            if (edges == null)
            {
                edges = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); edges.name = "World Outline Edges";
                AssetDatabase.AddObjectToAsset(edges, renderer); renderer.rendererFeatures.Insert(0, edges);
            }
            edges.passMaterial = material; edges.passIndex = 3; edges.fetchColorBuffer = false;
            edges.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            edges.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            edges.SetActive(true); edges.Create(); EditorUtility.SetDirty(edges);
            AddPass(renderer, material, "World Outline Glass Mask", RenderQueueType.Transparent, 1, RenderPassEvent.BeforeRenderingTransparents);
            AddPass(renderer, material, "World Outline Glass Edge", RenderQueueType.Transparent, 2, RenderPassEvent.BeforeRenderingTransparents);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
        return rendererAssets.Count;
    }
    static void AddPass(ScriptableRendererData renderer, Material material, string name, RenderQueueType queue, int pass, RenderPassEvent timing)
    {
        var feature = renderer.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == name);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<RenderObjects>(); feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
        }
        feature.settings.passTag = name; feature.settings.Event = timing;
        feature.settings.filterSettings.RenderQueueType = queue;
        feature.settings.filterSettings.LayerMask = ~((1 << 5) | (1 << 2));
        feature.settings.overrideMaterial = material; feature.settings.overrideMaterialPassIndex = pass;
        feature.SetActive(true); feature.Create(); EditorUtility.SetDirty(feature);
    }
}
