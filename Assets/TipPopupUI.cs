using System.Collections;
using TMPro;
using UnityEngine;

public class TipPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform animatedRoot;

    [Header("Animation")]
    [SerializeField] private float duration = 0.9f;
    [SerializeField] private float riseDistance = 55f;
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float peakScale = 1.08f;
    [SerializeField] private float endScale = 1f;

    private Coroutine playRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (animatedRoot == null)
            animatedRoot = transform as RectTransform;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void Show(int amount)
    {
        if (amountText != null)
            amountText.text = $"+₱{amount} TIP";

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (animatedRoot == null)
            animatedRoot = transform as RectTransform;

        Vector2 startPos = animatedRoot != null ? animatedRoot.anchoredPosition : Vector2.zero;
        Vector2 endPos = startPos + new Vector2(0f, riseDistance);

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = EaseOutCubic(p);

            if (animatedRoot != null)
            {
                animatedRoot.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);

                float scale;
                if (p < 0.2f)
                {
                    float first = p / 0.2f;
                    scale = Mathf.LerpUnclamped(startScale, peakScale, first);
                }
                else
                {
                    float second = (p - 0.2f) / 0.8f;
                    scale = Mathf.LerpUnclamped(peakScale, endScale, second);
                }

                animatedRoot.localScale = Vector3.one * scale;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - p;

            yield return null;
        }

        Destroy(gameObject);
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        t = 1f - Mathf.Pow(1f - t, 3f);
        return t;
    }
}