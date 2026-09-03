using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialDialogueUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button nextButton;

    [Header("Typing")]
    [SerializeField] private float typeSpeed = 0.02f;
    [SerializeField] private float portraitFadeDuration = 0.08f;

    private Coroutine typingRoutine;
    private Coroutine autoHideRoutine;
    private Coroutine portraitRoutine;
    private Action manualNextAction;

    private string currentFullMessage = string.Empty;
    private bool isTyping;

    public bool IsVisible => root != null ? root.activeSelf : gameObject.activeSelf;
    public string Speaker => speakerText != null ? speakerText.text : string.Empty;
    public string Message => bodyText != null ? bodyText.text : string.Empty;
    public Sprite Portrait => portraitImage != null ? portraitImage.sprite : null;
    public bool IsManualAdvanceVisible => nextButton != null && nextButton.gameObject.activeSelf;

    private void Awake()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextPressed);
        }

        HideImmediate();
    }

    public void ShowManual(string speaker, string message, Action onNext)
    {
        ShowManual(speaker, message, null, onNext);
    }

    public void ShowManual(string speaker, string message, Sprite portrait, Action onNext)
    {
        manualNextAction = onNext;
        ShowInternal(speaker, message, portrait, true);
    }

    public void ShowAuto(string speaker, string message, float duration)
    {
        ShowAuto(speaker, message, null, duration);
    }

    public void ShowAuto(string speaker, string message, Sprite portrait, float duration)
    {
        manualNextAction = null;
        ShowInternal(speaker, message, portrait, false);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        autoHideRoutine = StartCoroutine(AutoHideRoutine(duration));
    }

    public void ShowWaiting(string speaker, string message, Sprite portrait)
    {
        manualNextAction = null;
        ShowInternal(speaker, message, portrait, false);
    }

    public void ShowDialogue(string speaker, string message, Sprite portrait, Action onNext = null)
    {
        if (onNext != null)
            ShowManual(speaker, message, portrait, onNext);
        else
            ShowWaiting(speaker, message, portrait);
    }

    public void HideDialogue() => Hide();

    public void SetSpeaker(string speaker)
    {
        if (speakerText != null)
            speakerText.text = speaker ?? string.Empty;
    }

    public void SetMessage(string message)
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        currentFullMessage = message ?? string.Empty;

        if (bodyText != null)
            bodyText.text = currentFullMessage;
    }

    public void SetPortrait(Sprite portrait)
    {
        if (portraitImage == null || portrait == null || portraitImage.sprite == portrait)
            return;

        if (portraitRoutine != null)
            StopCoroutine(portraitRoutine);

        if (!Application.isPlaying || !isActiveAndEnabled || portraitFadeDuration <= 0f)
        {
            portraitImage.sprite = portrait;
            SetPortraitAlpha(1f);
            return;
        }

        portraitRoutine = StartCoroutine(SwapPortraitRoutine(portrait));
    }

    public void Hide()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        if (portraitRoutine != null)
            StopCoroutine(portraitRoutine);

        isTyping = false;
        manualNextAction = null;
        typingRoutine = null;
        autoHideRoutine = null;
        portraitRoutine = null;
        SetPortraitAlpha(1f);

        if (root != null)
            root.SetActive(false);
    }

    public void HideImmediate()
    {
        isTyping = false;
        manualNextAction = null;

        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        if (portraitRoutine != null)
            StopCoroutine(portraitRoutine);

        typingRoutine = null;
        autoHideRoutine = null;
        portraitRoutine = null;
        SetPortraitAlpha(1f);

        if (bodyText != null)
            bodyText.text = string.Empty;

        if (root != null)
            root.SetActive(false);
    }

    private void ShowInternal(string speaker, string message, Sprite portrait, bool manualMode)
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        if (root != null)
            root.SetActive(true);

        SetSpeaker(speaker);
        SetPortrait(portrait);

        currentFullMessage = message ?? string.Empty;

        if (nextButton != null)
            nextButton.gameObject.SetActive(manualMode);

        if (!Application.isPlaying || typeSpeed <= 0f)
            SetMessage(currentFullMessage);
        else
            typingRoutine = StartCoroutine(TypeRoutine(currentFullMessage));
    }

    private IEnumerator TypeRoutine(string message)
    {
        isTyping = true;

        if (bodyText != null)
            bodyText.text = string.Empty;

        for (int i = 0; i < message.Length; i++)
        {
            if (bodyText != null)
                bodyText.text += message[i];

            if (typeSpeed > 0f)
                yield return new WaitForSecondsRealtime(typeSpeed);
        }

        isTyping = false;
        typingRoutine = null;
    }

    private IEnumerator AutoHideRoutine(float duration)
    {
        while (isTyping)
            yield return null;

        yield return new WaitForSecondsRealtime(duration);
        Hide();
        autoHideRoutine = null;
    }

    private void OnNextPressed()
    {
        if (isTyping)
        {
            if (typingRoutine != null)
                StopCoroutine(typingRoutine);

            isTyping = false;
            typingRoutine = null;

            if (bodyText != null)
                bodyText.text = currentFullMessage;

            return;
        }

        Action callback = manualNextAction;
        manualNextAction = null;
        callback?.Invoke();
    }

    private IEnumerator SwapPortraitRoutine(Sprite portrait)
    {
        float halfDuration = Mathf.Max(0.01f, portraitFadeDuration * 0.5f);
        Color original = portraitImage.color;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
        {
            SetPortraitAlpha(1f - Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        portraitImage.sprite = portrait;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
        {
            SetPortraitAlpha(Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        portraitImage.color = new Color(original.r, original.g, original.b, 1f);
        portraitRoutine = null;
    }

    private void SetPortraitAlpha(float alpha)
    {
        if (portraitImage == null)
            return;

        Color color = portraitImage.color;
        color.a = alpha;
        portraitImage.color = color;
    }
}
