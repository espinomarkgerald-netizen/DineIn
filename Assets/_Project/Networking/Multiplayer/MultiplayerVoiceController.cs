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
    [SerializeField, Range(0, 3)] private int reconnectAttempts = 2;
    [SerializeField, Range(1f, 10f)] private float reconnectDelay = 3f;
    private float nextRefresh, retryAt;
    private int retries;
    private bool wasConnected;
    private string lastReportedStatus, lastReportedMicrophone;
    private const string UnavailableMessage = "Voice chat is unavailable right now.";
    public string MicrophoneMessage { get; private set; }
    public bool Connected
    {
        get
        {
#if DINEIN_PHOTON_VOICE
            return adapter != null && adapter.Connected;
#else
            return false;
#endif
        }
    }
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
                && Guid.TryParse(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice, out _);
#else
            return false;
#endif
        }
    }
    public string Status { get; private set; } = UnavailableMessage;
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
#if DINEIN_PHOTON_VOICE
        var appSettings = PhotonNetwork.PhotonServerSettings?.AppSettings;
        bool voiceConfigured = appSettings != null && Guid.TryParse(appSettings.AppIdVoice, out _);
        Diagnostic((voiceConfigured ? "Configuration ready" : "FAILED stage=Configuration")
            + " source=PhotonNetwork.PhotonServerSettings adapterCompiled=true"
            + " voiceIdConfigured=" + voiceConfigured
            + " voiceIdEmpty=" + string.IsNullOrWhiteSpace(appSettings?.AppIdVoice)
            + " realtimeIdConfigured=" + (appSettings != null && Guid.TryParse(appSettings.AppIdRealtime, out _)));
#else
        Diagnostic("FAILED stage=Configuration adapterCompiled=false (DINEIN_PHOTON_VOICE is disabled).");
#endif
    }

    private void PreferencesChanged(UserSettings value)
    {
        // Only toggling voice retries a failed connection; other sliders never retry it.
        if (value.voiceEnabled != lastVoiceEnabled) { failed = false; retries = 0; retryAt = 0; DisposeTransport(); }
        lastVoiceEnabled = value.voiceEnabled;
        nextRefresh = 0;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        TickVoice();
        if (!string.IsNullOrEmpty(MicrophoneMessage) && Preferences.voiceEnabled && Available)
            Status += "\n" + MicrophoneMessage;
        if (Status != lastReportedStatus || MicrophoneStatus != lastReportedMicrophone)
        {
            Diagnostic(Status + " Microphone=" + MicrophoneStatus);
            lastReportedStatus = Status; lastReportedMicrophone = MicrophoneStatus;
        }
    }

    private void LateUpdate()
    {
#if DINEIN_PHOTON_VOICE
        adapter?.UpdateSpeakingIndicators();
#endif
    }

    private void TickVoice()
    {
        MicrophoneMessage = null;
        if (stopped || session == null || session.Ended || string.IsNullOrEmpty(session.RunId) || !session.IsConnected || PhotonNetwork.OfflineMode)
        {
            DisposeTransport();
            if (session != null && !session.IsConnected)
            { failed = false; retries = 0; retryAt = 0; wasConnected = false; }
            Status = session != null && session.IsRecovering
                ? "Voice chat disconnected. Trying to reconnect…" : "Voice chat disconnected.";
            MicrophoneStatus = "MIC OFF";
            return;
        }
        if (!Preferences.voiceEnabled)
        {
            DisposeTransport();
            Status = "Voice chat is off.";
            MicrophoneStatus = "MIC OFF";
            return;
        }
        if (!Available)
        {
            Status = UnavailableMessage;
            MicrophoneStatus = "VOICE UNAVAILABLE";
            return;
        }
#if DINEIN_PHOTON_VOICE
        try
        {
            if (Time.unscaledTime < retryAt)
            { Status = "Voice chat disconnected. Trying to reconnect…"; MicrophoneStatus = "CONNECTING…"; return; }
            if (adapter == null)
            {
                Diagnostic(wasConnected ? "Reconnecting Photon Voice." : "Connecting Photon Voice for the current gameplay room.");
                adapter = new DineInPhotonVoiceAdapter(transform, session, settings, voiceDetectionThreshold);
                connectDeadline = Time.unscaledTime + connectionTimeout;
            }
            bool capture = adapter.Connected && CanUseMicrophone();
            adapter.Refresh(capture);
            if (adapter.Connected)
            {
                connectDeadline = Time.unscaledTime + connectionTimeout;
                wasConnected = true; retries = 0;
                Status = "Voice chat connected.";
                if (capture && !adapter.HasRecorder)
                { MicrophoneStatus = "MIC STARTING"; MicrophoneMessage = "Microphone is starting…"; }
                else if (capture && !adapter.CaptureReady)
                { MicrophoneStatus = "MIC UNAVAILABLE"; MicrophoneMessage = "Microphone could not start. You can still hear other players."; }
            }
            else if (Time.unscaledTime >= connectDeadline)
                RetryVoice("FAILED stage=ConnectOrJoin timeout " + adapter.ConnectionDiagnostic);
            else
            {
                Status = wasConnected ? "Voice chat disconnected. Trying to reconnect…" : "Connecting voice chat…";
                MicrophoneStatus = "CONNECTING…";
            }
        }
        catch (Exception error)
        {
            // Log only the exception type: SDK exception messages may contain connection settings.
            RetryVoice("Photon Voice initialization failed: " + error.GetType().Name);
        }
#endif
    }

#if DINEIN_PHOTON_VOICE
    private bool CanUseMicrophone()
    {
        if (Preferences.microphoneMuted) { MicrophoneStatus = "MIC MUTED"; MicrophoneMessage = "Microphone muted."; return false; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            MicrophoneStatus = "MIC PERMISSION REQUIRED";
            MicrophoneMessage = "Microphone permission is required to use voice chat.";
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
            if (Microphone.devices.Length == 0) { MicrophoneStatus = "NO MICROPHONE"; MicrophoneMessage = "No microphone detected."; return false; }
        }
        catch (Exception) { MicrophoneStatus = "NO MICROPHONE"; MicrophoneMessage = "No microphone detected."; return false; }
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
        Status = "Voice chat disconnected.";
        MicrophoneStatus = "MIC OFF";
    }

#if DINEIN_PHOTON_VOICE
    private void RetryVoice(string reason)
    {
        Diagnostic(reason);
        DisposeTransport();
        retries++;
        failed = retries > reconnectAttempts;
        retryAt = Time.unscaledTime + reconnectDelay;
        Status = failed ? UnavailableMessage : "Voice chat disconnected. Trying to reconnect…";
        MicrophoneStatus = failed ? "VOICE UNAVAILABLE" : "CONNECTING…";
    }
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    internal static void Diagnostic(string message) => Debug.Log("[Voice] " + message);

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