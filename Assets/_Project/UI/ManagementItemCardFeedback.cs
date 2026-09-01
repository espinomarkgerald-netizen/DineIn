using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared, lightweight interaction polish for Management Computer item cards.
/// Mouse hover exposes extra information; touch users can tap or long-press.
/// </summary>
public sealed class ManagementItemCardFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler
{
    [Header("Authored Visuals")]
    [SerializeField] private Image highlight;
    [SerializeField] private Shadow shadow;
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private CanvasGroup tooltipGroup;
    [SerializeField] private TMP_Text tooltipText;

    [Header("Interaction")]
    [SerializeField] private bool tapShowsTooltip;
    [SerializeField, Min(0.15f)] private float hoverTooltipDelay = 0.32f;
    [SerializeField, Min(0.2f)] private float longPressSeconds = 0.42f;
    [SerializeField, Range(1f, 1.08f)] private float hoverScale = 1.018f;
    [SerializeField, Range(0.88f, 1f)] private float pressedScale = 0.97f;

    private Vector3 authoredScale = Vector3.one;
    private Coroutine visualRoutine;
    private Coroutine tooltipRoutine;
    private bool selected;
    private bool pointerInside;
    private bool pointerHeld;
    private bool longPressTriggered;
    private float pointerDownTime;
    private bool tooltipVisible;
    private string tooltipValue;

    private float RestingScale => selected ? 1.012f : 1f;
    private float RestingHighlight => selected ? 0.16f : pointerInside ? 0.09f : 0f;

    private void Awake()
    {
        authoredScale = transform.localScale;
        EnsureRuntimeTooltipGroup();
        SetTooltipVisibleImmediate(false);
        ApplyVisual(RestingScale, RestingHighlight);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (authoredScale == Vector3.zero)
            authoredScale = transform.localScale;
        EnsureRuntimeTooltipGroup();
        pointerHeld = false;
        pointerInside = false;
        longPressTriggered = false;
        SetTooltipVisibleImmediate(false);
        ApplyVisual(RestingScale, RestingHighlight);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        StopAllCoroutines();
        visualRoutine = null;
        tooltipRoutine = null;
        pointerHeld = false;
        pointerInside = false;
        tooltipVisible = false;
        transform.localScale = authoredScale;
    }

    private void Update()
    {
        if (!pointerHeld || longPressTriggered ||
            Time.unscaledTime - pointerDownTime < longPressSeconds)
            return;

        longPressTriggered = true;
        ShowTooltip();
        PlayValueFeedback(true);
    }

    public void SetTooltip(string heading, string details)
    {
        string cleanHeading = string.IsNullOrWhiteSpace(heading)
            ? "ITEM DETAILS"
            : heading.Trim();
        string cleanDetails = string.IsNullOrWhiteSpace(details)
            ? "No additional details."
            : details.Trim();
        tooltipValue = "<b>" + cleanHeading + "</b>\n" + cleanDetails;
        if (tooltipText != null)
            tooltipText.text = tooltipValue;
    }

    public void SetSelected(bool value)
    {
        if (selected == value)
        {
            AnimateVisual(RestingScale, RestingHighlight, 0.1f);
            return;
        }

        selected = value;
        if (selected)
            PlaySelectionFeedback();
        else
            AnimateVisual(RestingScale, RestingHighlight, 0.12f);
    }

    public void PlaySelectionFeedback()
    {
        StartVisualRoutine(PulseRoutine(1.045f, 0.24f, 0.18f, null));
    }

    public void PlayValueFeedback(bool positive)
    {
        StartVisualRoutine(PulseRoutine(
            positive ? 1.035f : 1.025f,
            positive ? 0.22f : 0.16f,
            0.14f,
            null));
    }

    public void PlaySuccessFeedback(UnityAction completion)
    {
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            completion?.Invoke();
            return;
        }

        StartVisualRoutine(PulseRoutine(1.055f, 0.28f, 0.16f, completion));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        AnimateVisual(hoverScale, Mathf.Max(RestingHighlight, 0.1f), 0.1f);
        if (eventData.pointerId < 0)
            StartTooltipDelay();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerHeld = false;
        CancelTooltipDelay();
        HideTooltip();
        AnimateVisual(RestingScale, selected ? 0.16f : 0f, 0.12f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        longPressTriggered = false;
        pointerDownTime = Time.unscaledTime;
        CancelTooltipDelay();
        AnimateVisual(pressedScale, 0.2f, 0.06f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;
        AnimateVisual(pointerInside ? hoverScale : RestingScale, RestingHighlight, 0.1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!tapShowsTooltip || longPressTriggered)
            return;

        if (eventData.pointerId >= 0)
        {
            if (tooltipVisible) HideTooltip();
            else ShowTooltip();
        }
    }

    private void StartTooltipDelay()
    {
        CancelTooltipDelay();
        tooltipRoutine = StartCoroutine(TooltipDelayRoutine());
    }

    private IEnumerator TooltipDelayRoutine()
    {
        float elapsed = 0f;
        while (elapsed < hoverTooltipDelay)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            yield return null;
        }

        tooltipRoutine = null;
        if (pointerInside)
            ShowTooltip();
    }

