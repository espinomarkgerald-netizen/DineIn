using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIElementAnimator : MonoBehaviour
{
    [Header("Highlight")]
    public float highlightScale = 1.08f;
    public float scaleSpeed = 12f;

    [Header("Bounce")]
    public float pressedScale = 0.95f;
    public float overshootScale = 1.03f;

    public float pressInTime = 0.08f;
    public float releaseTime = 0.1f;
    public float settleTime = 0.12f;

    [Header("Behavior")]
    public bool bounceOnPointerDown = true;
    public bool bounceOnClick = true;

    private RectTransform rect;
    private Vector3 originalScale;

    private Coroutine scaleRoutine;
    private Coroutine bounceRoutine;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalScale = rect.localScale;

        SetupEvents();
    }

    void SetupEvents()
    {
        EventTrigger trigger = GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = gameObject.AddComponent<EventTrigger>();

        AddEvent(trigger, EventTriggerType.PointerEnter, () => StartScale(originalScale * highlightScale));
        AddEvent(trigger, EventTriggerType.PointerExit, () => StartScale(originalScale));
        AddEvent(trigger, EventTriggerType.Select, () => StartScale(originalScale * highlightScale));
        AddEvent(trigger, EventTriggerType.Deselect, () => StartScale(originalScale));

        if (bounceOnPointerDown)
            AddEvent(trigger, EventTriggerType.PointerDown, StartBounce);

        if (bounceOnClick)
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(StartBounce);
        }
    }

    void AddEvent(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((_) => action?.Invoke());
        trigger.triggers.Add(entry);
    }

    void StartScale(Vector3 target)
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleTo(target));
    }

    IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(rect.localScale, target) > 0.01f)
        {
            rect.localScale = Vector3.Lerp(rect.localScale, target, Time.deltaTime * scaleSpeed);
            yield return null;
        }

        rect.localScale = target;
    }

    void StartBounce()
    {
        if (bounceRoutine != null)
            StopCoroutine(bounceRoutine);

        bounceRoutine = StartCoroutine(Bounce());
    }

    IEnumerator Bounce()
    {
        Vector3 highlight = originalScale * highlightScale;
        Vector3 pressed = highlight * pressedScale;
        Vector3 overshoot = highlight * overshootScale;

        yield return LerpScale(highlight, pressed, pressInTime);
        yield return LerpScale(pressed, overshoot, releaseTime);
        yield return LerpScale(overshoot, highlight, settleTime);

        rect.localScale = highlight;
    }

    IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        rect.localScale = to;
    }
}
