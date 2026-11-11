using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class LoreIntro : MonoBehaviour
{
    private const string MenuScene = "NewMainMenu";
    private const double SkipTime = 2;
    private const float FadeDuration = 0.25f;
    private const float StartupTimeout = 10f;

    [SerializeField] private VideoClip clip;

    private VideoPlayer player;
    private RawImage picture;
    private AspectRatioFitter pictureFit;
    private Button skip;
    private CanvasGroup skipGroup;
    private RectTransform safeArea;
    private float playbackDeadline;
    private float revealTime = -1f;
    private bool hasFrame;
    private bool leaving;

    private void Start()
    {
        Camera screen = new GameObject("Lore Camera").AddComponent<Camera>();
        screen.transform.SetParent(transform, false);
        screen.clearFlags = CameraClearFlags.SolidColor;
        screen.backgroundColor = Color.black;
        screen.cullingMask = 0;

        if (clip == null)
        {
            Debug.LogWarning("[LoreIntro] No video clip is assigned.", this);
            Finish();
            return;
        }

        CreateIntroUI();
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.renderMode = VideoRenderMode.APIOnly;
        player.waitForFirstFrame = true;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        if (clip.audioTrackCount > 0)
        {
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
        }
        player.isLooping = false;
        player.prepareCompleted += PlayPreparedVideo;
        player.loopPointReached += CompleteVideo;
        player.errorReceived += HandleError;
        playbackDeadline = Time.realtimeSinceStartup + StartupTimeout;
        player.Prepare();
    }

    private void Update()
    {
        if (leaving || player == null)
            return;

        UpdateSafeArea();
        Texture frame = player.texture;
        if (player.frame >= 0 && frame != null)
        {
            if (picture.texture != frame)
            {
                pictureFit.aspectRatio = (float)frame.width / frame.height;
                picture.texture = frame;
            }
            picture.enabled = true;
            hasFrame = true;
        }
        if (!hasFrame && Time.realtimeSinceStartup >= playbackDeadline)
        {
            HandleError(player, "Playback did not start within 10 seconds.");
            return;
        }

        if (revealTime < 0f && hasFrame && player.time >= SkipTime)
        {
            revealTime = Time.unscaledTime;
            skipGroup.interactable = true;
            skipGroup.blocksRaycasts = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(skip.gameObject);
        }

        if (revealTime >= 0f)
            skipGroup.alpha = LevelOneUIAccessibility.ReducedMotion
                ? 1f
                : Mathf.Clamp01((Time.unscaledTime - revealTime) / FadeDuration);
    }

    private void CreateIntroUI()
    {
        var canvas = new GameObject("Lore UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform videoRect = CreateRect("Lore Video", canvas.transform);
        picture = videoRect.gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        picture.enabled = false;
        pictureFit = videoRect.gameObject.AddComponent<AspectRatioFitter>();
        pictureFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        safeArea = CreateRect("Safe Area", canvas.transform);
        UpdateSafeArea();

        RectTransform buttonRect = CreateRect("Skip", safeArea);
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-16f, 16f);
        buttonRect.sizeDelta = new Vector2(112f, 56f);
        var background = buttonRect.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        skip = buttonRect.gameObject.AddComponent<Button>();
        skip.onClick.AddListener(Finish);

        RectTransform labelRect = CreateRect("Label", buttonRect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 23;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "Skip";
        label.raycastTarget = false;
        var colors = skip.colors;
        colors.normalColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        skip.targetGraphic = label;
        skip.colors = colors;

        skipGroup = buttonRect.gameObject.AddComponent<CanvasGroup>();
        skipGroup.alpha = 0f;
        skipGroup.interactable = false;
        skipGroup.blocksRaycasts = false;

        if (EventSystem.current == null)
        {
            var input = new GameObject("Lore Input");
            input.transform.SetParent(transform, false);
            input.AddComponent<EventSystem>();
            input.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void UpdateSafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect area = Screen.safeArea;
        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }

    private void PlayPreparedVideo(VideoPlayer source)
    {
        if (!leaving)
            source.Play();
    }

    private void CompleteVideo(VideoPlayer source) => Finish();

    private void HandleError(VideoPlayer source, string message)
    {
        if (leaving)
            return;

        Debug.LogWarning("[LoreIntro] " + message, this);
        Finish();
    }

    private void Finish()
    {
        if (leaving)
            return;

        leaving = true;
        if (skipGroup != null)
            skipGroup.gameObject.SetActive(false);
        if (player != null)
            player.Stop();
        StartCoroutine(OpenMenu());
    }

    private IEnumerator OpenMenu()
    {
        while (SceneLoader.HasPendingLoad || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading))
            yield return null;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(MenuScene);
        else
            SceneLoader.TryLoadScene(MenuScene, LoadSceneMode.Single, out _);
    }

    private void OnDestroy()
    {
        if (player == null)
            return;

        player.prepareCompleted -= PlayPreparedVideo;
        player.loopPointReached -= CompleteVideo;
        player.errorReceived -= HandleError;
    }
}
