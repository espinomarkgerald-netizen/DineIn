using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Reusable, standalone panel opener/closer with animation presets.
/// Attach this to ANY panel GameObject (shop panel, wallet panel, settings
/// panel, popups, etc). It knows nothing about PlayFab, wallets, or any
/// other game system - other scripts just call Open()/Close()/Toggle() on it.
///
/// Single Responsibility: this script only activates/deactivates its own
/// GameObject and animates that transition. It does not fetch data, talk to
/// managers, or know why it's being opened - that's the caller's job.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AnimatedPanel : MonoBehaviour
{
    public enum AnimationPreset
    {
        None,
        Fade,
        ScalePop,
        SlideFromTop,
        SlideFromBottom,
        SlideFromLeft,
        SlideFromRight,
    }

    [Header("Animation Presets")]
    [Tooltip("Animation played when Open() is called.")]
    [SerializeField] private AnimationPreset openPreset = AnimationPreset.ScalePop;
    [Tooltip("Animation played when Close() is called.")]
    [SerializeField] private AnimationPreset closePreset = AnimationPreset.Fade;

    [Header("Timing")]
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField] private float closeDuration = 0.18f;
    [SerializeField] private AnimationCurve openEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve closeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behaviour")]
    [Tooltip("If true, the panel is forced inactive on Awake so it always starts closed, " +
             "regardless of how it was left in the editor.")]
    [SerializeField] private bool startClosed = true;
    [Tooltip("Distance in UI units used by the slide presets.")]
    [SerializeField] private float slideDistance = 60f;

    [Header("Events")]
    public UnityEvent OnOpened;
    public UnityEvent OnClosed;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalAnchoredPos;
    private Vector3 originalScale;
    private Coroutine activeAnimation;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        originalAnchoredPos = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;

        if (startClosed)
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }
        else
        {
            IsOpen = true;
        }
    }

    /// <summary>Opens the panel, playing the configured open animation.</summary>
    public void Open()
    {
        if (activeAnimation != null) StopCoroutine(activeAnimation);

        // Full-screen panels must render above the GameMenu's persistent controls.
        // Without this, the base Back/Shop HUD can remain visible and clickable over
        // the shop or game-mode surface on Android.
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        ResetTransform();
        IsOpen = true;

        activeAnimation = StartCoroutine(PlayAnimation(openPreset, openDuration, openEase, true));
    }

    /// <summary>Closes the panel, playing the configured close animation, then deactivates it.</summary>
    public void Close()
    {
        if (!gameObject.activeInHierarchy)
        {
            IsOpen = false;
            return;
        }

        if (activeAnimation != null) StopCoroutine(activeAnimation);

        IsOpen = false;
        activeAnimation = StartCoroutine(PlayAnimation(closePreset, closeDuration, closeEase, false));
    }

    /// <summary>Toggles between Open() and Close(). Handy for a single button that does both.</summary>
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void ResetTransform()
    {
        rectTransform.anchoredPosition = originalAnchoredPos;
        rectTransform.localScale = originalScale;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator PlayAnimation(AnimationPreset preset, float duration, AnimationCurve ease, bool opening)
    {
        canvasGroup.blocksRaycasts = opening;
        canvasGroup.interactable = opening;

        Vector2 startPos = originalAnchoredPos;
        Vector2 endPos = originalAnchoredPos;
        Vector3 startScale = originalScale;
        Vector3 endScale = originalScale;
        float startAlpha = opening ? 0f : 1f;
        float endAlpha = opening ? 1f : 0f;

        switch (preset)
        {
            case AnimationPreset.Fade:
                break; // alpha only; position/scale stay put

            case AnimationPreset.ScalePop:
                if (opening) startScale = originalScale * 0.8f;
                else endScale = originalScale * 0.8f;
                break;

            case AnimationPreset.SlideFromTop:
                if (opening) startPos = originalAnchoredPos + new Vector2(0, slideDistance);
                else endPos = originalAnchoredPos + new Vector2(0, slideDistance);
                break;

            case AnimationPreset.SlideFromBottom:
                if (opening) startPos = originalAnchoredPos - new Vector2(0, slideDistance);
                else endPos = originalAnchoredPos - new Vector2(0, slideDistance);
                break;

            case AnimationPreset.SlideFromLeft:
                if (opening) startPos = originalAnchoredPos - new Vector2(slideDistance, 0);
                else endPos = originalAnchoredPos - new Vector2(slideDistance, 0);
                break;

            case AnimationPreset.SlideFromRight:
                if (opening) startPos = originalAnchoredPos + new Vector2(slideDistance, 0);
                else endPos = originalAnchoredPos + new Vector2(slideDistance, 0);
                break;

            case AnimationPreset.None:
            default:
                canvasGroup.alpha = 1f;
                rectTransform.anchoredPosition = originalAnchoredPos;
                rectTransform.localScale = originalScale;
                FinishAnimation(opening);
                yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = ease.Evaluate(normalized);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, eased);

            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        rectTransform.anchoredPosition = endPos;
        rectTransform.localScale = endScale;

        FinishAnimation(opening);
    }

    private void FinishAnimation(bool opening)
    {
        activeAnimation = null;

        if (opening)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            OnOpened?.Invoke();
        }
        else
        {
            gameObject.SetActive(false);
            ResetTransform();
            OnClosed?.Invoke();
        }
    }
}
