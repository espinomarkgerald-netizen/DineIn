using UnityEngine;

/// <summary>Presentation only. Lives under the avatar's existing billboard/name Canvas.</summary>
[DisallowMultipleComponent]
public sealed class PlayerVoiceSpeakingIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform visual;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform[] voiceBars;
    [Header("Speech animation (unscaled seconds)")]
    [SerializeField, Min(0f)] private float silenceHold = .25f;
    [SerializeField, Min(.01f)] private float fadeIn = .15f;
    [SerializeField, Min(.01f)] private float fadeOut = .20f;
    [SerializeField, Range(0f, .08f)] private float pulseAmount = .025f;
    [Header("Readability across gameplay zoom")]
    [SerializeField, Min(1f)] private float referenceOrthoSize = 16f;
    [SerializeField] private Vector2 scaleLimits = new Vector2(.5f, 1.25f);
    private NameTagBillboard billboard;
    private RectTransform anchor;
    private Vector2 authoredPosition;
    private Vector3 authoredScale;
    private bool speaking;
    private float lastSpeechTime = float.NegativeInfinity;
    private float visibility;

    private void Awake()
    {
        billboard = GetComponentInParent<NameTagBillboard>(true);
        anchor = (RectTransform)transform;
        authoredPosition = anchor.anchoredPosition;
        authoredScale = anchor.localScale;
        HideImmediately();
    }

    public void SetSpeaking(bool value)
    {
        speaking = value;
        if (value) lastSpeechTime = Time.unscaledTime;
    }

    // Explicit invalidation skips the speech grace period (mute, disconnect, despawn).
    public void HideImmediately()
    {
        speaking = false;
        lastSpeechTime = float.NegativeInfinity;
        visibility = 0f;
        if (group != null) group.alpha = 0f;
        if (visual != null) visual.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!speaking && visibility <= 0f && Time.unscaledTime - lastSpeechTime > silenceHold) return;
        // Reuse the nameplate's cached LOCAL camera; never scan the scene per frame.
        var camera = billboard != null ? billboard.ViewCamera : null;
        float scale = camera != null && camera.orthographic
            ? Mathf.Clamp(camera.orthographicSize / referenceOrthoSize, scaleLimits.x, scaleLimits.y) : 1f;
        anchor.localScale = authoredScale * scale;
        // Keep the lower edge above the name even when zoom compensation enlarges the bubble.
        anchor.anchoredPosition = authoredPosition + Vector2.up * anchor.rect.height * .5f * (scale - 1f);
        Animate(Time.unscaledDeltaTime, Time.unscaledTime, LevelOneUIAccessibility.ReducedMotion);
    }

    private void Animate(float deltaTime, float now, bool reducedMotion)
    {
        if (visual == null || group == null) return;
        bool show = speaking || now - lastSpeechTime <= silenceHold;
        visibility = Mathf.MoveTowards(visibility, show ? 1f : 0f,
            deltaTime / Mathf.Max(.01f, show ? fadeIn : fadeOut));
        if (visibility <= 0f)
        {
            group.alpha = 0f;
            visual.gameObject.SetActive(false);
            return;
        }
        if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);
        group.alpha = Mathf.SmoothStep(0f, 1f, visibility);
        float scale = reducedMotion ? 1f : Mathf.Lerp(.88f, 1f, group.alpha);
        if (!reducedMotion && speaking) scale += pulseAmount * Mathf.Sin(now * 7f) * group.alpha;
        visual.localScale = Vector3.one * scale;
        for (int i = 0; i < voiceBars.Length; i++)
        {
            float height = !reducedMotion && speaking ? .65f + .35f * Mathf.Sin(now * 10f + i * 1.7f) : .8f;
            voiceBars[i].localScale = new Vector3(1f, height, 1f);
        }
    }

    private void OnDisable() => HideImmediately();
}