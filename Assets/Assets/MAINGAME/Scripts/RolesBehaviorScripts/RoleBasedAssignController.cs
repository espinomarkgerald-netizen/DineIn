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
    private LobbyLineManager lineManager;

    private void Awake()
    {
        staffRole = GetComponent<StaffRole>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        lineManager = FindFirstObjectByType<LobbyLineManager>();
    }

    private void Update()
    {
        if (RoleManager.Instance == null || !RoleManager.Instance.IsActiveRole(gameObject))
            return;

        if (staffRole == null)
            return;

        if (staffRole.role == StaffRole.Role.Waiter)
        {
            if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
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

                CustomerGreetBubbleSpawner.Instance?.Show(
                    group,
                    group.UIAnchor != null ? group.UIAnchor : group.transform,
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
            ShowWarning("Wait for customers...");
            return false;
        }

        if (group != front)
        {
            ShowWarning("Serve the first customer in line.");
            return false;
        }

        return true;
    }

    private void HandleWaiterTap(RaycastHit[] hits)
    {
        CustomerGroup group = GetTappedAssignableGroup(hits);
        if (group != null)
            ShowWarning("Only the host can assign customers to a table.");
    }

    private void HandleBusserTap(RaycastHit[] hits)
    {
        CustomerGroup group = GetTappedAssignableGroup(hits);
        if (group != null)
            ShowWarning("Only the host can assign customers to a table.");
    }

    private CustomerGroup GetTappedAssignableGroup(RaycastHit[] hits)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (((1 << hit.collider.gameObject.layer) & customerLayer) == 0)
                continue;

            CustomerGroup group = hit.collider.GetComponentInParent<CustomerGroup>();
            if (group == null) continue;
            if (!CanHostSelectGroup(group)) continue;

            return group;
        }

        return null;
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
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsActiveRole(gameObject)) return;
        if (staffRole == null || staffRole.role != StaffRole.Role.Host) return;

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
            ShowWarning("Table is occupied");
            return;
        }

        if (booth.seats == null || booth.seats.Count < group.Size)
        {
            int seatCount = booth.seats != null ? booth.seats.Count : 0;
            ShowWarning($"You can't assign a group of {group.Size} to a table with {seatCount} seats");
            return;
        }

        if (!booth.IsAvailableFor(group.Size))
        {
            ShowWarning("Table is not available");
            return;
        }

        if (agent != null && booth.approachPoint != null)
            agent.SetDestination(booth.approachPoint.position);

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

        group.SetSelected(false);
        selectedGroup = null;

        CustomerGreetBubbleSpawner.Instance?.Hide();
        BoothAssignArrowManager.Instance?.HideAll();
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