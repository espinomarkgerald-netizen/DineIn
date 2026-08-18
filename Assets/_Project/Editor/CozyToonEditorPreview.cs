using DineIn.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CozyToonEditorPreview
{
    private const string SettingsPath = "Assets/_Project/Resources/CozyToonGlobalSettings.asset";

    private static GameObject previewHost;
    private static CozyToonRuntime previewRuntime;
    private static bool suspended;
    private static bool queued;

    static CozyToonEditorPreview()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.quitting += StopPreview;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        QueuePreviewRefresh();
    }

    private static void OnEditorUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || suspended)
            return;

        CozyToonGlobalSettings settings = LoadSettings();
        if (settings == null || !settings.previewInEditMode)
        {
            StopPreview();
            return;
        }

        if (previewRuntime == null)
            StartPreview();
    }

    private static void StartPreview()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || suspended)
            return;

        CozyToonGlobalSettings settings = LoadSettings();
        if (settings == null || !settings.previewInEditMode || previewRuntime != null)
            return;

        previewHost = EditorUtility.CreateGameObjectWithHideFlags(
            "[Cozy Toon Edit Preview]",
            HideFlags.HideAndDontSave,
            typeof(CozyToonRuntime));
        previewRuntime = previewHost.GetComponent<CozyToonRuntime>();
        previewRuntime.RefreshNow(true);
        SceneView.RepaintAll();
    }

    private static void StopPreview()
    {
        queued = false;
        if (previewHost != null)
            Object.DestroyImmediate(previewHost);

        previewHost = null;
        previewRuntime = null;
        SceneView.RepaintAll();
    }

    private static void QueuePreviewRefresh()
    {
        if (queued)
            return;

        queued = true;
        EditorApplication.delayCall += StartPreview;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                suspended = true;
                StopPreview();
                break;
            case PlayModeStateChange.EnteredEditMode:
                suspended = false;
                QueuePreviewRefresh();
                break;
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        suspended = true;
        StopPreview();
    }

    private static void OnSceneSaved(Scene scene)
    {
        suspended = false;
        QueuePreviewRefresh();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (previewRuntime != null)
            previewRuntime.RefreshNow(true);
        else
            QueuePreviewRefresh();
    }

    private static CozyToonGlobalSettings LoadSettings()
    {
        return AssetDatabase.LoadAssetAtPath<CozyToonGlobalSettings>(SettingsPath);
    }
}
