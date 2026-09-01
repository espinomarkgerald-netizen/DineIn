using System;
using UnityEngine;

/// <summary>
/// Small persistent settings surface for Level 1 UI. Existing settings toggles
/// can call these public methods without depending on the newspaper or restock
/// implementations. Defaults preserve the authored presentation.
/// </summary>
[DefaultExecutionOrder(-460)]
public sealed class LevelOneUIAccessibility : MonoBehaviour
{
    // Matches the existing iris-transition safeguard. Loading/layout spikes on
    // slower devices must not consume an entire short UI animation in one frame.
    public const float MaximumAnimationFrameDelta = 0.05f;

    private const string ReducedMotionKey = "DineIn.ReducedMotion";
    private const string LargeTextKey = "DineIn.LargeText";
    private const string HighContrastKey = "DineIn.HighContrast";
    private static LevelOneUIAccessibility instance;

    public static bool ReducedMotion => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
    public static bool LargeText => PlayerPrefs.GetInt(LargeTextKey, 0) != 0;
    public static bool HighContrast => PlayerPrefs.GetInt(HighContrastKey, 0) != 0;
    public static event Action SettingsChanged;

    public static float UnscaledAnimationDeltaTime =>
        Mathf.Min(Mathf.Max(0f, Time.unscaledDeltaTime), MaximumAnimationFrameDelta);

    public static float ScaledAnimationDeltaTime =>
        Mathf.Min(Mathf.Max(0f, Time.deltaTime), MaximumAnimationFrameDelta);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        SettingsChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;
        GameObject root = new GameObject("Level 1 UI Accessibility");
        instance = root.AddComponent<LevelOneUIAccessibility>();
        DontDestroyOnLoad(root);
    }

    public void SetReducedMotion(bool enabled) =>
        SetSetting(ReducedMotionKey, enabled);

    public void SetLargeText(bool enabled) =>
        SetSetting(LargeTextKey, enabled);

    public void SetHighContrast(bool enabled) =>
        SetSetting(HighContrastKey, enabled);

    public static void SetReducedMotionEnabled(bool enabled) =>
        SetSetting(ReducedMotionKey, enabled);

    public static void SetLargeTextEnabled(bool enabled) =>
        SetSetting(LargeTextKey, enabled);

    public static void SetHighContrastEnabled(bool enabled) =>
        SetSetting(HighContrastKey, enabled);

    private static void SetSetting(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
        SettingsChanged?.Invoke();
    }
}
