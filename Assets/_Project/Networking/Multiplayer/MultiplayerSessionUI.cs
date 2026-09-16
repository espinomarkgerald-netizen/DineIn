using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local presentation of the host's day request. There is no persistent session bar.
public sealed class MultiplayerSessionUI : MonoBehaviour
{
    private MultiplayerSessionManager session;
    private MultiplayerDayBridge day;
    private MultiplayerReadyStyle style;
    private LobbyPauseMenu pause;
    private GameObject canvasRoot;
    private RectTransform safeArea;
    private TMP_Text title, detail, readyLabel;
    private Button readyButton, cancelButton;
    private bool connectionWarning, endedWarning;
    private float nextRefresh;

    private void Start()
    {
        session = GetComponent<MultiplayerSessionManager>();
        day = GetComponent<MultiplayerDayBridge>();
        style = Resources.Load<MultiplayerReadyStyle>("UI/MultiplayerReadyStyle");
        pause = FindFirstObjectByType<LobbyPauseMenu>();
        BuildPrompt();
    }

    private void BuildPrompt()
    {
        canvasRoot = new GameObject("Multiplayer Day Ready", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var shade = new GameObject("Preparation Input Shield", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(canvasRoot.transform, false);
        Place(shade.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        shade.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.12f, 0.86f);
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(canvasRoot.transform, false);
        title = Text(safeArea, "Title", 80);
        Place(title.rectTransform, new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.88f));
        detail = Text(safeArea, "Status", 32);
        Place(detail.rectTransform, new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.68f));
        readyButton = MakeButton(safeArea, "Ready", 64, out readyLabel);
        Place(readyButton.GetComponent<RectTransform>(), new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.54f));
        readyButton.onClick.AddListener(() => day.ToggleLocalReady());
        cancelButton = MakeButton(safeArea, "Cancel Start", 30, out var cancelLabel);
        cancelLabel.text = "CANCEL START";
        Place(cancelButton.GetComponent<RectTransform>(), new Vector2(0.38f, 0.16f), new Vector2(0.62f, 0.28f));
        cancelButton.onClick.AddListener(() => day.CancelReadyRequest());
        var pauseButton = MakeButton(safeArea, "Pause", 28, out var pauseLabel);
        pauseLabel.text = "PAUSE";
        Place(pauseButton.GetComponent<RectTransform>(), new Vector2(0.84f, 0.84f), new Vector2(0.97f, 0.96f));
        pauseButton.onClick.AddListener(() =>
        {
            if (pause == null) pause = FindFirstObjectByType<LobbyPauseMenu>();
            pause?.OpenFromReadyPrompt();
        });
        canvasRoot.SetActive(false);
    }

    private void Update()
    {
        if (session == null || day == null || canvasRoot == null) return;
        ShowConnectionChanges();
        var request = day.Readiness;
        bool visible = request != null && !session.Ended && (pause == null || !pause.IsOpen);
        if (canvasRoot.activeSelf != visible)
        {
            canvasRoot.SetActive(visible);
            GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(canvasRoot, visible);
            nextRefresh = 0f;
        }
        if (!visible || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        Rect safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        bool host = session.IsHostConnection;
        string count = request.Count + "/" + request.actors.Length;
        if (request.CountingDown)
        {
            double remaining = request.deadline - PhotonNetwork.Time;
            title.text = remaining > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)remaining), 1, 3).ToString()
                : remaining < -2 ? "SYNCHRONIZING..." : "STARTING...";
            detail.text = "DAY " + request.day + "  •  " + count + " READY";
        }
        else
        {
            title.text = "START DAY " + request.day;
            detail.text = day.VotePending ? "Updating your readiness..."
                : host ? "Waiting for everyone to be ready."
                : day.LocalReady ? "You are ready. Tap again to keep preparing." : "The host is ready to start the day.";
        }
        readyLabel.text = (host ? "READY " : day.LocalReady ? "READY ✓ " : "READY ") + count;
        readyButton.interactable = !host && session.CanAct && !day.VotePending;
        cancelButton.gameObject.SetActive(host);
        cancelButton.interactable = session.CanAct;
    }

    private void ShowConnectionChanges()
    {
        if (WarningSlideUI.Instance == null) return;
        if (session.Ended && !endedWarning)
        {
            endedWarning = true;
            WarningSlideUI.Instance.Show(session.Status + " Open Pause to return to the menu.");
        }
        else if (!session.Ended && !session.IsConnected && !connectionWarning)
        {
            connectionWarning = true;
            WarningSlideUI.Instance.Show("Connection lost. Reconnecting to the restaurant...");
        }
        else if (!session.Ended && connectionWarning && session.CanAct)
        {
            connectionWarning = false;
            WarningSlideUI.Instance.Show("Reconnected to the restaurant.");
        }
    }

    private TMP_Text Text(Transform parent, string name, float size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TMP_Text>();
        text.font = style != null && style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.fontSizeMax = size; text.fontSizeMin = size * 0.55f;
        text.enableAutoSizing = true; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
        return text;
    }

    private Button MakeButton(Transform parent, string name, float fontSize, out TMP_Text label)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        var graphic = root.GetComponent<Image>();
        graphic.sprite = style != null ? style.button : null;
        graphic.type = Image.Type.Sliced;
        var button = root.GetComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.SpriteSwap;
        var sprites = button.spriteState;
        sprites.pressedSprite = style != null ? style.pressed : null;
        button.spriteState = sprites;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        root.AddComponent<ButtonAnimator>();
        label = Text(root.transform, "Label", fontSize);
        Place(label.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f));
        return button;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private void OnDestroy()
    {
        if (canvasRoot == null) return;
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(canvasRoot, false);
        Destroy(canvasRoot);
    }
}
