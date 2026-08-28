#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the shared editable navigation profile once. Runtime character scripts
/// use this asset, so balancing does not require code changes.
/// </summary>
[InitializeOnLoad]
public static class CrowdNavigationAuthoring
{
    private const string SessionKey = "DineIn.CrowdNavigationAuthoring.Done.v1";
    private const string AssetPath =
        "Assets/_Project/Resources/Settings/CrowdNavigationProfile.asset";

    static CrowdNavigationAuthoring()
    {
        EditorApplication.delayCall += TryCreateProfile;
    }

    [MenuItem("Tools/Dine In/Navigation/Create or Select Crowd Profile")]
    public static void CreateOrSelectProfile()
    {
        CrowdNavigationProfile profile = EnsureProfile();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    private static void TryCreateProfile()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EnsureProfile();
    }

    private static CrowdNavigationProfile EnsureProfile()
    {
        CrowdNavigationProfile existing =
            AssetDatabase.LoadAssetAtPath<CrowdNavigationProfile>(AssetPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/_Project/Resources", "Settings");
        CrowdNavigationProfile profile =
            ScriptableObject.CreateInstance<CrowdNavigationProfile>();
        AssetDatabase.CreateAsset(profile, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrowdNavigation] Created editable profile at {AssetPath}.");
        return profile;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
