#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path))
                continue;

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
}
#endif
