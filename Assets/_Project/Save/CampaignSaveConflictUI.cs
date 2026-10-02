using System;
using TMPro;
using UnityEngine;

/// <summary>Only appears for a genuine save choice; uses the existing pause/menu style assets.</summary>
public sealed class CampaignSaveConflictUI : MonoBehaviour
{
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private Color windowColor = new Color(.86f, .86f, .91f, 1f);
    [SerializeField] private Color buttonColor = new Color(.04f, .64f, .88f, 1f);
    private GameObject canvasRoot;
    private TMP_Text localText, cloudText, message;
    private UnityEngine.UI.Button localButton, cloudButton;
    private TMP_FontAsset font;
    private Sprite frame;
    private Action<bool> choose;
    private bool canUseLocal, canUseCloud;

    public void Show(CampaignSaveStore.AccountSnapshot local, CampaignSaveStore.AccountSnapshot cloud, Action<bool> selection)
    {
        if (canvasRoot == null) Build();
        choose = selection;
        localText.text = Summary("THIS DEVICE", local);
        cloudText.text = Summary("CLOUD SAVE", cloud);
        localButton.interactable = canUseLocal = CampaignSaveStore.ValidContent(local);
        cloudButton.interactable = canUseCloud = cloud != null;
        message.text = "The save you choose will become your active progress on this account.";
        canvasRoot.SetActive(true);
    }
    public void Hide() { if (canvasRoot != null) canvasRoot.SetActive(false); }
    public void RetryAvailable()
    {
        message.text = "Your progress is safe on this device. Please retry when you're online.";
        localButton.interactable = canUseLocal; cloudButton.interactable = canUseCloud;
    }
    private void Select(bool local)
    {
        localButton.interactable = cloudButton.interactable = false;
        message.text = "Saving your choice...";
        choose?.Invoke(local);
    }
    public static string Summary(string title, CampaignSaveStore.AccountSnapshot snapshot)
    {
        if (snapshot == null) return title + "\n\nNo cloud progress found.";
        string Career(CampaignSaveStore.Snapshot pair, string name)
        {
            if (string.IsNullOrEmpty(pair?.save)) return name + "\nNot started";
            if (!CampaignSaveStore.Valid(pair)) return name + "\nNeeds recovery";
            try
            {
                var data = JsonUtility.FromJson<GameSaveData>(string.IsNullOrEmpty(pair.checkpoint) ? pair.save : pair.checkpoint);
                return name + "\nDay " + data.currentDay + "   •   ₱" + data.money.ToString("N0");
            }
            catch { return name + "\nNeeds recovery"; }
        }
        string played = DateTime.TryParse(snapshot.savedUtc, out var time) ? time.ToLocalTime().ToString("MMM d, yyyy") : "Date unavailable";
        return title + "\n\nLast played: " + played + "\n\n" + Career(snapshot.casual, "Casual Dining") + "\n\n" + Career(snapshot.fastFood, "Fast Food");
    }
    private void Build()
    {
        var style = Resources.Load<GameObject>("LobbyPauseMenu")?.GetComponentInChildren<LobbyPauseMenuView>(true);
        font = style != null ? style.Font : TMP_Settings.defaultFontAsset;
        frame = style != null ? style.NineSlicedFrame : null;
        canvasRoot = new GameObject("Choose Progress", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32600;
        var scaler = canvasRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution; scaler.matchWidthOrHeight = .5f;
        var shade = Panel(canvasRoot.transform, "Backdrop", new Color(0, .04f, .08f, .8f), Vector2.zero, Vector2.one);
        var window = Panel(shade, "Window", windowColor, new Vector2(.08f, .12f), new Vector2(.92f, .88f));
        Text(window, "Title", "CHOOSE PROGRESS", 52, new Vector2(.04f, .86f), new Vector2(.96f, .98f));
        Text(window, "Explanation", "We found different progress on this device and your account.", 27, new Vector2(.04f, .76f), new Vector2(.96f, .87f));
        localText = Text(window, "Device", "", 33, new Vector2(.05f, .28f), new Vector2(.47f, .75f));
        cloudText = Text(window, "Cloud", "", 33, new Vector2(.53f, .28f), new Vector2(.95f, .75f));
        localButton = Button(window, "Use This Device", "USE THIS DEVICE", new Vector2(.05f, .16f), new Vector2(.47f, .27f), true);
        cloudButton = Button(window, "Use Cloud Save", "USE CLOUD SAVE", new Vector2(.53f, .16f), new Vector2(.95f, .27f), false);
        message = Text(window, "Message", "", 26, new Vector2(.05f, .015f), new Vector2(.95f, .14f));
    }
    private RectTransform Panel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = root.GetComponent<UnityEngine.UI.Image>(); image.color = color;
        if (name != "Backdrop") { image.sprite = frame; image.type = UnityEngine.UI.Image.Type.Sliced; }
        return rect;
    }
    private TMP_Text Text(Transform parent, string name, string value, float size, Vector2 min, Vector2 max)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = root.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size;
        text.enableAutoSizing = true; text.fontSizeMin = size * .7f; text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center; text.color = new Color(.05f, .23f, .29f); text.raycastTarget = false;
        return text;
    }
    private UnityEngine.UI.Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, bool local)
    {
        var rect = Panel(parent, name, buttonColor, min, max);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        var text = Text(rect, "Label", label, 32, new Vector2(.03f, .08f), new Vector2(.97f, .92f)); text.color = Color.white;
        button.onClick.AddListener(() => Select(local)); return button;
    }
    private void OnDestroy() { if (canvasRoot != null) { if (Application.isPlaying) Destroy(canvasRoot); else DestroyImmediate(canvasRoot); } }
}
