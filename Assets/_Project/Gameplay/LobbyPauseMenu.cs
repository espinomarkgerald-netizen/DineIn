using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Responsive, prefab-styled pause and settings overlay for Casual Dining.</summary>
public sealed class LobbyPauseMenu : MonoBehaviour
{
    private const string GameMenuSceneName = "NewGameMenu";
    private const string PausePrefabResourceName = "LobbyPauseMenu";
    private const string MusicPreference = "Settings_MusicVolume";
    private const string SfxPreference = "Settings_SfxVolume";

    private GameObject overlay;
    private RectTransform pauseWindow;
    private Button pauseButton;
    private Button largeTextButton;
    private Button reducedMotionButton;
    private Button highContrastButton;
    private Slider musicSlider;
    private Slider sfxSlider;
    private TMP_Text musicValue;
    private TMP_Text sfxValue;
    private Sprite frameSprite;
    private Sprite sliderHandleSprite;
    private TMP_FontAsset uiFont;
    private AudioMixer audioMixer;
    private AudioMixerGroup sfxMixerGroup;
    private float defaultMusicVolume = 1f;
    private float defaultSfxVolume = 0.5f;
    private Color buttonColor;
    private Color toggleColor;
    private Color dangerColor;
    private Color trackColor;
    private Color fillColor;
    private Color buttonHighlightTint = Color.white;
    private Color buttonPressedTint = new Color(0.72f, 0.88f, 0.95f, 1f);
    private bool paused;
    private bool usingCombinedHudView;
    private LobbyPauseMenuView combinedHudView;
    private float previousTimeScale = 1f;
    private Coroutine openRoutine;

    private void Awake()
    {
        paused = false;
        previousTimeScale = 1f;
        Time.timeScale = 1f;
        BuildUI();
    }

    private void OnDestroy()
    {
        LevelOneUIAccessibility.SettingsChanged -= RefreshAccessibilityLabels;
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
        if (paused) Time.timeScale = 1f;
        if (usingCombinedHudView && combinedHudView != null)
        {
            Transform generatedSettings = pauseWindow != null
                ? pauseWindow.Find("SettingsContent")
                : null;
            if (generatedSettings != null)
                Destroy(generatedSettings.gameObject);
            LobbyHUDRoot.Instance?.ReleasePauseMenuView(combinedHudView);
        }
    }

    private void LateUpdate()
    {
        if (paused || pauseButton == null) return;
        bool loading = SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
        bool shouldShow = !loading && !GameplayUIBlocker.IsBlocked();
        if (pauseButton.gameObject.activeSelf != shouldShow)
            pauseButton.gameObject.SetActive(shouldShow);
    }

    private void BuildUI()
    {
        LobbyHUDRoot combinedRoot = LobbyHUDRoot.EnsureInstance();
        combinedHudView = combinedRoot != null ? combinedRoot.AcquirePauseMenuView() : null;
        usingCombinedHudView = combinedHudView != null;

        GameObject canvasObject;
        LobbyPauseMenuView view;
        if (usingCombinedHudView)
        {
            view = combinedHudView;
            canvasObject = view.gameObject;
        }
        else
        {
            GameObject prefab = Resources.Load<GameObject>(PausePrefabResourceName);
            canvasObject = prefab != null
                ? Instantiate(prefab, transform, false)
                : CreateVisualTree(transform);
            view = canvasObject.GetComponent<LobbyPauseMenuView>();
        }
        if (view == null || view.PauseButton == null || view.Overlay == null ||
            view.ResumeButton == null || view.GameMenuButton == null)
        {
            Debug.LogError("[LobbyPauseMenu] Pause prefab references are incomplete.", canvasObject);
            return;
        }

        pauseButton = view.PauseButton;
        overlay = view.Overlay;
        pauseWindow = overlay.transform.Find("PauseWindow") as RectTransform;
        frameSprite = view.NineSlicedFrame;
        sliderHandleSprite = view.SliderHandle;
        uiFont = view.Font;
        audioMixer = view.AudioMixer;
        sfxMixerGroup = view.SfxMixerGroup;
        defaultMusicVolume = view.DefaultMusicVolume;
        defaultSfxVolume = view.DefaultSfxVolume;
        buttonColor = view.ButtonColor;
        toggleColor = view.ToggleColor;
        dangerColor = view.DangerColor;
        trackColor = view.TrackColor;
        fillColor = view.FillColor;
        buttonHighlightTint = view.ButtonHighlightTint;
        buttonPressedTint = view.ButtonPressedTint;
        pauseButton.onClick.RemoveListener(Pause);
        pauseButton.onClick.AddListener(Pause);
        view.ResumeButton.onClick.RemoveListener(Resume);
        view.ResumeButton.onClick.AddListener(Resume);
        view.GameMenuButton.onClick.RemoveListener(ReturnToGameMenu);
        view.GameMenuButton.onClick.AddListener(ReturnToGameMenu);

        StyleBaseVisuals(view);
        BuildSettingsControls(view);
        LoadAndApplyAudioSettings();
        RouteUnassignedSoundEffects();
        overlay.SetActive(false);
    }

