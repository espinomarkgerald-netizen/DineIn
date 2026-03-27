using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialHintTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private GameObject root;
    [SerializeField] private float typeSpeed = 0.02f;

    private Coroutine typingRoutine;
    private string currentMessage = string.Empty;
    private bool isTyping;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        HideImmediate();
    }

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Hide();
            return;
        }

        if (root != null && !root.activeSelf)
            root.SetActive(true);

        // Prevent restarting the same message over and over
        if (currentMessage == message)
            return;

        currentMessage = message;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        typingRoutine = StartCoroutine(TypeRoutine(message));
    }

    public void SetInstant(string message)
    {
        currentMessage = message;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;

        if (root != null)
            root.SetActive(!string.IsNullOrEmpty(message));

        if (targetText != null)
            targetText.text = message ?? string.Empty;
    }

    public void Hide()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        currentMessage = string.Empty;

        if (root != null)
            root.SetActive(false);
    }

    public void HideImmediate()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
        currentMessage = string.Empty;

        if (targetText != null)
            targetText.text = string.Empty;

        if (root != null)
            root.SetActive(false);
    }

    private IEnumerator TypeRoutine(string message)
    {
        if (targetText == null)
            yield break;

        isTyping = true;
        targetText.text = string.Empty;

        for (int i = 0; i < message.Length; i++)
        {
            targetText.text += message[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
        typingRoutine = null;
    }
}