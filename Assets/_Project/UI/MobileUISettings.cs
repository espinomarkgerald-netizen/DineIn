using UnityEngine;

[CreateAssetMenu(menuName = "Dine In/UI/Mobile UI Settings")]
public sealed class MobileUISettings : ScriptableObject
{
    [Header("Physical pixel minimums (test on your smallest supported device)")]
    [Min(44)] public float touchTargetPixels = 72;
    [Min(44)] public float persistentHudPixels = 70;
    [Min(44)] public float workspaceControlPixels = 58;
    [Tooltip("Runs the same mobile accessibility pass in Editor Play Mode. Does not change saved layouts.")]
    public bool previewMobileInEditor;

    private static MobileUISettings cached;
    public static MobileUISettings Active => cached != null ? cached : cached = Resources.Load<MobileUISettings>("UI/MobileUISettings");
    public static bool UseMobileLayout
    {
        get
        {
#if UNITY_EDITOR
            return Application.isMobilePlatform || (Active != null && Active.previewMobileInEditor);
#else
            return Application.isMobilePlatform;
#endif
        }
    }
}
