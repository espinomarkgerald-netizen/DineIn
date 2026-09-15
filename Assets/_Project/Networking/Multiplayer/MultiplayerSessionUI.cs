using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// One local session bar; no input/camera ownership is assigned to remote avatars.
public sealed class MultiplayerSessionUI : MonoBehaviour
{
    private MultiplayerSessionManager session;
    private TMP_Text status, readyLabel;
    private Button ready;
    private GameObject canvasRoot;
    private float nextRefresh;
    private void Start()
    {
        session = GetComponent<MultiplayerSessionManager>();
        canvasRoot = new GameObject("Multiplayer Session Bar", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 940;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var panel = new GameObject("Session", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot.transform, false);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.06f, 0.09f, 0.94f);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.18f, 0); rect.anchorMax = new Vector2(0.82f, 0);
        rect.pivot = new Vector2(0.5f, 0); rect.offsetMin = new Vector2(0, 8); rect.offsetMax = new Vector2(0, 94);
        status = Text(panel.transform, "Status", 18);
        Place(status.rectTransform, 0, 0.62f, 8, -8);
        ready = Button(panel.transform, "Ready", out readyLabel);
        Place(ready.GetComponent<RectTransform>(), 0.63f, 0.85f, 4, -4);
        ready.onClick.AddListener(ToggleReady);
        var exit = Button(panel.transform, "Leave Run", out var exitLabel);
        exitLabel.text = "LEAVE RUN";
        Place(exit.GetComponent<RectTransform>(), 0.86f, 1, 4, -8);
        exit.onClick.AddListener(() => session.LeaveToMenu());
    }
    private void Update()
    {
        if (session == null || status == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        int target = TargetDay();
        bool marked = session.IsConnected && Equals(PhotonNetwork.LocalPlayer.CustomProperties[MultiplayerSessionManager.ReadyKey], target);
        bool canReady = session.CanAct && GameDayManager.Instance != null && !GameDayManager.Instance.ServiceActive;
        ready.interactable = canReady;
        readyLabel.text = marked ? "READY ✓" : "READY • DAY " + target;
        string names = "";
        if (canReady && session.Run != null)
            foreach (var player in session.ConnectedPlayers)
                if (!player.IsInactive) names += (names.Length > 0 ? "  " : "") + player.NickName +
                    (Equals(player.CustomProperties[MultiplayerSessionManager.ReadyKey], target) ? " ✓" : " …");
        status.richText = false;
        status.text = session.Status + (names.Length > 0 ? "\n" + names : "") + "\n" + MultiplayerRunRecords.Status;
    }
    private int TargetDay() => (GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1)
        + (GameDayManager.Instance != null && GameDayManager.Instance.HasDayResults ? 1 : 0);
    private void ToggleReady()
    {
        if (!session.CanAct) return;
        int day = TargetDay();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.ReadyKey] =
            Equals(PhotonNetwork.LocalPlayer.CustomProperties[MultiplayerSessionManager.ReadyKey], day) ? 0 : day });
    }
    private static TMP_Text Text(Transform parent, string name, float size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset; text.fontSize = size;
        text.color = Color.white; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size;
        return text;
    }
    private static Button Button(Transform parent, string name, out TMP_Text label)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(0.06f, 0.38f, 0.46f);
        label = Text(root.transform, "Label", 20); label.alignment = TextAlignmentOptions.Center;
        Place(label.rectTransform, 0, 1, 5, -5);
        return root.GetComponent<Button>();
    }
    private static void Place(RectTransform rect, float min, float max, float left, float right)
    { rect.anchorMin = new Vector2(min, 0); rect.anchorMax = new Vector2(max, 1); rect.offsetMin = new Vector2(left, 6); rect.offsetMax = new Vector2(right, -6); }
    private void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot); }
}
