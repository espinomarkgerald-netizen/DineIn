using System.Collections;
using UnityEngine;

/// <summary>
/// Animates burger layers (TopBun, Cheese, Tomato, Patty, BottomBun) into place
/// for a loading screen, gives the stack a cartoonish "squish" right before it's
/// done, then pops to a single "complete burger" sprite with a bit of overshoot
/// and wiggle. On loop, the whole thing reverses smoothly (unpop -> unsquish ->
/// unstack -> re-stack) instead of hard-resetting.
///
/// Setup:
/// 1. Attach to the "Burger" object (or any holder).
/// 2. Drag layer RectTransforms into "Layers", in build order
///    (e.g. BottomBun, Patty, Tomato, Cheese, TopBun).
/// 3. Drag a separate "complete burger" image's RectTransform into "Complete Burger".
///    It should sit in the same spot as the assembled stack. Its starting state
///    doesn't matter - the script controls it entirely.
/// </summary>
public class BurgerLoadingAnimation : MonoBehaviour
{
    public enum AnimationPreset
    {
        StackSequential,   // Default: each layer drops straight down into its slot, one after another
        DropBounce,        // Same as above but with a springy bounce landing
        FadeInSequential,  // Layers fade in in place, no movement
        SlideFromSides,    // Layers alternate sliding in from left/right
        ScalePopIn,        // Layers pop in from zero scale with overshoot
        SpinIn             // Layers spin + scale in from zero
    }

    [Header("Burger Layers (in build order)")]
    [Tooltip("Assign in the order they should appear, e.g. BottomBun, Patty, Tomato, Cheese, TopBun")]
    public RectTransform[] layers;

    [Header("Complete Burger (shown after assembly)")]
    [Tooltip("A single sprite/image representing the fully built burger. Optional - leave empty to skip this step.")]
    public RectTransform completeBurger;
    [Min(0.01f)] public float completeBurgerTransitionDuration = 0.4f;
    [Min(0f)] public float completeBurgerHoldTime = 1.0f;

    [Header("Squish & Pop (cartoon assembly)")]
    [Tooltip("Adds a squash-together bounce on the layers, and an overshoot/wiggle pop on the complete burger.")]
    public bool useSquishAndPop = true;
    [Min(0.01f)] public float squishDuration = 0.12f;
    [Min(0.01f)] public float squishRecoverDuration = 0.2f;
    [Range(0f, 0.6f)] public float squishAmount = 0.18f;
    [Min(1f)] public float popOvershootScale = 1.18f;
    [Range(0f, 30f)] public float popWiggleAngle = 6f;

    [Header("Preset")]
    public AnimationPreset preset = AnimationPreset.StackSequential;

    [Header("Timing")]
    [Min(0f)] public float startDelay = 0.2f;
    [Min(0f)] public float delayBetweenLayers = 0.15f;
    [Min(0.01f)] public float layerAnimDuration = 0.4f;

    [Header("Behavior")]
    public bool playOnStart = true;
    public bool loop = true;
    [Min(0f)] public float loopPause = 0.3f;

    [Header("Stack / Drop Settings")]
    [Tooltip("How far above (in canvas units) each layer starts before falling into place")]
    public float dropHeight = 300f;

    [Header("Slide Settings")]
    [Tooltip("How far left/right each layer starts before sliding into place")]
    public float slideDistance = 400f;

