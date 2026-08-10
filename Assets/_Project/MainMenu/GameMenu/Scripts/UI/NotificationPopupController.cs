using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Universal sliding notification/status popup. Any script in the project
/// can call NotificationPopupController.Instance.Show(message, type) to
/// surface a warning/info/error/success message, without knowing anything
/// about how it is displayed or animated.
///
/// Single Responsibility: this class only shows/animates/queues messages.
/// It has zero knowledge of PlayFab, wallets, auth, or any other gameplay
/// system - callers just report text + severity here, the same way UI
/// scripts report through SettingsManager/PlayFabWalletManager rather than
/// touching PlayFab directly.
///
/// Setup:
/// - Put this on the popup's root UI GameObject (the panel that slides
///   on/off screen), which needs a RectTransform.
/// - Assign panelRect (usually the same RectTransform this is on) and
///   messageText. canvasGroup and backgroundImage are optional but enable
///   fade + color-by-type.
/// - Position the panel in the Scene/Game view exactly where it should
///   rest when visible - that resting position is captured automatically
///   at startup and used as the "shown" position for every animation, so
///   no position math needs to happen by hand.
///
/// Usage from any other script:
///
///   NotificationPopupController.Instance?.Show("Not enough Gold Coins.", NotificationPopupController.PopupType.Warning);
///   NotificationPopupController.Instance?.Show("Purchase complete!", NotificationPopupController.PopupType.Success);
///   NotificationPopupController.Instance?.ShowPersistent("Connecting...", NotificationPopupController.PopupType.Info);
///   NotificationPopupController.Instance?.Hide();
/// </summary>
public class NotificationPopupController : MonoBehaviour
{
    public static NotificationPopupController Instance { get; private set; }

    public enum PopupType { Info, Warning, Error, Success }
    public enum PopupSlideDirection { FromTop, FromBottom, FromLeft, FromRight, FadeOnly }

    [Header("Core References")]
    [Tooltip("The RectTransform that slides on/off screen. If left empty, GetComponent<RectTransform>() is used.")]
    [SerializeField] private RectTransform panelRect;
    [Tooltip("Optional. Enables smooth fade in/out alongside the slide.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Optional. Tinted per message type if tintBackgroundByType is true.")]
    [SerializeField] private Image backgroundImage;

