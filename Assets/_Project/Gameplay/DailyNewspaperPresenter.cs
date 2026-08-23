using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Build-safe newspaper presentation. It uses an editable settings asset and
/// a runtime Times New Roman font so the operating-system font file is never
/// bundled without an explicit licensing decision.
/// </summary>
public sealed class DailyNewspaperPresenter : MonoBehaviour
{
    private const string LobbySceneName = "Lobby1";
    private const float VisibilityRefreshSeconds = 0.25f;

    private CasualDiningPolishManager manager;
    private CasualDiningPolishSettings settings;
    private Font newspaperFont;
    private Canvas canvas;
    private GameObject canvasRoot;
    private GameObject newspaperButtonRoot;
    private RectTransform newspaperButtonRect;
    private Text newspaperButtonText;
    private GameObject overlayRoot;
    private Image dimmer;
    private Image paperImage;
    private RectTransform paperRect;
    private Text mastheadText;
    private Text issueText;
    private Text headlineText;
    private Text bylineText;
    private Text bodyText;
    private Button closeButton;
    private Button previousButton;
    private Button nextButton;
    private Text previousLabel;
    private Text nextLabel;
    private ScrollRect articleScroll;
    private AudioSource audioSource;
    private AudioClip fallbackRustle;
    private AudioClip fallbackSlap;
    private AudioClip fallbackPageTurn;
    private Coroutine animationRoutine;
    private int animationVersion;
    private bool opening;
    private bool open;
    private float nextVisibilityRefresh;
    private int displayedIssueDay;

    public bool IsOpen => open || opening;

