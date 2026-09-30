using System;
using DineIn.NewMenu;
using DineInSettings = DineIn.NewMenu.SettingsManager;
using Photon.Pun;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>Optional room voice, owned and destroyed by the existing multiplayer session.</summary>
[DisallowMultipleComponent]
public sealed class MultiplayerVoiceController : MonoBehaviour
{
    [SerializeField, Range(.001f, .1f)] private float voiceDetectionThreshold = .01f;
    [SerializeField, Range(5f, 45f)] private float connectionTimeout = 20f;
    private MultiplayerSessionManager session;
    private DineInSettings settings;
    private readonly UserSettings defaults = new();
    private bool stopped, failed, lastVoiceEnabled;
    private float nextRefresh;
#if DINEIN_PHOTON_VOICE
    private DineInPhotonVoiceAdapter adapter;
    private float connectDeadline;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
    private bool permissionPending;
    private PermissionCallbacks permissionCallbacks;
    private const string PermissionAskedKey = "Settings_VoiceMicrophonePermissionAsked";
#endif

    public bool Available
    {
        get
        {
#if DINEIN_PHOTON_VOICE
            return !failed && PhotonNetwork.PhotonServerSettings != null
                && !string.IsNullOrWhiteSpace(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice);
#else
            return false;
#endif
        }
    }
    public string Status { get; private set; } = "Voice unavailable — Photon Voice setup required";
    public string MicrophoneStatus { get; private set; } = "VOICE UNAVAILABLE";
    public bool LocalSpeaking
    {
        get
        {
#if DINEIN_PHOTON_VOICE
            return adapter != null && adapter.LocalSpeaking;
#else
            return false;
#endif
        }
    }
    private UserSettings Preferences => settings != null ? settings.Current : defaults;

    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        settings = DineInSettings.EnsureInstance();
        if (settings != null) settings.OnSettingsLoaded += PreferencesChanged;
        lastVoiceEnabled = Preferences.voiceEnabled;
    }

    private void PreferencesChanged(UserSettings value)
    {
        // Only toggling voice retries a failed connection; other sliders never retry it.
        if (failed && value.voiceEnabled != lastVoiceEnabled) { failed = false; DisposeTransport(); }
        lastVoiceEnabled = value.voiceEnabled;
        nextRefresh = 0;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        if (stopped || session == null || session.Ended || string.IsNullOrEmpty(session.RunId) || !session.IsConnected || PhotonNetwork.OfflineMode)
        {
            DisposeTransport();
            Status = "Voice disconnected";
            MicrophoneStatus = "MIC OFF";
            return;
        }
        if (!Preferences.voiceEnabled)
        {
            DisposeTransport();
            Status = "Voice disabled";
            MicrophoneStatus = "MIC OFF";
            return;
        }
        if (!Available)
        {
            Status = failed ? "Voice unavailable" : "Voice unavailable — Photon Voice setup required";
            MicrophoneStatus = "VOICE UNAVAILABLE";
            return;
        }
#if DINEIN_PHOTON_VOICE
        try
        {
            if (adapter == null)
            {
                adapter = new DineInPhotonVoiceAdapter(transform, session, settings, voiceDetectionThreshold);
                connectDeadline = Time.unscaledTime + connectionTimeout;
            }
            bool capture = CanUseMicrophone();
            adapter.Refresh(capture);
            if (adapter.Connected)
            {
                connectDeadline = Time.unscaledTime + connectionTimeout;
                Status = "Voice connected";
                if (capture && adapter.HasRecorder && !adapter.CaptureReady)
                    MicrophoneStatus = "NO MICROPHONE";
            }
            else if (Time.unscaledTime >= connectDeadline)
            {
                failed = true;
                DisposeTransport();
                Status = "Voice unavailable — toggle Voice to retry";
                MicrophoneStatus = "VOICE UNAVAILABLE";
            }
            else Status = "Connecting voice…";
        }
        catch (Exception)
        {
            // Voice failure is local and optional. Never leave the gameplay room.
            // Avoid emitting authentication settings or third-party exception payloads.
            failed = true;
            DisposeTransport();
            Status = "Voice unavailable";
            MicrophoneStatus = "VOICE UNAVAILABLE";
        }
#endif
    }

#if DINEIN_PHOTON_VOICE
    private bool CanUseMicrophone()
    {
        if (Preferences.microphoneMuted) { MicrophoneStatus = "MIC MUTED"; return false; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            MicrophoneStatus = "PERMISSION REQUIRED";
            if (!permissionPending && PlayerPrefs.GetInt(PermissionAskedKey, 0) == 0)
            {
                PlayerPrefs.SetInt(PermissionAskedKey, 1);
                PlayerPrefs.Save();
                permissionPending = true;
                permissionCallbacks = new PermissionCallbacks();
                permissionCallbacks.PermissionGranted += PermissionFinished;
                permissionCallbacks.PermissionDenied += PermissionFinished;
                permissionCallbacks.PermissionDeniedAndDontAskAgain += PermissionFinished;
                Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
            }
            return false;
        }
#endif
        try
        {
            if (Microphone.devices.Length == 0) { MicrophoneStatus = "NO MICROPHONE"; return false; }
        }
        catch (Exception) { MicrophoneStatus = "NO MICROPHONE"; return false; }
        MicrophoneStatus = "MIC ON";
        return true;
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private void PermissionFinished(string permission)
    {
        permissionPending = false;
        nextRefresh = 0;
        RemovePermissionCallbacks();
    }
    private void RemovePermissionCallbacks()
    {
        if (permissionCallbacks == null) return;
        permissionCallbacks.PermissionGranted -= PermissionFinished;
        permissionCallbacks.PermissionDenied -= PermissionFinished;
        permissionCallbacks.PermissionDeniedAndDontAskAgain -= PermissionFinished;
        permissionCallbacks = null;
    }
#endif

    public bool IsSpeaking(int punActorNumber)
    {
#if DINEIN_PHOTON_VOICE
        return adapter != null && adapter.IsSpeaking(punActorNumber);
#else
        return false;
#endif
    }

    public void StopVoice()
    {
        stopped = true;
        DisposeTransport();
        Status = "Voice disconnected";
        MicrophoneStatus = "MIC OFF";
    }

    private void DisposeTransport()
    {
#if DINEIN_PHOTON_VOICE
        if (adapter == null) return;
        try { adapter.Dispose(); }
        catch (Exception) { /* Gameplay shutdown must complete even if native audio teardown fails. */ }
        adapter = null;
#endif
    }

    private void OnDisable() => DisposeTransport();
    private void OnDestroy()
    {
        if (settings != null) settings.OnSettingsLoaded -= PreferencesChanged;
#if UNITY_ANDROID && !UNITY_EDITOR
        RemovePermissionCallbacks();
#endif
        StopVoice();
    }
}