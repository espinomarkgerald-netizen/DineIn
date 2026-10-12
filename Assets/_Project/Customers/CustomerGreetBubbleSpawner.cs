using UnityEngine;

public class CustomerGreetBubbleSpawner : MonoBehaviour
{
    public static CustomerGreetBubbleSpawner Instance;

    [SerializeField] private GameObject greetBubblePrefab;

    // Keep the passive marker hidden while this action is hidden for movement or table selection.
    public bool IsShowingFor(CustomerGroup group) => currentGroup == group && currentBubble != null;
    private GameObject currentBubble;
    private CustomerGroup currentGroup;
    private Camera currentCamera;
    private bool requestedVisible;
    private CustomerGreetBubbleUI currentUI;

    private void Awake()
    {
        Instance = this;
    }

    private void LateUpdate()
    {
        RefreshReservedVisibility();
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
            currentBubble.SetActive(FineDiningReservations.For(currentGroup) != null ? CanShowCurrent() : true);
            currentUI?.Refresh();
        }
    }

    public void Show(CustomerGroup group, Camera cam)
    {
        if (group != null && !group.ReadyForReceptionInteraction) return;
        if (group != null && !group.ReservedSeatingActionAvailable) return;
        if (BoothAssignArrowManager.Instance != null && BoothAssignArrowManager.Instance.IsSelectingFor(group)) return;
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
        requestedVisible = true;
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
            follow.ConfigureFineDiningSizing(FineDiningReservations.For(group));
        }
        else
        {
            Debug.LogWarning("[CustomerGreetBubbleSpawner] UIFollowWorldPoint not found on prefab");
        }

        var ui = currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true);
        currentUI = ui;
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

        requestedVisible = visible;
        bool show = visible && CanShowCurrent();
        currentBubble.SetActive(show);
        if (!show) return;

        CustomerGreetBubbleUI ui =
            currentBubble.GetComponentInChildren<CustomerGreetBubbleUI>(true);
        if (ui != null)
            ui.Init(group);

        Canvas.ForceUpdateCanvases();
    }

    private bool CanShowCurrent()
    {
        if (!requestedVisible || currentGroup == null) return false;
        if (FineDiningReservations.For(currentGroup) == null) return true;
        return currentGroup.CanBeSeated && currentGroup.ReadyForReceptionInteraction &&
            currentGroup.ReservedSeatingActionAvailable &&
            (BoothAssignArrowManager.Instance == null || !BoothAssignArrowManager.Instance.IsSelectingFor(currentGroup));
    }

    private void RefreshReservedVisibility()
    {
        if (currentBubble == null || currentGroup == null || FineDiningReservations.For(currentGroup) == null) return;
        bool show = CanShowCurrent();
        if (currentBubble.activeSelf != show) currentBubble.SetActive(show);
        if (show) currentUI?.Refresh();
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
        currentUI = null;
        requestedVisible = false;
    }

    private void HandleCurrentGroupLeftLine(CustomerGroup group)
    {
        if (group == currentGroup)
            Hide();
    }
}
