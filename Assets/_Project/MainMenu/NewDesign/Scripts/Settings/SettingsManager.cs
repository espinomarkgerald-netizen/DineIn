using System;
using UnityEngine;

namespace DineIn.NewMenu
{
    [Serializable]
    public class UserSettings
    {
        public int graphicsQualityIndex;
        public float musicVolume = 1f;
        public float sfxVolume = 0.5f;
    }

    /// <summary>
    /// Owns local menu settings. Values are stored only in PlayerPrefs and
    /// never sent to PlayFab or another remote service.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string PrefGraphicsQuality = "Settings_Quality";
        private const string PrefQualityUserSet = "Settings_QualityUserSet";
        private const string PrefMusicVolume = "Settings_MusicVolume";
        private const string PrefSfxVolume = "Settings_SfxVolume";
        private const float DefaultSfxVolume = 0.5f;

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
        }

        private void Start()
        {
            LoadLocal();
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
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SetSfxVolume(float volume)
        {
            Current.sfxVolume = Mathf.Clamp01(volume);
            SaveLocal();
            OnSettingsLoaded?.Invoke(Current);
        }

        public void SaveLocal()
        {
            PlayerPrefs.SetInt(PrefGraphicsQuality, Current.graphicsQualityIndex);
            PlayerPrefs.SetFloat(PrefMusicVolume, Current.musicVolume);
            PlayerPrefs.SetFloat(PrefSfxVolume, Current.sfxVolume);
            PlayerPrefs.Save();
        }

        public void LoadLocal()
        {
            int fallbackQuality = QualitySettings.GetQualityLevel();
            Current.graphicsQualityIndex = ClampQualityIndex(PlayerPrefs.GetInt(PrefGraphicsQuality, fallbackQuality));
            Current.musicVolume = PlayerPrefs.GetFloat(PrefMusicVolume, 1f);
            Current.sfxVolume = PlayerPrefs.GetFloat(PrefSfxVolume, DefaultSfxVolume);

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
