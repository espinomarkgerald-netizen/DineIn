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

    private CustomerGroup selectedGroup;
    private StaffRole staffRole;

    private void Awake()
    {
        staffRole = GetComponent<StaffRole>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
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

        // Mobile
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

        // PC / Editor
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
                HandleHostTap(hits);
                break;

            case StaffRole.Role.Waiter:
                HandleWaiterTap(hits);
                break;

            case StaffRole.Role.Busser:
                HandleBusserTap(hits);
                break;
        }
    }

    // =========================
    // HOST
    // =========================
    private void HandleHostTap(RaycastHit[] hits)
    {
        // 1) Customer priority
        foreach (var hit in hits)
        {
            if (((1 << hit.collider.gameObject.layer) & customerLayer) != 0)
            {
                var group = hit.collider.GetComponentInParent<CustomerGroup>();
                if (group != null && CanHostSelectGroup(group))
                {
                    SelectGroup(group);
                    return;
                }
            }
        }

        // 2) Booth (only if group selected)
        if (selectedGroup != null)
        {
            foreach (var hit in hits)
            {
                if (((1 << hit.collider.gameObject.layer) & boothLayer) != 0)
                {
                    var booth = hit.collider.GetComponentInParent<Booth>();
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

        return group.state == CustomerGroup.GroupState.Waiting ||
               group.state == CustomerGroup.GroupState.WalkingToLobby;
    }

    // =========================
    // WAITER
    // =========================
    private void HandleWaiterTap(RaycastHit[] hits)
    {
        Debug.Log("[Waiter] waiter branch next");
    }

    // =========================
    // BUSSER
    // =========================
    private void HandleBusserTap(RaycastHit[] hits)
    {
        Debug.Log("[Busser] busser branch next");
    }

    // =========================
    // SHARED SELECTION / ASSIGN
    // =========================
    private void SelectGroup(CustomerGroup group)
    {
        if (selectedGroup != null)
            selectedGroup.SetSelected(false);

        selectedGroup = group;
        selectedGroup.SetSelected(true);
        Debug.Log($"Selected group: {group.name}");
    }

    private void AssignGroupToBooth(CustomerGroup group, Booth booth)
    {
        if (group == null || booth == null) return;

        if (!booth.IsAvailableFor(group.Size))
        {
            Debug.Log("Booth not available.");
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

            Debug.Log($"Menu spawned for {group.name} at {booth.name}");
        }

        group.OnGroupSeated -= HandleSeated;
        group.OnGroupSeated += HandleSeated;

        group.AssignToBooth(booth);

        group.SetSelected(false);
        selectedGroup = null;
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