    private void StyleBaseVisuals(LobbyPauseMenuView view)
    {
        if (pauseWindow != null)
        {
            pauseWindow.sizeDelta = view.SettingsWindowSize;
            ApplySlicedStyle(pauseWindow.GetComponent<Image>(), view.WindowColor);
        }
        StyleButton(view.ResumeButton, buttonColor);
        StyleButton(view.GameMenuButton, dangerColor);
        StyleButton(view.PauseButton, buttonColor);
        ApplyFont(view.transform);
    }

    private void BuildSettingsControls(LobbyPauseMenuView view)
    {
        if (pauseWindow == null || pauseWindow.Find("SettingsContent") != null) return;
        Transform window = pauseWindow;

        RectTransform title = FindRect(window, "Title");
        if (title != null)
        {
            title.anchoredPosition = new Vector2(0f, -28f);
            title.sizeDelta = new Vector2(-60f, 68f);
        }
        RectTransform resumeRect = view.ResumeButton.transform as RectTransform;
        if (resumeRect != null)
        {
            resumeRect.anchoredPosition = view.ResumePosition;
            resumeRect.sizeDelta = view.ResumeSize;
        }
        RectTransform menuRect = view.GameMenuButton.transform as RectTransform;
        if (menuRect != null)
        {
            menuRect.anchoredPosition = view.GameMenuPosition;
            menuRect.sizeDelta = view.GameMenuSize;
        }

        GameObject content = CreateUIObject("SettingsContent", window);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = view.SettingsContentSize;

        CreateSectionTitle(content.transform, "AudioTitle", "AUDIO", view.AudioTitleY);
        musicSlider = CreateVolumeRow(content.transform, "MusicVolume", "MUSIC", view.MusicRowY,
            view.SettingsRowSize, out musicValue);
        sfxSlider = CreateVolumeRow(content.transform, "SfxVolume", "SFX", view.SfxRowY,
            view.SettingsRowSize, out sfxValue);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);

        CreateSectionTitle(content.transform, "AccessibilityTitle", "ACCESSIBILITY", view.AccessibilityTitleY);
        largeTextButton = CreateAccessibilityButton(content.transform, "LargeTextButton", view.LargeTextRowY,
            view.SettingsRowSize,
            () => LevelOneUIAccessibility.SetLargeTextEnabled(!LevelOneUIAccessibility.LargeText));
        reducedMotionButton = CreateAccessibilityButton(content.transform, "ReducedMotionButton", view.ReducedMotionRowY,
            view.SettingsRowSize,
            () => LevelOneUIAccessibility.SetReducedMotionEnabled(!LevelOneUIAccessibility.ReducedMotion));
        highContrastButton = CreateAccessibilityButton(content.transform, "HighContrastButton", view.HighContrastRowY,
            view.SettingsRowSize,
            () => LevelOneUIAccessibility.SetHighContrastEnabled(!LevelOneUIAccessibility.HighContrast));

