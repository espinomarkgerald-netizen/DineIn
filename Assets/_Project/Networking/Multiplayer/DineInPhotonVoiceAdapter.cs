#if DINEIN_PHOTON_VOICE
using System;
using System.Collections.Generic;
using DineIn.NewMenu;
using DineInSettings = DineIn.NewMenu.SettingsManager;
using Photon.Realtime;
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;

// Enable DINEIN_PHOTON_VOICE only after importing compatible Photon Voice 2 (PUN/Realtime 4)
// and configuring AppIdVoice. No reflection or replacement voice transport is used.
// Supported PUN integration: https://doc.photonengine.com/voice/v2/getting-started/voice-for-pun
internal sealed class DineInPhotonVoiceAdapter : IDisposable
{
    private sealed class Binding
    {
        public GameObject root;
        public PhotonView networkView;
        public PhotonVoiceView voiceView;
        public AudioSource output;
        public Recorder recorder;
        public VoiceLogger ownedLogger;
    }

    private readonly MultiplayerSessionManager session;
    private readonly DineInSettings settings;
    private readonly float detectionThreshold;
    private readonly Dictionary<int, Binding> bindings = new();
    private readonly List<int> expired = new();
    private GameObject clientRoot;
    private PunVoiceClient client;
    private Recorder localRecorder;
    public bool Connected => client != null && client.Client != null && client.Client.InRoom;
    public bool LocalSpeaking => localRecorder != null && localRecorder.IsCurrentlyTransmitting;
    public bool HasRecorder => localRecorder != null;
    public bool CaptureReady => localRecorder != null && localRecorder.RecordingEnabled && localRecorder.LevelMeter != null && !(localRecorder.LevelMeter is Photon.Voice.AudioUtil.LevelMeterDummy);

    public DineInPhotonVoiceAdapter(Transform owner, MultiplayerSessionManager session, DineInSettings settings, float threshold)
    {
        this.session = session;
        this.settings = settings;
        detectionThreshold = threshold;
        if (UnityEngine.Object.FindFirstObjectByType<PunVoiceClient>() != null)
            throw new InvalidOperationException("A voice client already belongs to this session.");
        clientRoot = new GameObject("Multiplayer Voice");
        clientRoot.SetActive(false);
        clientRoot.transform.SetParent(owner, false);
        try
        {
            clientRoot.AddComponent<VoiceLogger>().LogLevel = Photon.Voice.LogLevel.Warning;
            client = clientRoot.AddComponent<PunVoiceClient>();
            client.ApplyDontDestroyOnLoad = false;
            // PUN uses a runtime protocol/version override; voice must share that partition.
            client.UsePunAppSettings = false;
            client.Settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            client.Settings.AppVersion = PhotonNetwork.NetworkingClient.AppVersion;
            client.Settings.FixedRegion = PhotonNetwork.CloudRegion;
            // A Realtime PlayFab token is scoped to the Realtime app. Never forward it
            // to the separate Voice app. Default Voice auth uses the same stable UserId.
            client.UsePunAuthValues = false;
            client.AutoConnectAndJoin = true;
            clientRoot.SetActive(true);
            client.Client.AuthValues = new AuthenticationValues(PhotonNetwork.LocalPlayer.UserId);
            client.Client.ServerPortOverrides = PhotonNetwork.ServerPortOverrides;
            client.Client.SerializationProtocol = PhotonNetwork.NetworkingClient.SerializationProtocol;
        }
        catch
        {
            UnityEngine.Object.Destroy(clientRoot);
            throw;
        }
    }

    public void Refresh(bool captureAllowed)
    {
        if (client == null || client.Client == null) return;
        expired.Clear();
        foreach (var entry in bindings)
            if (entry.Value.networkView == null || !session.ValidActor(entry.Key)
                || !session.TryGetManager(entry.Key, out var manager) || manager != entry.Value.networkView.gameObject)
                expired.Add(entry.Key);
        foreach (int actor in expired) RemoveBinding(actor);

        foreach (var player in session.ConnectedPlayers)
        {
            if (!session.ValidActor(player.ActorNumber) || !session.TryGetManager(player.ActorNumber, out var manager)) continue;
            if (!bindings.TryGetValue(player.ActorNumber, out var binding))
            {
                binding = CreateBinding(manager);
                bindings.Add(player.ActorNumber, binding);
            }
            // Local playback preference is never published in Photon properties.
            bool muted = settings != null && settings.GetPlayerVoiceMuted(player.UserId);
            float gain = settings != null ? settings.GetPlayerVoiceVolume(player.UserId) * settings.Current.masterVoiceVolume : .8f;
            binding.output.volume = player.IsLocal || muted ? 0f : gain;
            if (binding.recorder != null)
            {
                bool record = captureAllowed && Connected;
                binding.recorder.TransmitEnabled = record;
                binding.recorder.RecordingEnabled = record;
            }
        }
    }

