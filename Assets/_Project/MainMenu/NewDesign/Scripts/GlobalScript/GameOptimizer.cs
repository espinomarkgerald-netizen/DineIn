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
        // 1. Graphics quality default, on every platform. Runs in Awake,
        // which always finishes before any Start() (e.g.
        // SettingsController's) in the same frame - so by the time
        // SettingsController reads QualitySettings.GetQualityLevel() as
        // its fallback, this has already set it. Only skip if the player
        // has genuinely picked a quality level in Settings before -
        // NOT just because some quality value happens to be saved.
        if (autoConfigureSettings && PlayerPrefs.GetInt(PREF_QUALITY_USER_SET, 0) == 0)
        {
            ApplyDefaultQuality();
        }

        // 2. Configure VSync and Framerate properly
        QualitySettings.vSyncCount = vSyncCount;

        if (autoConfigureSettings)
        {
            #if UNITY_ANDROID || UNITY_IOS
            Application.targetFrameRate = mobileTargetFPS;
            #else
            Application.targetFrameRate = targetFPS;
            #endif
        }
        else
        {
            Application.targetFrameRate = targetFPS;
        }

        // 3. Prevent screen dimming on mobile devices
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // 4. Optimize garbage collection settings to reduce stutter spikes
        System.GC.Collect();
        
        // Persist this manager across scene loads
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"[GameOptimizer] Initialized. Target FPS: {Application.targetFrameRate} | VSync: {QualitySettings.vSyncCount} | Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }

    // Looks up defaultQualityName by name in Project Settings > Quality
    // and applies it. Falls back to the lowest tier if the name isn't found
    // (e.g. tiers got renamed) instead of throwing.
    void ApplyDefaultQuality()
    {
        string[] names = QualitySettings.names;
        int index = System.Array.FindIndex(names, n => string.Equals(n, defaultQualityName, System.StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            Debug.LogWarning($"[GameOptimizer] Quality tier '{defaultQualityName}' not found. Falling back to lowest tier.");
            index = 0;
        }

        QualitySettings.SetQualityLevel(index, true);
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