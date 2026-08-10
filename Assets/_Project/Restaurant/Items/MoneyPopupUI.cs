using TMPro;
using UnityEngine;

public class MoneyPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private float risePixels = 80f;
    [SerializeField] private float duration = 0.9f;

    private RectTransform rt;
    private Vector2 startPos;
    private float t;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Init(int amount)
    {
        if (amountText != null) amountText.text = $"+{amount}";
        startPos = rt.anchoredPosition;
        t = 0f;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float a = Mathf.Clamp01(t / duration);

        rt.anchoredPosition = startPos + Vector2.up * (risePixels * a);
        if (canvasGroup != null) canvasGroup.alpha = 1f - a;

        if (t >= duration)
            Destroy(gameObject);
    }
}