    private void CancelTooltipDelay()
    {
        if (tooltipRoutine == null)
            return;
        StopCoroutine(tooltipRoutine);
        tooltipRoutine = null;
    }

    private void ShowTooltip()
    {
        EnsureRuntimeTooltipGroup();
        if (tooltipRoot == null || tooltipGroup == null || string.IsNullOrWhiteSpace(tooltipValue))
            return;

        if (tooltipText != null)
            tooltipText.text = tooltipValue;
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        tooltipVisible = true;
        StartCoroutine(FadeTooltip(tooltipGroup.alpha, 1f, 0.1f, false));
    }

    private void HideTooltip()
    {
        if (!tooltipVisible || tooltipRoot == null || tooltipGroup == null)
            return;
        tooltipVisible = false;
        StartCoroutine(FadeTooltip(tooltipGroup.alpha, 0f, 0.08f, true));
    }

    private IEnumerator FadeTooltip(float from, float to, float duration, bool disableAfter)
    {
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            tooltipGroup.alpha = to;
            if (disableAfter)
                tooltipRoot.gameObject.SetActive(false);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            tooltipGroup.alpha = Mathf.Lerp(from, to,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        tooltipGroup.alpha = to;
        if (disableAfter && !tooltipVisible)
            tooltipRoot.gameObject.SetActive(false);
    }

    private void AnimateVisual(float scale, float highlightAlpha, float duration)
    {
        StartVisualRoutine(AnimateVisualRoutine(scale, highlightAlpha, duration));
    }

    private void StartVisualRoutine(IEnumerator routine)
    {
        if (!isActiveAndEnabled)
            return;
        if (visualRoutine != null)
            StopCoroutine(visualRoutine);
        visualRoutine = StartCoroutine(routine);
    }

    private IEnumerator PulseRoutine(
        float peakScale,
        float peakHighlight,
        float duration,
        UnityAction completion)
    {
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            ApplyVisual(RestingScale, RestingHighlight);
            completion?.Invoke();
            visualRoutine = null;
            yield break;
        }

        float firstDuration = duration * 0.44f;
        yield return AnimateVisualRoutine(peakScale, peakHighlight, firstDuration, false);
        completion?.Invoke();
        yield return AnimateVisualRoutine(RestingScale, RestingHighlight,
            Mathf.Max(0.04f, duration - firstDuration), false);
        visualRoutine = null;
    }

    private IEnumerator AnimateVisualRoutine(
        float targetScale,
        float targetHighlight,
        float duration,
        bool clearRoutine = true)
    {
        Vector3 startScale = transform.localScale;
        float startHighlight = highlight != null ? highlight.color.a : 0f;

        if (LevelOneUIAccessibility.ReducedMotion || duration <= 0f)
        {
            ApplyVisual(targetScale, targetHighlight);
            if (clearRoutine)
                visualRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.localScale = Vector3.LerpUnclamped(
                startScale,
                authoredScale * targetScale,
                t);
            SetHighlightAlpha(Mathf.Lerp(startHighlight, targetHighlight, t));
            yield return null;
        }

        ApplyVisual(targetScale, targetHighlight);
        if (clearRoutine)
            visualRoutine = null;
    }

    private void ApplyVisual(float scale, float highlightAlpha)
    {
        transform.localScale = authoredScale * scale;
        SetHighlightAlpha(highlightAlpha);
        if (shadow != null)
        {
            Color color = shadow.effectColor;
            color.a = Mathf.Lerp(0.12f, 0.3f, Mathf.Clamp01(highlightAlpha / 0.28f));
            shadow.effectColor = color;
        }
    }

    private void SetHighlightAlpha(float alpha)
    {
        if (highlight == null)
            return;
        Color color = highlight.color;
        color.a = Mathf.Clamp01(alpha);
        highlight.color = color;
    }

    private void SetTooltipVisibleImmediate(bool value)
    {
        tooltipVisible = value;
        if (tooltipGroup != null)
        {
            tooltipGroup.alpha = value ? 1f : 0f;
            tooltipGroup.interactable = false;
            tooltipGroup.blocksRaycasts = false;
        }
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(value);
    }

    private void EnsureRuntimeTooltipGroup()
    {
        if (tooltipGroup != null || tooltipRoot == null || !Application.isPlaying)
            return;
        tooltipGroup = tooltipRoot.GetComponent<CanvasGroup>();
        if (tooltipGroup == null)
            tooltipGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Image configuredHighlight,
        Shadow configuredShadow,
        RectTransform configuredTooltipRoot,
        CanvasGroup configuredTooltipGroup,
        TMP_Text configuredTooltipText,
        bool configuredTapShowsTooltip)
    {
        highlight = configuredHighlight;
        shadow = configuredShadow;
        tooltipRoot = configuredTooltipRoot;
        tooltipGroup = configuredTooltipGroup;
        tooltipText = configuredTooltipText;
        tapShowsTooltip = configuredTapShowsTooltip;
    }
#endif
}
