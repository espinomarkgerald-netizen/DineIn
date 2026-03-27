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
    [SerializeField] private Button nextButton;

    [Header("Typing")]
    [SerializeField] private float typeSpeed = 0.02f;

    private Coroutine typingRoutine;
    private Coroutine autoHideRoutine;
    private Action manualNextAction;

    private string currentFullMessage = string.Empty;
    private bool isTyping;

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
        manualNextAction = onNext;
        ShowInternal(speaker, message, true);
    }

    public void ShowAuto(string speaker, string message, float duration)
    {
        manualNextAction = null;
        ShowInternal(speaker, message, false);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        autoHideRoutine = StartCoroutine(AutoHideRoutine(duration));
    }

    public void Hide()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        isTyping = false;
        manualNextAction = null;

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

        if (bodyText != null)
            bodyText.text = string.Empty;

        if (root != null)
            root.SetActive(false);
    }

    private void ShowInternal(string speaker, string message, bool manualMode)
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        if (root != null)
            root.SetActive(true);

        if (speakerText != null)
            speakerText.text = speaker;

        currentFullMessage = message ?? string.Empty;

        if (nextButton != null)
            nextButton.gameObject.SetActive(manualMode);

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

            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
        typingRoutine = null;
    }

    private IEnumerator AutoHideRoutine(float duration)
    {
        while (isTyping)
            yield return null;

        yield return new WaitForSeconds(duration);
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
}