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

    [Header("Big Boss Portrait Reaction")]
    [SerializeField, Min(0f)] private float portraitBopDuration = 0.22f;
    [SerializeField, Range(0.8f, 1f)] private float portraitBopStartScale = 0.94f;
    [SerializeField, Range(1f, 1.2f)] private float portraitBopPeakScale = 1.06f;
    [SerializeField, Min(0f)] private float portraitBopLift = 5f;

    private Coroutine typingRoutine;
    private Coroutine autoHideRoutine;
    private Coroutine portraitRoutine;
    private Action manualNextAction;
    private string currentFullMessage = string.Empty;
    private bool isTyping;
    private RectTransform portraitRect;
    private Vector2 portraitHome;
    private Vector3 portraitHomeScale = Vector3.one;
    private readonly Vector3[] corners = new Vector3[4];

    public bool IsVisible => root != null ? root.activeSelf : gameObject.activeSelf;
    public string Speaker => speakerText != null ? speakerText.text : string.Empty;
    public string Message => bodyText != null ? bodyText.text : string.Empty;
    public Sprite Portrait => portraitImage != null ? portraitImage.sprite : null;
    public bool IsManualAdvanceVisible => nextButton != null && nextButton.gameObject.activeSelf;

    private void Awake()
    {
        // The dialogue panel, nameplate, and text always stay at their authored pose.
        // Only the Big Boss portrait reacts when its sprite actually changes.
        portraitRect = portraitImage != null ? portraitImage.rectTransform : null;
        if (portraitRect != null)
        {
            portraitHome = portraitRect.anchoredPosition;
            portraitHomeScale = portraitRect.localScale;
        }

        if (FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include) != null)
        {
            Canvas layer = GetComponent<Canvas>();
            if (layer == null) layer = gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 32761;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (bodyText != null && nextButton != null)
            {
                RectTransform body = bodyText.rectTransform;
                ((RectTransform)nextButton.transform).GetWorldCorners(corners);
                float top = body.parent.InverseTransformPoint(corners[0]).y - 4f;
                float bottom = body.localPosition.y + body.rect.yMin;
                if (top > bottom && top < body.localPosition.y + body.rect.yMax)
                {
                    body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top - bottom);
                    Vector3 position = body.localPosition;
                    position.y = bottom + (top - bottom) * body.pivot.y;
                    body.localPosition = position;
                }
            }
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextPressed);
        }
        HideImmediate();
    }

    // Retained for the existing TutorialSystem API. Dialogue placement is authored and fixed.
    public void SetFocusTarget(RectTransform target) { }

    public void ShowManual(string speaker, string message, Action onNext) =>
        ShowManual(speaker, message, null, onNext);

    public void ShowManual(string speaker, string message, Sprite portrait, Action onNext)
    {
        manualNextAction = onNext;
        ShowInternal(speaker, message, portrait, true);
    }

    public void ShowAuto(string speaker, string message, float duration) =>
        ShowAuto(speaker, message, null, duration);

    public void ShowAuto(string speaker, string message, Sprite portrait, float duration)
    {
        manualNextAction = null;
        ShowInternal(speaker, message, portrait, false);
        if (autoHideRoutine != null) StopCoroutine(autoHideRoutine);
        autoHideRoutine = StartCoroutine(AutoHideRoutine(duration));
    }

    public void ShowWaiting(string speaker, string message, Sprite portrait)
    {
        manualNextAction = null;
        ShowInternal(speaker, message, portrait, false);
    }

    public void ShowDialogue(string speaker, string message, Sprite portrait, Action onNext = null)
    {
        if (onNext != null) ShowManual(speaker, message, portrait, onNext);
        else ShowWaiting(speaker, message, portrait);
    }

    public void HideDialogue() => Hide();

    public void HideDialogueAnimated(Action onHidden)
    {
        // Kept for TutorialSystem compatibility. The panel itself never slides,
        // scales, or bops; it disappears before the PlayerAction starts.
        Hide();
        onHidden?.Invoke();
    }

    public void SetSpeaker(string speaker)
    {
        if (speakerText != null) speakerText.text = speaker ?? string.Empty;
    }

    public void SetMessage(string message)
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        isTyping = false;
        currentFullMessage = message ?? string.Empty;
        if (bodyText != null) bodyText.text = currentFullMessage;
    }

    public void SetPortrait(Sprite portrait)
    {
        if (portraitImage == null || portrait == null || portraitImage.sprite == portrait) return;
        if (portraitRoutine != null) StopCoroutine(portraitRoutine);
        ResetPortraitVisuals();
        portraitImage.sprite = portrait;
        if (!Application.isPlaying || !isActiveAndEnabled || portraitBopDuration <= 0f ||
            LevelOneUIAccessibility.ReducedMotion)
        {
            SetPortraitAlpha(1f);
            return;
        }
        portraitRoutine = StartCoroutine(BopPortraitRoutine());
    }

    public void Hide()
    {
        StopAllPresentationRoutines();
        manualNextAction = null;
        isTyping = false;
        ResetPresentationVisuals();
        if (root != null) root.SetActive(false);
    }

    public void HideImmediate()
    {
        StopAllPresentationRoutines();
        manualNextAction = null;
        isTyping = false;
        ResetPresentationVisuals();
        if (bodyText != null) bodyText.text = string.Empty;
        if (root != null) root.SetActive(false);
    }

    private void ShowInternal(string speaker, string message, Sprite portrait, bool manualMode)
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        if (autoHideRoutine != null) StopCoroutine(autoHideRoutine);
        typingRoutine = autoHideRoutine = null;
        if (root != null) root.SetActive(true);
        SetSpeaker(speaker);
        SetPortrait(portrait);
        currentFullMessage = message ?? string.Empty;
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(manualMode);
            nextButton.interactable = true;
        }
        if (!Application.isPlaying || typeSpeed <= 0f) SetMessage(currentFullMessage);
        else typingRoutine = StartCoroutine(TypeRoutine(currentFullMessage));
    }

    private IEnumerator TypeRoutine(string message)
    {
        isTyping = true;
        if (bodyText != null) bodyText.text = string.Empty;
        for (int i = 0; i < message.Length; i++)
        {
            if (bodyText != null) bodyText.text += message[i];
            if (typeSpeed > 0f) yield return new WaitForSecondsRealtime(typeSpeed);
        }
        isTyping = false;
        typingRoutine = null;
    }

    private IEnumerator AutoHideRoutine(float duration)
    {
        while (isTyping) yield return null;
        yield return new WaitForSecondsRealtime(duration);
        Hide();
    }

    private void OnNextPressed()
    {
        if (isTyping)
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;
            typingRoutine = null;
            if (bodyText != null) bodyText.text = currentFullMessage;
            return;
        }
        Action callback = manualNextAction;
        manualNextAction = null;
        if (nextButton != null) nextButton.interactable = false;
        callback?.Invoke();
    }

    private IEnumerator BopPortraitRoutine()
    {
        if (portraitRect == null)
        {
            portraitRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.01f, portraitBopDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.55f
                ? Mathf.Lerp(portraitBopStartScale, portraitBopPeakScale,
                    Mathf.SmoothStep(0f, 1f, t / 0.55f))
                : Mathf.Lerp(portraitBopPeakScale, 1f,
                    Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
            float lift = Mathf.Sin(t * Mathf.PI) * portraitBopLift;
            portraitRect.localScale = Vector3.Scale(portraitHomeScale, Vector3.one * scale);
            portraitRect.anchoredPosition = portraitHome + Vector2.up * lift;
            yield return null;
        }
        ResetPortraitVisuals();
        portraitRoutine = null;
    }

    private void StopAllPresentationRoutines()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        if (autoHideRoutine != null) StopCoroutine(autoHideRoutine);
        if (portraitRoutine != null) StopCoroutine(portraitRoutine);
        typingRoutine = autoHideRoutine = portraitRoutine = null;
        ResetPortraitVisuals();
        SetPortraitAlpha(1f);
    }

    private void ResetPresentationVisuals()
    {
        ResetPortraitVisuals();
        if (nextButton != null) nextButton.interactable = true;
    }

    private void ResetPortraitVisuals()
    {
        if (portraitRect == null) return;
        portraitRect.anchoredPosition = portraitHome;
        portraitRect.localScale = portraitHomeScale;
    }

    private void SetPortraitAlpha(float alpha)
    {
        if (portraitImage == null) return;
        Color color = portraitImage.color;
        color.a = alpha;
        portraitImage.color = color;
    }
}
