// Run with Unity CLI eval_file after import_models.cs. Uses existing customer
// behavior, controller, collider, navigation and tray anchors without changing them.
var names = new[] { "OldAlien", "OrangeAlien", "PurpleAlien", "YellowAlien" };
var assetRoot = "Assets/_Project/Art/Models/Customer/AdditionalAliens";
var referencePath = "Assets/_Project/Art/Models/Customer/Old Alien Models/GreenCustomer.prefab";
System.Func<GameObject, Bounds> bindBounds = obj =>
{
    var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
    var vertices = renderer.sharedMesh.vertices;
    var bounds = new Bounds(obj.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[0])), Vector3.zero);
    foreach (var vertex in vertices)
        bounds.Encapsulate(obj.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
    return bounds;
};
var report = new System.Text.StringBuilder();
foreach (var name in names)
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(referencePath);
    try
    {
        var referenceAnimator = root.GetComponentInChildren<Animator>();
        var referenceMaterial = root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
        var targetBounds = bindBounds(root);
        var modelPath = assetRoot + "/" + name + "/" + name + ".fbx";
        var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var visual = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model, root.scene);
        visual.transform.SetParent(root.transform, false);
        var animator = visual.GetComponent<Animator>();
        var avatar = animator.avatar;
        UnityEditor.EditorUtility.CopySerialized(referenceAnimator, animator);
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        var sourceBounds = bindBounds(visual);
        float scale = targetBounds.size.y / sourceBounds.size.y;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(0, targetBounds.min.y - sourceBounds.min.y * scale, 0);
        var oldVisuals = root.GetComponentsInChildren<Animator>(true)
            .Where(a => a != animator).Select(a => a.gameObject).ToArray();
        foreach (var old in oldVisuals) UnityEngine.Object.DestroyImmediate(old);
        var materialPath = assetRoot + "/" + name + "/" + name + ".mat";
        var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(referenceMaterial) { name = name + "Material" };
            UnityEditor.AssetDatabase.CreateAsset(material, materialPath);
        }
        var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetRoot + "/" + name + "/" + name + "Albedo.png");
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        UnityEditor.EditorUtility.SetDirty(material);
        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.sharedMaterial = material;
            renderer.quality = SkinQuality.Bone4;
        }
        foreach (var child in visual.GetComponentsInChildren<Transform>()) child.gameObject.layer = root.layer;
        var customer = new UnityEditor.SerializedObject(root.GetComponent<CustomerAgent>());
        customer.FindProperty("animator").objectReferenceValue = animator;
        customer.ApplyModifiedPropertiesWithoutUndo();
        root.name = name + "Customer";
        var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, assetRoot + "/" + name + "/" + name + "Customer.prefab");
        if (saved == null) throw new System.InvalidOperationException("Could not save " + name);
        report.AppendLine(name + ": height=" + targetBounds.size.y.ToString("F2") + ", scale=" + scale.ToString("F3") + ", existing controller and customer components retained");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
UnityEditor.AssetDatabase.SaveAssets();
return report.ToString();
