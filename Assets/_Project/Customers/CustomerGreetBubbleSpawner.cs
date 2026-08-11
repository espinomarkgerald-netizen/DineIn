using UnityEngine;

public class CustomerGreetBubbleSpawner : MonoBehaviour
{
    public static CustomerGreetBubbleSpawner Instance;

    [SerializeField] private GameObject greetBubblePrefab;

    private GameObject currentBubble;

    private void Awake()
    {
        Instance = this;
    }

    public void Show(CustomerGroup group, Camera cam)
    {
        Debug.Log("[CustomerGreetBubbleSpawner] Show called");

        if (group == null)
        {
            Debug.LogError("[CustomerGreetBubbleSpawner] Group is NULL");
            return;
        }

        if (greetBubblePrefab == null)
        {
            Debug.LogError("[CustomerGreetBubbleSpawner] greetBubblePrefab is NULL");
            return;
        }

        Hide();

        currentBubble = Instantiate(greetBubblePrefab);
        currentBubble.name = $"{group.name}_GreetBubble";

        Debug.Log("[CustomerGreetBubbleSpawner] Bubble instantiated: " + currentBubble.name);

        RectTransform rect = currentBubble.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;
        }

        var follow = currentBubble.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
        {
            group.ConfigureCustomerBubble(follow, cam);
            Debug.Log("[CustomerGreetBubbleSpawner] UIFollowWorldPoint initialized");
        }
        else
        {
            Debug.LogWarning("[CustomerGreetBubbleSpawner] UIFollowWorldPoint not found on prefab");
        }

        var ui = currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true);
        if (ui != null)
        {
            ui.Init(group);
            Debug.Log("[CustomerGreetBubbleSpawner] CustomerGreetBubbleUI initialized");
        }
        else
        {
            Debug.LogError("[CustomerGreetBubbleSpawner] CustomerGreetBubbleUI not found on prefab");
        }

        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        if (currentBubble != null)
        {
            Destroy(currentBubble);
            currentBubble = null;
        }
    }
}
