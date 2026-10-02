using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One ownership presenter on each existing action root. Gameplay remains in its
// original button handler; this component never sends a claim or completes work.
[DisallowMultipleComponent]
public sealed class MultiplayerTaskPresentation : MonoBehaviour
{
    public static int LiveCount { get; private set; }
    private static readonly Dictionary<string, GameObject> bubbles = new();
    public string Key { get; private set; }
    private string kind;
    private int boundDay;
    private string boundRun;
    private int paymentView;
    private static string KeyFor(string task, string kind) => MultiplayerSessionManager.Instance.RunId + ":"
        + (GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0) + ":" + task + ":" + kind;
    public static GameObject Acquire(GameObject prefab, string task, string kind = "Action")
    {
        if (!MultiplayerDayBridge.IsActive || string.IsNullOrEmpty(task)) return Instantiate(prefab);
        string key = KeyFor(task, kind);
        if (bubbles.TryGetValue(key, out var existing) && existing != null && existing.activeSelf) return existing;
        var root = Instantiate(prefab);
        Bind(root, task, kind);
        return root;
    }
    public static void DestroyBubble(GameObject root)
    {
        if (root == null) return;
        if (MultiplayerDayBridge.IsActive)
        {
            var presenter = root.GetComponentInParent<MultiplayerTaskPresentation>(true);
            if (presenter != null) { root = presenter.gameObject; presenter.Forget(); }
            root.SetActive(false); // Destroy is deferred; remove old input/rendering immediately.
        }
        Destroy(root);
    }
    private void Forget()
    { if (Key != null && bubbles.TryGetValue(Key, out var root) && root == gameObject) bubbles.Remove(Key); }
    private void OnDestroy() => Forget();
    private void OnEnable() => LiveCount++;
    private void OnDisable() => LiveCount--;
    private string task;
    private UnityEngine.UI.Button[] buttons;
    [SerializeField, Min(8f)] private float ownershipFontSize = 14f;
    private TMPro.TextMeshProUGUI ownershipText;
    private UnityEngine.UI.Image ownershipBackground;
    private MultiplayerTaskClaims claimSource;
    private float nextStatusRefresh;
    private string shownStatus;
    private void EnsureOwnershipLabel()
    {
        if (ownershipText != null || buttons == null || buttons.Length == 0 || buttons[0] == null) return;
        var parent = buttons[0].transform as RectTransform;
        if (parent == null) return;
        var existingText = parent.GetComponentInChildren<TMPro.TMP_Text>(true);
        var badge = new GameObject("Task ownership", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)badge.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -3);
        rect.sizeDelta = new Vector2(20, ownershipFontSize * 2.5f);
        ownershipBackground = badge.GetComponent<UnityEngine.UI.Image>();
        ownershipBackground.raycastTarget = false;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        var textRect = (RectTransform)label.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4, 2); textRect.offsetMax = new Vector2(-4, -2);
        ownershipText = label.GetComponent<TMPro.TextMeshProUGUI>();
        ownershipText.font = existingText != null ? existingText.font : TMPro.TMP_Settings.defaultFontAsset;
        ownershipText.fontSize = ownershipFontSize;
        ownershipText.alignment = TMPro.TextAlignmentOptions.Center;
        ownershipText.color = Color.white; ownershipText.richText = false;
        ownershipText.raycastTarget = false;
        ownershipText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
    }
    public static void Bind(GameObject root, string id, string kind = null)
    {
        if (root == null || !MultiplayerDayBridge.IsActive) return;
        var presenter = root.GetComponentInParent<MultiplayerTaskPresentation>(true);
        if (presenter == null) presenter = root.AddComponent<MultiplayerTaskPresentation>();
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        string run = MultiplayerSessionManager.Instance.RunId;
        if (presenter.Key != null && presenter.task == id && presenter.boundDay == day && presenter.boundRun == run
            && (kind == null || kind == presenter.kind)) { presenter.gameObject.SetActive(true); return; }
        presenter.Forget();
        presenter.task = id;
        presenter.paymentView = 0;
        if (id != null && id.EndsWith(":Payment", System.StringComparison.Ordinal))
        { var parts = id.Split(':'); if (parts.Length == 3) int.TryParse(parts[1], out presenter.paymentView); }
        presenter.kind = kind ?? presenter.kind ?? "Action";
        presenter.boundDay = day; presenter.boundRun = run;
        presenter.Key = KeyFor(id, presenter.kind);
        bubbles[presenter.Key] = presenter.gameObject;
        presenter.gameObject.SetActive(true);
    }
    private void Awake()
    {
        buttons = GetComponentsInChildren<UnityEngine.UI.Button>(true);
    }
    private void LateUpdate()
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || string.IsNullOrEmpty(task)) return;
        if (GameFlowManager.Instance != null && boundDay != GameFlowManager.Instance.CurrentDay)
        { DestroyBubble(gameObject); return; }
        if (claimSource == null) claimSource = session.GetComponent<MultiplayerTaskClaims>();
        var claims = claimSource;
        if (claims == null) return;
        int owner = claims.GetPresentationOwner(task, paymentView);
        var bot = claims.GetBotClaim(task);
        bool available = owner == 0 && (bot == null || !bot.committed);
        bool own = owner == session.LocalActorNumber;
        if (Time.unscaledTime >= nextStatusRefresh)
        {
            nextStatusRefresh = Time.unscaledTime + 0.1f;
            EnsureOwnershipLabel();
            string status = claims.Describe(task);
            if (ownershipText != null && shownStatus != status)
            {
                shownStatus = status; ownershipText.text = status;
                ownershipBackground.color = claims.IsPending(task) ? new Color(0.48f, 0.30f, 0.04f, 0.96f)
                    : own ? new Color(0.03f, 0.38f, 0.25f, 0.96f) : new Color(0.06f, 0.18f, 0.25f, 0.96f);
            }
        }
        foreach (var button in buttons) if (button != null)
            button.interactable = session.CanAct && !claims.IsPending(task) && !MultiplayerRestockView.Active
                && !MultiplayerDayBridge.PreparationLocked && (available || own);
    }
}
