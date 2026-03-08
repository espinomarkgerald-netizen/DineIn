using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class HostAssignController : MonoBehaviour
{
    [Header("Raycast")]
    public LayerMask customerLayer;
    public LayerMask boothLayer;
    public float maxRayDistance = 200f;

    [Header("Host Movement")]
    public NavMeshAgent hostAgent;

    [Header("Input")]
    public bool ignoreWhenPointerOverUI = true;

    private CustomerGroup selectedGroup;

    private void Update()
    {
        if (RoleManager.Instance == null)
            return;

        if (!RoleManager.Instance.IsActiveRole(gameObject))
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[HostAssign] MouseDown detected");

            bool overUi = ignoreWhenPointerOverUI && IsPointerOverUI_Mouse();
            Debug.Log("[HostAssign] Pointer over UI = " + overUi);

            if (overUi)
                return;

            HandleTap(Input.mousePosition);
        }
    }

    private bool IsCurrentRoleHost()
    {
        var staffRole = GetComponent<StaffRole>();
        if (staffRole == null) return false;
        if (staffRole.role != StaffRole.Role.Host) return false;

        return enabled;
    }

    private void HandleTap(Vector2 screenPos)
    {
        Debug.Log("[HostAssign] HandleTap called");

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

        foreach (var hit in hits)
        {
            if (((1 << hit.collider.gameObject.layer) & customerLayer) != 0)
            {
                var group = hit.collider.GetComponentInParent<CustomerGroup>();
                if (group != null && CanSelectGroup(group))
                {
                    SelectGroup(group);
                    return;
                }
            }
        }

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

    private bool CanSelectGroup(CustomerGroup group)
    {
        if (group == null) return false;

        Debug.Log("[Host] Group state = " + group.state);

        return group.state == CustomerGroup.GroupState.Waiting;
    }

    private void SelectGroup(CustomerGroup group)
    {
        
        if (selectedGroup != null)
            selectedGroup.SetSelected(false);

        selectedGroup = group;
        selectedGroup.SetSelected(true);

        Debug.Log($"[Host] Selected group: {group.name}");
    }

    private void AssignGroupToBooth(CustomerGroup group, Booth booth)
    {
        if (group == null || booth == null) return;

        if (!booth.IsAvailableFor(group.Size))
        {
            Debug.Log("[Host] Booth not available.");
            return;
        }

        if (hostAgent != null && booth.approachPoint != null)
            hostAgent.SetDestination(booth.approachPoint.position);

        void HandleSeated(CustomerGroup g)
        {
            if (g != group) return;
            group.OnGroupSeated -= HandleSeated;

            if (booth != null)
                booth.SpawnMenuBook();

            Debug.Log($"[Host] Menu spawned for {group.name} at {booth.name}");
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