using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LobbyPauseMenuPrefabInstaller
{
    public const string PrefabPath =
        "Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab";

    static LobbyPauseMenuPrefabInstaller()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    private static void EnsurePrefabExists()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        LobbyPauseMenuView view = existing != null
            ? existing.GetComponent<LobbyPauseMenuView>()
            : null;
        if (existing == null || view == null || view.PauseButton == null ||
            view.Overlay == null || view.ResumeButton == null || view.GameMenuButton == null)
            CreatePrefab();
    }

    [MenuItem("Tools/Dine In/Rebuild Lobby Pause Menu Prefab")]
    public static void RebuildPrefab()
    {
        CreatePrefab();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void CreatePrefab()
    {
        EnsureFolder("Assets/_Project/Gameplay/UI");
        EnsureFolder("Assets/_Project/Gameplay/UI/Resources");

        GameObject root = LobbyPauseMenu.CreateVisualTree();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[LobbyPauseMenu] Editable prefab ready at " + PrefabPath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string child = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }
}