    [Header("Animation Preset")]
    [SerializeField] private PopupSlideDirection defaultDirection = PopupSlideDirection.FromTop;
    [Tooltip("How far beyond the panel's own size to push it off-screen, so no edge peeks into view while hidden.")]
    [SerializeField] private float offscreenPadding = 40f;
    [SerializeField] private float slideInDuration = 0.35f;
    [SerializeField] private float slideOutDuration = 0.30f;
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("If true (and canvasGroup is assigned), alpha fades alongside the slide for a smoother feel. If false, only position animates.")]
    [SerializeField] private bool fadeAlongsideSlide = true;

    [Header("Timing")]
    [SerializeField] private float defaultDisplayDuration = 2.5f;
    [Tooltip("If true, calls to Show()/ShowPersistent() queue up and play one after another. If false, a new call immediately interrupts whatever is currently showing (good for fast-changing status text).")]
    [SerializeField] private bool queueMessages = false;
    [Tooltip("Extra pause between queued messages. Only used when queueMessages is true.")]
    [SerializeField] private float gapBetweenQueuedMessages = 0.15f;

    [Header("Colors By Type (optional)")]
    [SerializeField] private bool tintBackgroundByType = true;
    [SerializeField] private bool tintTextByType = false;
    [SerializeField] private Color infoColor = new Color(0.20f, 0.55f, 0.95f);
    [SerializeField] private Color warningColor = new Color(0.95f, 0.65f, 0.10f);
    [SerializeField] private Color errorColor = new Color(0.85f, 0.20f, 0.20f);
    [SerializeField] private Color successColor = new Color(0.20f, 0.75f, 0.35f);

    [Header("Close Button")]
    [Tooltip("Optional. If assigned, clicking this immediately dismisses the popup (faster than the normal auto-hide slide-out) instead of waiting for displayDuration to elapse.")]
    [SerializeField] private Button closeButton;
    [Tooltip("Optional. The Image tinted to match the current popup type when tintCloseButtonByType is true. If left empty, GetComponent<Image>() on closeButton is used.")]
    [SerializeField] private Image closeButtonImage;
    [SerializeField] private bool tintCloseButtonByType = true;
    [Tooltip("How long the slide-out takes when the close button is clicked, instead of the normal slideOutDuration - lower for a snappier manual dismiss.")]
    [SerializeField] private float fastCloseDuration = 0.15f;

    [Header("Close Button Click Animation")]
    [Tooltip("Scale the close button shrinks to on click, as a fraction of its normal size (e.g. 0.85 = shrinks to 85%).")]
    [SerializeField] private float closeButtonPunchScale = 0.85f;
    [SerializeField] private float closeButtonPunchDuration = 0.12f;
    [SerializeField] private AnimationCurve closeButtonPunchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Persistence")]
    [Tooltip("If true, this popup survives scene loads (root-detach + DontDestroyOnLoad, same fix used on PlayFabAuthManager), so one popup instance can be reused across the whole project instead of one per scene.")]
    [SerializeField] private bool persistAcrossScenes = false;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = false;

    private struct PopupRequest
    {
        public string message;
        public PopupType type;
        public PopupSlideDirection direction;
        public float displayDuration;
        public bool persistent;
    }

    private readonly Queue<PopupRequest> pendingQueue = new Queue<PopupRequest>();
    private Coroutine activeRoutine;
    private bool queueRunning;
    private bool isShowing;
    private bool hideRequested;
    private bool fastCloseRequested;
    private Vector2 shownAnchoredPosition;
    private Vector3 closeButtonBaseScale = Vector3.one;
    private Coroutine closeButtonPunchRoutine;

    /// <summary>True while a message is currently visible or animating.</summary>
    public bool IsShowing => isShowing;

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

        if (panelRect == null)
        {
            Debug.LogError("NotificationPopupController: no RectTransform found/assigned. This component must be on a UI object.");
            enabled = false;
            return;
        }

        if (persistAcrossScenes)
        {
            if (transform.parent != null)
            {
                Debug.LogWarning("NotificationPopupController was not on a root GameObject. Detaching before DontDestroyOnLoad so it survives scene changes.");
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
        }

        // Whatever position this panel is placed at in the editor is treated
        // as its resting/"shown" position for every animation - no manual
        // position configuration needed.
        shownAnchoredPosition = panelRect.anchoredPosition;

        panelRect.gameObject.SetActive(false);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (closeButton != null)
        {
            closeButtonBaseScale = closeButton.transform.localScale;

            if (closeButtonImage == null)
                closeButtonImage = closeButton.GetComponent<Image>();

            closeButton.onClick.AddListener(HandleCloseButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
    }

    // ================= PUBLIC API =================
    /// <summary>Show a message using the default direction and display duration.</summary>
    public void Show(string message)
    {
        Show(message, PopupType.Info, defaultDisplayDuration, defaultDirection);
    }

    /// <summary>Show a message with a specific severity/type (affects color tinting).</summary>
    public void Show(string message, PopupType type)
    {
        Show(message, type, defaultDisplayDuration, defaultDirection);
    }

    /// <summary>Show a message with a specific severity and how long it should stay up.</summary>
    public void Show(string message, PopupType type, float displayDuration)
    {
        Show(message, type, displayDuration, defaultDirection);
    }

    /// <summary>Full control: severity, display duration, and which slide preset to use.</summary>
    public void Show(string message, PopupType type, float displayDuration, PopupSlideDirection direction)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Enqueue(new PopupRequest
        {
            message = message,
            type = type,
            direction = direction,
            displayDuration = Mathf.Max(0f, displayDuration),
            persistent = false
        });
    }

    /// <summary>Shows a message that stays up until Hide() is explicitly called (e.g. "Connecting...").</summary>
    public void ShowPersistent(string message, PopupType type = PopupType.Info, PopupSlideDirection? direction = null)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Enqueue(new PopupRequest
        {
            message = message,
            type = type,
            direction = direction ?? defaultDirection,
            displayDuration = 0f,
            persistent = true
        });
    }

    /// <summary>Manually dismiss whatever is currently showing. No-op if nothing is showing.</summary>
    public void Hide()
    {
        hideRequested = true;
    }

    // ================= CLOSE BUTTON =================
    private void HandleCloseButtonClicked()
    {
        if (closeButton != null)
        {
            if (closeButtonPunchRoutine != null)
                StopCoroutine(closeButtonPunchRoutine);

            closeButtonPunchRoutine = StartCoroutine(PunchCloseButton());
        }

        // Skip the remaining display time and use the snappier fast-close
        // duration for the slide-out instead of the normal slideOutDuration.
        fastCloseRequested = true;
        hideRequested = true;
    }

    private IEnumerator PunchCloseButton()
    {
        Transform buttonTransform = closeButton.transform;
        Vector3 shrunkScale = closeButtonBaseScale * closeButtonPunchScale;
        float halfDuration = Mathf.Max(0.01f, closeButtonPunchDuration * 0.5f);

        yield return LerpScale(buttonTransform, closeButtonBaseScale, shrunkScale, halfDuration);
        yield return LerpScale(buttonTransform, shrunkScale, closeButtonBaseScale, halfDuration);

        closeButtonPunchRoutine = null;
    }

    private IEnumerator LerpScale(Transform target, Vector3 fromScale, Vector3 toScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = closeButtonPunchCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, progress);
            yield return null;
        }

        target.localScale = toScale;
    }

    // ================= QUEUEING =================
    private void Enqueue(PopupRequest request)
    {
        if (queueMessages)
        {
            pendingQueue.Enqueue(request);
            if (!queueRunning)
                StartCoroutine(ProcessQueue());
        }
        else
        {
            // Interrupt mode: drop anything queued/playing and show this now.
            pendingQueue.Clear();

            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            hideRequested = false;
            activeRoutine = StartCoroutine(ShowRoutine(request));
        }
    }

    private IEnumerator ProcessQueue()
    {
        queueRunning = true;

        while (pendingQueue.Count > 0)
        {
            var request = pendingQueue.Dequeue();
            hideRequested = false;
            activeRoutine = StartCoroutine(ShowRoutine(request));
            yield return activeRoutine;

            if (pendingQueue.Count > 0 && gapBetweenQueuedMessages > 0f)
                yield return new WaitForSecondsRealtime(gapBetweenQueuedMessages);
        }

        queueRunning = false;
    }

    // ================= ANIMATION =================
    private IEnumerator ShowRoutine(PopupRequest request)
    {
        // Capture the panel's actual current state BEFORE touching anything,
        // so interrupting a mid-flight animation continues smoothly from
        // wherever it visually is, instead of snapping to a fixed hidden
        // position.
        bool wasAlreadyVisible = panelRect.gameObject.activeSelf;
        Vector2 currentPos = panelRect.anchoredPosition;
        float currentAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        isShowing = true;
        ApplyContent(request);

        panelRect.gameObject.SetActive(true);

        Vector2 hiddenPos = GetHiddenPosition(request.direction);
        bool useFade = canvasGroup != null && fadeAlongsideSlide;

        Vector2 fromPos = wasAlreadyVisible ? currentPos : hiddenPos;
        float fromAlpha = wasAlreadyVisible ? currentAlpha : (useFade ? 0f : 1f);

        if (verboseLogging)
            Debug.Log("NotificationPopupController: showing [" + request.type + "] \"" + request.message + "\"");

        yield return AnimatePanel(fromPos, shownAnchoredPosition, fromAlpha, 1f, slideInDuration);

        if (request.persistent)
        {
            while (!hideRequested)
                yield return null;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < request.displayDuration && !hideRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        hideRequested = false;

        float outDuration = fastCloseRequested ? fastCloseDuration : slideOutDuration;
        fastCloseRequested = false;

        yield return AnimatePanel(shownAnchoredPosition, hiddenPos, 1f, useFade ? 0f : 1f, outDuration);

        panelRect.gameObject.SetActive(false);
        isShowing = false;
        activeRoutine = null;
    }

    private IEnumerator AnimatePanel(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0f)
        {
            panelRect.anchoredPosition = toPos;
            if (canvasGroup != null) canvasGroup.alpha = toAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // unscaledDeltaTime so popups still animate smoothly even if
            // Time.timeScale is 0 (paused) or slowed down.
            elapsed += Time.unscaledDeltaTime;
            float progress = slideCurve.Evaluate(Mathf.Clamp01(elapsed / duration));

            panelRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, progress);
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, progress);

            yield return null;
        }

        panelRect.anchoredPosition = toPos;
        if (canvasGroup != null) canvasGroup.alpha = toAlpha;
    }

    private Vector2 GetHiddenPosition(PopupSlideDirection direction)
    {
        Rect rect = panelRect.rect;

        switch (direction)
        {
            case PopupSlideDirection.FromTop:
                return shownAnchoredPosition + new Vector2(0f, rect.height + offscreenPadding);
            case PopupSlideDirection.FromBottom:
                return shownAnchoredPosition - new Vector2(0f, rect.height + offscreenPadding);
            case PopupSlideDirection.FromLeft:
                return shownAnchoredPosition - new Vector2(rect.width + offscreenPadding, 0f);
            case PopupSlideDirection.FromRight:
                return shownAnchoredPosition + new Vector2(rect.width + offscreenPadding, 0f);
            case PopupSlideDirection.FadeOnly:
            default:
                return shownAnchoredPosition;
        }
    }

    private void ApplyContent(PopupRequest request)
    {
        if (messageText != null)
        {
            messageText.text = request.message;
            if (tintTextByType)
                messageText.color = GetColorForType(request.type);
        }

        if (backgroundImage != null && tintBackgroundByType)
            backgroundImage.color = GetColorForType(request.type);

        if (closeButtonImage != null && tintCloseButtonByType)
            closeButtonImage.color = GetColorForType(request.type);
    }

    private Color GetColorForType(PopupType type)
    {
        switch (type)
        {
            case PopupType.Warning: return warningColor;
            case PopupType.Error: return errorColor;
            case PopupType.Success: return successColor;
            default: return infoColor;
        }
    }
}