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
    [SerializeField] private string defaultText = "Processing Bill";
    [SerializeField] private float dotSpeed = 0.35f;
    [SerializeField] private int maxDots = 3;

    private Coroutine textRoutine;
    private Coroutine autoHideRoutine;
    private bool isShowing;
    private bool initialized;
    private string currentText;

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

        currentText = defaultText;
        SetupInitialPosition();
    }

    private void Update()
    {
        if (panelRect == null) return;

        float targetX = isShowing ? shownX : hiddenX;
        Vector2 pos = panelRect.anchoredPosition;
        pos.x = Mathf.Lerp(
            pos.x,
            targetX,
            LevelOneUIAccessibility.ScaledAnimationDeltaTime * slideSpeed);
        panelRect.anchoredPosition = pos;
    }

    private void SetupInitialPosition()
    {
        if (panelRect == null) return;

        Vector2 pos = panelRect.anchoredPosition;
        pos.x = hiddenX;
        panelRect.anchoredPosition = pos;

        if (label != null)
            label.text = currentText;

        initialized = true;
    }

    public void Show()
    {
        Show(defaultText, 0f);
    }

    public void Show(string message)
    {
        Show(message, 0f);
    }

    public void ShowForSeconds(string message, float seconds)
    {
        Show(message, seconds);
    }

    private void Show(string message, float seconds)
    {
        if (!initialized)
            SetupInitialPosition();

        currentText = message;
        isShowing = true;

        if (textRoutine != null)
            StopCoroutine(textRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        textRoutine = StartCoroutine(AnimateText());

        if (seconds > 0f)
            autoHideRoutine = StartCoroutine(AutoHide(seconds));
    }

    public void Hide()
    {
        isShowing = false;

        if (textRoutine != null)
        {
            StopCoroutine(textRoutine);
            textRoutine = null;
        }

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        if (label != null)
            label.text = currentText;
    }

    private IEnumerator AnimateText()
    {
        int dots = 0;

        while (true)
        {
            if (label != null)
                label.text = currentText + new string('.', dots);

            dots++;
            if (dots > maxDots)
                dots = 0;

            yield return new WaitForSeconds(dotSpeed);
        }
    }

    private IEnumerator AutoHide(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
    }
}
