using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Auto-plays an "open" (small -> big) animation when the scene starts.
/// Exposes PlayCloseThenInvoke() for external scripts (e.g. a scene loader/trigger)
/// to call BEFORE requesting a scene change — this is the only reliable way to get
/// a "closing" animation to finish before objects are destroyed, since Unity destroys
/// everything in an unloading scene in the same frame with no way to delay it from
/// inside OnDestroy().
/// </summary>
public class IrisScaleToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The RectTransform to animate. Defaults to this object's own RectTransform if left empty.")]
    [SerializeField] private RectTransform target;

    [Header("Mask Component")]
    [Tooltip("Disabled automatically after reaching small scale, following Disable Delay below. " +
             "E.g. an Image, RectMask2D, or your iris shader's Graphic component.")]
    [SerializeField] private Behaviour maskComponent;

    [Tooltip("Seconds to wait after reaching small scale before disabling Mask Component.")]
    [SerializeField] private float disableDelay = 0.25f;

    [Header("Scale Values")]
    [SerializeField] private float smallScale = 0f;
    [SerializeField] private float bigScale = 1f;

    [Header("Timing")]
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Safety clamp so a single hitch/spike frame (e.g. right after a scene load,
    // where Unity can report one huge bundled deltaTime) can't skip the whole
    // animation to its end value in a single iteration.
    private const float MaxFrameDelta = 0.05f;

    private bool isBig;
    private bool isAnimating = false;
    private Coroutine activeRoutine;
    private Coroutine disableRoutine;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }

        // Start closed/small, as if covering the screen before the scene reveals itself.
        isBig = false;
        ApplyScaleImmediate(smallScale);

        if (maskComponent != null)
        {
            maskComponent.enabled = true; // ensure it's on before the opening animation plays
        }
    }

    private void Start()
    {
        // Scene has just loaded -> reveal it.
        // We wait one frame first because the frame right after a scene finishes
        // loading can report an inflated deltaTime (all the load time bundled into
        // it). Starting the timer immediately would let that single huge delta
        // jump straight past `duration`, making the animation appear to skip
        // straight to its end value instead of playing. Skipping one frame lets
        // that spike get consumed before we start measuring.
        StartCoroutine(DelayedPlayOpen());
    }

    private IEnumerator DelayedPlayOpen()
    {
        yield return null;
        PlayOpen();
    }

    // ---------- Public API ----------

    /// <summary>Plays small -> big. Safe to call manually too (e.g. after a fade-in trigger).</summary>
    public void PlayOpen(Action onComplete = null)
    {
        if (isAnimating)
        {
            Debug.LogWarning("[IrisScaleToggle] PlayOpen ignored — already animating.");
            return;
        }

        if (maskComponent != null)
        {
            maskComponent.enabled = true;
        }
        CancelPendingDisable();

        RunToggle(bigScale, true, onComplete);
    }

    /// <summary>
    /// Plays big -> small, then invokes onComplete. Call this from your scene-change
    /// trigger BEFORE actually requesting the scene load/unload, and load the scene
    /// inside the onComplete callback.
    /// </summary>
    public void PlayCloseThenInvoke(Action onComplete)
    {
        if (isAnimating)
        {
            Debug.LogWarning("[IrisScaleToggle] PlayCloseThenInvoke ignored — already animating.");
            return;
        }

        RunToggle(smallScale, false, onComplete);
    }

    // ---------- Internal ----------

    private void RunToggle(float to, bool nextIsBig, Action onComplete)
    {
        float from = target != null ? target.localScale.x : (isBig ? bigScale : smallScale);
        isBig = nextIsBig;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(AnimateScale(from, to, duration, onComplete));
    }

    private IEnumerator AnimateScale(float from, float to, float dur, Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        isAnimating = true;
        dur = Mathf.Max(dur, 0.01f);
        float t = 0f;

        while (t < dur)
        {
            // Clamp so a single hitch/spike frame can't skip the whole animation.
            float delta = Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);
            t += delta;
            float normalized = Mathf.Clamp01(t / dur);
            float eased = easing.Evaluate(normalized);
            float scale = Mathf.Lerp(from, to, eased);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = new Vector3(to, to, 1f);
        isAnimating = false;
        activeRoutine = null;

        // If we just finished shrinking down to small, schedule the mask disable.
        if (!isBig && maskComponent != null)
        {
            CancelPendingDisable();
            disableRoutine = StartCoroutine(DisableMaskAfterDelay());
        }

        onComplete?.Invoke();
    }

    private IEnumerator DisableMaskAfterDelay()
    {
        yield return new WaitForSecondsRealtime(disableDelay);
        if (maskComponent != null)
        {
            maskComponent.enabled = false;
        }
        disableRoutine = null;
    }

    private void CancelPendingDisable()
    {
        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }
    }

    private void ApplyScaleImmediate(float scale)
    {
        if (target != null)
        {
            target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}