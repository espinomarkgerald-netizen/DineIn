using UnityEngine;

namespace DineIn.NewMenu
{
    public partial class UserSettings
    {
        public float masterVolume = 1f, panSpeed = 1f, zoomSpeed = 1f, uiScale = 1f, dialogueSpeed = 1f;
        public bool muteAll, invertZoom, edgePan;
        public int vSync, fpsLimit = 60, displayWidth, displayHeight;
        public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;
    }

    public partial class SettingsManager
    {
        public static float PanMultiplier => Instance != null ? Instance.Current.panSpeed : 1f;
        public static float ZoomMultiplier => Instance != null ? Instance.Current.zoomSpeed * (Instance.Current.invertZoom ? -1 : 1) : 1f;
        public static float DialogueMultiplier => Instance != null ? Instance.Current.dialogueSpeed : 1f;
        public static float UIScaleMultiplier => Instance != null ? Instance.Current.uiScale : 1f;
        public static bool EdgePanEnabled => SupportsDesktopDisplay && Instance != null && Instance.Current.edgePan;
        public static bool SupportsDesktopDisplay => !Application.isMobilePlatform;

        // The same serialized mixer references are used when a restaurant is opened directly.
        public static SettingsManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<SettingsManager>("UI/PlayerSettings");
            if (prefab != null) Instantiate(prefab);
            return Instance;
        }

        public void SetMasterVolume(float value) { Current.masterVolume = Mathf.Clamp01(value); Changed(true); }
        public void SetMuteAll(bool value) { Current.muteAll = value; Changed(true); }
        public void SetControls(float pan, float zoom, bool invert)
        {
            Current.panSpeed = Mathf.Clamp(pan, .25f, 2f);
            Current.zoomSpeed = Mathf.Clamp(zoom, .25f, 2f);
            Current.invertZoom = invert;
            Changed();
        }
        public void SetEdgePan(bool value) { Current.edgePan = value; Changed(); }
        public void ResetControls() { Current.edgePan = false; SetControls(1, 1, false); }
        public void SetPresentation(float scale, float dialogue)
        {
            Current.uiScale = Mathf.Clamp(scale, .85f, 1.15f);
            Current.dialogueSpeed = Mathf.Clamp(dialogue, .5f, 2f);
            Changed();
        }
        public void SetFrameTiming(int vsync, int fps)
        {
            Current.vSync = Mathf.Clamp(vsync, 0, 1);
            Current.fpsLimit = fps == 30 || fps == 60 || fps == 120 ? fps : -1;
            ApplyFrameTiming(); Changed();
        }
        private void ApplyFrameTiming()
        {
            // Android uses targetFrameRate. Desktop VSync takes precedence over the cap.
            QualitySettings.vSyncCount = Application.isMobilePlatform ? 0 : Current.vSync;
            Application.targetFrameRate = Current.fpsLimit;
        }
        public void CommitDisplay(int width, int height, FullScreenMode mode)
        {
            Current.displayWidth = Mathf.Max(640, width); Current.displayHeight = Mathf.Max(360, height);
            Current.windowMode = mode; Changed();
        }
        private void ApplySavedDisplay()
        {
            if (SupportsDesktopDisplay && Current.displayWidth > 0 && Current.displayHeight > 0)
                Screen.SetResolution(Current.displayWidth, Current.displayHeight, Current.windowMode);
        }
        private void Changed(bool audio = false)
        {
            ApplyGlobalUIScale();
            if (audio) ApplyAudio();
            SaveLocal(); OnSettingsLoaded?.Invoke(Current);
        }
        private void SaveAdditional()
        {
            PlayerPrefs.SetFloat("Settings_MasterVolume", Current.masterVolume);
            PlayerPrefs.SetInt("Settings_MuteAll", Current.muteAll ? 1 : 0);
            PlayerPrefs.SetFloat("Settings_PanSpeed", Current.panSpeed);
            PlayerPrefs.SetFloat("Settings_ZoomSpeed", Current.zoomSpeed);
            PlayerPrefs.SetInt("Settings_InvertZoom", Current.invertZoom ? 1 : 0);
            PlayerPrefs.SetInt("Settings_EdgePan", Current.edgePan ? 1 : 0);
            PlayerPrefs.SetFloat("Settings_UIScale", Current.uiScale);
            PlayerPrefs.SetFloat("Settings_DialogueSpeed", Current.dialogueSpeed);
            PlayerPrefs.SetInt("Settings_VSync", Current.vSync);
            PlayerPrefs.SetInt("Settings_FPSLimit", Current.fpsLimit);
            PlayerPrefs.SetInt("Settings_DisplayWidth", Current.displayWidth);
            PlayerPrefs.SetInt("Settings_DisplayHeight", Current.displayHeight);
            PlayerPrefs.SetInt("Settings_WindowMode", (int)Current.windowMode);
        }
        private void LoadAdditional()
        {
            Current.masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_MasterVolume", 1));
            Current.muteAll = PlayerPrefs.GetInt("Settings_MuteAll", 0) != 0;
            Current.panSpeed = Mathf.Clamp(PlayerPrefs.GetFloat("Settings_PanSpeed", 1), .25f, 2);
            Current.zoomSpeed = Mathf.Clamp(PlayerPrefs.GetFloat("Settings_ZoomSpeed", 1), .25f, 2);
            Current.invertZoom = PlayerPrefs.GetInt("Settings_InvertZoom", 0) != 0;
            Current.edgePan = PlayerPrefs.GetInt("Settings_EdgePan", 0) != 0;
            Current.uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("Settings_UIScale", 1), .85f, 1.15f);
            Current.dialogueSpeed = Mathf.Clamp(PlayerPrefs.GetFloat("Settings_DialogueSpeed", 1), .5f, 2);
            Current.vSync = Mathf.Clamp(PlayerPrefs.GetInt("Settings_VSync", QualitySettings.vSyncCount), 0, 1);
            int fps = PlayerPrefs.GetInt("Settings_FPSLimit", 60);
            Current.fpsLimit = fps == 30 || fps == 60 || fps == 120 ? fps : -1;
            Current.displayWidth = Mathf.Clamp(PlayerPrefs.GetInt("Settings_DisplayWidth", 0), 0, 16384);
            Current.displayHeight = Mathf.Clamp(PlayerPrefs.GetInt("Settings_DisplayHeight", 0), 0, 16384);
            int mode = PlayerPrefs.GetInt("Settings_WindowMode", (int)Screen.fullScreenMode);
            Current.windowMode = mode == (int)FullScreenMode.Windowed ? FullScreenMode.Windowed :
                mode == (int)FullScreenMode.ExclusiveFullScreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
        }
    }
}
