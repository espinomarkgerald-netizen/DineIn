using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Occasional, non-blocking coaching using the existing Big Boss dialogue presentation.</summary>
public sealed class RestaurantBossTips : MonoBehaviour
{
    public static RestaurantBossTips Instance { get; private set; }
    [SerializeField] private TutorialDialogueUI dialogue;
    [SerializeField] private Sprite portrait;
    [SerializeField, Min(30f)] private float quietInterval = 120f;
    [SerializeField, Min(1)] private int maximumTipsPerDay = 2;
    [SerializeField, Min(4f)] private float displaySeconds = 9f;
    [TextArea] public string cleaningTip = "It has come to my attention that the lobby needs cleaning. When your staff warn you, give them time to clean. A clean restaurant keeps customers happy.";
    [TextArea] public string fastFoodTip = "Keep the pickup counter clear. Takeout customers collect their own bags, while pink diners always expect their food at the table.";
    [TextArea] public string serviceTip = "A quiet moment is your chance to clear used trays and check the next table. Those small jobs keep the next rush moving.";
    [TextArea] public string upgradeTip = "Check today's newspaper and the equipment catalog before opening. Buy upgrades when your team needs them, and leave enough money for stock and wages.";
    private readonly HashSet<string> shown = new();
    private string dayKey;
    private float quietSeconds, nextCheck;

    public static RestaurantBossTips EnsureInstance()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<RestaurantBossTips>("UI/RestaurantBossTips");
        return prefab != null ? Instantiate(prefab) : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        dialogue?.HideDialogue();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        string scene = SceneManager.GetActiveScene().name;
        bool restaurant = scene == "Lobby1" || scene == "Lobby2" || scene == "Lobby1 Multiplayer";
        if (!restaurant || TutorialSystem.IsTutorialMode || GameplayUIBlocker.IsBlocked() ||
            Time.timeScale <= 0f || HygieneManager.DecisionPaused || ManagerComplaintSystem.Instance?.HasActiveComplaint == true)
        {
            dialogue?.HideDialogue();
            quietSeconds = 0f;
            return;
        }
        if (GameSaveManager.Instance != null &&
            (!GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave)) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 2f;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        string key = scene + ":" + day + ":";
        if (key != dayKey) { dayKey = key; quietSeconds = 0f; dialogue?.HideDialogue(); }
        int count = 0;
        foreach (string id in shown) if (id.StartsWith(key, System.StringComparison.Ordinal)) count++;
        if (count >= maximumTipsPerDay || dialogue == null || dialogue.IsVisible) return;
        var dayManager = GameDayManager.Instance;
        if (dayManager == null || !dayManager.ServiceActive) { quietSeconds = 0f; return; }
        var groups = FindObjectsByType<CustomerGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (groups.Length > 2 || !HygieneManager.HandsEmpty(RoleManager.Instance?.GetActivePlayerMovement()))
        { quietSeconds = 0f; return; }
        quietSeconds += 2f;
        if (quietSeconds < quietInterval) return;
        bool dirty = HygieneManager.Instance != null && HygieneManager.Instance.State.lobbyDirt >= HygieneState.WarningLevel;
        string topic = dirty && !shown.Contains(key + "cleaning") ? "cleaning"
            : scene == "Lobby2" && !shown.Contains(key + "pickup") ? "pickup"
            : !shown.Contains(key + "service") ? "service" : "upgrades";
        if (!shown.Add(key + topic)) return;
        string line = topic == "cleaning" ? cleaningTip : topic == "pickup" ? fastFoodTip
            : topic == "service" ? serviceTip : upgradeTip;
        dialogue.ShowAuto("Big Boss", line, portrait, displaySeconds);
        quietSeconds = 0f;
        GameSaveManager.Instance?.RequestSave();
    }

    public void FillSaveData(GameSaveData data) => data.restaurantBossTipsShown = new List<string>(shown);
    public void ApplySaveData(GameSaveData data)
    {
        shown.Clear();
        if (data.restaurantBossTipsShown != null)
            foreach (string id in data.restaurantBossTipsShown) if (!string.IsNullOrEmpty(id)) shown.Add(id);
        quietSeconds = 0f;
        dayKey = null;
        dialogue?.HideDialogue();
    }
}
