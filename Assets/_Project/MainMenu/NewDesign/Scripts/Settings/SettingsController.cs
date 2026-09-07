using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro

public class SettingsController : MonoBehaviour
{
    [Header("Audio")]
    public Slider audioSlider;

    [Header("UI References")]
    public TMP_Text qualityDisplayText; // Drag your TMP object here
    private Slider musicSlider, sfxSlider;
    private TMP_Text musicPercent, sfxPercent;
    private readonly TMP_Text[] qualityLabels = new TMP_Text[3];
    private Toggle fpsToggle;
    private DineIn.NewMenu.SettingsManager settings;
    private TMP_FontAsset chalkFont;
    private Color chalk = new Color(1f, .97f, .89f);

    private const string PREF_VOLUME = "Settings_Volume";
    private const string PREF_QUALITY = "Settings_Quality";

    // Set to 1 ONLY when the player picks a quality level through this
    // script's own SetGraphicsQuality(). GameOptimizer reads this (not
    // PREF_QUALITY's mere existence) to decide whether to apply its own
    // platform default - see GameOptimizer.PREF_QUALITY_USER_SET.
    private const string PREF_QUALITY_USER_SET = "Settings_QualityUserSet";

    void Start()
    {
        if (gameObject.scene.name == "NewMainMenu")
        {
            settings = DineIn.NewMenu.SettingsManager.Instance;
            BuildChalkboard();
            if (settings != null) settings.OnSettingsLoaded += SyncSettings;
            SyncSettings(settings != null ? settings.Current : new DineIn.NewMenu.UserSettings());
            return;
        }
        // Load saved values BEFORE hooking up listeners, so setting the
        // slider value doesn't immediately re-trigger a save with defaults
        float savedVolume = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        int savedQuality = ClampQualityIndex(PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel()));

        AudioListener.volume = savedVolume;

        // Only force-apply the saved quality if the player actually chose
        // it before. Otherwise leave whatever GameOptimizer already set in
        // Awake() alone - this is display-only until the player picks one.
        if (PlayerPrefs.GetInt(PREF_QUALITY_USER_SET, 0) == 1)
        {
            ApplyQuality(savedQuality);
        }

        if (audioSlider != null)
        {
            audioSlider.SetValueWithoutNotify(savedVolume); // sync slider position without firing the event
            audioSlider.onValueChanged.AddListener(SetVolume);
        }

