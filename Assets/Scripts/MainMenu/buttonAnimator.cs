using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonBounceAnimator : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Drag your buttons here (4 or more)")]
    public List<Button> buttons = new List<Button>();

    [Header("Highlight Scale")]
    public float highlightScale = 1.1f;
    public float scaleSpeed = 12f;

    [Header("Bounce Settings")]
    public float bounceScale = 0.9f;   // Squash amount
    public float bounceDuration = 0.12f;

    private Dictionary<Button, Vector3> originalScales = new Dictionary<Button, Vector3>();
    private Dictionary<Button, Coroutine> scaleCoroutines = new Dictionary<Button, Coroutine>();

    void Start()
    {
        foreach (Button button in buttons)
        {
            if (button == null) continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            originalScales[button] = rect.localScale;

            // Hover / Highlight
            AddEvent(button.gameObject, EventTriggerType.PointerEnter,
                () => StartScale(button, originalScales[button] * highlightScale));

            AddEvent(button.gameObject, EventTriggerType.PointerExit,
                () => StartScale(button, originalScales[button]));

            // Selected (keyboard / controller)
            AddEvent(button.gameObject, EventTriggerType.Select,
                () => StartScale(button, originalScales[button] * highlightScale));

            AddEvent(button.gameObject, EventTriggerType.Deselect,
                () => StartScale(button, originalScales[button]));

            // Pressed bounce
            AddEvent(button.gameObject, EventTriggerType.PointerDown,
                () => StartCoroutine(Bounce(rect, button)));

            button.onClick.AddListener(() =>
            {
                TryStartBounce(rect, button);
            });
        }
    }

    void TryStartBounce(RectTransform rect, Button button)
    {
        // If this object is disabled/inactive, Unity can't run coroutines here
        if (!isActiveAndEnabled || gameObject == null || !gameObject.activeInHierarchy)
            return;

        StartCoroutine(Bounce(rect, button));
    }

    void AddEvent(GameObject obj, EventTriggerType type, System.Action action)
    {
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = obj.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((_) => action.Invoke());
        trigger.triggers.Add(entry);
    }

    void StartScale(Button button, Vector3 target)
    {
        if (scaleCoroutines.ContainsKey(button) && scaleCoroutines[button] != null)
            StopCoroutine(scaleCoroutines[button]);

        scaleCoroutines[button] = StartCoroutine(ScaleTo(button.GetComponent<RectTransform>(), target));
    }

    IEnumerator ScaleTo(RectTransform rect, Vector3 target)
    {
        while (Vector3.Distance(rect.localScale, target) > 0.01f)
        {
            rect.localScale = Vector3.Lerp(rect.localScale, target, Time.deltaTime * scaleSpeed);
            yield return null;
        }
        rect.localScale = target;
    }

    IEnumerator Bounce(RectTransform rect, Button button)
    {
        Vector3 highlight = originalScales[button] * highlightScale;
        Vector3 pressed = highlight * 0.95f;   // subtle dip
        Vector3 overshoot = highlight * 1.03f; // gentle pop

        float t = 0f;

        // Press in
        while (t < 1f)
        {
            t += Time.deltaTime / 0.08f;
            rect.localScale = Vector3.Lerp(highlight, pressed, EaseOut(t));
            yield return null;
        }

        t = 0f;

        // Release + overshoot
        while (t < 1f)
        {
            t += Time.deltaTime / 0.1f;
            rect.localScale = Vector3.Lerp(pressed, overshoot, EaseOut(t));
            yield return null;
        }

        t = 0f;

        // Settle back to highlight
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            rect.localScale = Vector3.Lerp(overshoot, highlight, EaseInOut(t));
            yield return null;
        }

        rect.localScale = highlight;
    }

    float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }


}
