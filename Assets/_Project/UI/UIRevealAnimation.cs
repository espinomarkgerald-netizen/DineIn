using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small reusable reveal transition for authored UI prefabs. It uses unscaled
/// time, restores its authored pose, and becomes instant when Reduced Motion is
/// enabled.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIRevealAnimation : MonoBehaviour
{
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float duration = 0.18f;
    [SerializeField, Min(0f)] private float delay;
    [SerializeField, Range(0.7f, 1f)] private float startScale = 0.94f;
    [SerializeField] private Vector2 startOffset = Vector2.zero;

    private CanvasGroup canvasGroup;
    private RectTransform rect;
    private Vector2 visiblePosition;
    private Coroutine routine;
    private bool animateAnchoredPosition;

    private void Awake()
    {
        ResolveReferences();
        CaptureVisiblePosition();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureVisiblePosition();
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        ApplyVisibleState();
    }

    public void Play(float additionalDelay = 0f)
    {
        ResolveReferences();
        CaptureVisiblePosition();
        animateAnchoredPosition = CanAnimateAnchoredPosition();
        if (!isActiveAndEnabled || LevelOneUIAccessibility.ReducedMotion || duration <= 0f)
        {
            ApplyVisibleState();
            return;
        }

        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(Reveal(Mathf.Max(0f, delay + additionalDelay)));
    }

    private IEnumerator Reveal(float wait)
    {
        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * startScale;
        if (animateAnchoredPosition && rect != null)
            rect.anchoredPosition = visiblePosition + startOffset;

        float waited = 0f;
        while (waited < wait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            canvasGroup.alpha = t;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, t);
            if (animateAnchoredPosition && rect != null)
                rect.anchoredPosition = Vector2.Lerp(visiblePosition + startOffset, visiblePosition, t);
            yield return null;
        }

        ApplyVisibleState();
        routine = null;
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (rect == null)
            rect = transform as RectTransform;
    }

    private void CaptureVisiblePosition()
    {
        if (rect != null && routine == null && CanAnimateAnchoredPosition())
            visiblePosition = rect.anchoredPosition;
    }

    private bool CanAnimateAnchoredPosition()
    {
        if (rect == null || startOffset.sqrMagnitude <= 0.0001f)
            return false;

        // Layout Groups own the anchored position of their direct children.
        // Writing that value from an animation after a layout rebuild leaves
        // runtime-created cards at their prefab position until a resolution
        // change happens to rebuild the layout again.
        LayoutGroup parentLayout = rect.parent != null
            ? rect.parent.GetComponent<LayoutGroup>()
            : null;
        return parentLayout == null || !parentLayout.isActiveAndEnabled;
    }

    private void ApplyVisibleState()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;
        if (animateAnchoredPosition && CanAnimateAnchoredPosition() && rect != null)
            rect.anchoredPosition = visiblePosition;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        bool configuredPlayOnEnable,
        float configuredDuration,
        float configuredDelay,
        float configuredStartScale,
        Vector2 configuredStartOffset)
    {
        playOnEnable = configuredPlayOnEnable;
        duration = Mathf.Max(0f, configuredDuration);
        delay = Mathf.Max(0f, configuredDelay);
        startScale = Mathf.Clamp(configuredStartScale, 0.7f, 1f);
        startOffset = configuredStartOffset;
    }
#endif
}
