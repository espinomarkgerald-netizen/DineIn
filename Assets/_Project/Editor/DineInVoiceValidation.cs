#if UNITY_EDITOR && DINEIN_PHOTON_VOICE
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// Explicit, read-only development check. Never edits IDs or attempts a network/microphone connection.
public static class DineInVoiceValidation
{
    [MenuItem("Dine In/Validation/Validate Voice Setup")]
    public static void Run() => Debug.Log(Check());

    public static string Check()
    {
        var settings = PhotonNetwork.PhotonServerSettings;
        Require(settings != null && Guid.TryParse(settings.AppSettings.AppIdRealtime, out _), "Realtime App ID is missing or invalid.");
        bool voiceConfigured = Guid.TryParse(settings.AppSettings.AppIdVoice, out _);
        Require(!voiceConfigured || settings.AppSettings.AppIdVoice != settings.AppSettings.AppIdRealtime,
            "Voice and Realtime must use separate applications.");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/AudioMixer.mixer");
        Require(mixer != null && mixer.FindMatchingGroups("Voice").Count(g => g.name == "Voice") == 1,
            "Expected exactly one Voice group in the existing gameplay mixer.");
        var preferences = Resources.Load<DineIn.NewMenu.SettingsManager>("UI/PlayerSettings");
        Require(preferences != null && preferences.VoiceOutputGroup != null && preferences.VoiceOutputGroup.audioMixer == mixer,
            "The existing player settings must reference the Voice output mixer.");
        var player = Resources.Load<GameObject>("ManagerMultiplayer");
        Require(player != null && player.GetComponentsInChildren<Recorder>(true).Length == 0
            && player.GetComponentsInChildren<Photon.Voice.PUN.PhotonVoiceView>(true).Length == 0,
            "Player prefab must not duplicate the session-owned runtime voice bindings.");
        foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Standalone, UnityEditor.Build.NamedBuildTarget.Android })
            Require(PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Contains("DINEIN_PHOTON_VOICE"),
                "Voice integration define is missing for " + target.TargetName);
        string libs = "Assets/Photon/PhotonVoice/PhotonVoiceLibs/";
        CheckPlugin(libs + "Android/libs/arm64-v8a/libopus_egpv.so", BuildTarget.Android, "ARM64");
        CheckPlugin(libs + "Android/libs/armeabi-v7a/libopus_egpv.so", BuildTarget.Android, "ARMv7");
        CheckPlugin(libs + "Android/libs/audioinaec.aar", BuildTarget.Android, null);
        CheckPlugin(libs + "x86_64/opus_egpv.dll", BuildTarget.StandaloneWindows64, "x86_64");
        CheckPlugin(libs + "x86_64/AudioIn.dll", BuildTarget.StandaloneWindows64, "x86_64");
        string manifest = File.ReadAllText("Assets/_Project/Editor/DineInVoiceAndroidManifest.cs");
        Require(manifest.Contains("android.permission.RECORD_AUDIO") && manifest.Contains("unityplayer.SkipPermissionsDialog")
            && manifest.Contains("IPostGenerateGradleAndroidProject"), "Android microphone manifest integration is missing.");
        string voiceSource = File.ReadAllText("Assets/_Project/Networking/Multiplayer/MultiplayerVoiceController.cs");
        Require(!voiceSource.Contains("Photon Voice setup required") && !voiceSource.Contains("Voice SDK unavailable"),
            "Player-facing voice status still exposes setup details.");
        if (EditorApplication.isPlaying)
        {
            var clients = UnityEngine.Object.FindObjectsByType<Photon.Voice.PUN.PunVoiceClient>(FindObjectsSortMode.None);
            Require(clients.Length <= 1, "Duplicate voice connections exist.");
            var recorders = UnityEngine.Object.FindObjectsByType<Recorder>(FindObjectsSortMode.None);
            Require(recorders.Length <= 1, "Duplicate local Recorders exist.");
            foreach (var recorder in recorders)
            {
                var view = recorder.GetComponentInParent<PhotonView>();
                Require(view != null && view.IsMine && view.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber,
                    "A remote avatar is attempting microphone capture.");
                Require(!DineIn.NewMenu.SettingsManager.Instance.Current.microphoneMuted || !recorder.TransmitEnabled,
                    "Muted microphone still transmits.");
            }
        }
        return "[Voice] PASS: PUN " + PhotonNetwork.PunVersion + ", Voice SDK types, preserved Realtime configuration, mixer routing, runtime-only player bindings, build defines and native plugin imports. "
            + (voiceConfigured ? "Voice App ID configured. " : "ONLY CONFIGURATION MISSING: Photon Voice App ID in PhotonServerSettings > App Settings > App Id Voice. ")
            + "Live microphone, two-client audio and Android/IL2CPP build acceptance are not established by this check.";
    }

    private static void CheckPlugin(string path, BuildTarget platform, string cpu)
    {
        var importer = AssetImporter.GetAtPath(path) as PluginImporter;
        Require(importer != null && importer.GetCompatibleWithPlatform(platform), "Required native plugin is unavailable: " + path);
        if (cpu != null) Require(importer.GetPlatformData(platform, "CPU") == cpu || (platform == BuildTarget.StandaloneWindows64 && importer.GetPlatformData(platform, "CPU") == "AnyCPU" && importer.GetEditorData("CPU") == cpu), "Unexpected plugin CPU import setting: " + path);
    }
    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException("[Voice] " + message); }
}
#endif