        UpdateGraphicsText();
    }

    public void SetVolume(float volume)
    {
        if (settings != null) { settings.SetMusicVolume(volume); return; }
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(PREF_VOLUME, volume);
        PlayerPrefs.Save();
        Debug.Log("Volume: " + volume);
    }

    public void SetGraphicsQuality(int qualityIndex)
    {
        int resolvedQuality = ClampQualityIndex(qualityIndex);
        ApplyQuality(resolvedQuality);

        PlayerPrefs.SetInt(PREF_QUALITY, resolvedQuality);
        PlayerPrefs.SetInt(PREF_QUALITY_USER_SET, 1); // this is the one true "player chose this" signal
        PlayerPrefs.Save();

        if (DineIn.NewMenu.SettingsManager.Instance != null)
            DineIn.NewMenu.SettingsManager.Instance.SetGraphicsQuality(resolvedQuality);

        UpdateGraphicsText();
        Debug.Log("Graphics quality set to: " + QualitySettings.names[resolvedQuality]);
    }

    void UpdateGraphicsText()
    {
        for (int i = 0; i < qualityLabels.Length; i++)
            if (qualityLabels[i] != null)
            {
                string label = i == 0 ? "LOW" : i == 1 ? "MEDIUM" : "HIGH";
                qualityLabels[i].text = QualitySettings.GetQualityLevel() == i ? "<u>" + label + "</u>" : label;
                qualityLabels[i].color = QualitySettings.GetQualityLevel() == i ? chalk : new Color(.66f, .66f, .62f);
            }
        if (qualityDisplayText != null)
        {
            int quality = ClampQualityIndex(QualitySettings.GetQualityLevel());
            string[] names = QualitySettings.names;

            qualityDisplayText.text = "Current Quality: " + names[quality];
        }
    }

    public void CurrentGraphics()
    {
        int quality = ClampQualityIndex(QualitySettings.GetQualityLevel());
        string[] names = QualitySettings.names;

        Debug.Log("Current quality: " + names[quality]);
    }

    private void OnDestroy()
    {
        if (settings != null) settings.OnSettingsLoaded -= SyncSettings;
        if (audioSlider != null) audioSlider.onValueChanged.RemoveListener(SetVolume);
    }

    private void SyncSettings(DineIn.NewMenu.UserSettings value)
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(value.musicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(value.sfxVolume);
        if (musicPercent != null) musicPercent.text = Mathf.RoundToInt(value.musicVolume * 100) + "%";
        if (sfxPercent != null) sfxPercent.text = Mathf.RoundToInt(value.sfxVolume * 100) + "%";
        if (fpsToggle != null) fpsToggle.SetIsOnWithoutNotify(value.showFps);
        UpdateGraphicsText();
    }

    private void BuildChalkboard()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || qualityDisplayText == null) return;
        chalkFont = qualityDisplayText.font;
        Quaternion facing = qualityDisplayText.transform.rotation;
        float depth = canvas.transform.InverseTransformPoint(qualityDisplayText.transform.position).z;
        Button close = null;
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (button.name == "Options ExitButton") { close = button; break; }
        // Retain the authored chalk X and move it out of the retired contents.
        if (close != null) close.transform.SetParent(canvas.transform, true);
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        RectTransform root = NewRect("Chalk Settings", canvas.transform, Vector2.zero, new Vector2(800, 820));
        root.localScale = Vector3.one * .001f;
        root.rotation = facing;
        Vector3 position = root.localPosition; position.z = depth; root.localPosition = position;
        Text(root, "OPTIONS", new Vector2(0, 335), new Vector2(650, 90), 64, TextAlignmentOptions.Center);
        Text(root, "GRAPHICS", new Vector2(-190, 225), new Vector2(340, 65), 40);
        Text(root, "AUDIO", new Vector2(200, 225), new Vector2(330, 65), 40);
        Stroke(root, new Vector2(0, 275), new Vector2(710, 3));
        Stroke(root, new Vector2(0, -10), new Vector2(2, 420));
        Text(root, "Quality", new Vector2(-190, 150), new Vector2(340, 55), 33);
        for (int i = 0; i < 3; i++)
        {
            RectTransform rect = NewRect("Quality " + i, root, new Vector2(-310 + i * 116, 75), new Vector2(112, 72));
            Image hit = rect.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
            int index = i;
            button.onClick.AddListener(() => SetGraphicsQuality(index));
            qualityLabels[i] = Text(rect, "", Vector2.zero, new Vector2(110, 65), 26, TextAlignmentOptions.Center);
        }
        RectTransform toggleRect = NewRect("Show FPS", root, new Vector2(-190, -85), new Vector2(340, 80));
        Image toggleHit = toggleRect.gameObject.AddComponent<Image>(); toggleHit.color = new Color(1, 1, 1, .001f);
        fpsToggle = toggleRect.gameObject.AddComponent<Toggle>(); fpsToggle.targetGraphic = toggleHit;
        Text(toggleRect, "[   ]", new Vector2(-137, 0), new Vector2(75, 65), 39, TextAlignmentOptions.Center);
        TMP_Text check = Text(toggleRect, "X", new Vector2(-137, 0), new Vector2(50, 60), 32, TextAlignmentOptions.Center);
        fpsToggle.graphic = check;
        Text(toggleRect, "SHOW FPS", new Vector2(32, 0), new Vector2(245, 65), 31);
        fpsToggle.onValueChanged.AddListener(visible =>
        {
            PerformanceMonitor monitor = FindFirstObjectByType<PerformanceMonitor>(FindObjectsInactive.Include);
            if (monitor != null) monitor.ToggleVisibility(visible); else settings?.SetShowFps(visible);
        });
        Text(root, "MUSIC", new Vector2(180, 150), new Vector2(280, 55), 33);
        musicSlider = VolumeSlider(root, new Vector2(175, 70));
        musicPercent = Text(root, "", new Vector2(200, 10), new Vector2(290, 48), 29, TextAlignmentOptions.Right);
        musicSlider.onValueChanged.AddListener(value => settings?.SetMusicVolume(value));
        Text(root, "SFX", new Vector2(180, -80), new Vector2(280, 55), 33);
        sfxSlider = VolumeSlider(root, new Vector2(175, -160));
        sfxPercent = Text(root, "", new Vector2(200, -220), new Vector2(290, 48), 29, TextAlignmentOptions.Right);
        sfxSlider.onValueChanged.AddListener(value => settings?.SetSfxVolume(value));
        CameraRigController.PrepareCloseButton(close);
    }

    private Slider VolumeSlider(Transform parent, Vector2 position)
    {
        RectTransform root = NewRect("Volume", parent, position, new Vector2(300, 76));
        Image hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);
        Slider slider = root.gameObject.AddComponent<Slider>();
        Stroke(root, Vector2.zero, new Vector2(278, 4));
        RectTransform travel = NewRect("Handle Travel", root, Vector2.zero, new Vector2(278, 76));
        RectTransform handle = NewRect("Chalk Handle", travel, Vector2.zero, new Vector2(18, 38));
        Image mark = handle.gameObject.AddComponent<Image>(); mark.color = chalk;
        slider.handleRect = handle;
        slider.targetGraphic = mark;
        slider.minValue = 0; slider.maxValue = 1;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text text = NewRect(value, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = chalkFont; text.text = value; text.color = chalk; text.fontSize = fontSize;
        text.enableAutoSizing = true; text.fontSizeMin = fontSize * .75f; text.fontSizeMax = fontSize;
        text.alignment = alignment; text.raycastTarget = false;
        return text;
    }

    private void Stroke(Transform parent, Vector2 position, Vector2 size)
    {
        Image image = NewRect("Chalk Line", parent, position, size).gameObject.AddComponent<Image>();
        image.color = new Color(chalk.r, chalk.g, chalk.b, .65f); image.raycastTarget = false;
    }

    private static void ApplyQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex, true);
    }

    private static int ClampQualityIndex(int qualityIndex)
    {
        int qualityCount = QualitySettings.names.Length;
        return qualityCount == 0 ? 0 : Mathf.Clamp(qualityIndex, 0, qualityCount - 1);
    }
}
