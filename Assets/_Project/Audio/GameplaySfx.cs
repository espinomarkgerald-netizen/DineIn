using UnityEngine;
using UnityEngine.Audio;

// Shared authored presentation. Confirmed actions own their cues; no gameplay state is changed.
public sealed class GameplaySfx : ScriptableObject
{
    public AudioMixerGroup mixerGroup;
    public AudioClip serve, payment, clean, success, error;
    public AudioClip click, panel, info, notification, excellent, endDay, blocked, declined, mistake, ambience;
    [Header("UI sound families (multiple clips, no immediate repeat)")]
    public AudioClip[] menuClicks, computerClicks, hudClicks;
    public AudioClip[] cratePlacements;
    [Range(0f, 1f)] public float cratePlacementLevel = .85f;
    private static int previousCrate = -1;
    public static void CratePlaced()
    {
        if (GetPlayback() == null) return;
        var clips = settings.cratePlacements;
        if (clips == null || clips.Length == 0) return;
        int index = uiRandom.Next(clips.Length);
        if (clips.Length > 1 && index == previousCrate) index = (index + 1) % clips.Length;
        previousCrate = index;
        PlayUiClip(clips[index], settings.volume * settings.cratePlacementLevel);
    }
    public AudioClip deliveryHold, hotbarArrival, selection, conversation;
    [Range(0f, 1f)] public float deliveryHoldLevel = .35f, hotbarArrivalLevel = .6f, selectionLevel = .35f, conversationLevel = .12f;
    [Min(.1f)] public float conversationFadeSeconds = .6f, conversationGapSeconds = 2f;
    [Range(0, 1)] public float conversationEnterZoom = .35f, conversationExitZoom = .45f;
    private static int selectionFrame = -1;
    public static void SelectWorld()
    {
        if (selectionFrame == Time.frameCount || GetPlayback() == null) return;
        selectionFrame = Time.frameCount;
        PlayUiClip(settings.selection, settings.volume * settings.selectionLevel);
    }
    public static void HotbarArrived()
    {
        if (GetPlayback() != null) PlayUiClip(settings.hotbarArrival, settings.volume * settings.hotbarArrivalLevel);
    }
    public static bool PresentationSuspended => AudioListener.pause || LobbyPauseMenu.IsAnyOpen ||
        MultiplayerSessionManager.Instance != null && MultiplayerSessionManager.Instance.IsMultiplayerSession &&
        (MultiplayerSessionManager.Instance.IsRecovering || !MultiplayerSessionManager.Instance.IsConnected);
    public static void SetDeliveryHold(AudioSource source, bool active)
    {
        var player = GetPlayback();
        if (source == null) return;
        if (!active || player == null || PresentationSuspended) { source.Stop(); return; }
        if (!source.isPlaying && settings.deliveryHold != null)
            player.StartUiLoop(source, settings.deliveryHold, settings.volume * settings.deliveryHoldLevel, false);
    }
    public static void PaymentCompleted(CustomerGroup group, int amount)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession)
        {
            if (session.IsAuthority) MultiplayerServiceActions.Active?.PublishFeedback(Cue.Payment, group, amount);
            return;
        }
        Completed(Cue.Payment, group);
        if (amount > 0) group?.PresentPayment(amount);
    }
    public AudioClip waterPour, tip;
    [Range(0f, 1f)] public float tipLevel = .85f;
    [Range(0f, 1f)] public float waterPourLevel = .65f;
    public enum ButtonFamily { Menu, Computer, Hud }
    private static readonly System.Random uiRandom = new System.Random();
    private static readonly int[] previousUiClip = { -1, -1, -1 };
    private static float lastButtonTime = -100f;
    public static void ButtonClick(ButtonFamily family)
    {
        var player = GetPlayback();
        if (player == null || AudioListener.pause || Time.unscaledTime - lastButtonTime < .04f) return;
        var clips = family == ButtonFamily.Computer ? settings.computerClicks : family == ButtonFamily.Hud ? settings.hudClicks : settings.menuClicks;
        if (clips == null || clips.Length == 0) { Play(Cue.Click); return; }
        int key = (int)family, index = uiRandom.Next(clips.Length);
        if (clips.Length > 1 && index == previousUiClip[key]) index = (index + 1 + uiRandom.Next(clips.Length - 1)) % clips.Length;
        previousUiClip[key] = index;
        PlayUiClip(clips[index], settings.volume * settings.clickLevel);
        lastButtonTime = Time.unscaledTime;
    }
    public static void SetWaterPour(AudioSource source, bool flowing)
    {
        var player = GetPlayback();
        if (source == null) return;
        if (!flowing || player == null || AudioListener.pause || LobbyPauseMenu.IsAnyOpen ||
            MultiplayerSessionManager.Instance != null && MultiplayerSessionManager.Instance.IsMultiplayerSession &&
            (MultiplayerSessionManager.Instance.IsRecovering || !MultiplayerSessionManager.Instance.IsConnected))
        { source.Stop(); return; }
        if (!source.isPlaying && settings.waterPour != null)
            player.StartUiLoop(source, settings.waterPour, settings.volume * settings.waterPourLevel);
    }
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
    [Range(0f, 1f)] public float ambienceLevel = .025f;
    [Min(.1f)] public float ambienceFadeSeconds = 1f, ambienceCrossfadeSeconds = 3f;
    [Range(.98f, 1.02f)] public float minimumAmbiencePitch = .995f, maximumAmbiencePitch = 1.005f;

    // Keep the original first five IDs for existing callers and multiplayer messages.
    public enum Cue { Serve, Payment, Clean, Success, Error, Click, Panel, Info, Notification, Excellent, EndDay, Blocked, Declined, Mistake, Tip }
    private static GameplaySfx settings;
    private static GameplaySfxPlayback playback;
    private static readonly System.Collections.Generic.HashSet<string> milestones = new();
    private static readonly float[] lastPlayed = new float[15];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        settings = null; playback = null; milestones.Clear();
        lastButtonTime = -100f; previousCrate = -1; selectionFrame = -1;
        for (int i = 0; i < previousUiClip.Length; i++) previousUiClip[i] = -1;
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
            Cue.Declined => settings.declined, Cue.Tip => settings.tip, _ => settings.mistake };
        float level = cue switch {
            Cue.Serve => settings.serveLevel, Cue.Payment => settings.paymentLevel, Cue.Clean => settings.cleanLevel,
            Cue.Success => settings.successLevel, Cue.Error => settings.errorLevel, Cue.Click => settings.clickLevel,
            Cue.Panel => settings.panelLevel, Cue.Info => settings.infoLevel, Cue.Notification => settings.notificationLevel,
            Cue.Excellent => settings.excellentLevel, Cue.EndDay => settings.endDayLevel, Cue.Blocked => settings.blockedLevel,
            Cue.Declined => settings.declinedLevel, Cue.Tip => settings.tipLevel, _ => settings.mistakeLevel };
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
