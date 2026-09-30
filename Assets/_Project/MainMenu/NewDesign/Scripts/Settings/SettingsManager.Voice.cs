using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DineIn.NewMenu
{
    public partial class UserSettings
    {
        public bool voiceEnabled = true;
        public float masterVoiceVolume = .8f;
        public bool microphoneMuted;
    }

    public partial class SettingsManager
    {
        private readonly Dictionary<string, float> playerVoiceVolumes = new();
        private readonly Dictionary<string, bool> playerVoiceMutes = new();

        public void SetVoiceEnabled(bool value) { Current.voiceEnabled = value; Changed(); }
        public void SetMasterVoiceVolume(float value) { Current.masterVoiceVolume = Mathf.Clamp01(value); Changed(); }
        public void SetMicrophoneMuted(bool value) { Current.microphoneMuted = value; Changed(); }

        public float GetPlayerVoiceVolume(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return 1f;
            if (!playerVoiceVolumes.TryGetValue(userId, out float value))
                playerVoiceVolumes[userId] = value = Mathf.Clamp01(PlayerPrefs.GetFloat(VoicePlayerKey(userId) + "Volume", 1f));
            return value;
        }

        public bool GetPlayerVoiceMuted(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            if (!playerVoiceMutes.TryGetValue(userId, out bool value))
                playerVoiceMutes[userId] = value = PlayerPrefs.GetInt(VoicePlayerKey(userId) + "Muted", 0) != 0;
            return value;
        }

        public void SetPlayerVoiceVolume(string userId, float value)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            playerVoiceVolumes[userId] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VoicePlayerKey(userId) + "Volume", playerVoiceVolumes[userId]);
            Changed();
        }

        public void SetPlayerVoiceMuted(string userId, bool value)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            playerVoiceMutes[userId] = value;
            PlayerPrefs.SetInt(VoicePlayerKey(userId) + "Muted", value ? 1 : 0);
            Changed();
        }

        // A stable account key, never a display name or a Photon actor number.
        private static string VoicePlayerKey(string userId)
        {
            using (var hash = SHA256.Create())
                return "Settings_VoicePlayer_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(userId))).Replace("-", "") + "_";
        }

        private void SaveVoice()
        {
            PlayerPrefs.SetInt("Settings_VoiceEnabled", Current.voiceEnabled ? 1 : 0);
            PlayerPrefs.SetFloat("Settings_VoiceVolume", Current.masterVoiceVolume);
            PlayerPrefs.SetInt("Settings_MicrophoneMuted", Current.microphoneMuted ? 1 : 0);
        }

        private void LoadVoice()
        {
            Current.voiceEnabled = PlayerPrefs.GetInt("Settings_VoiceEnabled", 1) != 0;
            Current.masterVoiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_VoiceVolume", .8f));
            Current.microphoneMuted = PlayerPrefs.GetInt("Settings_MicrophoneMuted", 0) != 0;
            playerVoiceVolumes.Clear();
            playerVoiceMutes.Clear();
        }
    }
}