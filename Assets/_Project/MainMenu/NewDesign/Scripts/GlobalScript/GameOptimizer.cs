using UnityEngine;

public class GameOptimizer : MonoBehaviour
{
    [Header("Frame Rate & VSync")]
    [Tooltip("Target framerate for the game. Set to -1 for uncapped.")]
    [Range(30, 300)]
    public int targetFPS = 60;
    
    [Tooltip("0 = VSync Off, 1 = VSync Every V-Blank, 2 = Every Second V-Blank")]
    [Range(0, 2)]
    public int vSyncCount = 0;

    [Header("Quality & Performance Tweaks")]
    [Tooltip("Automatically adjust target frame rate based on platform")]
    public bool autoConfigureSettings = true;

    [Tooltip("Target frame rate if the game detects it's running on a mobile device")]
    public int mobileTargetFPS = 60;

    [Header("Graphics Quality Default")]
    [Tooltip("Quality Settings tier name to apply on ANY platform, but ONLY on a fresh " +
             "install before the player has ever picked one in Settings.")]
    public string defaultQualityName = "Low";

    // Must match SettingsController.PREF_QUALITY_USER_SET exactly - that's
    // how the two scripts agree on "has the player deliberately chosen a
    // quality level via the Settings UI?" without referencing each other
    // directly. NOTE: this is intentionally NOT the same as checking
    // whether "Settings_Quality" exists in PlayerPrefs - that key can get
    // written by Unity's own per-platform project default or by earlier
    // test builds, without the player ever touching a Settings screen.
    private const string PREF_QUALITY_USER_SET = "Settings_QualityUserSet";

    void Awake()
    {
        ApplyOptimizations();
    }

    void ApplyOptimizations()
    {
        // The existing settings owner applies persisted choices and platform defaults.
        DineIn.NewMenu.SettingsManager.EnsureInstance();
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        DontDestroyOnLoad(gameObject);
    }
    /// <summary>
    /// Call this method during heavy loading screens to free up unused RAM.
    /// </summary>
    public static void CleanMemory()
    {
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }
}
