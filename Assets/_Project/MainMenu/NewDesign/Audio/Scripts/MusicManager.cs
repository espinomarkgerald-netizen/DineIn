using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays background music and keeps the Music mixer group's volume in sync
/// with SettingsManager.Current.musicVolume - including whenever settings
/// are (re)loaded locally or from the cloud, not just at startup.
///
/// This is the piece SettingsManager's comments pointed to: it doesn't know
/// about AudioMixers, this class is where that wiring actually happens.
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string musicVolumeParam = "MusicVolume"; // must match the exposed parameter name

    [Header("Playback")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip defaultTrack;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;

    private void Awake()
    {
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = loop;
        musicSource.playOnAwake = false;
    }

    private void Start()
    {
        // Apply whatever volume is already loaded (SettingsManager.Start()
        // runs its own LoadLocal() before this, as long as script execution
        // order doesn't need to be forced - if it does, put SettingsManager
        // earlier in Project Settings > Script Execution Order).
        if (DineIn.NewMenu.SettingsManager.Instance != null)
            ApplyVolume(DineIn.NewMenu.SettingsManager.Instance.Current.musicVolume);

        if (playOnStart && defaultTrack != null)
            Play(defaultTrack);

        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.OnSettingsLoaded += HandleSettingsLoaded;
    }

    private void OnDestroy()
    {
        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.OnSettingsLoaded -= HandleSettingsLoaded;
    }

    private void HandleSettingsLoaded(DineIn.NewMenu.UserSettings settings)
    {
        ApplyVolume(settings.musicVolume);
    }

    public void Play(AudioClip clip)
    {
        if (clip == null) return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void Stop() => musicSource.Stop();

    // Converts a 0-1 linear slider value to the decibel scale AudioMixer
    // expects. -80dB is effectively silent; 0dB is unity gain (unchanged).
    private void ApplyVolume(float linearVolume)
    {
        if (mixer == null) return;

        float dB = linearVolume > 0.0001f
            ? Mathf.Log10(linearVolume) * 20f
            : -80f;

        mixer.SetFloat(musicVolumeParam, dB);
    }
}
