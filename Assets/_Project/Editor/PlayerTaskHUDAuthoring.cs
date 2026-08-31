#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Saves the Lobby mobile layout into the existing task HUD prefab so every
/// anchor and icon remains visible and editable in the Inspector.
/// </summary>
[InitializeOnLoad]
internal static class PlayerTaskHUDAuthoring
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/PlayerTaskHUD.prefab";
    private const string TaskIconPath = "Assets/_Project/Art/Icons/GameIcons/HUD/TaskUI.png";

    static PlayerTaskHUDAuthoring()
    {
        EditorApplication.delayCall += EnsurePrefabLayout;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += EnsurePrefabLayout;
    }

    private static void EnsurePrefabLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null)
            return;

        try
        {
            PlayerTaskHUD hud = contents.GetComponent<PlayerTaskHUD>();
            Sprite taskIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TaskIconPath);
            if (hud == null || !hud.EnsureLobbyHudLayoutForEditor(taskIcon))
                return;

            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerTaskHUD] Saved the editable Lobby right-side task layout.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
#endif
