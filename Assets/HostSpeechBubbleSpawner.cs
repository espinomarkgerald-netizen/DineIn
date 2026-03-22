using UnityEngine;

public class HostSpeechBubbleSpawner : MonoBehaviour
{
    public static HostSpeechBubbleSpawner Instance;

    [SerializeField] private GameObject speechBubblePrefab;
    [SerializeField] private Canvas targetCanvas;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Size")]
    [SerializeField] private float bubbleScale = 1f; // 🔥 adjust in inspector

    [Header("Text")]
    [TextArea]
    [SerializeField] private string[] greetings =
    {
        "Welcome!",
        "Hello!",
        "Good day!",
        "Right this way!",
        "Nice to see you!"
    };

    private GameObject currentBubble;

    private void Awake()
    {
        Instance = this;

        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();
    }

    public void ShowForHost(Transform hostAnchor, Camera cam)
    {
        if (speechBubblePrefab == null || hostAnchor == null || cam == null)
            return;

        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null)
            return;

        HideImmediate();

        currentBubble = Instantiate(speechBubblePrefab, targetCanvas.transform);
        currentBubble.name = "HostSpeechBubble";

        RectTransform rect = currentBubble.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one * bubbleScale; // 🔥 APPLY SCALE
            rect.anchoredPosition3D = Vector3.zero;
        }

        var follow = currentBubble.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(hostAnchor, worldOffset, cam);

        var ui = currentBubble.GetComponentInChildren<HostSpeechBubbleUI>(true);
        if (ui != null)
            ui.SetText(GetRandomGreeting());
    }

    private string GetRandomGreeting()
    {
        if (greetings == null || greetings.Length == 0)
            return "Welcome!";

        int index = Random.Range(0, greetings.Length);
        return greetings[index];
    }

    public void HideImmediate()
    {
        if (currentBubble != null)
        {
            Destroy(currentBubble);
            currentBubble = null;
        }
    }
}