        LevelOneUIAccessibility.SettingsChanged -= RefreshAccessibilityLabels;
        LevelOneUIAccessibility.SettingsChanged += RefreshAccessibilityLabels;
        RefreshAccessibilityLabels();
        ApplyFont(window);
    }

    private Slider CreateVolumeRow(Transform parent, string objectName, string label, float y,
        Vector2 rowSize, out TMP_Text valueText)
    {
        GameObject row = CreateUIObject(objectName, parent, typeof(Image));
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = rowSize;
        ApplySlicedStyle(row.GetComponent<Image>(), new Color(toggleColor.r, toggleColor.g, toggleColor.b, 0.82f));

        TMP_Text nameText = CreateText(row.transform, "Label", label, 23f);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(20f, 0f);
        nameRect.sizeDelta = new Vector2(120f, 42f);
        nameText.alignment = TextAlignmentOptions.Left;

        GameObject track = CreateUIObject("Slider", row.transform, typeof(Image), typeof(Slider));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0f, 0.5f);
        trackRect.anchoredPosition = new Vector2(153f, 0f);
        trackRect.sizeDelta = new Vector2(280f, 24f);
        Image trackImage = track.GetComponent<Image>();
        ApplySlicedStyle(trackImage, trackColor);

        GameObject fillArea = CreateUIObject("Fill Area", track.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(5f, 5f);
        fillAreaRect.offsetMax = new Vector2(-5f, -5f);
        GameObject fill = CreateUIObject("Fill", fillArea.transform, typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect);
        ApplySlicedStyle(fill.GetComponent<Image>(), fillColor);

        GameObject handleArea = CreateUIObject("Handle Slide Area", track.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);
        GameObject handle = CreateUIObject("Handle", handleArea.transform, typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(36f, 42f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = sliderHandleSprite != null ? sliderHandleSprite : frameSprite;
        handleImage.preserveAspect = true;
        handleImage.color = Color.white;

        Slider slider = track.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        valueText = CreateText(row.transform, "Value", "100%", 22f);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-18f, 0f);
        valueRect.sizeDelta = new Vector2(82f, 42f);
        valueText.alignment = TextAlignmentOptions.Right;
        return slider;
    }

    private void CreateSectionTitle(Transform parent, string objectName, string label, float y)
    {
        TMP_Text title = CreateText(parent, objectName, label, 25f);
        RectTransform rect = title.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(540f, 40f);
        title.color = new Color(0.77f, 0.92f, 1f, 1f);
    }

    private Button CreateAccessibilityButton(Transform parent, string objectName, float y, Vector2 rowSize,
        UnityEngine.Events.UnityAction onClick)
    {
        Button button = CreateButton(parent, objectName, string.Empty, toggleColor);
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(rowSize.x, Mathf.Max(48f, rowSize.y - 2f));
        button.onClick.AddListener(onClick);
        return button;
    }

    private void LoadAndApplyAudioSettings()
    {
        float music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicPreference, defaultMusicVolume));
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxPreference, defaultSfxVolume));
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
        ApplyMixerVolume("MusicVol", music);
        ApplyMixerVolume("SFXVol", sfx);
        RefreshVolumeLabel(musicValue, music);
        RefreshVolumeLabel(sfxValue, sfx);
    }

    private void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicPreference, value);
        PlayerPrefs.Save();
        ApplyMixerVolume("MusicVol", value);
        RefreshVolumeLabel(musicValue, value);
        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.SetMusicVolume(value);
    }

    private void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxPreference, value);
        PlayerPrefs.Save();
        ApplyMixerVolume("SFXVol", value);
        RefreshVolumeLabel(sfxValue, value);
        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.SetSfxVolume(value);
    }

    private void ApplyMixerVolume(string parameter, float value)
    {
        if (audioMixer == null) return;
        float decibels = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        audioMixer.SetFloat(parameter, decibels);
    }

    private static void RefreshVolumeLabel(TMP_Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void RouteUnassignedSoundEffects()
    {
        if (sfxMixerGroup == null) return;
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource source in sources)
        {
            if (source != null && source.outputAudioMixerGroup == null)
                source.outputAudioMixerGroup = sfxMixerGroup;
        }
    }

    private void Pause()
    {
        if (paused || Time.timeScale <= 0f) return;
        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        overlay.SetActive(true);
        pauseButton.gameObject.SetActive(false);
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(AnimateWindowOpen());
    }

    private IEnumerator AnimateWindowOpen()
    {
        if (pauseWindow == null) yield break;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            pauseWindow.localScale = Vector3.one;
            yield break;
        }
        float elapsed = 0f;
        const float duration = 0.24f;
        pauseWindow.localScale = Vector3.one * 0.88f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.LerpUnclamped(0.88f, 1.03f, eased);
            if (t > 0.72f) scale = Mathf.Lerp(1.03f, 1f, (t - 0.72f) / 0.28f);
            pauseWindow.localScale = Vector3.one * scale;
            yield return null;
        }
        pauseWindow.localScale = Vector3.one;
        openRoutine = null;
    }

    private void Resume()
    {
        if (!paused) return;
        paused = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        overlay.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    private void ReturnToGameMenu()
    {
        paused = false;
        Time.timeScale = 1f;
        GameSaveManager.Instance?.RestoreDayStartCheckpoint();
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(GameMenuSceneName);
        else SceneManager.LoadSceneAsync(GameMenuSceneName, LoadSceneMode.Single);
    }

    private void RefreshAccessibilityLabels()
    {
        SetButtonLabel(largeTextButton, $"LARGE TEXT    {OnOff(LevelOneUIAccessibility.LargeText)}");
        SetButtonLabel(reducedMotionButton, $"REDUCED MOTION    {OnOff(LevelOneUIAccessibility.ReducedMotion)}");
        SetButtonLabel(highContrastButton, $"HIGH CONTRAST    {OnOff(LevelOneUIAccessibility.HighContrast)}");
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private static string OnOff(bool enabled) => enabled ? "ON" : "OFF";

    private void StyleButton(Button button, Color color)
    {
        if (button == null) return;
        ApplySlicedStyle(button.GetComponent<Image>(), color);
        ColorBlock stateColors = button.colors;
        stateColors.normalColor = Color.white;
        stateColors.highlightedColor = buttonHighlightTint;
        stateColors.selectedColor = buttonHighlightTint;
        stateColors.pressedColor = buttonPressedTint;
        stateColors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
        stateColors.colorMultiplier = 1f;
        stateColors.fadeDuration = 0.08f;
        button.colors = stateColors;
        button.GetComponent<ButtonAnimator>()?.SetBaseColor(color);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && uiFont != null) label.font = uiFont;
    }

    private void ApplySlicedStyle(Image image, Color color)
    {
        if (image == null) return;
        image.sprite = frameSprite;
        image.type = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
    }

    private void ApplyFont(Transform root)
    {
        if (root == null || uiFont == null) return;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = uiFont;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent, typeof(Image), typeof(Button), typeof(CanvasGroup));
        Image image = buttonObject.GetComponent<Image>();
        ApplySlicedStyle(image, color);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        buttonObject.AddComponent<ButtonAnimator>();
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 25f);
        Stretch(text.rectTransform);
        text.margin = new Vector4(18f, 4f, 18f, 4f);
        return button;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, float size)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform FindRect(Transform parent, string name) => parent.Find(name) as RectTransform;

    private static GameObject CreateUIObject(string objectName, Transform parent, params System.Type[] components)
    {
        System.Type[] all = new System.Type[components.Length + 1];
        all[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++) all[i + 1] = components[i];
        GameObject created = new GameObject(objectName, all);
        created.layer = 5;
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Fallback tree used only if the authored resource prefab is unavailable.</summary>
    public static GameObject CreateVisualTree(Transform parent = null)
    {
        GameObject canvasObject = new GameObject("LobbyPauseMenu", typeof(RectTransform));
        if (parent != null) canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Button pause = CreateFallbackButton(canvasObject.transform, "PauseButton", "II", new Color(0.04f, 0.64f, 0.88f, 1f));
        RectTransform pauseRect = pause.transform as RectTransform;
        pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = new Vector2(0f, 1f);
        pauseRect.anchoredPosition = new Vector2(149f, -28f);
        pauseRect.sizeDelta = new Vector2(82f, 82f);

        GameObject createdOverlay = CreateUIObject("PauseOverlay", canvasObject.transform, typeof(Image));
        Stretch(createdOverlay.GetComponent<RectTransform>());
        createdOverlay.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.06f, 0.78f);
        GameObject window = CreateUIObject("PauseWindow", createdOverlay.transform, typeof(Image));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(720f, 880f);
        TMP_Text title = CreateFallbackText(window.transform, "Title", "PAUSED", 48f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(-60f, 68f);
        Button resume = CreateFallbackButton(window.transform, "ResumeButton", "RESUME", new Color(0.04f, 0.64f, 0.88f, 1f));
        Button menu = CreateFallbackButton(window.transform, "GameMenuButton", "GAME MENU", new Color(0.82f, 0.16f, 0.2f, 1f));
        LobbyPauseMenuView view = canvasObject.AddComponent<LobbyPauseMenuView>();
        view.Configure(pause, createdOverlay, resume, menu);
        createdOverlay.SetActive(false);
        return canvasObject;
    }

    private static Button CreateFallbackButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateFallbackText(buttonObject.transform, "Label", label, 25f);
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateFallbackText(Transform parent, string objectName, string value, float size)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
