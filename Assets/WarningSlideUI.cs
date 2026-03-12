using System.Collections;
using TMPro;
using UnityEngine;

public class WarningSlideUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Slide")]
    [SerializeField] private Vector2 shownPosition = new Vector2(0f, 120f);
    [SerializeField] private Vector2 hiddenPosition = new Vector2(0f, -300f);
    [SerializeField] private float slideInDuration = 0.2f;
    [SerializeField] private float stayDuration = 1.8f;
    [SerializeField] private float slideOutDuration = 0.2f;

    private Coroutine showRoutine;

    private void Awake()
    {
        if (panel == null)
            panel = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);

        ApplyHiddenImmediate();
    }

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (messageText != null)
            messageText.text = message;

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return Animate(hiddenPosition, shownPosition, 0f, 1f, slideInDuration);
        yield return new WaitForSeconds(stayDuration);
        yield return Animate(shownPosition, hiddenPosition, 1f, 0f, slideOutDuration);
        showRoutine = null;
    }

    private IEnumerator Animate(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = Mathf.SmoothStep(0f, 1f, k);

            if (panel != null)
                panel.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, k);

            yield return null;
        }

        if (panel != null)
            panel.anchoredPosition = toPos;

        if (canvasGroup != null)
            canvasGroup.alpha = toAlpha;
    }

    private void ApplyHiddenImmediate()
    {
        if (panel != null)
            panel.anchoredPosition = hiddenPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}