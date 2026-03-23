using System.Collections;
using TMPro;
using UnityEngine;

public class ProcessingBillIndicatorUI : MonoBehaviour
{
    public static ProcessingBillIndicatorUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text label;

    [Header("Slide")]
    [SerializeField] private float shownX = 0f;
    [SerializeField] private float hiddenX = 900f;
    [SerializeField] private float slideSpeed = 8f;

    [Header("Text Animation")]
    [SerializeField] private string baseText = "Processing Bill";
    [SerializeField] private float dotSpeed = 0.35f;
    [SerializeField] private int maxDots = 3;

    private Coroutine textRoutine;
    private bool isShowing;
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();

        SetupInitialPosition();
    }

    private void Update()
    {
        if (panelRect == null) return;

        float targetX = isShowing ? shownX : hiddenX;
        Vector2 pos = panelRect.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * slideSpeed);
        panelRect.anchoredPosition = pos;
    }

    private void SetupInitialPosition()
    {
        if (panelRect == null) return;

        Vector2 pos = panelRect.anchoredPosition;
        pos.x = hiddenX;
        panelRect.anchoredPosition = pos;

        if (label != null)
            label.text = baseText;

        initialized = true;
    }

    public void Show()
    {
        if (!initialized)
            SetupInitialPosition();

        isShowing = true;

        if (textRoutine != null)
            StopCoroutine(textRoutine);

        textRoutine = StartCoroutine(AnimateText());
    }

    public void Hide()
    {
        isShowing = false;

        if (textRoutine != null)
        {
            StopCoroutine(textRoutine);
            textRoutine = null;
        }

        if (label != null)
            label.text = baseText;
    }

    private IEnumerator AnimateText()
    {
        int dots = 0;

        while (true)
        {
            if (label != null)
                label.text = baseText + new string('.', dots);

            dots++;
            if (dots > maxDots)
                dots = 0;

            yield return new WaitForSeconds(dotSpeed);
        }
    }
}