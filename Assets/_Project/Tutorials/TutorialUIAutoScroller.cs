using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Moves the nearest real ScrollRect until a tutorial target is visible.</summary>
[DisallowMultipleComponent]
public sealed class TutorialUIAutoScroller : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.28f;
    [SerializeField, Min(0f)] private float viewportPadding = 18f;
    private Coroutine routine;
    [SerializeField, Min(0f)] private float keyboardGap = 24f;
    [SerializeField, Range(.25f, .7f)] private float unknownKeyboardHeightFraction = .5f;
    private TMP_InputField keyboardField;
    private RectTransform keyboardPanel;
    private Vector2 keyboardPanelHome;
    private bool originalHideMobileInput, originalHideKeyboard;
    private readonly Vector3[] keyboardCorners = new Vector3[4];

    public void ConfigureKeyboard(float gap, float fallbackHeight)
    {
        keyboardGap = Mathf.Max(0f, gap);
        unknownKeyboardHeightFraction = Mathf.Clamp(fallbackHeight, .25f, .7f);
    }

    public void TrackKeyboard(TMP_InputField field)
    {
        RestoreKeyboardLayout();
        if (!Application.isMobilePlatform || field == null) return;
        keyboardField = field;
        originalHideMobileInput = field.shouldHideMobileInput;
        originalHideKeyboard = field.shouldHideSoftKeyboard;
        field.shouldHideMobileInput = false;
        field.shouldHideSoftKeyboard = false;
        // Only move the tutorial's active menu content, preserving its context.
        keyboardPanel = field.GetComponentInParent<ManagementComputerCatalogPanelUI>()?.transform as RectTransform;
        if (keyboardPanel != null) keyboardPanelHome = keyboardPanel.anchoredPosition;
    }

    private void LateUpdate()
    {
        if (keyboardField == null || keyboardPanel == null) return;
        if (!keyboardField.isFocused || !TouchScreenKeyboard.visible)
        { keyboardPanel.anchoredPosition = keyboardPanelHome; return; }
        Canvas owner = keyboardField.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera camera = owner == null || owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
        keyboardPanel.anchoredPosition = keyboardPanelHome;
        keyboardField.GetComponent<RectTransform>().GetWorldCorners(keyboardCorners);
        float bottom = RectTransformUtility.WorldToScreenPoint(camera, keyboardCorners[0]).y;
        float keyboardTop = TouchScreenKeyboard.area.height > 0f
            ? TouchScreenKeyboard.area.yMax : Screen.height * unknownKeyboardHeightFraction;
        float shift = Mathf.Max(0f, keyboardTop + keyboardGap - bottom);
        var parent = keyboardPanel.parent as RectTransform;
        if (parent == null || shift <= 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out Vector2 from);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.up * shift, camera, out Vector2 to);
        keyboardPanel.anchoredPosition = keyboardPanelHome + to - from;
    }

    private void RestoreKeyboardLayout()
    {
        if (keyboardPanel != null) keyboardPanel.anchoredPosition = keyboardPanelHome;
        if (keyboardField != null)
        {
            keyboardField.shouldHideMobileInput = originalHideMobileInput;
            keyboardField.shouldHideSoftKeyboard = originalHideKeyboard;
        }
        keyboardField = null;
        keyboardPanel = null;
    }

    public void Prepare(RectTransform target, Action onReady)
    {
        Cancel();
        routine = StartCoroutine(PrepareRoutine(target, onReady));
    }

    public void Cancel()
    {
        RestoreKeyboardLayout();
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator PrepareRoutine(RectTransform target, Action onReady)
    {
        yield return null; // allow runtime-created cards and Destroy calls to settle
        ForceLayout(target);
        List<ScrollRect> scrolls = FindAncestorScrolls(target);
        // Resolve the outer page first, then any nested horizontal card rail.
        // This keeps the mask on its previous stable target until the whole card is visible.
        for (int i = scrolls.Count - 1; i >= 0; i--)
        {
            ScrollRect scroll = scrolls[i];
            if (scroll == null || scroll.content == null)
                continue;

            RectTransform viewport = scroll.viewport != null
                ? scroll.viewport : scroll.transform as RectTransform;
            if (viewport != null)
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
                Rect view = viewport.rect;
                Vector2 delta = Vector2.zero;
                if (bounds.min.x < view.xMin + viewportPadding)
                    delta.x = view.xMin + viewportPadding - bounds.min.x;
                else if (bounds.max.x > view.xMax - viewportPadding)
                    delta.x = view.xMax - viewportPadding - bounds.max.x;
                if (bounds.min.y < view.yMin + viewportPadding)
                    delta.y = view.yMin + viewportPadding - bounds.min.y;
                else if (bounds.max.y > view.yMax - viewportPadding)
                    delta.y = view.yMax - viewportPadding - bounds.max.y;

                if (delta.sqrMagnitude > 0.25f)
                {
                    scroll.StopMovement();
                    Vector2 start = scroll.content.anchoredPosition;
                    Vector2 destination = start + delta;
                    if (!scroll.horizontal) destination.x = start.x;
                    if (!scroll.vertical) destination.y = start.y;
                    for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, duration));
                        scroll.content.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
                        yield return null;
                    }
                    scroll.content.anchoredPosition = destination;
                    ForceLayout(target);
                }
            }
        }

        ForceLayout(target);
        routine = null;
        onReady?.Invoke();
    }

    public static void ForceLayout(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();
        for (RectTransform current = target; current != null; current = current.parent as RectTransform)
            LayoutRebuilder.ForceRebuildLayoutImmediate(current);
        Canvas.ForceUpdateCanvases();
    }

    private static List<ScrollRect> FindAncestorScrolls(RectTransform target)
    {
        List<ScrollRect> result = new List<ScrollRect>();
        for (Transform current = target; current != null; current = current.parent)
        {
            ScrollRect scroll = current.GetComponent<ScrollRect>();
            if (scroll != null && scroll.gameObject.activeInHierarchy)
                result.Add(scroll);
        }
        return result;
    }

    private void OnDisable() => Cancel();
}