    public void Bind(
        CasualDiningPolishManager configuredManager,
        CasualDiningPolishSettings configuredSettings)
    {
        manager = configuredManager;
        settings = configuredSettings;
        if (manager != null)
        {
            manager.NewspaperStateChanged -= RefreshVisibility;
            manager.NewspaperStateChanged += RefreshVisibility;
        }
        RefreshVisibility();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.NewspaperStateChanged -= RefreshVisibility;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextVisibilityRefresh)
        {
            nextVisibilityRefresh = Time.unscaledTime + VisibilityRefreshSeconds;
            RefreshVisibility();
        }

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (newspaperButtonRect != null && newspaperButtonRoot.activeSelf && !open && !opening)
        {
            int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
            NewspaperIssueSaveEntry issue = manager != null ? manager.GetIssueForDay(day) : null;
            float pulse = issue != null && !issue.viewed
                ? 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.045f
                : 1f;
            newspaperButtonRect.localScale = Vector3.one * pulse;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && opening)
            ForceOpenFinal();
        else if (hasFocus)
            RefreshVisibility();
    }

    public void RefreshVisibility()
    {
        bool inLobby = SceneManager.GetActiveScene().name == LobbySceneName;
        GameFlowManager flow = GameFlowManager.Instance;
        bool preparation = flow != null && flow.UsesSingleRestaurantFlow &&
            (flow.CurrentRestaurantSessionState == GameFlowManager.RestaurantSessionState.PreOpen ||
             flow.CurrentRestaurantSessionState == GameFlowManager.RestaurantSessionState.Endless);
        bool serviceRunning = GameDayManager.Instance != null && GameDayManager.Instance.ServiceActive;
        bool shouldShowButton = inLobby && preparation && !serviceRunning;

        if ((shouldShowButton || IsOpen) && canvasRoot == null)
            BuildUI();
        if (canvasRoot == null)
            return;

        canvasRoot.SetActive(shouldShowButton || IsOpen);
        if (newspaperButtonRoot != null)
            newspaperButtonRoot.SetActive(shouldShowButton && !IsOpen);

        if (newspaperButtonText != null && manager != null && flow != null)
        {
            NewspaperIssueSaveEntry issue = manager.GetIssueForDay(flow.CurrentDay);
            newspaperButtonText.text = issue != null && issue.viewed
                ? "✓  NEWS READ"
                : "◆  TODAY'S NEWS";
        }
    }

    public void Open(NewspaperIssueSaveEntry issue)
    {
        if (issue == null)
            return;
        if (canvasRoot == null)
            BuildUI();
        if (canvasRoot == null || overlayRoot == null)
            return;

        Populate(issue);
        displayedIssueDay = issue.day;
        RefreshArchiveButtons();
        canvasRoot.SetActive(true);
        newspaperButtonRoot.SetActive(false);
        overlayRoot.SetActive(true);
        if (closeButton != null)
            closeButton.interactable = false;

        animationVersion++;
        PlayPaperSound(settings != null ? settings.paperRustleSound : null, ref fallbackRustle,
            "Newspaper Rustle", 0.30f, 0.12f, false);
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(OpenRoutine(animationVersion));
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        animationVersion++;
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
        opening = false;
        open = false;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
        RefreshVisibility();
    }

    public void CloseImmediately()
    {
        Close();
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private IEnumerator OpenRoutine(int version)
    {
        opening = true;
        open = false;
        bool reducedMotion = LevelOneUIAccessibility.ReducedMotion;
        bool slapPlayed = false;
        float duration = reducedMotion
            ? 0.18f
            : settings != null ? Mathf.Max(0.2f, settings.openingAnimationSeconds) : 0.78f;
        double started = Time.realtimeSinceStartupAsDouble;

        while (version == animationVersion)
        {
            float elapsed = (float)(Time.realtimeSinceStartupAsDouble - started);
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = reducedMotion
                ? Mathf.SmoothStep(0f, 1f, normalized)
                : EaseOutBack(normalized);

            if (dimmer != null)
            {
                Color color = dimmer.color;
                color.a = Mathf.Lerp(0f, 0.72f, Mathf.Clamp01(normalized * 1.4f));
                dimmer.color = color;
            }

            if (paperRect != null)
            {
                paperRect.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, eased);
                paperRect.localEulerAngles = reducedMotion
                    ? Vector3.zero
                    : new Vector3(0f, 0f, Mathf.Lerp(405f, 0f, Mathf.Clamp01(normalized)));
            }

            if (!slapPlayed && normalized >= 0.72f)
            {
                slapPlayed = true;
                PlayPaperSound(settings != null ? settings.paperSlapSound : null, ref fallbackSlap,
                    "Newspaper Slap", 0.14f, 0.22f, true);
            }

            if (normalized >= 1f)
                break;
            yield return null;
        }

        if (version != animationVersion)
            yield break;
        animationRoutine = null;
        ForceOpenFinal();
    }

    private void ForceOpenFinal()
    {
        animationVersion++;
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
        opening = false;
        open = true;
        if (overlayRoot != null)
            overlayRoot.SetActive(true);
        if (paperRect != null)
        {
            paperRect.localScale = Vector3.one;
            paperRect.localEulerAngles = Vector3.zero;
        }
        if (dimmer != null)
        {
            Color color = dimmer.color;
            color.a = 0.72f;
            dimmer.color = color;
        }
        if (closeButton != null)
            closeButton.interactable = true;
        manager?.MarkCurrentIssueViewed();
    }

    private void Populate(NewspaperIssueSaveEntry issue)
    {
        if (issue == null)
            return;
        displayedIssueDay = issue.day;
        int largeTextBonus = LevelOneUIAccessibility.LargeText ? 4 : 0;
        bool highContrast = LevelOneUIAccessibility.HighContrast;
        if (paperImage != null)
            paperImage.color = highContrast ? Color.white : new Color(0.97f, 0.93f, 0.80f, 1f);
        if (mastheadText != null)
        {
            mastheadText.text = settings != null && !string.IsNullOrWhiteSpace(settings.newspaperName)
                ? settings.newspaperName
                : "THE GALACTIC GAZETTE";
            mastheadText.fontSize = 42 + largeTextBonus;
        }
        if (issueText != null)
            issueText.text = "ISSUE " + issue.day.ToString("00") + "  •  RESTAURANT DAY " + issue.day;
        if (headlineText != null)
        {
            headlineText.text = issue.headline ?? string.Empty;
            headlineText.fontSize = 30 + largeTextBonus;
        }
        if (bylineText != null)
            bylineText.text = "Reported by " + (issue.byline ?? "Alien Correspondent");
        if (bodyText != null)
        {
            bodyText.color = highContrast ? Color.black : new Color(0.10f, 0.085f, 0.07f, 1f);
            bodyText.text = PrepareDisplayContent(issue.renderedContent);
            bodyText.fontSize = 19 + largeTextBonus;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);
        }
        if (articleScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            articleScroll.verticalNormalizedPosition = 1f;
        }
    }

    private static string PrepareDisplayContent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Contains("<size="))
            return raw ?? string.Empty;

        string[] lines = raw.Replace("\r\n", "\n").Split('\n');
        StringBuilder result = new StringBuilder(raw.Length + 400);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            switch (trimmed)
            {
                case "<b>EARTH RESTAURANT WATCH</b>":
                    line = "<size=26><b>◎ APPROVAL WATCH</b></size>";
                    break;
                case "<b>RESTAURANT RATING</b>":
                    line = "<size=26><b>★ RESTAURANT RATING</b></size>";
                    break;
                case "<b>YESTERDAY AT THE DINER</b>":
                    line = "<size=26><b>● SHIFT SNAPSHOT</b></size>";
                    break;
                case "<b>VOICE FROM THE QUEUE</b>":
                    line = "<size=25><b>◆ VOICE FROM THE QUEUE</b></size>";
                    break;
                case "<b>MARKET WATCH</b>":
                    line = "<size=26><b>₱ MARKET WATCH</b></size>";
                    break;
                case "<b>ALIEN BOSS ADVISORY</b>":
                    line = "<size=26><b>! BOSS ORDER</b></size>";
                    break;
                case "<b>STAFF SPOTLIGHT</b>":
                    line = "<size=25><b>★ STAFF SPOTLIGHT</b></size>";
                    break;
                default:
                    if (trimmed.StartsWith("▲ "))
                    {
                        line = "<color=#A31818><size=24><b>▲ PRICE INCREASE</b></size></color>\n" +
                               "<b>" + trimmed + "</b>";
                    }
                    else if (trimmed.StartsWith("▼ "))
                    {
                        line = "<color=#176B36><size=24><b>▼ PRICE DECREASE</b></size></color>\n" +
                               "<b>" + trimmed + "</b>";
                    }
                    break;
            }

            result.Append(line);
            if (i < lines.Length - 1)
                result.Append('\n');
        }
        return result.ToString();
    }

    private void BuildUI()
    {
        if (canvasRoot != null)
            return;

        ResolveFont();
        canvasRoot = new GameObject(
            "Daily Alien Newspaper UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        audioSource = canvasRoot.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        BuildButton();
        BuildPaper();
        overlayRoot.SetActive(false);
        canvasRoot.SetActive(false);
    }

    private void BuildButton()
    {
        newspaperButtonRoot = CreateImageObject(
            "Newspaper Button",
            canvasRoot.transform,
            new Color(0.94f, 0.85f, 0.62f, 1f));
        newspaperButtonRect = newspaperButtonRoot.GetComponent<RectTransform>();
        newspaperButtonRect.anchorMin = new Vector2(0f, 0f);
        newspaperButtonRect.anchorMax = new Vector2(0f, 0f);
        newspaperButtonRect.pivot = new Vector2(0f, 0f);
        newspaperButtonRect.anchoredPosition = new Vector2(34f, 34f);
        newspaperButtonRect.sizeDelta = new Vector2(230f, 76f);
        UnityEngine.UI.Outline outline =
            newspaperButtonRoot.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.16f, 0.12f, 0.08f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);

        Button button = newspaperButtonRoot.AddComponent<Button>();
        newspaperButtonRoot.AddComponent<ButtonAnimator>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
        colors.pressedColor = new Color(0.83f, 0.72f, 0.50f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        button.colors = colors;
        button.onClick.AddListener(() => manager?.OpenCurrentIssue());

        newspaperButtonText = CreateText(
            "Label",
            newspaperButtonRoot.transform,
            "◆  TODAY'S NEWS",
            25,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.13f, 0.09f, 0.06f, 1f));
        Stretch(newspaperButtonText.rectTransform, 10f, 8f, 10f, 8f);
    }

    private void BuildPaper()
    {
        overlayRoot = CreateImageObject(
            "Newspaper Overlay",
            canvasRoot.transform,
            new Color(0.02f, 0.04f, 0.08f, 0f));
        Stretch(overlayRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        dimmer = overlayRoot.GetComponent<Image>();

        GameObject paper = CreateImageObject(
            "Newspaper Paper",
            overlayRoot.transform,
            new Color(0.97f, 0.93f, 0.80f, 1f));
        paperImage = paper.GetComponent<Image>();
        paperRect = paper.GetComponent<RectTransform>();
        paperRect.anchorMin = new Vector2(0.5f, 0.5f);
        paperRect.anchorMax = new Vector2(0.5f, 0.5f);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperRect.anchoredPosition = Vector2.zero;
        paperRect.sizeDelta = new Vector2(760f, 940f);
        Shadow shadow = paper.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(12f, -14f);
        UnityEngine.UI.Outline border = paper.AddComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(0.18f, 0.13f, 0.08f, 1f);
        border.effectDistance = new Vector2(3f, -3f);

        mastheadText = CreateText(
            "Masthead",
            paper.transform,
            "THE GALACTIC GAZETTE",
            42,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.12f, 0.09f, 0.06f, 1f));
        SetAnchored(mastheadText.rectTransform, new Vector2(0.045f, 0.89f), new Vector2(0.955f, 0.978f));

        issueText = CreateText(
            "Issue Line",
            paper.transform,
            string.Empty,
            18,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.42f, 0.12f, 0.10f, 1f));
        SetAnchored(issueText.rectTransform, new Vector2(0.075f, 0.845f), new Vector2(0.925f, 0.89f));

        GameObject divider = CreateImageObject(
            "Masthead Divider",
            paper.transform,
            new Color(0.22f, 0.12f, 0.08f, 1f));
        SetAnchored(divider.GetComponent<RectTransform>(), new Vector2(0.05f, 0.835f), new Vector2(0.95f, 0.841f));

        GameObject dividerThin = CreateImageObject(
            "Masthead Thin Rule",
            paper.transform,
            new Color(0.22f, 0.12f, 0.08f, 1f));
        SetAnchored(dividerThin.GetComponent<RectTransform>(), new Vector2(0.05f, 0.826f), new Vector2(0.95f, 0.829f));

        headlineText = CreateText(
            "Headline",
            paper.transform,
            string.Empty,
            30,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.08f, 0.07f, 0.06f, 1f));
        headlineText.resizeTextForBestFit = true;
        headlineText.resizeTextMinSize = 20;
        headlineText.resizeTextMaxSize = 32;
        SetAnchored(headlineText.rectTransform, new Vector2(0.055f, 0.705f), new Vector2(0.945f, 0.82f));

        bylineText = CreateText(
            "Byline",
            paper.transform,
            string.Empty,
            17,
            FontStyle.Italic,
            TextAnchor.MiddleCenter,
            new Color(0.27f, 0.22f, 0.18f, 1f));
        SetAnchored(bylineText.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.708f));

        BuildScrollView(paper.transform);
        BuildCloseButton(paper.transform);
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject scrollObject = new GameObject(
            "Newspaper Scroll",
            typeof(RectTransform),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetAnchored(scrollRectTransform, new Vector2(0.055f, 0.115f), new Vector2(0.945f, 0.66f));

        GameObject viewport = CreateImageObject(
            "Viewport",
            scrollObject.transform,
            new Color(1f, 1f, 1f, 0.025f));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 0f, 0f, 0f, 0f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        bodyText = CreateText(
            "Article Content",
            viewport.transform,
            string.Empty,
            19,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.10f, 0.085f, 0.07f, 1f));
        RectTransform contentRect = bodyText.rectTransform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(-22f, 1100f);
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.supportRichText = true;
        bodyText.lineSpacing = 1.05f;
        ContentSizeFitter fitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        articleScroll = scrollObject.GetComponent<ScrollRect>();
        articleScroll.viewport = viewportRect;
        articleScroll.content = contentRect;
        articleScroll.horizontal = false;
        articleScroll.vertical = true;
        articleScroll.movementType = ScrollRect.MovementType.Elastic;
        articleScroll.elasticity = 0.08f;
        articleScroll.scrollSensitivity = 32f;
        articleScroll.inertia = true;
        articleScroll.decelerationRate = 0.12f;

        BuildArchiveButtons(parent);
    }

    private void BuildArchiveButtons(Transform parent)
    {
        previousButton = CreateFooterButton(
            "Previous Issue",
            parent,
            new Vector2(0.055f, 0.025f),
            new Vector2(0.30f, 0.095f),
            "‹ PREVIOUS",
            out previousLabel);
        previousButton.onClick.AddListener(() => OpenAdjacentIssue(-1));

        nextButton = CreateFooterButton(
            "Next Issue",
            parent,
            new Vector2(0.70f, 0.025f),
            new Vector2(0.945f, 0.095f),
            "NEXT ›",
            out nextLabel);
        nextButton.onClick.AddListener(() => OpenAdjacentIssue(1));
    }

    private Button CreateFooterButton(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string labelValue,
        out Text label)
    {
        GameObject root = CreateImageObject(
            objectName,
            parent,
            new Color(0.28f, 0.20f, 0.13f, 1f));
        SetAnchored(root.GetComponent<RectTransform>(), anchorMin, anchorMax);
        Button button = root.AddComponent<Button>();
        root.AddComponent<ButtonAnimator>();
        label = CreateText(
            "Label",
            root.transform,
            labelValue,
            18,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white);
        Stretch(label.rectTransform, 8f, 4f, 8f, 4f);
        return button;
    }

    private void OpenAdjacentIssue(int direction)
    {
        if (manager == null || manager.NewspaperIssues == null)
            return;
        int currentIndex = -1;
        for (int i = 0; i < manager.NewspaperIssues.Count; i++)
        {
            NewspaperIssueSaveEntry issue = manager.NewspaperIssues[i];
            if (issue != null && issue.day == displayedIssueDay)
            {
                currentIndex = i;
                break;
            }
        }
        int nextIndex = currentIndex + direction;
        if (nextIndex < 0 || nextIndex >= manager.NewspaperIssues.Count)
            return;
        NewspaperIssueSaveEntry next = manager.NewspaperIssues[nextIndex];
        if (next == null)
            return;
        Populate(next);
        PlayPaperSound(settings != null ? settings.pageTurnSound : null, ref fallbackPageTurn,
            "Newspaper Page Turn", 0.18f, 0.10f, false);
        RefreshArchiveButtons();
    }

    private void RefreshArchiveButtons()
    {
        if (manager == null || manager.NewspaperIssues == null)
            return;
        int index = -1;
        for (int i = 0; i < manager.NewspaperIssues.Count; i++)
        {
            NewspaperIssueSaveEntry issue = manager.NewspaperIssues[i];
            if (issue != null && issue.day == displayedIssueDay)
            {
                index = i;
                break;
            }
        }
        if (previousButton != null)
            previousButton.interactable = index > 0;
        if (nextButton != null)
            nextButton.interactable = index >= 0 && index < manager.NewspaperIssues.Count - 1;
        if (previousLabel != null)
            previousLabel.color = previousButton != null && previousButton.interactable
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
        if (nextLabel != null)
            nextLabel.color = nextButton != null && nextButton.interactable
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
    }

    private void BuildCloseButton(Transform parent)
    {
        GameObject closeObject = CreateImageObject(
            "Close Newspaper",
            parent,
            new Color(0.70f, 0.13f, 0.12f, 1f));
        RectTransform rect = closeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(62f, 62f);
        closeButton = closeObject.AddComponent<Button>();
        closeObject.AddComponent<ButtonAnimator>();
        closeButton.onClick.AddListener(Close);
        Text label = CreateText(
            "X",
            closeObject.transform,
            "×",
            42,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white);
        Stretch(label.rectTransform, 0f, 1f, 0f, 5f);
    }

    private void ResolveFont()
    {
        if (newspaperFont != null)
            return;
        newspaperFont = Font.CreateDynamicFontFromOSFont("Times New Roman", 32);
        if (newspaperFont == null)
            newspaperFont = Font.CreateDynamicFontFromOSFont("Liberation Serif", 32);
        if (newspaperFont == null)
            newspaperFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void PlayPaperSound(
        AudioClip authoredClip,
        ref AudioClip fallbackClip,
        string fallbackName,
        float duration,
        float impact,
        bool heavyImpact)
    {
        if (audioSource == null)
            return;

        AudioClip clip = authoredClip;
        bool allowFallback = settings == null || settings.useProceduralPaperSoundsWhenClipsAreMissing;
        if (clip == null && allowFallback)
        {
            if (fallbackClip == null)
                fallbackClip = CreatePaperSound(fallbackName, duration, impact, heavyImpact);
            clip = fallbackClip;
        }
        if (clip == null)
            return;

        float volume = settings != null ? settings.newspaperSoundVolume : 0.22f;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip CreatePaperSound(
        string clipName,
        float duration,
        float impact,
        bool heavyImpact)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.Max(64, Mathf.CeilToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(clipName.GetHashCode());
        float filteredNoise = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, noise, heavyImpact ? 0.18f : 0.42f);
            float envelope = heavyImpact
                ? Mathf.Exp(-t * 9f)
                : Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * (1f - t * 0.35f);
            float lowImpact = heavyImpact
                ? Mathf.Sin(t * Mathf.PI * 16f) * Mathf.Exp(-t * 12f)
                : 0f;
            samples[i] = Mathf.Clamp(
                (filteredNoise * envelope + lowImpact * 0.35f) * impact,
                -1f,
                1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        string value,
        int size,
        FontStyle style,
        TextAnchor alignment,
        Color color)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        root.transform.SetParent(parent, false);
        Text text = root.GetComponent<Text>();
        text.font = newspaperFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateImageObject(string objectName, Transform parent, Color color)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = color;
        return root;
    }

    private static void SetAnchored(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = Mathf.Clamp01(value) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
