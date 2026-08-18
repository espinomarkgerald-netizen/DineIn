using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Small responsive pause overlay used by the single-scene restaurant.</summary>
public sealed class LobbyPauseMenu : MonoBehaviour
{
    private const string GameMenuSceneName = "NewGameMenu";
    private const string PausePrefabResourceName = "LobbyPauseMenu";

    private GameObject overlay;
    private Button pauseButton;
    private bool paused;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        paused = false;
        previousTimeScale = 1f;
        Time.timeScale = 1f;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (paused)
            Time.timeScale = 1f;
    }

    private void LateUpdate()
    {
        if (paused || pauseButton == null)
            return;

        bool loading = SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
        bool shouldShow = !loading && !GameplayUIBlocker.IsBlocked();
        if (pauseButton.gameObject.activeSelf != shouldShow)
            pauseButton.gameObject.SetActive(shouldShow);
    }

    private void BuildUI()
    {
        GameObject prefab = Resources.Load<GameObject>(PausePrefabResourceName);
        GameObject canvasObject = prefab != null
            ? Instantiate(prefab, transform, false)
            : CreateVisualTree(transform);

        LobbyPauseMenuView view = canvasObject.GetComponent<LobbyPauseMenuView>();
        if (view == null || view.PauseButton == null || view.Overlay == null ||
            view.ResumeButton == null || view.GameMenuButton == null)
        {
            Debug.LogError("[LobbyPauseMenu] Pause prefab references are incomplete.", canvasObject);
            return;
        }

        pauseButton = view.PauseButton;
        overlay = view.Overlay;
        pauseButton.onClick.RemoveListener(Pause);
        pauseButton.onClick.AddListener(Pause);
        view.ResumeButton.onClick.RemoveListener(Resume);
        view.ResumeButton.onClick.AddListener(Resume);
        view.GameMenuButton.onClick.RemoveListener(ReturnToGameMenu);
        view.GameMenuButton.onClick.AddListener(ReturnToGameMenu);
        overlay.SetActive(false);
    }

    /// <summary>Creates the visual tree used once by the editor prefab installer.</summary>
    public static GameObject CreateVisualTree(Transform parent = null)
    {
        GameObject canvasObject = new GameObject("LobbyPauseMenu", typeof(RectTransform));
        if (parent != null)
            canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Button createdPauseButton = CreateButton(canvasObject.transform, "PauseButton", "II",
            new Color(0.04f, 0.19f, 0.31f, 0.96f));
        RectTransform pauseRect = (RectTransform)createdPauseButton.transform;
        pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = new Vector2(1f, 1f);
        pauseRect.anchoredPosition = new Vector2(-34f, -34f);
        pauseRect.sizeDelta = new Vector2(72f, 64f);

        GameObject createdOverlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
        createdOverlay.transform.SetParent(canvasObject.transform, false);
        Stretch((RectTransform)createdOverlay.transform);
        createdOverlay.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.06f, 0.78f);

        GameObject window = new GameObject("PauseWindow", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(createdOverlay.transform, false);
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(520f, 340f);
        window.GetComponent<Image>().color = new Color(0.07f, 0.22f, 0.34f, 1f);

        TMP_Text title = CreateText(window.transform, "Title", "PAUSED", 48f);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(-40f, 70f);

        Button resume = CreateButton(window.transform, "ResumeButton", "RESUME",
            new Color(0.12f, 0.59f, 0.84f, 1f));
        RectTransform resumeRect = (RectTransform)resume.transform;
        resumeRect.anchorMin = resumeRect.anchorMax = resumeRect.pivot = new Vector2(0.5f, 0.5f);
        resumeRect.anchoredPosition = new Vector2(0f, 18f);
        resumeRect.sizeDelta = new Vector2(350f, 72f);

        Button gameMenu = CreateButton(window.transform, "GameMenuButton", "GAME MENU",
            new Color(0.77f, 0.19f, 0.22f, 1f));
        RectTransform menuRect = (RectTransform)gameMenu.transform;
        menuRect.anchorMin = menuRect.anchorMax = menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.anchoredPosition = new Vector2(0f, -82f);
        menuRect.sizeDelta = new Vector2(350f, 72f);

        LobbyPauseMenuView view = canvasObject.AddComponent<LobbyPauseMenuView>();
        view.Configure(createdPauseButton, createdOverlay, resume, gameMenu);
        createdOverlay.SetActive(false);
        return canvasObject;
    }

    private void Pause()
    {
        if (paused || Time.timeScale <= 0f)
            return;

        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        overlay.SetActive(true);
        pauseButton.gameObject.SetActive(false);
    }

    private void Resume()
    {
        if (!paused)
            return;

        paused = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        overlay.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    private void ReturnToGameMenu()
    {
        paused = false;
        Time.timeScale = 1f;
        GameSaveManager.Instance?.SaveGame();

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(GameMenuSceneName);
        else
            SceneManager.LoadSceneAsync(GameMenuSceneName, LoadSceneMode.Single);
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(CanvasGroup));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        buttonObject.AddComponent<ButtonAnimator>();

        TMP_Text text = CreateText(buttonObject.transform, "Label", label, 30f);
        Stretch((RectTransform)text.transform);
        text.margin = new Vector4(12f, 4f, 12f, 4f);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
