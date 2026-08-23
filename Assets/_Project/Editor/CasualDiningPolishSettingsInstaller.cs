#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CasualDiningPolishSettingsInstaller
{
    // Keeps the build-facing settings asset present without modifying gameplay scenes.
    private const string FolderPath = "Assets/_Project/Resources/CasualDining";
    private const string AssetPath = FolderPath + "/CasualDiningPolishSettings.asset";

    static CasualDiningPolishSettingsInstaller()
    {
        EditorApplication.delayCall += EnsureSettingsAsset;
    }

    [MenuItem("Tools/Dine In/Create Missing Casual Dining Polish Settings")]
    public static void EnsureSettingsAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<CasualDiningPolishSettings>(AssetPath) != null)
            return;

        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/_Project/Resources", "CasualDining");

        CasualDiningPolishSettings settings =
            ScriptableObject.CreateInstance<CasualDiningPolishSettings>();
        settings.name = "CasualDiningPolishSettings";
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CasualDiningPolish] Created editable settings at " + AssetPath + ".");
    }
}
#endif
