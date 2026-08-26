using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>Serialized references belonging to the editable pause prefab.</summary>
public sealed class LobbyPauseMenuView : MonoBehaviour
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button gameMenuButton;

    [Header("Project Style Assets")]
    [SerializeField] private Sprite nineSlicedFrame;
    [SerializeField] private Sprite sliderHandle;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Editable Settings Colors")]
    [SerializeField] private Color windowColor = new Color(0.035f, 0.16f, 0.31f, 0.99f);
    [SerializeField] private Color buttonColor = new Color(0.04f, 0.64f, 0.88f, 1f);
    [SerializeField] private Color toggleColor = new Color(0.08f, 0.35f, 0.5f, 1f);
    [SerializeField] private Color dangerColor = new Color(0.82f, 0.16f, 0.2f, 1f);
    [SerializeField] private Color trackColor = new Color(0.75f, 0.84f, 0.9f, 1f);
    [SerializeField] private Color fillColor = new Color(0.04f, 0.64f, 0.88f, 1f);

    [Header("Editable HUD Layout")]
    [Tooltip("Position of the pause button's top-right corner on the 1920 x 1080 HUD canvas.")]
    [SerializeField] private Vector2 pauseButtonPosition = new Vector2(149f, -28f);
    [Tooltip("Pause button size. Change this in the LobbyPauseMenu prefab.")]
    [SerializeField] private Vector2 pauseButtonSize = new Vector2(82f, 82f);
    [Tooltip("Keeps the Day and Time text on the pause button's vertical centerline.")]
    [SerializeField] private bool alignDayAndTimeToPause = true;
    [SerializeField] private float dayTimeVerticalOffset;

    [Header("Editable Settings Layout (1920 x 1080)")]
    [SerializeField] private Vector2 settingsWindowSize = new Vector2(720f, 880f);
    [SerializeField] private Vector2 resumePosition = new Vector2(0f, 282f);
    [SerializeField] private Vector2 resumeSize = new Vector2(500f, 68f);
    [SerializeField] private Vector2 gameMenuPosition = new Vector2(0f, -354f);
    [SerializeField] private Vector2 gameMenuSize = new Vector2(500f, 68f);
    [SerializeField] private Vector2 settingsContentSize = new Vector2(610f, 570f);
    [SerializeField] private Vector2 settingsRowSize = new Vector2(540f, 58f);
    [SerializeField] private float audioTitleY = 205f;
    [SerializeField] private float musicRowY = 145f;
    [SerializeField] private float sfxRowY = 72f;
    [SerializeField] private float accessibilityTitleY = -5f;
    [SerializeField] private float largeTextRowY = -68f;
    [SerializeField] private float reducedMotionRowY = -139f;
    [SerializeField] private float highContrastRowY = -210f;

    public Button PauseButton => pauseButton;
    public GameObject Overlay => overlay;
    public Button ResumeButton => resumeButton;
    public Button GameMenuButton => gameMenuButton;
    public Sprite NineSlicedFrame => nineSlicedFrame;
    public Sprite SliderHandle => sliderHandle;
    public TMP_FontAsset Font => font;
    public AudioMixer AudioMixer => audioMixer;
    public AudioMixerGroup SfxMixerGroup => sfxMixerGroup;
    public Color WindowColor => windowColor;
    public Color ButtonColor => buttonColor;
    public Color ToggleColor => toggleColor;
    public Color DangerColor => dangerColor;
    public Color TrackColor => trackColor;
    public Color FillColor => fillColor;
    public Vector2 SettingsWindowSize => settingsWindowSize;
    public Vector2 ResumePosition => resumePosition;
    public Vector2 ResumeSize => resumeSize;
    public Vector2 GameMenuPosition => gameMenuPosition;
    public Vector2 GameMenuSize => gameMenuSize;
    public Vector2 SettingsContentSize => settingsContentSize;
    public Vector2 SettingsRowSize => settingsRowSize;
    public float AudioTitleY => audioTitleY;
    public float MusicRowY => musicRowY;
    public float SfxRowY => sfxRowY;
    public float AccessibilityTitleY => accessibilityTitleY;
    public float LargeTextRowY => largeTextRowY;
    public float ReducedMotionRowY => reducedMotionRowY;
    public float HighContrastRowY => highContrastRowY;
    public bool AlignDayAndTimeToPause => alignDayAndTimeToPause;
    public float DayTimeVerticalOffset => dayTimeVerticalOffset;

    private void OnEnable()
    {
        ApplyPauseButtonLayout();
    }

    private void OnValidate()
    {
        pauseButtonSize.x = Mathf.Max(44f, pauseButtonSize.x);
        pauseButtonSize.y = Mathf.Max(44f, pauseButtonSize.y);
        settingsWindowSize.x = Mathf.Max(560f, settingsWindowSize.x);
        settingsWindowSize.y = Mathf.Max(720f, settingsWindowSize.y);
        settingsRowSize.x = Mathf.Max(420f, settingsRowSize.x);
        settingsRowSize.y = Mathf.Max(48f, settingsRowSize.y);
        ApplyPauseButtonLayout();
    }

    private void ApplyPauseButtonLayout()
    {
        if (pauseButton == null || pauseButton.transform is not RectTransform rect)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = pauseButtonPosition;
        rect.sizeDelta = pauseButtonSize;
    }

    public void Configure(Button pause, GameObject configuredOverlay, Button resume, Button gameMenu)
    {
        pauseButton = pause;
        overlay = configuredOverlay;
        resumeButton = resume;
        gameMenuButton = gameMenu;
    }
}
