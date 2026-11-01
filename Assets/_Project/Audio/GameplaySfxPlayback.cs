using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// Playback owned by the existing GameplaySfx root, not a gameplay/audio-state manager.
public sealed class GameplaySfxPlayback : MonoBehaviour
{
    private sealed class Voice
    {
        public AudioSource source;
        public bool world, paused;
        public Vector3 position;
        public float gain;
        public Scene scene;
    }
    private GameplaySfx config;
    private Voice[] voices;
    private readonly GameplaySfxPitch pitches = new GameplaySfxPitch();
    private Camera view;
    private MainCameraController controller;
    private float nextCameraSearch;
    private AudioSource[] bed;
    private int activeBed;
    private float bedGain, crossfade;
    private bool bedStarted, crossfading, bedPaused, bedWasPlaying;

    public void Configure(GameplaySfx settings)
    {
        config = settings;
        voices = new Voice[Mathf.Clamp(settings.voiceCount, 2, 24)];
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice { source = NewSource() };
        bed = new[] { NewSource(), NewSource() };
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private AudioSource NewSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false;
        source.spatialBlend = 0; source.dopplerLevel = 0;
        source.outputAudioMixerGroup = config.mixerGroup;
        return source;
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        view = null; controller = null; nextCameraSearch = 0;
    }
    private void ResolveCamera()
    {
        if (view != null && view.isActiveAndEnabled) return;
        if (Time.unscaledTime < nextCameraSearch) return;
        nextCameraSearch = Time.unscaledTime + .5f;
        view = PlayerSetup.FindActiveSceneCamera();
        controller = view != null ? view.GetComponentInParent<MainCameraController>() : null;
        if (controller == null) controller = FindFirstObjectByType<MainCameraController>();
    }
    private static bool Suspended
    {
        get
        {
            var session = MultiplayerSessionManager.Instance;
            return AudioListener.pause || LobbyPauseMenu.IsAnyOpen ||
                session != null && session.IsMultiplayerSession && (session.IsRecovering || !session.IsConnected);
        }
    }
    private static bool StorageOpen => RestockFlowCoordinator.Instance != null && RestockFlowCoordinator.Instance.IsRestockRoomOpen;
    private float WorldGain(Vector3 position, out float pan)
    {
        ResolveCamera(); pan = 0;
        if (view == null || StorageOpen) return 0;
        var point = view.WorldToViewportPoint(position);
        float zoom = controller != null && controller.Cam == view ? controller.NormalizedZoom : 0;
        float gain = GameplaySfxView.Gain(point.x, point.y, point.z, zoom,
            config.distanceFalloff.Evaluate, config.zoomFalloff.Evaluate);
        if (gain > 0) pan = GameplaySfxView.Pan(point.x, config.maximumPan);
        return gain;
    }
    public bool Play(AudioClip clip, float gain, bool world, Vector3 position, AudioMixerGroup mixer)
    {
        if (clip == null || voices == null || gain <= 0 || AudioListener.pause || world && Suspended) return false;
        float pan = 0, attenuation = world ? WorldGain(position, out pan) : 1;
        if (attenuation <= 0) return false;
        foreach (var voice in voices)
        {
            if (voice.source.isPlaying || voice.paused) continue;
            voice.world = world; voice.position = position; voice.gain = Mathf.Clamp01(gain);
            voice.scene = SceneManager.GetActiveScene();
            voice.source.outputAudioMixerGroup = config.mixerGroup != null ? config.mixerGroup : mixer;
            voice.source.clip = clip;
            voice.source.volume = voice.gain * attenuation;
            voice.source.panStereo = pan;
            voice.source.pitch = pitches.Next(clip.GetInstanceID(), config.minimumPitch, config.maximumPitch);
            voice.source.Play();
            return true;
        }
        // Never retune or steal an already-playing voice to make room for a new cue.
        return false;
    }
    public void MixExistingWorldSource(AudioSource source, Vector3 position, float gain, bool newIteration)
    {
        if (source == null || config == null) return;
        source.spatialBlend = 0; source.dopplerLevel = 0;
        if (config.mixerGroup != null) source.outputAudioMixerGroup = config.mixerGroup;
        source.volume = Mathf.Clamp01(gain) * WorldGain(position, out float pan);
        source.panStereo = pan;
        if (newIteration && source.clip != null)
            source.pitch = pitches.Next(source.clip.GetInstanceID(), config.minimumPitch, config.maximumPitch);
    }
    private void Update()
    {
        if (config == null || voices == null) return;
        foreach (var voice in voices)
        {
            if (!voice.world || !voice.source.isPlaying && !voice.paused) continue;
            if (!voice.scene.IsValid() || !voice.scene.isLoaded || StorageOpen)
            { voice.source.Stop(); voice.paused = false; continue; }
            if (Suspended)
            { if (!voice.paused) { voice.source.Pause(); voice.paused = true; } continue; }
            if (voice.paused) { voice.source.UnPause(); voice.paused = false; }
            float gain = WorldGain(voice.position, out float pan);
            voice.source.volume = voice.gain * gain; voice.source.panStereo = pan;
        }
        UpdateAmbience();
    }
    private static bool Restaurant(string name) => name == "Lobby1" || name == "Lobby2" || name == "Lobby3" ||
        name == "Lobby1Tutorial" || name == "Lobby2Tutorial" || name == "Lobby3Tutorial" ||
        name == "Lobby1 Multiplayer" || name == "Lobby2 Multiplayer" || name == "Lobby3 Multiplayer";
    private void StartBed(int index)
    {
        var source = bed[index]; source.Stop(); source.clip = config.ambience;
        if (index == activeBed) bedWasPlaying = false;
        source.volume = 0;
        source.pitch = pitches.Next(config.ambience.GetInstanceID(), config.minimumAmbiencePitch, config.maximumAmbiencePitch);
        source.Play();
    }
    private void UpdateAmbience()
    {
        if (bed == null || config.ambience == null || config.ambience.length <= 0) return;
        var session = MultiplayerSessionManager.Instance;
        bool wanted = Restaurant(SceneManager.GetActiveScene().name) && !StorageOpen &&
            !(session != null && session.IsMultiplayerSession && session.Ended);
        bool suspend = wanted && (Suspended || Time.timeScale <= 0);
        if (suspend != bedPaused)
        {
            foreach (var source in bed) { if (suspend) source.Pause(); else source.UnPause(); }
            bedPaused = suspend;
        }
        if (suspend) return;
        float target = wanted ? config.volume * config.ambienceLevel : 0;
        bedGain = Mathf.MoveTowards(bedGain, target, Time.unscaledDeltaTime *
            Mathf.Max(.001f, config.volume * config.ambienceLevel) / Mathf.Max(.1f, config.ambienceFadeSeconds));
        if (wanted && !bedStarted)
        { activeBed = 0; StartBed(activeBed); bedStarted = true; }
        if (!bedStarted) return;
        if (bed[activeBed].isPlaying) bedWasPlaying = true;
        else if (wanted && bedWasPlaying && !crossfading) StartBed(activeBed);
        if (bedGain <= 0 && !wanted)
        {
            foreach (var source in bed) source.Stop();
            bedStarted = crossfading = false; crossfade = 0;
            return;
        }
        float duration = Mathf.Min(Mathf.Max(.1f, config.ambienceCrossfadeSeconds), config.ambience.length * .25f);
        if (wanted && !crossfading && config.ambience.length - bed[activeBed].time <= duration * bed[activeBed].pitch)
        { StartBed(1 - activeBed); crossfading = true; crossfade = 0; }
        if (crossfading)
        {
            crossfade = Mathf.Min(1, crossfade + Time.unscaledDeltaTime / Mathf.Max(.1f, duration));
            bed[activeBed].volume = bedGain * (1 - crossfade);
            bed[1 - activeBed].volume = bedGain * crossfade;
            if (crossfade >= 1)
            { bed[activeBed].Stop(); activeBed = 1 - activeBed; crossfading = false; }
        }
        else bed[activeBed].volume = bedGain;
    }
    private void OnDestroy() => SceneManager.sceneLoaded -= SceneLoaded;
}
