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
    [SerializeField, Min(0f)] private float letterBounceAmount = 1.2f;
    [SerializeField, Min(.01f)] private float letterBounceDuration = .075f;

    [Header("Big Boss Portrait Reaction")]
    [SerializeField, Min(0f)] private float portraitBopDuration = 0.22f;
    [SerializeField, Range(0.8f, 1f)] private float portraitBopStartScale = 0.94f;
    [SerializeField, Range(1f, 1.2f)] private float portraitBopPeakScale = 1.06f;
    [SerializeField, Min(0f)] private float portraitBopLift = 5f;
    [Header("Lobby Tutorial Layout")]
    [Tooltip("Reuse the authored tutorial presentation in campaign guides without starting TutorialSystem.")]
    [SerializeField] private bool useTutorialPresentation;
    [SerializeField] private Vector2 dialogueOffset = Vector2.zero;
    [SerializeField, Min(12f)] private float dialogueMinimumFontSize = 18f;
    [SerializeField, Min(12f)] private float dialogueMaximumFontSize = 30f;
    [Tooltip("Text insets in panel units: left, top (below the nameplate), right, bottom.")]
    [SerializeField] private Vector4 dialogueTextPadding = new Vector4(20f, 20f, 20f, 14f);
    [Tooltip("Additional placement offset; negative X gives the left portrait more text clearance.")]
    [SerializeField] private Vector2 portraitLeftOffset = new Vector2(-36f, 0f);
    [Tooltip("Additional placement offset; positive X gives the right portrait more text clearance.")]
    [SerializeField] private Vector2 portraitRightOffset = new Vector2(36f, 0f);
    [SerializeField] private Vector2 continuePromptOffset = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 continuePromptSize = new Vector2(220f, 32f);
    [Tooltip("Corner radius as a fraction of the prompt height.")]
    [SerializeField, Range(0f, .5f)] private float continuePromptCornerRounding = .3f;
    [SerializeField, Min(12f)] private float continuePromptFontSize = 20f;
    [SerializeField, Min(.05f)] private float portraitSideCheckInterval = .35f;

    private TutorialSystem.TutorialStep chatterStep;
    private float chatterWaitStarted;
    private bool chatterShown, chatterActive;
    private CanvasGroup chatterInput;
    private bool previousChatterRaycasts, previousChatterInteractable;

    private void Update()
    {
        // Lobby1Tutorial uses the existing task area during actions/travel.
        if (polishedLobby) { UpdateDialogueTap(); return; }
        TutorialSystem tutorial = TutorialSystem.Instance;
        var step = tutorial != null ? tutorial.CurrentStep : null;
        bool waiting = tutorial != null && tutorial.IsWaitingForGameplayAction &&
                       step != null;
        if (chatterStep != step || !waiting)
        {
            if (chatterActive) Hide();
            chatterStep = step;
            chatterWaitStarted = Time.unscaledTime;
            chatterShown = false;
            return;
        }
        if (chatterShown || IsVisible || Time.unscaledTime - chatterWaitStarted < 2.75f) return;
        string line = WaitLine(step.ActionKey);
        if (line == null) return;
        chatterShown = true;
        ShowNonBlockingChatter(line);
    }

    public bool IsChatterActive => chatterActive;
    public bool SupportsNonBlockingChatter => gameObject.scene.name != "Lobby1Tutorial";

    public bool ShowNonBlockingChatter(string line, bool replaceChatter = false)
    {
        if (!SupportsNonBlockingChatter) return false;
        if (replaceChatter && chatterActive) Hide();
        var tutorial = TutorialSystem.Instance;
        if (IsVisible || tutorial == null || !tutorial.IsWaitingForGameplayAction) return false;
        chatterStep = tutorial.CurrentStep;
        ShowAuto("Big Boss", line, chatterStep.Portrait, 4f);
        GameObject panel = root != null ? root : gameObject;
        chatterInput = panel.GetComponent<CanvasGroup>();
        if (chatterInput == null) chatterInput = panel.AddComponent<CanvasGroup>();
        previousChatterRaycasts = chatterInput.blocksRaycasts;
        previousChatterInteractable = chatterInput.interactable;
        chatterInput.blocksRaycasts = false;
        chatterInput.interactable = false;
        chatterActive = true;
        return true;
    }

    private static string WaitLine(string action)
    {
        switch (action)
        {
            case "Computer.Open": return "Most of your planning happens at the computer. You'll be visiting it often.";
            case "Restock.TruckOpened": return "Those boxes are the ingredients we ordered earlier.";
            case "Restock.WaitForDelivery": return "Our ingredients are on their way. We'll collect them when the truck arrives.";
            case "Restock.ExitRoom": return "That's the stock handled. Let's get back to the lobby.";
            case "Customer.FrontOfLine": return "They're making their way to the front. We'll greet them once they're ready.";
            case "Customer.Seated": return "Give them a moment to get settled.";
            case "Customer.NotepadOpened": return "Keep an eye on those bubbles. They'll tell you when a customer needs something.";
            case "Customer.FoodDelivered": return "The table number helps you keep track of who you're serving.";
            case "Customer.NeedsBill": return "They're enjoying their meal. Keep an eye on the lobby while they eat.";
            case "Customer.FoodReady": return "While the kitchen handles that, keep an eye on the rest of the lobby.";
            case "Customer.CashierOpened": return "Head over to the register. We'll take care of their payment there.";
            default: return null;
        }
    }

    private void RestoreChatterInput()
    {
        if (chatterActive && chatterInput != null)
        {
            chatterInput.blocksRaycasts = previousChatterRaycasts;
            chatterInput.interactable = previousChatterInteractable;
        }
        chatterActive = false;
    }

    private Coroutine typingRoutine;
    private Coroutine autoHideRoutine;
    private Coroutine portraitRoutine;
    private Action manualNextAction;
    private string currentFullMessage = string.Empty;
    private bool isTyping;
    private float[] letterRevealTimes = Array.Empty<float>();
    private float lastLetterRevealTime;
    private bool letterBounceActive;
    private RectTransform portraitRect;
    private Vector2 portraitHome;
    private Vector3 portraitHomeScale = Vector3.one;
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform focusTarget;
    private RectTransform panelRect;
    private Vector2 panelHome;
    private Vector2 panelSize;
    private bool portraitRight;
    private float nextSideCheck;
    private bool polishedLobby;
    private Transform focusWorld;
    private TMP_Text continuePrompt;
    private Sprite continuePromptBackground;
    private bool pointerReleased;
    private RectTransform portraitPlacement;
    public Func<Vector2, bool> CanAdvanceAt { get; set; }
    public TMP_Text BodyText => bodyText;

    public void SetWorldFocusTarget(Transform target) => focusWorld = target;

    public bool IsVisible => root != null ? root.activeSelf : gameObject.activeSelf;
    public string Speaker => speakerText != null ? speakerText.text : string.Empty;
    public string Message => bodyText != null ? bodyText.text : string.Empty;
    public Sprite Portrait => portraitImage != null ? portraitImage.sprite : null;
    public bool IsManualAdvanceVisible => nextButton != null && nextButton.gameObject.activeSelf;

    public void ApplyDebugBop(float peak, float duration)
    {
        portraitBopPeakScale = Mathf.Clamp(peak, 1f, 1.2f);
        portraitBopDuration = Mathf.Clamp(duration, .05f, 1f);
    }

    private void Awake()
    {
        polishedLobby = useTutorialPresentation || gameObject.scene.name == "Lobby1Tutorial";
        if (bodyText != null) bodyText.OnPreRenderText += AnimateRevealedLetters;
        // The dialogue panel, nameplate, and text always stay at their authored pose.
        // Only the Big Boss portrait reacts when its sprite actually changes.
        portraitRect = portraitImage != null ? portraitImage.rectTransform : null;
        if (portraitRect != null)
        {
            portraitHome = portraitRect.anchoredPosition;
            portraitHomeScale = portraitRect.localScale;
            if (polishedLobby)
            {
                // Placement owns the side/flip; the original artwork keeps its
                // own transform for the existing sprite-change bop animation.
                portraitPlacement = new GameObject("Big Boss Placement", typeof(RectTransform)).GetComponent<RectTransform>();
                portraitPlacement.SetParent(portraitRect.parent, false);
                portraitPlacement.SetSiblingIndex(portraitRect.GetSiblingIndex());
                portraitPlacement.anchorMin = portraitRect.anchorMin;
                portraitPlacement.anchorMax = portraitRect.anchorMax;
                portraitPlacement.pivot = portraitRect.pivot;
                portraitPlacement.sizeDelta = portraitRect.sizeDelta;
                portraitPlacement.anchoredPosition = portraitHome;
                portraitRect.SetParent(portraitPlacement, false);
                portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(.5f, .5f);
                portraitRect.anchoredPosition = Vector2.zero;
            }
        }

        if (useTutorialPresentation || FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include) != null)
        {
            Canvas layer = GetComponent<Canvas>();
            if (layer == null) layer = gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 32761;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (polishedLobby && bodyText != null && nextButton != null)
            {
                panelRect = bodyText.transform.parent as RectTransform;
                panelHome = panelRect.anchoredPosition;
                panelSize = panelRect.sizeDelta;
                SetNextVisible(false);
                Image promptBacking = new GameObject("Tap Anywhere Prompt", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                promptBacking.transform.SetParent(panelRect, false);
                promptBacking.color = new Color32(35, 30, 28, 210);
                promptBacking.raycastTarget = false;
                promptBacking.rectTransform.anchorMin = promptBacking.rectTransform.anchorMax = Vector2.one;
                promptBacking.rectTransform.pivot = new Vector2(1f, 0f);
                promptBacking.rectTransform.anchoredPosition = continuePromptOffset;
                promptBacking.rectTransform.sizeDelta = continuePromptSize;
                RoundContinuePrompt(promptBacking);
                continuePrompt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                continuePrompt.transform.SetParent(promptBacking.transform, false);
                continuePrompt.font = bodyText.font;
                continuePrompt.fontSize = continuePromptFontSize;
                continuePrompt.color = Color.white;
                continuePrompt.alignment = TextAlignmentOptions.Center;
                continuePrompt.raycastTarget = false;
                continuePrompt.text = TutorialInputTerminology.IsMobile ? "Tap to continue" : "Click to continue";
                continuePrompt.rectTransform.anchorMin = Vector2.zero;
                continuePrompt.rectTransform.anchorMax = Vector2.one;
                continuePrompt.rectTransform.sizeDelta = Vector2.zero;
                promptBacking.gameObject.SetActive(false);
                ApplyDialogueTextLayout();
            }
            else if (bodyText != null && nextButton != null)
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
    public void SetFocusTarget(RectTransform target)
    {
        if (focusTarget == target) return;
        focusTarget = target;
        nextSideCheck = 0f;
    }

    private Rect ScreenRect(RectTransform rect)
    {
        Canvas owner = rect.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera camera = owner == null || owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void LateUpdate()
    {
        UpdateLetterBounce();
        if (!polishedLobby || !IsVisible || portraitPlacement == null) return;
        Rect focus = focusTarget != null && focusTarget.gameObject.activeInHierarchy ? ScreenRect(focusTarget) : default;
        if (focus.width <= 0f && focusWorld != null)
            TutorialWorldTargetGeometry.TryGetScreenRect(focusWorld,
                TutorialWorldTargetGeometry.ResolveCamera(focusWorld, Camera.main), out focus);
        if (portraitRect == null || Time.unscaledTime < nextSideCheck) return;
        nextSideCheck = Time.unscaledTime + portraitSideCheckInterval;
        // Test two authored poses, never mirror the panel or its text.
        portraitPlacement.anchoredPosition = new Vector2(-Mathf.Abs(portraitHome.x), portraitHome.y) + portraitLeftOffset;
        Rect left = ScreenRect(portraitRect);
        bool right = focus.width > 0 && left.Overlaps(focus);
        if (portraitRight && focus.width > 0)
        {
            // A small margin prevents side changes at the edge while the camera settles.
            left.xMin -= 20f; left.xMax += 20f;
            right = left.Overlaps(focus);
        }
        if (right)
        {
            portraitPlacement.anchoredPosition = new Vector2(Mathf.Abs(portraitHome.x), portraitHome.y) + portraitRightOffset;
            // If both sides are occupied, leave the explanation readable and
            // temporarily omit only the artwork.
            portraitImage.enabled = !ScreenRect(portraitRect).Overlaps(focus);
        }
        else portraitImage.enabled = true;
        portraitRight = right;
        portraitPlacement.anchoredPosition = new Vector2((right ? 1f : -1f) * Mathf.Abs(portraitHome.x), portraitHome.y) + (right ? portraitRightOffset : portraitLeftOffset);
        portraitPlacement.localScale = new Vector3(right ? -1f : 1f, 1f, 1f);
    }

    private void ApplyPortraitPose()
    {
        if (portraitRect == null) return;
        portraitRect.anchoredPosition = portraitPlacement != null ? Vector2.zero : portraitHome;
        portraitRect.localScale = portraitHomeScale;
    }

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
        StopLetterBounce();
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        isTyping = false;
        currentFullMessage = message ?? string.Empty;
        if (bodyText != null)
        {
            bodyText.text = currentFullMessage;
            bodyText.maxVisibleCharacters = int.MaxValue;
        }
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
        RestoreChatterInput();
        StopAllPresentationRoutines();
        manualNextAction = null;
        isTyping = false;
        ResetPresentationVisuals();
        if (root != null) root.SetActive(false);
        SetNextVisible(false);
    }

    public void HideImmediate()
    {
        RestoreChatterInput();
        StopAllPresentationRoutines();
        manualNextAction = null;
        isTyping = false;
        ResetPresentationVisuals();
        if (bodyText != null) bodyText.text = string.Empty;
        if (root != null) root.SetActive(false);
        SetNextVisible(false);
    }

    private void ShowInternal(string speaker, string message, Sprite portrait, bool manualMode)
    {
        StopLetterBounce();
        RestoreChatterInput();
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        if (autoHideRoutine != null) StopCoroutine(autoHideRoutine);
        typingRoutine = autoHideRoutine = null;
        if (root != null) root.SetActive(true);
        SetSpeaker(speaker);
        SetPortrait(portrait);
        currentFullMessage = message ?? string.Empty;
        ApplyDialogueTextLayout();
        SetNextVisible(manualMode);
        pointerReleased = false;
        if (continuePrompt != null) continuePrompt.transform.parent.gameObject.SetActive(false);
        if (!Application.isPlaying || typeSpeed <= 0f) SetMessage(currentFullMessage);
        else typingRoutine = StartCoroutine(TypeRoutine(currentFullMessage));
    }

    private IEnumerator TypeRoutine(string message)
    {
        isTyping = true;
        letterRevealTimes = new float[message.Length];
        for (int i = 0; i < letterRevealTimes.Length; i++) letterRevealTimes[i] = float.NegativeInfinity;
        // Size against the whole message once; revealing characters must not
        // repeatedly change the font size or push text toward the nameplate.
        if (bodyText != null)
        {
            bodyText.text = message;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
        }
        for (int i = 0; i < message.Length; i++)
        {
            if (bodyText != null) bodyText.maxVisibleCharacters = i + 1;
            if (polishedLobby && letterBounceAmount > 0f && !LevelOneUIAccessibility.ReducedMotion)
            {
                letterRevealTimes[i] = lastLetterRevealTime = Time.unscaledTime;
                letterBounceActive = true;
            }
            if (typeSpeed > 0f) yield return new WaitForSecondsRealtime(typeSpeed);
        }
        isTyping = false;
        typingRoutine = null;
        pointerReleased = false;
    }

    private void UpdateLetterBounce()
    {
        if (!letterBounceActive || bodyText == null) return;
        if (!IsVisible || LevelOneUIAccessibility.ReducedMotion ||
            Time.unscaledTime - lastLetterRevealTime >= letterBounceDuration)
        {
            StopLetterBounce();
            return;
        }
        // TMP regenerates the base mesh first, so offsets never accumulate.
        // Only the few characters revealed in the last fraction of a second move.
        bodyText.ForceMeshUpdate();
    }

    private void AnimateRevealedLetters(TMP_TextInfo textInfo)
    {
        if (!letterBounceActive || !polishedLobby || LevelOneUIAccessibility.ReducedMotion) return;
        int count = Mathf.Min(textInfo.characterCount, Mathf.Min(bodyText.maxVisibleCharacters, letterRevealTimes.Length));
        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            float age = Time.unscaledTime - letterRevealTimes[i];
            if (!character.isVisible || age < 0f || age >= letterBounceDuration) continue;
            float lift = Mathf.Sin(Mathf.PI * age / Mathf.Max(.01f, letterBounceDuration)) * letterBounceAmount;
            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int corner = 0; corner < 4; corner++) vertices[character.vertexIndex + corner].y += lift;
        }
    }

    private void StopLetterBounce()
    {
        if (!letterBounceActive) return;
        letterBounceActive = false;
        if (bodyText != null && bodyText.isActiveAndEnabled) bodyText.ForceMeshUpdate();
    }

    private void OnDestroy()
    {
        if (bodyText != null) bodyText.OnPreRenderText -= AnimateRevealedLetters;
        if (continuePromptBackground != null)
        {
            Destroy(continuePromptBackground.texture);
            Destroy(continuePromptBackground);
        }
    }

    private void RoundContinuePrompt(Image backing)
    {
        if (continuePromptCornerRounding <= 0f) return;
        // A small, antialiased white sprite keeps the existing background tint.
        // Nine-slicing preserves circular corners without stretching the text.
        const int size = 64;
        const float radius = 16f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "Tutorial Continue Rounded Background", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x + .5f - size * .5f), Mathf.Abs(y + .5f - size * .5f)) - Vector2.one * radius;
                float distance = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(.5f - distance));
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        float referencePixels = backing.canvas != null ? backing.canvas.referencePixelsPerUnit : 100f;
        continuePromptBackground = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * .5f,
            referencePixels, 0, SpriteMeshType.FullRect, Vector4.one * radius);
        backing.sprite = continuePromptBackground;
        backing.type = Image.Type.Sliced;
        backing.pixelsPerUnitMultiplier = radius / Mathf.Max(.5f, Mathf.Min(continuePromptSize.x, continuePromptSize.y) * continuePromptCornerRounding);
    }

    private void ApplyDialogueTextLayout()
    {
        if (!polishedLobby || panelRect == null || bodyText == null) return;
        // Keep the authored panel and header in place. Only the text fits itself.
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = panelHome + dialogueOffset;
        RectTransform body = bodyText.rectTransform;
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(dialogueTextPadding.x, dialogueTextPadding.w);
        body.offsetMax = -new Vector2(dialogueTextPadding.z, dialogueTextPadding.y);
        bodyText.enableAutoSizing = true;
        float textScale = useTutorialPresentation && LevelOneUIAccessibility.LargeText ? 1.15f : 1f;
        bodyText.fontSizeMin = dialogueMinimumFontSize * textScale;
        bodyText.fontSizeMax = Mathf.Max(dialogueMinimumFontSize, dialogueMaximumFontSize) * textScale;
        bodyText.fontSize = bodyText.fontSizeMax;
        bodyText.overflowMode = TextOverflowModes.Truncate;
    }

    private IEnumerator AutoHideRoutine(float duration)
    {
        while (isTyping) yield return null;
        yield return new WaitForSecondsRealtime(duration);
        Hide();
    }

    private void OnNextPressed()
    {
        if (polishedLobby)
        {
            if (!IsVisible || manualNextAction == null) return;
        }
        else if (nextButton == null || !nextButton.gameObject.activeInHierarchy || !nextButton.interactable) return;
        if (isTyping)
        {
            StopLetterBounce();
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;
            typingRoutine = null;
            if (bodyText != null)
            {
                bodyText.text = currentFullMessage;
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
            pointerReleased = false;
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
            ApplyPortraitPose();
            portraitRect.localScale *= scale;
            portraitRect.anchoredPosition += Vector2.up * lift;
            yield return null;
        }
        ResetPortraitVisuals();
        portraitRoutine = null;
    }

    private void StopAllPresentationRoutines()
    {
        StopLetterBounce();
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
        ApplyPortraitPose();
    }

    private void SetNextVisible(bool visible)
    {
        if (polishedLobby) visible = false;
        if (nextButton == null) return;
        nextButton.interactable = visible;
        foreach (Graphic graphic in nextButton.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = visible;
        nextButton.gameObject.SetActive(visible);
    }

    private void UpdateDialogueTap()
    {
        bool manual = IsVisible && manualNextAction != null;
        if (continuePrompt != null) continuePrompt.transform.parent.gameObject.SetActive(manual && !isTyping);
        if (!manual) { pointerReleased = false; return; }
        bool held = Input.GetMouseButton(0) || Input.touchCount > 0;
        if (!held) { pointerReleased = true; return; }
        bool began = Input.GetMouseButtonDown(0);
        if (Input.touchCount == 1) began |= Input.GetTouch(0).phase == TouchPhase.Began;
        if (!pointerReleased || !began || Input.touchCount > 1) return;
        pointerReleased = false;
        Vector2 position = Input.touchCount == 1 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        if (CanAdvanceAt != null && !CanAdvanceAt(position)) return;
        OnNextPressed(); // One input reveals OR advances, never both.
    }

    private void OnDisable()
    {
        StopAllPresentationRoutines();
        manualNextAction = null;
        pointerReleased = false;
        if (continuePrompt != null) continuePrompt.transform.parent.gameObject.SetActive(false);
        SetNextVisible(false);
    }

    private void SetPortraitAlpha(float alpha)
    {
        if (portraitImage == null) return;
        Color color = portraitImage.color;
        color.a = alpha;
        portraitImage.color = color;
    }
}
