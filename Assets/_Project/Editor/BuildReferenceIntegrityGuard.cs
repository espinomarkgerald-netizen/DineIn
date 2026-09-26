#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;
using UnityEngine;

/// <summary>
/// Stops builds before Unity's serializer encounters stale project-setting PPtrs.
/// Deleted preloaded/config assets otherwise surface as the opaque
/// "Casting from GameObject to Prefab" build failure.
/// </summary>
internal sealed class BuildReferenceIntegrityGuard : IPreprocessBuildWithReport
{
    private const string PlayerSettingsPath = "ProjectSettings/ProjectSettings.asset";
    private const string EditorBuildSettingsPath = "ProjectSettings/EditorBuildSettings.asset";
    private const string ComplaintSystemPrefabPath =
        "Assets/_Project/Resources/ManagerComplaints/ManagerComplaintSystem.prefab";
    private const string ComplaintMarkerPrefabPath =
        "Assets/_Project/Resources/ManagerComplaints/CustomerComplaintMarker.prefab";
    private static readonly Regex GuidPattern =
        new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
    private static readonly Regex ScriptGuidPattern =
        new Regex(@"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        ValidateOrThrow();
    }

    [MenuItem("Dine In/Validation/Validate Build References")]
    private static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("[BuildReferenceIntegrityGuard] Project build references are valid.");
    }

    internal static void ValidateOrThrow()
    {
        List<string> problems = new List<string>();
        ValidateSection(PlayerSettingsPath, "preloadedAssets:", problems);
        ValidateSection(EditorBuildSettingsPath, "m_configObjects:", problems);
        ValidatePrefabComponent<ManagerComplaintSystem>(ComplaintSystemPrefabPath, problems);
        ValidatePrefabComponent<ManagerComplaintMarker>(ComplaintMarkerPrefabPath, problems);
        ValidateEnabledSceneScripts(problems);
        ValidatePresentation(problems);

        UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
        for (int index = 0; index < preloadedAssets.Length; index++)
        {
            if (preloadedAssets[index] == null)
                problems.Add(PlayerSettingsPath + " contains a missing preloaded asset at index " + index + ".");
        }

        if (problems.Count == 0)
            return;

        throw new BuildFailedException(
            "Invalid project-level asset references were found. Fix these before building:\n- " +
            string.Join("\n- ", problems));
    }

    private static void ValidateSection(string relativePath, string sectionName, List<string> problems)
    {
        string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativePath);
        if (!File.Exists(absolutePath))
        {
            problems.Add(relativePath + " is missing.");
            return;
        }

        string[] lines = File.ReadAllLines(absolutePath);
        bool insideSection = false;
        foreach (string line in lines)
        {
            if (!insideSection)
            {
                insideSection = line.TrimStart().StartsWith(sectionName, StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("  ", StringComparison.Ordinal) &&
                !line.StartsWith("    ", StringComparison.Ordinal) &&
                !line.StartsWith("  -", StringComparison.Ordinal))
            {
                break;
            }

            Match match = GuidPattern.Match(line);
            if (!match.Success)
                continue;

            string guid = match.Groups[1].Value;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
                problems.Add(relativePath + " / " + sectionName + " references missing asset GUID " + guid + ".");
        }
    }

    private static void ValidatePrefabComponent<T>(string assetPath, List<string> problems)
        where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            problems.Add(assetPath + " does not import as a GameObject prefab.");
            return;
        }

        if (prefab.GetComponent<T>() == null)
            problems.Add(assetPath + " is missing required component " + typeof(T).Name + ".");
    }

    private static void ValidateEnabledSceneScripts(List<string> problems)
    {
        HashSet<string> checkedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EditorBuildSettingsScene scene in EffectiveScenes())
        {
            if (!scene.enabled)
                continue;
            if (string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path))
            { problems.Add("Enabled build scene is missing: " + scene.path); continue; }

            string yaml = File.ReadAllText(scene.path);
            foreach (Match match in ScriptGuidPattern.Matches(yaml))
            {
                string guid = match.Groups[1].Value;
                if (!checkedGuids.Add(guid))
                    continue;

                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = string.IsNullOrEmpty(scriptPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null || script.GetClass() == null)
                {
                    problems.Add(scene.path + " references a missing or invalid script GUID " +
                                 guid + " (" + (string.IsNullOrEmpty(scriptPath) ? "missing asset" : scriptPath) + ").");
                }
            }
        }
    }

    internal static EditorBuildSettingsScene[] EffectiveScenes()
    {
        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile == null) return EditorBuildSettings.scenes;
        var serialized = new SerializedObject(profile);
        if (serialized.FindProperty("m_OverrideGlobalSceneList")?.boolValue != true) return EditorBuildSettings.scenes;
        var list = serialized.FindProperty("m_Scenes");
        if (list == null) throw new BuildFailedException("Cannot inspect the active Build Profile scene override.");
        var scenes = new EditorBuildSettingsScene[list.arraySize];
        for (int i=0; i<scenes.Length; i++)
        {
            var item = list.GetArrayElementAtIndex(i);
            scenes[i] = new EditorBuildSettingsScene(item.FindPropertyRelative("m_path").stringValue,
                item.FindPropertyRelative("m_enabled").boolValue);
        }
        return scenes;
    }

    private static void ValidatePresentation(List<string> problems)
    {
        var scenes = EffectiveScenes();
        foreach (string required in new[] { "Bootstrap", "NewMainMenu", "NewGameMenu", "Lobby1", "Lobby1Tutorial", "Lobby1 Multiplayer", "Lobby2", "RestockScene" })
            if (!Array.Exists(scenes, s => s.enabled && Path.GetFileNameWithoutExtension(s.path) == required))
                problems.Add("Active build scene list is missing " + required + ". Campaign/menu routing requires it.");
        foreach (string path in new[] { LobbyPauseMenuPrefabInstaller.PrefabPath, "Assets/_Project/Resources/UI/LobbyHUD.prefab" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var view = root != null ? root.GetComponentInChildren<LobbyPauseMenuView>(true) : null;
            if (view == null || view.ResumeButton == null || view.GameMenuButton == null || view.Overlay == null)
            { problems.Add(path + " has incomplete pause references."); continue; }
            if (view.Overlay.activeSelf) problems.Add(path + " must be saved with the pause overlay closed.");
            if (view.Overlay.transform.Find("PauseWindow/SettingsContent") == null)
                problems.Add(path + " needs saved settings controls: Dine In > Polish > Author Build Parity UI.");
            if (view.GetComponent<Canvas>().sortingOrder <= 32761)
                problems.Add(path + " pause canvas must render above tutorial dialogue and readiness screens.");
            var settings = view.GetComponentInChildren<PauseSettingsPanel>(true);
            if (settings == null || settings.rows == null || settings.rows.Length != 18)
                problems.Add(path + " needs the authored four-tab settings controls.");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/PlayerSettings.prefab")?.GetComponent<DineIn.NewMenu.SettingsManager>() == null)
            problems.Add("PlayerSettings prefab is missing.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/DeveloperSettings.prefab")?.GetComponent<DeveloperSettingsView>() == null)
            problems.Add("DeveloperSettings prefab is missing.");
        var ready = AssetDatabase.LoadAssetAtPath<GameObject>(BuildParityAuthoring.ReadyPath)?.GetComponent<MultiplayerReadyView>();
        if (ready == null || ready.pauseButton == null || ready.readyButton == null || ready.cancelButton == null || ready.roster == null)
            problems.Add("The editable MultiplayerReadyPrompt prefab is missing required controls.");
        if (AssetDatabase.LoadAssetAtPath<MobileUISettings>(BuildParityAuthoring.SettingsPath) == null)
            problems.Add("Mobile UI Settings asset is missing.");
        foreach (string name in new[] { "Dine In/Cozy Toon", "Dine In/World Outline", "Custom/Outline Fill", "Custom/Outline Mask" })
        {
            Shader shader = Shader.Find(name);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) problems.Add("Missing or invalid shader: " + name);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            bool edges = false;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null) { problems.Add(renderer.name + " has a missing renderer feature."); continue; }
                if (feature.name != "World Outline Edges") continue;
                var pass = feature as FullScreenPassRendererFeature;
                edges = pass != null && pass.isActive && pass.passMaterial != null && pass.passIndex == 3 &&
                    (pass.requirements & UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Normal) != 0;
            }
            if (!edges) problems.Add(renderer.name + " is missing its active world-outline material/pass/normal input.");
        }
    }
}

// Runs against the actual scenes passed to BuildPipeline, including future levels
// and programmatic builds that do not use the active profile's scene list.
internal sealed class BuildSceneInputGuard : IProcessSceneWithReport
{
    public int callbackOrder => -9999;
    public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
    {
        if (report == null) return;
        bool hasScreenUI = false;
        int systems = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode != RenderMode.WorldSpace && canvas.GetComponentInChildren<UnityEngine.UI.Selectable>(true) != null)
                    hasScreenUI = true;
            foreach (var events in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
            {
                if (!events.enabled || !events.gameObject.activeInHierarchy) continue;
                systems++;
                bool input = false;
                foreach (var module in events.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
                    input |= module.enabled;
                if (!input) throw new BuildFailedException(scene.path + " has an EventSystem without an enabled input module.");
                var modern = events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (modern != null && modern.enabled && (modern.point == null || modern.leftClick == null))
                    throw new BuildFailedException(scene.path + " is missing pointer/click UI input actions.");
            }
        }
        if (hasScreenUI && systems != 1)
            throw new BuildFailedException(scene.path + " requires exactly one active EventSystem for its UI; found " + systems + ".");
    }
}
#endif
