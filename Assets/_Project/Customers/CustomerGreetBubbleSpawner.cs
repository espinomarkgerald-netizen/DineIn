using UnityEngine;

public class CustomerGreetBubbleSpawner : MonoBehaviour
{
    public static CustomerGreetBubbleSpawner Instance;

    [SerializeField] private GameObject greetBubblePrefab;

    private GameObject currentBubble;
    private CustomerGroup currentGroup;
    private Camera currentCamera;

    private void Awake()
    {
        Instance = this;
    }

    private void LateUpdate()
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsMultiplayerSession) return;

        CustomerGroup visibleGroup = null;
        foreach (var customer in MultiplayerWorldRegistry.All<MultiplayerCustomerSpawn>())
        {
            if (customer.ReadyForInteraction && customer.Group != null && !customer.Group.HasBeenAssigned
                && !MultiplayerCustomerInteractionBridge.IsGreetingActionHidden(customer.Group))
            {
                visibleGroup = customer.Group;
                break;
            }
        }
        if (visibleGroup == null)
        {
            if (currentGroup != null) Hide();
            return;
        }
        if (currentGroup != visibleGroup || currentBubble == null)
            Show(visibleGroup, Camera.main);
        else
        {
            currentBubble.SetActive(true);
            currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true)?.Refresh();
        }
    }

    public void Show(CustomerGroup group, Camera cam)
    {
        if (MultiplayerDayBridge.IsActive)
        {
            if (MultiplayerCustomerInteractionBridge.IsGreetingActionHidden(group)) return;
            if (group == currentGroup && currentBubble != null)
            { currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true)?.Refresh(); return; }
            if (group == null || group.GetComponentInParent<MultiplayerCustomerSpawn>()?.ReadyForInteraction != true) return;
        }

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

        Clear();

        currentGroup = group;
        currentCamera = cam;
        currentGroup.OnGroupLeftLine -= HandleCurrentGroupLeftLine;
        currentGroup.OnGroupLeftLine += HandleCurrentGroupLeftLine;

        var networkGroup = group.GetComponentInParent<MultiplayerCustomerSpawn>();
        currentBubble = MultiplayerTaskPresentation.Acquire(greetBubblePrefab,
            networkGroup != null ? $"Customer:{networkGroup.photonView.ViewID}:GreetSeat" : null);
        currentBubble.name = $"{group.name}_GreetBubble";


        RectTransform rect = currentBubble.GetComponent<RectTransform>();
        if (rect != null)
        {
            // Keep the prefab's authored scale. Forcing this to one doubled the
            // greet/assign bubble compared with the order and pickup bubbles.
            rect.anchoredPosition3D = Vector3.zero;
        }

        var follow = currentBubble.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
        {
            // Keep the greet/assign action close to the group. It is the base
            // bubble in the stack; any other group status bubbles are laid out
            // above it with a readable gap instead of overlapping it.
            follow.enabled = true;
            follow.InitAboveTarget(
                group.UIAnchor,
                Vector3.zero,
                cam != null ? cam : Camera.main,
                10f,
                -10,
                10f);
        }
        else
        {
            Debug.LogWarning("[CustomerGreetBubbleSpawner] UIFollowWorldPoint not found on prefab");
        }

        var ui = currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true);
        if (ui != null)
        {
            ui.Init(group);
            var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
            if (customer != null) MultiplayerTaskPresentation.Bind(currentBubble, $"Customer:{customer.photonView.ViewID}:GreetSeat");
        }
        else
        {
            Debug.LogError("[CustomerGreetBubbleSpawner] CustomerGreetBubbleUI not found on prefab");
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Hides the current action while the Manager is walking, then restores the
    /// same bubble and re-runs its original greet/assign refresh on arrival.
    /// </summary>
    public void SetVisibleAndRefresh(CustomerGroup group, bool visible)
    {
        if (group == null) return;
        if (MultiplayerDayBridge.IsActive && visible && MultiplayerCustomerInteractionBridge.IsGreetingActionHidden(group)) return;

        if (currentBubble == null || currentGroup != group)
        {
            if (visible)
                Show(group, currentCamera != null ? currentCamera : Camera.main);
            return;
        }

        currentBubble.SetActive(visible);
        if (!visible) return;

        CustomerGreetBubbleUI ui =
            currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true);
        if (ui != null)
            ui.Init(group);

        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        Clear();
    }

    private void Clear()
    {
        if (currentGroup != null)
            currentGroup.OnGroupLeftLine -= HandleCurrentGroupLeftLine;

        if (currentBubble != null)
        {
            MultiplayerTaskPresentation.DestroyBubble(currentBubble);
            currentBubble = null;
        }

        currentGroup = null;
    }

    private void HandleCurrentGroupLeftLine(CustomerGroup group)
    {
        if (group == currentGroup)
            Hide();
    }
}
