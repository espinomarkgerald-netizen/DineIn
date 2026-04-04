using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyTaskUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text helperText;

    [Header("Text")]
    [SerializeField] private string title = "Current Task";
    [SerializeField] private bool hideHelperWhenEmpty = true;

    [Header("Animation")]
    [SerializeField] private Vector2 shownAnchoredPosition = new Vector2(0f, -20f);
    [SerializeField] private Vector2 hiddenAnchoredPosition = new Vector2(0f, 140f);
    [SerializeField] private float slideDuration = 0.22f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool keepRootActiveWhenHidden = true;

    [Header("Raycast")]
    [SerializeField] private bool disableAllGraphicRaycasts = true;

    private Coroutine transitionRoutine;
    private bool isVisible;
    private string currentTask = string.Empty;
    private string currentHelper = string.Empty;

    private void Awake()
    {
        if (root == null && panelRect != null)
            root = panelRect.gameObject;

        if (canvasGroup == null && panelRect != null)
            canvasGroup = panelRect.GetComponent<CanvasGroup>();

        ConfigureRaycastPassthrough();
        ApplyImmediate(!startHidden);
        RefreshTexts();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            ConfigureRaycastPassthrough();
    }

    public void ShowTask(string task, string helper = "")
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            HideTask();
            return;
        }

        currentTask = task;
        currentHelper = helper ?? string.Empty;

        RefreshTexts();

        if (!isVisible)
            StartTransition(true);
    }

    public void HideTask()
    {
        currentTask = string.Empty;
        currentHelper = string.Empty;
        RefreshTexts();

        if (isVisible)
            StartTransition(false);
        else
            ApplyImmediate(false);
    }

    public void RefreshTexts()
    {
        if (titleText != null)
            titleText.text = title;

        if (taskText != null)
            taskText.text = currentTask;

        if (helperText != null)
        {
            helperText.text = currentHelper;

            if (hideHelperWhenEmpty)
                helperText.gameObject.SetActive(!string.IsNullOrWhiteSpace(currentHelper));
        }
    }

    private void StartTransition(bool show)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(show));
    }

    private IEnumerator TransitionRoutine(bool show)
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (panelRect == null)
        {
            isVisible = show;

            if (!show && root != null && !keepRootActiveWhenHidden)
                root.SetActive(false);

            yield break;
        }

        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 endPos = show ? shownAnchoredPosition : hiddenAnchoredPosition;

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : (show ? 0f : 1f);
        float endAlpha = show ? 1f : 0f;

        float time = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);

        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = t * t * (3f - 2f * t);

            panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        panelRect.anchoredPosition = endPos;

        if (canvasGroup != null)
            canvasGroup.alpha = endAlpha;

        isVisible = show;

        if (!show && root != null && !keepRootActiveWhenHidden)
            root.SetActive(false);

        transitionRoutine = null;
    }

    private void ApplyImmediate(bool show)
    {
        isVisible = show;

        if (root != null)
            root.SetActive(show || keepRootActiveWhenHidden);

        if (panelRect != null)
            panelRect.anchoredPosition = show ? shownAnchoredPosition : hiddenAnchoredPosition;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void ConfigureRaycastPassthrough()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (!disableAllGraphicRaycasts || root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }
}