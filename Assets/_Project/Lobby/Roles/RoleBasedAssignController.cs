using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class RoleBasedAssignController : MonoBehaviour
{
    [Header("Raycast")]
    public LayerMask customerLayer;
    public LayerMask boothLayer;
    public LayerMask cleanableLayer;
    public float maxRayDistance = 200f;

    [Header("Movement")]
    public NavMeshAgent agent;

    [Header("Input")]
    public bool ignoreWhenPointerOverUI = true;

    [Header("UI")]
    [SerializeField] private WarningSlideUI warningUI;

    private CustomerGroup selectedGroup;
    private StaffRole staffRole;
    private ManagerPlayer managerPlayer;
    private LobbyLineManager lineManager;

    private void Awake()
    {
        staffRole = GetComponent<StaffRole>();
        managerPlayer = GetComponent<ManagerPlayer>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        lineManager = FindFirstObjectByType<LobbyLineManager>();
    }

    private void Update()
    {
        bool isManager = managerPlayer != null && managerPlayer.isActiveAndEnabled;
        bool isLegacyActiveRole = RoleManager.Instance != null &&
                                  RoleManager.Instance.IsActiveRole(gameObject);
        if (!isManager && !isLegacyActiveRole)
            return;

        if (!isManager && staffRole == null)
            return;

        if (staffRole != null && staffRole.role == StaffRole.Role.Waiter)
        {
            if (WaiterHands.ActivePlayerHands != null && WaiterHands.ActivePlayerHands.HasTray)
                return;
        }

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                if (ignoreWhenPointerOverUI && IsPointerOverUI_Touch(t.fingerId))
                    return;

                HandleTap(t.position);
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreWhenPointerOverUI && IsPointerOverUI_Mouse())
                return;

            HandleTap(Input.mousePosition);
        }
    }

    private void HandleTap(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("No active MainCamera found.");
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance);

        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        if (managerPlayer != null && managerPlayer.Can(ManagerPlayer.Capability.Host))
        {
            HandleHostTap(hits, cam);
            return;
        }

        switch (staffRole.role)
        {
            case StaffRole.Role.Host:
                HandleHostTap(hits, cam);
                break;

            case StaffRole.Role.Waiter:
                HandleWaiterTap(hits);
                break;

            case StaffRole.Role.Busser:
                HandleBusserTap(hits);
                break;

            case StaffRole.Role.Cashier:
                HandleCashierTap(hits);
                break;
        }
    }

    private void HandleHostTap(RaycastHit[] hits, Camera cam)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (((1 << hit.collider.gameObject.layer) & customerLayer) != 0)
            {
                CustomerGroup group = hit.collider.GetComponentInParent<CustomerGroup>();
                if (group == null)
                    continue;

                if (!CanHostSelectGroup(group))
                    return;

                // Selecting a waiting group only reveals the offer. Ownership
                // begins when the player presses Greet, so the receptionist may
                // take over after the one-second response window if they do not.
                if (group.IsReceptionClaimedByBot || RestaurantTaskClaim.IsClaimedByBot(group))
                {
                    ShowWarning("The receptionist is already helping this group.");
                    return;
                }

                if (RestaurantTaskClaim.PlayerHasActiveTask &&
                    !RestaurantTaskClaim.IsClaimedByPlayer(group))
                {
                    ShowWarning("Finish your current task first.");
                    return;
                }

                CustomerGreetBubbleSpawner.Instance?.Show(
                    group,
                    cam
                );
                return;
            }
        }

        if (selectedGroup != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (((1 << hit.collider.gameObject.layer) & boothLayer) != 0)
                {
                    Booth booth = hit.collider.GetComponentInParent<Booth>();
                    if (booth != null)
                    {
                        AssignGroupToBooth(selectedGroup, booth);
                        return;
                    }
                }
            }
        }
    }

    private bool CanHostSelectGroup(CustomerGroup group)
    {
        if (group == null) return false;

        if (group.HasBeenAssigned)
            return false;

        if (lineManager == null)
            return false;

        if (!lineManager.IsGroupInLine(group))
            return false;

        CustomerGroup front = lineManager.GetFrontOfLine();

        if (front == null)
        {
            ShowWarning("No customers are ready yet.");
            return false;
        }

        if (group != front)
        {
            ShowWarning("Please assist the first group in line first.");
            return false;
        }

        return true;
    }

    private void HandleWaiterTap(RaycastHit[] hits)
    {
        HandleNonHostSeatingTap(hits);
    }

    private void HandleBusserTap(RaycastHit[] hits)
    {
        HandleNonHostSeatingTap(hits);
    }

    private void HandleCashierTap(RaycastHit[] hits)
    {
        HandleNonHostSeatingTap(hits);
    }

    private void HandleNonHostSeatingTap(RaycastHit[] hits)
    {
        if (HasTappedCustomer(hits) || HasTappedBooth(hits))
            ShowWarning("Only the host can seat customers.");
    }

    private bool HasTappedCustomer(RaycastHit[] hits)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (((1 << hit.collider.gameObject.layer) & customerLayer) == 0)
                continue;

            CustomerGroup group = hit.collider.GetComponentInParent<CustomerGroup>();
            if (group != null)
                return true;
        }

        return false;
    }

    private bool HasTappedBooth(RaycastHit[] hits)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (((1 << hit.collider.gameObject.layer) & boothLayer) == 0)
                continue;

            Booth booth = hit.collider.GetComponentInParent<Booth>();
            if (booth != null)
                return true;
        }

        return false;
    }

    private void SelectGroup(CustomerGroup group)
    {
        if (selectedGroup != null)
            selectedGroup.SetSelected(false);

        selectedGroup = group;
        selectedGroup.SetSelected(true);

        CustomerGreetBubbleSpawner.Instance?.Hide();

        Debug.Log($"Selected group: {group.name}");
    }

    public void BeginAssignFromBubble(CustomerGroup group)
    {
        if (group == null) return;
        bool managerCanHost = managerPlayer != null &&
                              managerPlayer.Can(ManagerPlayer.Capability.Host);
        bool legacyHost = RoleManager.Instance != null &&
                          RoleManager.Instance.IsActiveRole(gameObject) &&
                          staffRole != null &&
                          staffRole.role == StaffRole.Role.Host;
        if (!managerCanHost && !legacyHost) return;

        if (BoothAssignArrowManager.Instance == null)
        {
            ShowWarning("No table indicator system found.");
            return;
        }

        if (!BoothAssignArrowManager.Instance.HasValidBooth(group))
        {
            ShowWarning("No available tables for this group.");
            return;
        }

        SelectGroup(group);
        BoothAssignArrowManager.Instance.ShowValidBooths(group);

        Debug.Log($"Ready to assign table for: {group.name}");
    }

    private void AssignGroupToBooth(CustomerGroup group, Booth booth)
    {
        if (group == null || booth == null) return;

        if (booth.CurrentGroup != null)
        {
            ShowWarning("That table is already occupied.");
            return;
        }

        if (booth.seats == null || booth.seats.Count < group.Size)
        {
            ShowWarning("This group needs a bigger table.");
            return;
        }

        if (!booth.IsAvailableFor(group.Size))
        {
            ShowWarning("That table is not available for this group.");
            return;
        }

        // The choice has been made. Remove the indicator immediately while
        // the Manager walks to the booth and completes the seating action.
        BoothAssignArrowManager.Instance?.HideAll();

        void CompleteAssignment()
        {
            if (group == null || booth == null || group.HasBeenAssigned ||
                !booth.IsAvailableFor(group.Size))
            {
                ShowWarning("That table is no longer available.");
                RestaurantTaskClaim.ReleasePlayer(group);
                BoothAssignArrowManager.Instance?.HideAll();
                return;
            }

            void HandleSeated(CustomerGroup g)
            {
                if (g != group) return;

                group.OnGroupSeated -= HandleSeated;

                if (booth != null)
                    booth.SpawnMenuBook();
            }

            group.OnGroupSeated -= HandleSeated;
            group.OnGroupSeated += HandleSeated;

            group.AssignToBooth(booth);
            group.CompleteReceptionTask();
            RestaurantTaskClaim.Complete(group);

            group.SetSelected(false);
            selectedGroup = null;

            CustomerGreetBubbleSpawner.Instance?.Hide();
            BoothAssignArrowManager.Instance?.HideAll();
        }

        if (managerPlayer != null && managerPlayer.Movement != null)
        {
            Transform approach = booth.approachPoint != null
                ? booth.approachPoint
                : booth.transform;

            managerPlayer.Movement.UI_MoveToAction(
                approach,
                2.75f,
                CompleteAssignment,
                () =>
                {
                    if (group != null)
                    {
                        group.SetSelected(false);
                        group.ReleasePlayerReceptionTask();
                        RestaurantTaskClaim.ReleasePlayer(group);
                    }

                    selectedGroup = null;
                    BoothAssignArrowManager.Instance?.HideAll();
                });
            return;
        }

        if (agent != null && agent.isOnNavMesh && booth.approachPoint != null)
            agent.SetDestination(booth.approachPoint.position);

        CompleteAssignment();
    }

    private void ShowWarning(string message)
    {
        if (warningUI != null)
            warningUI.Show(message);
        else if (WarningSlideUI.Instance != null)
            WarningSlideUI.Instance.Show(message);
        else
            Debug.LogWarning(message);
    }

    private bool IsPointerOverUI_Mouse()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOverUI_Touch(int fingerId)
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject(fingerId);
    }
}
