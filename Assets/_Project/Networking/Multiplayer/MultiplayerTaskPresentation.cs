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
    private Button[] buttons;
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
        buttons = GetComponentsInChildren<Button>(true);
    }
    private void LateUpdate()
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || string.IsNullOrEmpty(task)) return;
        if (GameFlowManager.Instance != null && boundDay != GameFlowManager.Instance.CurrentDay)
        { DestroyBubble(gameObject); return; }
        var claims = session.GetComponent<MultiplayerTaskClaims>();
        int owner = claims.GetOwner(task);
        if (owner == 0 && paymentView > 0) owner = MultiplayerServiceActions.Active?.PaymentOwner(paymentView) ?? 0;
        var bot = claims.GetBotClaim(task);
        bool available = owner == 0 && (bot == null || !bot.committed);
        bool own = owner == session.LocalActorNumber;
        foreach (var button in buttons) if (button != null)
            button.interactable = session.CanAct && !MultiplayerRestockView.Active
                && !MultiplayerDayBridge.PreparationLocked && (available || own);
    }
}