    [Header("Easing (used by Stack / Fade / Slide)")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector2[] originalAnchoredPositions;
    private Vector3[] originalScales;
    private Quaternion[] originalRotations;
    private CanvasGroup[] canvasGroups;

    private CanvasGroup completeBurgerCanvasGroup;
    private Vector3 completeBurgerOriginalScale;

    private Coroutine playRoutine;

    void Awake()
    {
        CacheOriginalStates();
    }

    void OnEnable()
    {
        // Fires every time this GameObject is reactivated (e.g. SceneLoader calling
        // SetActive(true) on a reused, persistent loading canvas), not just the first
        // time it's ever enabled - unlike Start(), which only runs once per lifetime.
        if (playOnStart) Play();
    }

    void OnDisable()
    {
        // Coroutines on a disabled GameObject are killed automatically by Unity anyway,
        // but clearing the reference keeps Play()/Stop() from acting on a stale handle.
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    void CacheOriginalStates()
    {
        int n = layers.Length;
        originalAnchoredPositions = new Vector2[n];
        originalScales = new Vector3[n];
        originalRotations = new Quaternion[n];
        canvasGroups = new CanvasGroup[n];

        for (int i = 0; i < n; i++)
        {
            var rt = layers[i];
            if (rt == null) continue;

            originalAnchoredPositions[i] = rt.anchoredPosition;
            originalScales[i] = rt.localScale;
            originalRotations[i] = rt.localRotation;

            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            canvasGroups[i] = cg;
        }

        if (completeBurger != null)
        {
            completeBurgerOriginalScale = completeBurger.localScale;
            completeBurgerCanvasGroup = completeBurger.GetComponent<CanvasGroup>();
            if (completeBurgerCanvasGroup == null)
                completeBurgerCanvasGroup = completeBurger.gameObject.AddComponent<CanvasGroup>();

            completeBurgerCanvasGroup.alpha = 0f;
            completeBurgerCanvasGroup.interactable = false;
            completeBurgerCanvasGroup.blocksRaycasts = false;
        }
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void Stop()
    {
        if (playRoutine != null) StopCoroutine(playRoutine);
        SnapToAssembled();
    }

    public void PlayWithPreset(AnimationPreset newPreset)
    {
        preset = newPreset;
        Play();
    }

    void SnapToAssembled()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            layers[i].anchoredPosition = originalAnchoredPositions[i];
            layers[i].localScale = originalScales[i];
            layers[i].localRotation = originalRotations[i];
            canvasGroups[i].alpha = 1f;
        }
        if (completeBurger != null)
            completeBurgerCanvasGroup.alpha = 0f;
    }

    // ---- Hidden-state calculation (shared by instant-set and animated paths) ----

    void GetHiddenState(int i, AnimationPreset p, out Vector2 pos, out Vector3 scale, out Quaternion rot, out float alpha)
    {
        pos = originalAnchoredPositions[i];
        scale = originalScales[i];
        rot = originalRotations[i];
        alpha = 1f;

        switch (p)
        {
            case AnimationPreset.StackSequential:
            case AnimationPreset.DropBounce:
                pos = originalAnchoredPositions[i] + new Vector2(0, dropHeight);
                break;

            case AnimationPreset.FadeInSequential:
                alpha = 0f;
                break;

            case AnimationPreset.SlideFromSides:
                float dir = (i % 2 == 0) ? -1f : 1f;
                pos = originalAnchoredPositions[i] + new Vector2(dir * slideDistance, 0);
                break;

            case AnimationPreset.ScalePopIn:
                scale = Vector3.zero;
                break;

            case AnimationPreset.SpinIn:
                scale = Vector3.zero;
                rot = Quaternion.Euler(0, 0, 180f);
                break;
        }
    }

    void SetLayerInstant(int i, AnimationPreset p, bool hidden)
    {
        var rt = layers[i];
        if (rt == null) return;

        if (hidden)
        {
            GetHiddenState(i, p, out Vector2 pos, out Vector3 scale, out Quaternion rot, out float alpha);
            rt.anchoredPosition = pos;
            rt.localScale = scale;
            rt.localRotation = rot;
            canvasGroups[i].alpha = alpha;
        }
        else
        {
            rt.anchoredPosition = originalAnchoredPositions[i];
            rt.localScale = originalScales[i];
            rt.localRotation = originalRotations[i];
            canvasGroups[i].alpha = 1f;
        }
    }

    // ---- Main sequence ----

    IEnumerator PlayRoutine()
    {
        for (int i = 0; i < layers.Length; i++)
            SetLayerInstant(i, preset, hidden: true);

        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            yield return StartCoroutine(AssembleSequence());
            yield return new WaitForSeconds(completeBurgerHoldTime);

            if (!loop) yield break;

            yield return StartCoroutine(DisassembleSequence());
            yield return new WaitForSeconds(loopPause);
        }
    }

