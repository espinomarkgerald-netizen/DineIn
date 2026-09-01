using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight press feedback for authored UI buttons. The target and strength
/// remain editable, and the animation uses unscaled time so pause menus work.
/// </summary>
[DisallowMultipleComponent]
public sealed class UISubtlePressFeedback : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private RectTransform visualTarget;
    [SerializeField, Range(0.88f, 1f)] private float pressedScale = 0.96f;
    [SerializeField, Min(0f)] private float duration = 0.07f;

    private Vector3 restingScale = Vector3.one;
    private bool restingScaleCaptured;
    private Coroutine routine;

    private void Awake()
    {
        ResolveReferences();
        CaptureRestingScale();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureRestingScale();
        ApplyScale(restingScale);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        ApplyScale(restingScale);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable)
            return;
        AnimateTo(restingScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData) => AnimateTo(restingScale);

    public void OnPointerExit(PointerEventData eventData) => AnimateTo(restingScale);

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (visualTarget == null)
            visualTarget = transform as RectTransform;
    }

    private void CaptureRestingScale()
    {
        if (visualTarget == null || restingScaleCaptured)
            return;

        restingScale = visualTarget.localScale;
        restingScaleCaptured = true;
    }

    private void AnimateTo(Vector3 target)
    {
        if (visualTarget == null)
            return;
        if (LevelOneUIAccessibility.ReducedMotion || duration <= 0f || !isActiveAndEnabled)
        {
            ApplyScale(target);
            return;
        }

        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = visualTarget.localScale;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            ApplyScale(Vector3.LerpUnclamped(start, target, t));
            yield return null;
        }

        ApplyScale(target);
        routine = null;
    }

    private void ApplyScale(Vector3 scale)
    {
        if (visualTarget != null)
            visualTarget.localScale = scale;
    }
}
