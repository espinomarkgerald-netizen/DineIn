using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DineIn.NewMenu
{
    [Serializable]
    public class UserSettings
    {
        public int graphicsQualityIndex;
        public float musicVolume = 1f;
        public float sfxVolume = 0.5f;
        public bool showFps;
    }

    /// <summary>
    /// Owns local menu settings. Values are stored only in PlayerPrefs and
    /// never sent to PlayFab or another remote service.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [SerializeField] private AudioMixer menuMusicMixer;
        [SerializeField] private AudioMixer menuSfxMixer;
        [SerializeField] private AudioMixer gameplayMixer;
        [SerializeField] private AudioMixerGroup defaultSfxGroup;
        public static SettingsManager Instance { get; private set; }

        private const string PrefGraphicsQuality = "Settings_Quality";
        private const string PrefQualityUserSet = "Settings_QualityUserSet";
        private const string PrefMusicVolume = "Settings_MusicVolume";
        private const string PrefSfxVolume = "Settings_SfxVolume";
        private const float DefaultSfxVolume = 0.5f;
        public const string PrefShowFps = "Settings_ShowFPS";

        public UserSettings Current { get; private set; } = new UserSettings();
        public event Action<UserSettings> OnSettingsLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            LoadLocal();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAudio();

        private void LateUpdate()
        {
            // Legacy effect sources have no mixer assignment, including dynamically
            // spawned UI/interaction one-shots. Preserve explicitly routed music/SFX.
            if (defaultSfxGroup == null) return;
            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = defaultSfxGroup;
        }

        private void ApplyAudio()
        {
            SetMixer(menuMusicMixer, "MusicVolume", Current.musicVolume);
            SetMixer(menuSfxMixer, "SFXVolume", Current.sfxVolume);
            SetMixer(gameplayMixer, "MusicVol", Current.musicVolume);
            SetMixer(gameplayMixer, "SFXVol", Current.sfxVolume);
        }

        private static void SetMixer(AudioMixer mixer, string parameter, float volume)
        {
            if (mixer != null) mixer.SetFloat(parameter, volume > .0001f ? Mathf.Log10(volume) * 20f : -80f);
        }

        public void SetShowFps(bool visible)
        {
            Current.showFps = visible;
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SetGraphicsQuality(int qualityIndex)
        {
            Current.graphicsQualityIndex = ClampQualityIndex(qualityIndex);
            QualitySettings.SetQualityLevel(Current.graphicsQualityIndex, true);
            PlayerPrefs.SetInt(PrefQualityUserSet, 1);
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SetMusicVolume(float volume)
        {
            Current.musicVolume = Mathf.Clamp01(volume);
            ApplyAudio();
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SetSfxVolume(float volume)
        {
            Current.sfxVolume = Mathf.Clamp01(volume);
            ApplyAudio();
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SaveLocal()
        {
            PlayerPrefs.SetInt(PrefGraphicsQuality, Current.graphicsQualityIndex);
            PlayerPrefs.SetFloat(PrefMusicVolume, Current.musicVolume);
            PlayerPrefs.SetFloat(PrefSfxVolume, Current.sfxVolume);
            PlayerPrefs.SetInt(PrefShowFps, Current.showFps ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void LoadLocal()
        {
            int fallbackQuality = QualitySettings.GetQualityLevel();
            Current.graphicsQualityIndex = ClampQualityIndex(PlayerPrefs.GetInt(PrefGraphicsQuality, fallbackQuality));
            Current.musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusicVolume, PlayerPrefs.GetFloat("Settings_Volume", 1f)));
            Current.sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, PlayerPrefs.GetFloat("Settings_Volume", DefaultSfxVolume)));
            Current.showFps = PlayerPrefs.GetInt(PrefShowFps, 0) == 1;
            AudioListener.volume = 1f; // Legacy master slider migrated to the two channels.
            ApplyAudio();

            if (PlayerPrefs.GetInt(PrefQualityUserSet, 0) == 1)
                QualitySettings.SetQualityLevel(Current.graphicsQualityIndex, true);
            else
                Current.graphicsQualityIndex = ClampQualityIndex(QualitySettings.GetQualityLevel());

            OnSettingsLoaded?.Invoke(Current);
        }

        private static int ClampQualityIndex(int qualityIndex)
        {
            int qualityCount = QualitySettings.names.Length;
            return qualityCount == 0 ? 0 : Mathf.Clamp(qualityIndex, 0, qualityCount - 1);
        }
    }
}