    IEnumerator AssembleSequence()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
                StartCoroutine(AnimateLayer(i, preset, reverse: false));
            yield return new WaitForSeconds(delayBetweenLayers);
        }

        yield return new WaitForSeconds(layerAnimDuration);

        if (completeBurger != null)
        {
            if (useSquishAndPop)
                yield return StartCoroutine(SquishStackTogether());

            yield return StartCoroutine(CrossfadeToComplete(true));
        }
    }

    IEnumerator DisassembleSequence()
    {
        if (completeBurger != null)
        {
            yield return StartCoroutine(CrossfadeToComplete(false));

            if (useSquishAndPop)
                yield return StartCoroutine(SquishStackTogether());
        }

        // Remove layers in reverse build order (last one placed comes off first).
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            if (layers[i] != null)
                StartCoroutine(AnimateLayer(i, preset, reverse: true));
            yield return new WaitForSeconds(delayBetweenLayers);
        }

        yield return new WaitForSeconds(layerAnimDuration);
    }

    // ---- Cartoon squish (assembled layers squash together, then spring back) ----

    IEnumerator SquishStackTogether()
    {
        if (layers.Length == 0) yield break;

        int n = layers.Length;
        Vector3[] squishScales = new Vector3[n];
        Vector2[] squishPositions = new Vector2[n];
        Vector3[] overshootScales = new Vector3[n];

        float centerY = 0f;
        int validCount = 0;
        for (int i = 0; i < n; i++)
        {
            if (layers[i] == null) continue;
            centerY += originalAnchoredPositions[i].y;
            validCount++;
        }
        if (validCount > 0) centerY /= validCount;

        for (int i = 0; i < n; i++)
        {
            if (layers[i] == null) continue;
            Vector3 s = originalScales[i];
            squishScales[i] = new Vector3(s.x * (1f + squishAmount * 0.5f), s.y * (1f - squishAmount), s.z);
            overshootScales[i] = s * 1.04f;

            Vector2 p = originalAnchoredPositions[i];
            squishPositions[i] = Vector2.Lerp(p, new Vector2(p.x, centerY), 0.25f);
        }

        // squash together
        yield return StartCoroutine(LerpAllLayers(originalScales, squishScales, originalAnchoredPositions, squishPositions, squishDuration, EaseOutQuad));
        // spring slightly past normal (bounce)
        yield return StartCoroutine(LerpAllLayers(squishScales, overshootScales, squishPositions, originalAnchoredPositions, squishRecoverDuration * 0.55f, EaseOutQuad));
        // settle back to normal
        yield return StartCoroutine(LerpAllLayers(overshootScales, originalScales, originalAnchoredPositions, originalAnchoredPositions, squishRecoverDuration * 0.45f, EaseOutQuad));
    }

    IEnumerator LerpAllLayers(Vector3[] fromScales, Vector3[] toScales, Vector2[] fromPos, Vector2[] toPos, float duration, System.Func<float, float> easeFn)
    {
        float t = 0f;
        while (t < duration)
        {
            t += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
            float eased = easeFn(Mathf.Clamp01(t / duration));

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                layers[i].localScale = Vector3.LerpUnclamped(fromScales[i], toScales[i], eased);
                layers[i].anchoredPosition = Vector2.LerpUnclamped(fromPos[i], toPos[i], eased);
            }
            yield return null;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            layers[i].localScale = toScales[i];
            layers[i].anchoredPosition = toPos[i];
        }
    }

    // ---- Crossfade + cartoon pop between layers and complete burger ----

    IEnumerator CrossfadeToComplete(bool toComplete)
    {
        float duration = completeBurgerTransitionDuration;
        const float phaseSplit = 0.55f;

        float startLayersAlpha = toComplete ? 1f : 0f;
        float endLayersAlpha = toComplete ? 0f : 1f;
        float startBurgerAlpha = toComplete ? 0f : 1f;
        float endBurgerAlpha = toComplete ? 1f : 0f;

        Vector3 hiddenBurgerScale = completeBurgerOriginalScale * 0.6f;
        Vector3 overshootBurgerScale = completeBurgerOriginalScale * popOvershootScale;
        Vector3 normalBurgerScale = completeBurgerOriginalScale;

        Quaternion normalRot = Quaternion.identity;
        Quaternion wiggleRot = Quaternion.Euler(0, 0, toComplete ? popWiggleAngle : -popWiggleAngle);

        float t = 0f;
        while (t < duration)
        {
            t += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
            float linear = Mathf.Clamp01(t / duration);
            float alphaEase = SmoothStep01(linear);

            for (int i = 0; i < layers.Length; i++)
                if (canvasGroups[i] != null)
                    canvasGroups[i].alpha = Mathf.Lerp(startLayersAlpha, endLayersAlpha, alphaEase);

            completeBurgerCanvasGroup.alpha = Mathf.Lerp(startBurgerAlpha, endBurgerAlpha, alphaEase);

            // Two-phase overshoot pop, mirrored for the reverse direction.
            Vector3 phaseAStart = toComplete ? hiddenBurgerScale : normalBurgerScale;
            Vector3 phaseAEnd = overshootBurgerScale;
            Vector3 phaseBStart = overshootBurgerScale;
            Vector3 phaseBEnd = toComplete ? normalBurgerScale : hiddenBurgerScale;

            if (linear < phaseSplit)
            {
                float p = linear / phaseSplit;
                float e = EaseOutQuad(p);
                completeBurger.localScale = Vector3.LerpUnclamped(phaseAStart, phaseAEnd, e);
                completeBurger.localRotation = Quaternion.SlerpUnclamped(normalRot, wiggleRot, e);
            }
            else
            {
                float p = (linear - phaseSplit) / (1f - phaseSplit);
                float e = EaseOutQuad(p);
                completeBurger.localScale = Vector3.LerpUnclamped(phaseBStart, phaseBEnd, e);
                completeBurger.localRotation = Quaternion.SlerpUnclamped(wiggleRot, normalRot, e);
            }

            yield return null;
        }

        for (int i = 0; i < layers.Length; i++)
            if (canvasGroups[i] != null)
                canvasGroups[i].alpha = endLayersAlpha;

        completeBurgerCanvasGroup.alpha = endBurgerAlpha;
        completeBurger.localScale = toComplete ? normalBurgerScale : hiddenBurgerScale;
        completeBurger.localRotation = normalRot;
    }

    // ---- Per-layer arrival / departure animation ----

    IEnumerator AnimateLayer(int i, AnimationPreset p, bool reverse)
    {
        var rt = layers[i];

        GetHiddenState(i, p, out Vector2 hiddenPos, out Vector3 hiddenScale, out Quaternion hiddenRot, out float hiddenAlpha);
        Vector2 shownPos = originalAnchoredPositions[i];
        Vector3 shownScale = originalScales[i];
        Quaternion shownRot = originalRotations[i];
        const float shownAlpha = 1f;

        Vector2 startPos = reverse ? shownPos : hiddenPos;
        Vector2 endPos = reverse ? hiddenPos : shownPos;
        Vector3 startScale = reverse ? shownScale : hiddenScale;
        Vector3 endScale = reverse ? hiddenScale : shownScale;
        Quaternion startRot = reverse ? shownRot : hiddenRot;
        Quaternion endRot = reverse ? hiddenRot : shownRot;
        float startAlpha = reverse ? shownAlpha : hiddenAlpha;
        float endAlpha = reverse ? hiddenAlpha : shownAlpha;

        bool useBounce = p == AnimationPreset.DropBounce;
        bool useBack = p == AnimationPreset.ScalePopIn || p == AnimationPreset.SpinIn;

        float t = 0f;
        while (t < layerAnimDuration)
        {
            t += LevelOneUIAccessibility.ScaledAnimationDeltaTime;
            float lerpT = Mathf.Clamp01(t / layerAnimDuration);

            float eased;
            if (useBounce) eased = reverse ? EaseInBounce(lerpT) : EaseOutBounce(lerpT);
            else if (useBack) eased = reverse ? EaseInBack(lerpT) : EaseOutBack(lerpT);
            else eased = easeCurve.Evaluate(lerpT);

            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            rt.localRotation = Quaternion.SlerpUnclamped(startRot, endRot, eased);
            canvasGroups[i].alpha = Mathf.Lerp(startAlpha, endAlpha, SmoothStep01(lerpT));

            yield return null;
        }

        rt.anchoredPosition = endPos;
        rt.localScale = endScale;
        rt.localRotation = endRot;
        canvasGroups[i].alpha = endAlpha;
    }

    // ---- Easing helpers ----

    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);

    static float SmoothStep01(float x) => x * x * (3f - 2f * x);

    static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (x < 1f / d1) return n1 * x * x;
        if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
        if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
        x -= 2.625f / d1;
        return n1 * x * x + 0.984375f;
    }

    static float EaseInBounce(float x) => 1f - EaseOutBounce(1f - x);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }

    static float EaseInBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * x * x * x - c1 * x * x;
    }
}
