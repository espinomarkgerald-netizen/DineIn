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
    private int animationVersion;

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
        animationVersion++;
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
        animationVersion++;
        int version = animationVersion;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        gameObject.SetActive(true);
        raycastTarget = true;
        routine = StartCoroutine(AnimateRoutine(from, to, completed, version));
    }

    private IEnumerator AnimateRoutine(float from, float to, Action completed, int version)
    {
        float animationDuration = Mathf.Max(0.01f, duration);
        double startedAt = Time.realtimeSinceStartupAsDouble;
        while (version == animationVersion)
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            float normalized = Mathf.Clamp01((float)(elapsed / animationDuration));
            float t = easing != null && easing.length > 0
                ? Mathf.Clamp01(easing.Evaluate(normalized))
                : normalized;
            opening = Mathf.Lerp(from, to, t);
            SetVerticesDirty();

            if (normalized >= 1f)
                break;
            yield return null;
        }

        if (version != animationVersion)
            yield break;

        opening = to;
        SetVerticesDirty();
        routine = null;
        if (to >= 0.999f)
            raycastTarget = false;

        try
        {
            completed?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError("[RestockIris] Transition callback failed; releasing the screen blocker.");
            Debug.LogException(exception);
            ForceOpen();
        }

        // A completion callback may immediately start the opposite animation.
        // Do not let the finishing coroutine deactivate that new transition.
        if (version == animationVersion && to >= 0.999f)
            gameObject.SetActive(false);
    }
}
