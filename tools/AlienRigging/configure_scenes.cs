// Opt in only normal single-player lobbies. Tutorial and multiplayer scenes are untouched.
var paths = new[] { "Assets/_Project/Scenes/RoleBased/Lobby1.unity", "Assets/_Project/Scenes/RoleBased/Lobby2.unity" };
var names = new[] { "OldAlien", "OrangeAlien", "PurpleAlien", "YellowAlien" };
var prefabs = names.Select(name => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/_Project/Art/Models/Customer/AdditionalAliens/" + name + "/" + name + "Customer.prefab").GetComponent<CustomerAgent>()).ToArray();
var report = new System.Text.StringBuilder();
foreach (var path in paths)
{
    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
    bool openedHere = !scene.isLoaded;
    if (!openedHere && scene.isDirty) throw new System.InvalidOperationException("Unsaved scene changes: " + path);
    if (openedHere) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    try
    {
        var spawners = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GroupSpawner>(true)).ToArray();
        if (spawners.Length != 1) throw new System.InvalidOperationException("Expected one customer spawner in " + path);
        var data = new UnityEditor.SerializedObject(spawners[0]);
        var variants = data.FindProperty("regularCustomerVisualVariants");
        if (variants == null) throw new System.InvalidOperationException("GroupSpawner has not recompiled yet.");
        variants.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Save failed: " + path);
        report.AppendLine(path + ": four visual variants assigned");
    }
    finally { if (openedHere) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
}
return report.ToString();
