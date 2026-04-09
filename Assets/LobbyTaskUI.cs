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
    [SerializeField] private bool forceTopCenterAnchor = true;
    [SerializeField] private bool bringToFrontOnShow = true;

    [Header("Raycast")]
    [SerializeField] private bool disableAllGraphicRaycasts = true;

    private Coroutine transitionRoutine;
    private bool isVisible;
    private string currentTask = string.Empty;
    private string currentHelper = string.Empty;

    private bool UseSingleTextMode =>
        titleText == null &&
        taskText == null &&
        helperText != null;

    private void Reset()
    {
        AutoAssignReferences(false);
    }

    private void Awake()
    {
        AutoAssignReferences(true);
        EnsureAnchorSetup();
        ConfigureRaycastPassthrough();
        ApplyImmediate(!startHidden);
        RefreshTexts();
    }

    private void OnValidate()
    {
        AutoAssignReferences(false);

        if (!Application.isPlaying)
        {
            EnsureAnchorSetup();
            ConfigureRaycastPassthrough();
        }
    }

    public void ShowTask(string task, string helper = "")
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            HideTask();
            return;
        }

        currentTask = task.Trim();
        currentHelper = helper == null ? string.Empty : helper.Trim();

        RefreshTexts();

        if (bringToFrontOnShow && panelRect != null)
            panelRect.SetAsLastSibling();

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
        if (UseSingleTextMode)
        {
            string combined = currentTask;

            if (!string.IsNullOrWhiteSpace(currentHelper))
                combined = string.IsNullOrWhiteSpace(combined)
                    ? currentHelper
                    : currentTask + "\n" + currentHelper;

            helperText.text = combined;
            helperText.gameObject.SetActive(!string.IsNullOrWhiteSpace(combined));
            return;
        }

        if (titleText != null)
            titleText.text = title;

        if (taskText != null)
            taskText.text = currentTask;

        if (helperText != null)
        {
            helperText.text = currentHelper;

            if (hideHelperWhenEmpty)
                helperText.gameObject.SetActive(!string.IsNullOrWhiteSpace(currentHelper));
            else
                helperText.gameObject.SetActive(true);
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

            transitionRoutine = null;
            yield break;
        }

        if (bringToFrontOnShow && show)
            panelRect.SetAsLastSibling();

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
        {
            panelRect.anchoredPosition = show ? shownAnchoredPosition : hiddenAnchoredPosition;
            panelRect.localScale = Vector3.one;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void AutoAssignReferences(bool allowAddCanvasGroup)
    {
        if (root == null)
            root = gameObject;

        if (panelRect == null)
        {
            if (transform.childCount > 0)
                panelRect = transform.GetChild(0).GetComponent<RectTransform>();
            else
                panelRect = GetComponent<RectTransform>();
        }

        if (panelRect != null && canvasGroup == null)
        {
            canvasGroup = panelRect.GetComponent<CanvasGroup>();

            if (canvasGroup == null && allowAddCanvasGroup)
                canvasGroup = panelRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (panelRect == null)
            return;

        TMP_Text[] texts = panelRect.GetComponentsInChildren<TMP_Text>(true);

        if (titleText == null)
            titleText = FindTextByName(texts, "title");

        if (taskText == null)
            taskText = FindTextByName(texts, "task");

        if (helperText == null)
            helperText = FindTextByName(texts, "helper");

        if (taskText == null && helperText == null && texts.Length == 1)
            taskText = texts[0];
    }

    private TMP_Text FindTextByName(TMP_Text[] texts, string contains)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name.ToLower().Contains(contains))
                return texts[i];
        }

        return null;
    }

    private void EnsureAnchorSetup()
    {
        if (!forceTopCenterAnchor || panelRect == null)
            return;

        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
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