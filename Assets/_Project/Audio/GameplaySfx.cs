using UnityEngine;
using UnityEngine.Audio;

// Shared authored presentation. Confirmed actions own their cues; no gameplay state is changed.
public sealed class GameplaySfx : ScriptableObject
{
    public AudioMixerGroup mixerGroup;
    public AudioClip serve, payment, clean, success, error;
    public AudioClip click, panel, info, notification, excellent, endDay, blocked, declined, mistake, ambience;
    [Range(0f, 1f)] public float volume = .45f;
    [Min(0f)] public float minimumInterval = .15f;
    [Header("Cue levels")]
    [Range(0f, 1f)] public float serveLevel = .94f, paymentLevel = 1f, cleanLevel = .81f, successLevel = .8f, errorLevel = .65f;
    [Range(0f, 1f)] public float clickLevel = .6f, panelLevel = .4f, infoLevel = .45f, notificationLevel = .6f;
    [Range(0f, 1f)] public float excellentLevel = .9f, endDayLevel = .85f, blockedLevel = .6f, declinedLevel = .7f, mistakeLevel = .65f;
    [Header("Camera-relative world sound")]
    public AnimationCurve distanceFalloff = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, .55f), new Keyframe(2, .1f), new Keyframe(3, 0));
    public AnimationCurve zoomFalloff = AnimationCurve.Linear(0, 1, 1, .6f);
    [Range(0f, .5f)] public float maximumPan = .35f;
    [Range(2, 24)] public int voiceCount = 8;
    [Header("Small pitch variation")]
    [Range(.9f, 1.1f)] public float minimumPitch = .985f, maximumPitch = 1.015f;
    [Header("Restaurant ambience (SFX mixer)")]
    [Range(0f, 1f)] public float ambienceLevel = .08f;
    [Min(.1f)] public float ambienceFadeSeconds = 1f, ambienceCrossfadeSeconds = 3f;
    [Range(.98f, 1.02f)] public float minimumAmbiencePitch = .995f, maximumAmbiencePitch = 1.005f;

    // Keep the original first five IDs for existing callers and multiplayer messages.
    public enum Cue { Serve, Payment, Clean, Success, Error, Click, Panel, Info, Notification, Excellent, EndDay, Blocked, Declined, Mistake }
    private static GameplaySfx settings;
    private static GameplaySfxPlayback playback;
    private static readonly System.Collections.Generic.HashSet<string> milestones = new();
    private static readonly float[] lastPlayed = new float[14];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        settings = null; playback = null; milestones.Clear();
        for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -100f;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() => GetPlayback();
    private static GameplaySfxPlayback GetPlayback()
    {
        if (!Application.isPlaying) return null;
        if (settings == null) settings = Resources.Load<GameplaySfx>("Audio/GameplaySfx");
        if (settings == null) return null;
        if (playback == null)
        {
            var root = new GameObject("Gameplay SFX");
            DontDestroyOnLoad(root);
            playback = root.AddComponent<GameplaySfxPlayback>();
            playback.Configure(settings);
        }
        return playback;
    }
    public static bool TryPosition(Object target, out Vector3 position)
    {
        if (target is Component component) { position = component.transform.position; return true; }
        if (target is GameObject gameObject) { position = gameObject.transform.position; return true; }
        position = default; return false;
    }
    public static void Completed(Cue cue, Object target)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession)
        {
            if (session.IsAuthority) MultiplayerServiceActions.Active?.PublishFeedback(cue, target);
            return;
        }
        if (TryPosition(target, out var position)) PresentAction(cue, position);
        else PresentAction(cue);
    }
    public static void PresentAction(Cue cue)
    {
        Play(cue);
        GameDayManager.Instance?.PresentActionFeedback(cue);
    }
    public static void PresentAction(Cue cue, Vector3 position)
    {
        Play(cue, position);
        GameDayManager.Instance?.PresentActionFeedback(cue);
    }
    public static void Milestone(string identity)
    {
        if (!string.IsNullOrEmpty(identity) && milestones.Add(identity)) Play(Cue.Excellent);
    }
    public static void Play(Cue cue) => PlayCue(cue, false, default);
    public static void Play(Cue cue, Vector3 position) => PlayCue(cue, true, position);
    private static void PlayCue(Cue cue, bool world, Vector3 position)
    {
        var player = GetPlayback();
        if (player == null || AudioListener.pause) return;
        int index = (int)cue;
        if (index < 0 || index >= lastPlayed.Length) return;
        float interval = cue == Cue.Click ? .04f : settings.minimumInterval;
        if (Time.unscaledTime - lastPlayed[index] < interval) return;
        AudioClip clip = cue switch {
            Cue.Serve => settings.serve, Cue.Payment => settings.payment, Cue.Clean => settings.clean,
            Cue.Success => settings.success, Cue.Error => settings.error, Cue.Click => settings.click,
            Cue.Panel => settings.panel, Cue.Info => settings.info, Cue.Notification => settings.notification,
            Cue.Excellent => settings.excellent, Cue.EndDay => settings.endDay, Cue.Blocked => settings.blocked,
            Cue.Declined => settings.declined, _ => settings.mistake };
        float level = cue switch {
            Cue.Serve => settings.serveLevel, Cue.Payment => settings.paymentLevel, Cue.Clean => settings.cleanLevel,
            Cue.Success => settings.successLevel, Cue.Error => settings.errorLevel, Cue.Click => settings.clickLevel,
            Cue.Panel => settings.panelLevel, Cue.Info => settings.infoLevel, Cue.Notification => settings.notificationLevel,
            Cue.Excellent => settings.excellentLevel, Cue.EndDay => settings.endDayLevel, Cue.Blocked => settings.blockedLevel,
            Cue.Declined => settings.declinedLevel, _ => settings.mistakeLevel };
        if (clip != null && player.Play(clip, settings.volume * level, world, position, settings.mixerGroup))
            lastPlayed[index] = Time.unscaledTime;
    }
    public static void PlayWorldClip(AudioClip clip, Vector3 position, float gain, AudioMixerGroup group = null)
    {
        var player = GetPlayback();
        if (player != null && !AudioListener.pause) player.Play(clip, gain, true, position, group != null ? group : settings.mixerGroup);
    }
    public static void MixExistingWorldSource(AudioSource source, Vector3 position, float gain, bool newIteration)
    {
        GetPlayback()?.MixExistingWorldSource(source, position, gain, newIteration);
    }
    public static void PlayUiClip(AudioClip clip, float gain, AudioMixerGroup group = null)
    {
        var player = GetPlayback();
        if (player != null && !AudioListener.pause) player.Play(clip, gain, false, default, group != null ? group : settings.mixerGroup);
    }
}
