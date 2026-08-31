using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates the complete flattened Lobby HUD once.  It deliberately does not
/// auto-rebuild an existing combined prefab, so direct designer edits and icon
/// changes remain authoritative.
/// </summary>
[InitializeOnLoad]
internal static class LobbyHUDRootAuthoring
{
    private const string CombinedPrefabPath =
        "Assets/_Project/Resources/UI/LobbyHUD.prefab";
    private const string ProgressPrefabPath =
        "Assets/_Project/Resources/UI/CasualDiningProgressHUD.prefab";
    private const string ControlsPrefabPath =
        "Assets/_Project/Resources/UI/LobbyHUDRedesign.prefab";
    private const string TaskPrefabPath =
        "Assets/_Project/Resources/UI/PlayerTaskHUD.prefab";
    private const string PausePrefabPath =
        "Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab";

    static LobbyHUDRootAuthoring()
    {
        EditorApplication.delayCall += EnsureCombinedPrefabExists;
    }

    private static void EnsureCombinedPrefabExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
        if (existing != null)
        {
            if (!HasCompleteEditableHierarchy(existing))
            {
                RepairDesignerBindings();
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
            }

            if (HasCompleteEditableHierarchy(existing))
                return;

            Debug.LogError(
                "[LobbyHUD] The combined LobbyHUD prefab exists but is incomplete. " +
                "Use Tools/Dine In/UI/Rebuild Complete Lobby HUD only if you intend to replace it.",
                existing);
            return;
        }

        CreateCombinedPrefab();
    }

    [MenuItem("Tools/Dine In/UI/Open Complete Lobby HUD Prefab")]
    private static void OpenCombinedPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
        if (prefab == null)
        {
            CreateCombinedPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
        }

        if (prefab != null)
            AssetDatabase.OpenAsset(prefab);
    }

    [MenuItem("Tools/Dine In/UI/Rebuild Complete Lobby HUD")]
    private static void RebuildCombinedPrefab()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Rebuild Complete Lobby HUD?",
            "This replaces the combined LobbyHUD prefab with flattened copies of the four " +
            "source HUD prefabs. Direct edits made only in LobbyHUD will be lost.",
            "Rebuild",
            "Cancel");
        if (!confirmed)
            return;

        CreateCombinedPrefab();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
    }

    private static void CreateCombinedPrefab()
    {
        GameObject progress = AssetDatabase.LoadAssetAtPath<GameObject>(ProgressPrefabPath);
        GameObject controls = AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefabPath);
        GameObject task = AssetDatabase.LoadAssetAtPath<GameObject>(TaskPrefabPath);
        GameObject pause = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        if (progress == null || controls == null || task == null || pause == null)
        {
            Debug.LogError("[LobbyHUD] One or more source HUD prefabs are missing; combined prefab was not changed.");
            return;
        }

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject("LobbyHUD");
            SceneManager.MoveGameObjectToScene(root, previewScene);
            LobbyHUDRoot owner = root.AddComponent<LobbyHUDRoot>();

            CloneFlattened(progress, root.transform, "Progress Day Time HUD", previewScene);
            CloneFlattened(controls, root.transform, "Lobby Controls HUD", previewScene);
            CloneFlattened(task, root.transform, "Task HUD", previewScene);
            GameObject pauseBranch = CloneFlattened(
                pause, root.transform, "Pause And Settings HUD", previewScene);

            LobbyPauseMenuView pauseView = pauseBranch.GetComponent<LobbyPauseMenuView>();
            owner.ConfigureForEditor(pauseView);

            PrefabUtility.SaveAsPrefabAsset(root, CombinedPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[LobbyHUD] Saved one complete, flattened, editable Lobby HUD prefab at " +
                CombinedPrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static GameObject CloneFlattened(
        GameObject source,
        Transform parent,
        string branchName,
        Scene previewScene)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
        instance.name = branchName;
        instance.transform.SetParent(parent, false);
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
        return instance;
    }

    private static bool HasCompleteEditableHierarchy(GameObject root)
    {
        LobbyHUDRedesign controls = root != null
            ? root.GetComponentInChildren<LobbyHUDRedesign>(true)
            : null;
        PlayerTaskHUD task = root != null
            ? root.GetComponentInChildren<PlayerTaskHUD>(true)
            : null;
        return root != null &&
               root.GetComponent<LobbyHUDRoot>() != null &&
               root.GetComponentInChildren<CasualDiningProgressHUD>(true) != null &&
               controls != null && controls.PreservesAuthoredSafeArea &&
               task != null && task.transform == controls.transform &&
               task.UsesCombinedAuthoredPresentation &&
               controls.GetComponent<PlayerTaskRestockSource>() != null &&
               root.GetComponentInChildren<LobbyPauseMenuView>(true) != null;
    }

    private static void RepairDesignerBindings()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(CombinedPrefabPath);
        try
        {
            LobbyHUDRedesign controls =
                contents.GetComponentInChildren<LobbyHUDRedesign>(true);
            if (controls == null)
                return;

            Transform safe = controls.transform.Find("SafeArea");
            Transform taskButtonTransform = safe != null ? safe.Find("TaskButton") : null;
            Transform panelTransform = safe != null ? safe.Find("TaskMessage") : null;
            if (taskButtonTransform == null || panelTransform == null)
                return;

            controls.ConfigureCombinedPrefabForEditor();

            PlayerTaskHUD task = controls.GetComponent<PlayerTaskHUD>();
            if (task == null)
                task = controls.gameObject.AddComponent<PlayerTaskHUD>();
            if (controls.GetComponent<PlayerTaskRestockSource>() == null)
                controls.gameObject.AddComponent<PlayerTaskRestockSource>();

            PlayerTaskHUD[] obsoleteTaskControllers =
                contents.GetComponentsInChildren<PlayerTaskHUD>(true);
            foreach (PlayerTaskHUD obsolete in obsoleteTaskControllers)
            {
                if (obsolete != null && obsolete != task)
                    Object.DestroyImmediate(obsolete.gameObject);
            }

            Transform iconTransform = taskButtonTransform.Find("TaskIcon");
            Transform badgeTransform = taskButtonTransform.Find("ReminderBadge");
            TMP_Text badgeText = badgeTransform != null
                ? badgeTransform.GetComponentInChildren<TMP_Text>(true)
                : null;
            TMP_Text actionText = panelTransform.Find("Action")?.GetComponent<TMP_Text>();
            TMP_Text detailText = panelTransform.Find("Detail")?.GetComponent<TMP_Text>();

            task.ConfigureCombinedPrefabForEditor(
                controls.GetComponent<Canvas>(),
                controls.GetComponent<CanvasGroup>(),
                taskButtonTransform.GetComponent<Button>(),
                taskButtonTransform as RectTransform,
                taskButtonTransform.GetComponent<Image>(),
                iconTransform != null ? iconTransform.GetComponent<Image>() : null,
                badgeTransform != null ? badgeTransform.gameObject : null,
                badgeTransform as RectTransform,
                badgeText,
                panelTransform.gameObject,
                panelTransform as RectTransform,
                panelTransform.GetComponent<CanvasGroup>(),
                panelTransform.GetComponent<Image>(),
                actionText,
                detailText);

            PrefabUtility.SaveAsPrefabAsset(contents, CombinedPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[LobbyHUD] Bound the designer-authored Task button/message and preserved all authored HUD transforms and colors.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
