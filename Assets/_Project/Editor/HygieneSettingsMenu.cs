#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class HygieneSettingsMenu
{
    [MenuItem("Dine In/Hygiene Settings")]
    private static void SelectSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<HygieneSettings>(
            "Assets/_Project/Resources/Hygiene/HygieneSettings.asset");
        if (settings == null) { Debug.LogError("Hygiene Settings asset could not be found."); return; }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
#endif
