using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro

public class SettingsController : MonoBehaviour
{
    [Header("Audio")]
    public Slider audioSlider;

    [Header("UI References")]
    public TMP_Text qualityDisplayText; // Drag your TMP object here

    private const string PREF_VOLUME = "Settings_Volume";
    private const string PREF_QUALITY = "Settings_Quality";

    // Set to 1 ONLY when the player picks a quality level through this
    // script's own SetGraphicsQuality(). GameOptimizer reads this (not
    // PREF_QUALITY's mere existence) to decide whether to apply its own
    // platform default - see GameOptimizer.PREF_QUALITY_USER_SET.
    private const string PREF_QUALITY_USER_SET = "Settings_QualityUserSet";

    void Start()
    {
        // Load saved values BEFORE hooking up listeners, so setting the
        // slider value doesn't immediately re-trigger a save with defaults
        float savedVolume = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        int savedQuality = ClampQualityIndex(PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel()));

        AudioListener.volume = savedVolume;

        // Only force-apply the saved quality if the player actually chose
        // it before. Otherwise leave whatever GameOptimizer already set in
        // Awake() alone - this is display-only until the player picks one.
        if (PlayerPrefs.GetInt(PREF_QUALITY_USER_SET, 0) == 1)
        {
            ApplyQuality(savedQuality);
        }

        if (audioSlider != null)
        {
            audioSlider.SetValueWithoutNotify(savedVolume); // sync slider position without firing the event
            audioSlider.onValueChanged.AddListener(SetVolume);
        }

        UpdateGraphicsText();
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(PREF_VOLUME, volume);
        PlayerPrefs.Save();
        Debug.Log("Volume: " + volume);
    }

    public void SetGraphicsQuality(int qualityIndex)
    {
        int resolvedQuality = ClampQualityIndex(qualityIndex);
        ApplyQuality(resolvedQuality);

        PlayerPrefs.SetInt(PREF_QUALITY, resolvedQuality);
        PlayerPrefs.SetInt(PREF_QUALITY_USER_SET, 1); // this is the one true "player chose this" signal
        PlayerPrefs.Save();

        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.SetGraphicsQuality(resolvedQuality);

        UpdateGraphicsText();
        Debug.Log("Graphics quality set to: " + QualitySettings.names[resolvedQuality]);
    }

    void UpdateGraphicsText()
    {
        if (qualityDisplayText != null)
        {
            int quality = ClampQualityIndex(QualitySettings.GetQualityLevel());
            string[] names = QualitySettings.names;

            qualityDisplayText.text = "Current Quality: " + names[quality];
        }
    }

    public void CurrentGraphics()
    {
        int quality = ClampQualityIndex(QualitySettings.GetQualityLevel());
        string[] names = QualitySettings.names;

        Debug.Log("Current quality: " + names[quality]);
    }

    private static void ApplyQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex, true);
    }

    private static int ClampQualityIndex(int qualityIndex)
    {
        int qualityCount = QualitySettings.names.Length;
        return qualityCount == 0 ? 0 : Mathf.Clamp(qualityIndex, 0, qualityCount - 1);
    }
}
