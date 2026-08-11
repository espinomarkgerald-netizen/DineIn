using UnityEngine;

public class HostSpeechBubbleSpawner : MonoBehaviour
{
    public static HostSpeechBubbleSpawner Instance;

    [SerializeField] private GameObject speechBubblePrefab;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Size")]
    [SerializeField] private float bubbleScale = 1f;

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
    }

    public void ShowForHost(Transform hostAnchor, Camera cam)
    {
        ShowBubble(hostAnchor, cam, GetRandomGreeting());
    }

    public void ShowForHostGroup(Transform hostAnchor, Camera cam, int groupSize)
    {
        ShowBubble(hostAnchor, cam, GetRandomGreetingForGroup(groupSize));
    }

    public void HideImmediate()
    {
        if (currentBubble == null)
            return;

        Destroy(currentBubble);
        currentBubble = null;
    }

    private void ShowBubble(Transform hostAnchor, Camera cam, string message)
    {
        if (speechBubblePrefab == null || hostAnchor == null || cam == null)
            return;

        HideImmediate();

        currentBubble = Instantiate(speechBubblePrefab);
        currentBubble.name = "HostSpeechBubble";

        RectTransform rect = currentBubble.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one * bubbleScale;
            rect.anchoredPosition3D = Vector3.zero;
        }

        UIFollowWorldPoint follow = currentBubble.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(hostAnchor, worldOffset, cam);

        HostSpeechBubbleUI ui = currentBubble.GetComponentInChildren<HostSpeechBubbleUI>(true);
        if (ui != null)
            ui.SetText(message);
    }

    private string GetRandomGreeting()
    {
        if (greetings == null || greetings.Length == 0)
            return "Welcome!";

        return greetings[Random.Range(0, greetings.Length)];
    }

    private string GetRandomGreetingForGroup(int groupSize)
    {
        if (groupSize > 0 && Random.value < 0.5f)
        {
            string[] tableGreetings =
            {
                $"Table for {groupSize}?",
                $"Party of {groupSize}?",
                $"Welcome, table for {groupSize}?",
                $"Good day, table for {groupSize}?"
            };

            return tableGreetings[Random.Range(0, tableGreetings.Length)];
        }

        return GetRandomGreeting();
    }
}