    private Binding CreateBinding(GameObject manager)
    {
        var networkView = manager.GetComponent<PhotonView>();
        if (networkView == null || networkView.ViewID == 0 || networkView.Owner == null)
            throw new InvalidOperationException("Player identity is not established.");
        if (manager.GetComponent<PhotonVoiceView>() != null)
            throw new InvalidOperationException("Player already has a voice binding.");

        var binding = new Binding { networkView = networkView };
        try
        {
            // PhotonVoiceView is on the manager; its logger must be on that
            // object or an ancestor, otherwise the SDK creates an orphan root logger.
            if (manager.GetComponent<VoiceLogger>() == null)
            {
                binding.ownedLogger = manager.AddComponent<VoiceLogger>();
                binding.ownedLogger.LogLevel = Photon.Voice.LogLevel.Warning;
            }
            binding.root = new GameObject("Session Voice Audio");
            binding.root.SetActive(false);
            binding.root.transform.SetParent(manager.transform, false);
            binding.root.AddComponent<DineInVoiceAudioSource>();
            binding.output = binding.root.AddComponent<AudioSource>();
            binding.output.playOnAwake = false;
            binding.output.spatialBlend = 0f;
            binding.output.volume = 0f;
            binding.root.AddComponent<Speaker>();
            if (networkView.OwnerActorNr == session.LocalActorNumber && networkView.IsMine)
            {
                binding.recorder = binding.root.AddComponent<Recorder>();
                binding.recorder.RecordingEnabled = false;
                binding.recorder.RecordWhenJoined = false;
                binding.recorder.TransmitEnabled = false;
                binding.recorder.DebugEchoMode = false;
                binding.recorder.VoiceDetection = true;
                binding.recorder.VoiceDetectionThreshold = detectionThreshold;
                binding.recorder.VoiceDetectionDelayMs = 500;
                binding.recorder.SourceType = Recorder.InputSourceType.Microphone;
                binding.recorder.MicrophoneType = Recorder.MicType.Photon;
                binding.recorder.UseMicrophoneTypeFallback = true;
                binding.recorder.SetAndroidNativeMicrophoneSettings(true, true, true);
                binding.recorder.StopRecordingWhenPaused = true;
                binding.recorder.InterestGroup = 0;
                binding.recorder.Encrypt = true;
                localRecorder = binding.recorder;
            }
            // PhotonVoiceView supplies PUN ViewID stream metadata and links this
            // avatar's Recorder/Speaker. Voice-room actor IDs are never treated as PUN actor IDs.
            // All microphone settings are applied before its GameObject awakens.
            // PhotonVoiceView.Start then discovers the active child components.
            binding.root.SetActive(true);
            binding.voiceView = manager.AddComponent<PhotonVoiceView>();
            return binding;
        }
        catch
        {
            DestroyBinding(binding);
            throw;
        }
    }

    public bool IsSpeaking(int actor)
    {
        return bindings.TryGetValue(actor, out var binding) && binding.voiceView != null
            && binding.output != null && binding.output.volume > 0f && binding.voiceView.IsSpeaking;
    }

    private void RemoveBinding(int actor)
    {
        if (!bindings.TryGetValue(actor, out var binding)) return;
        DestroyBinding(binding);
        bindings.Remove(actor);
    }

    private void DestroyBinding(Binding binding)
    {
        if (binding.recorder != null)
        {
            binding.recorder.TransmitEnabled = false;
            binding.recorder.RecordingEnabled = false;
            if (client != null) client.RemoveRecorder(binding.recorder);
            if (localRecorder == binding.recorder) localRecorder = null;
        }
        if (binding.output != null) { binding.output.Stop(); binding.output.mute = true; }
        if (binding.voiceView != null) UnityEngine.Object.Destroy(binding.voiceView);
        if (binding.root != null)
        {
            binding.root.SetActive(false);
            UnityEngine.Object.Destroy(binding.root);
        }
        if (binding.ownedLogger != null) UnityEngine.Object.Destroy(binding.ownedLogger);
    }

    public void Dispose()
    {
        foreach (var binding in bindings.Values) DestroyBinding(binding);
        bindings.Clear();
        if (client != null)
        {
            client.AutoConnectAndJoin = false;
            if (client.Client != null && client.Client.IsConnected) client.Disconnect();
        }
        if (clientRoot != null)
        {
            clientRoot.SetActive(false);
            UnityEngine.Object.Destroy(clientRoot);
        }
        client = null;
        clientRoot = null;
    }
}
#endif