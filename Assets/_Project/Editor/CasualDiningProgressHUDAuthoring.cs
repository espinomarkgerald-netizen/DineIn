#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-time migration from the old runtime-generated HUD to real, serialized
/// prefab children. Once the hierarchy exists this utility never rebuilds it
/// automatically, so designer edits remain authoritative.
/// </summary>
[InitializeOnLoad]
internal static class CasualDiningProgressHUDAuthoring
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/CasualDiningProgressHUD.prefab";
    private const string DayTimeFontPath =
        "Assets/_Project/UI/Assets/Legacy/Fonts/Fredoka,Lilita_One/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset";
    private const string MoneyIconPath =
        "Assets/_Project/Art/Icons/GameIcons/HUD/MoneyIcon.png";

    static CasualDiningProgressHUDAuthoring()
    {
        EditorApplication.delayCall += InitializeAuthoring;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += InitializeAuthoring;
    }

    private static void InitializeAuthoring()
    {
        EnsureSavedHierarchyExists();
        RemoveMalformedLobbySceneInstance();
        RemoveLegacyLobbyDayTime();
    }

    private static void EnsureSavedHierarchyExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null)
            return;

        try
        {
            CasualDiningProgressHUD hud = contents.GetComponent<CasualDiningProgressHUD>();
            if (hud == null)
                return;

            TMP_FontAsset dayTimeFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DayTimeFontPath);
            Sprite moneyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(MoneyIconPath);
            if (hud.EnsureLobbyHudRedesignForEditor(dayTimeFont, moneyIcon))
            {
                EditorUtility.SetDirty(hud);
                EditorUtility.SetDirty(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CasualDiningProgressHUD] Applied the mobile-first Lobby HUD arrangement.");
                return;
            }

            Transform objectives = contents.transform.Find("SafeAreaContent/ObjectivesResponsiveRoot/ObjectivesPanel") ??
                                   contents.transform.Find("SafeAreaContent/ObjectivesPanel") ??
                                   contents.transform.Find("ObjectivesPanel");
            bool changed;
            if (objectives == null)
            {
                hud.RebuildAuthoredVisualTreeForEditor();
                changed = true;
            }
            else
            {
                changed = hud.EnsureAuthoredResponsiveHierarchyForEditor(dayTimeFont);
            }

            if (!changed)
                return;

            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CasualDiningProgressHUD] Saved the editable safe-area and Day/Time prefab hierarchy.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void RemoveLegacyLobbyDayTime()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.transform.Find("SafeAreaContent/DayTimeRoot") == null)
            return;

        GameDayManager[] managers = Object.FindObjectsByType<GameDayManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (GameDayManager manager in managers)
        {
            if (manager == null || !manager.gameObject.scene.IsValid() ||
                manager.gameObject.scene.name != "Lobby1")
                continue;

            TMP_Text legacyDay = manager.DayHudText;
            TMP_Text legacyTime = manager.TimeHudText;
            if (legacyDay == null && legacyTime == null)
                continue;

            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty dayProperty = serializedManager.FindProperty("dayText");
            SerializedProperty timeProperty = serializedManager.FindProperty("timerText");
            if (dayProperty != null) dayProperty.objectReferenceValue = null;
            if (timeProperty != null) timeProperty.objectReferenceValue = null;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            if (legacyDay != null)
                Undo.DestroyObjectImmediate(legacyDay.gameObject);
            if (legacyTime != null && legacyTime.gameObject != null)
                Undo.DestroyObjectImmediate(legacyTime.gameObject);

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            Debug.Log("[CasualDiningProgressHUD] Removed Lobby1's duplicate Day and Timer scene objects.", manager);
        }
    }

    [MenuItem("Tools/Dine In/UI/Open Casual Dining Progress HUD Prefab")]
    private static void OpenHudPrefab()
    {
        Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
            AssetDatabase.OpenAsset(prefab);
    }

    private static void RemoveMalformedLobbySceneInstance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        CasualDiningProgressHUD[] huds = Object.FindObjectsByType<CasualDiningProgressHUD>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (CasualDiningProgressHUD hud in huds)
        {
            if (hud == null || !hud.gameObject.scene.IsValid() || !hud.gameObject.scene.isLoaded ||
                hud.gameObject.scene.name != "Lobby1" || !PrefabUtility.IsPartOfPrefabInstance(hud.gameObject))
                continue;

            UnityEngine.SceneManagement.Scene scene = hud.gameObject.scene;
            Undo.DestroyObjectImmediate(hud.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CasualDiningProgressHUD] Removed the malformed Lobby preview instance; the saved prefab remains the editable source and is loaded at runtime.");
        }
    }

    [MenuItem("Tools/Dine In/UI/Rebuild Casual Dining Progress HUD Prefab")]
    private static void RebuildHudPrefab()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Rebuild Progress HUD?",
            "This replaces the saved ObjectivesPanel children with values from the root HUD component. " +
            "Only use this if you intentionally want to discard direct child edits.",
            "Rebuild",
            "Cancel");
        if (!confirmed)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null)
            return;

        try
        {
            CasualDiningProgressHUD hud = contents.GetComponent<CasualDiningProgressHUD>();
            if (hud == null)
                return;
            hud.RebuildAuthoredVisualTreeForEditor();
            EditorUtility.SetDirty(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
#endif
