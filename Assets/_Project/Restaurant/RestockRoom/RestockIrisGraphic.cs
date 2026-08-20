using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shader-free iris transition that draws the area outside a circular opening.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RestockIrisGraphic : MaskableGraphic
{
    [SerializeField, Range(24, 96)] private int segments = 64;
    [SerializeField, Min(0.05f)] private float duration = 0.35f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float opening = 1f;
    private Coroutine routine;

    public void Close(Action completed)
    {
        Animate(opening, 0f, completed);
    }

    public void Open(Action completed = null)
    {
        Animate(opening, 1f, completed);
    }

    public void ForceOpen()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        opening = 1f;
        raycastTarget = false;
        SetVerticesDirty();
        gameObject.SetActive(false);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height);
        float innerRadius = outerRadius * Mathf.Clamp01(opening);
        Vector2 center = rect.center;
        int count = Mathf.Clamp(segments, 24, 96);

        for (int i = 0; i <= count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + direction * innerRadius, color, Vector2.zero);
            vh.AddVert(center + direction * outerRadius, color, Vector2.zero);
        }

        for (int i = 0; i < count; i++)
        {
            int inner = i * 2;
            int outer = inner + 1;
            int nextInner = inner + 2;
            int nextOuter = inner + 3;
            vh.AddTriangle(inner, nextOuter, outer);
            vh.AddTriangle(inner, nextInner, nextOuter);
        }
    }

    private void Animate(float from, float to, Action completed)
    {
        if (routine != null)
            StopCoroutine(routine);
        gameObject.SetActive(true);
        raycastTarget = true;
        routine = StartCoroutine(AnimateRoutine(from, to, completed));
    }

    private IEnumerator AnimateRoutine(float from, float to, Action completed)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float t = easing != null && easing.length > 0
                ? Mathf.Clamp01(easing.Evaluate(normalized))
                : normalized;
            opening = Mathf.Lerp(from, to, t);
            SetVerticesDirty();
            yield return null;
        }

        opening = to;
        SetVerticesDirty();
        routine = null;
        if (to >= 0.999f)
        {
            raycastTarget = false;
            gameObject.SetActive(false);
        }
        completed?.Invoke();
    }
}
