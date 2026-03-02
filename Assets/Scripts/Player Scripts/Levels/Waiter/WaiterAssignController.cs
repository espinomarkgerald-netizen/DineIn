using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class WaiterAssignController : MonoBehaviour
{
    [Header("Raycast")]
    public LayerMask customerLayer;
    public LayerMask boothLayer;
    public float maxRayDistance = 200f;

    [Header("Waiter Movement")]
    public NavMeshAgent waiterAgent;

    [Header("Input")]
    public bool ignoreWhenPointerOverUI = true;

    private CustomerGroup selectedGroup;

    private void Update()
    {
        // ✅ NEW: don't run seating/assignment input while carrying a tray
        // (prevents "Booth not available" when you're trying to deliver food)
        if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
            return;

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

        // 1) Customer priority
        foreach (var hit in hits)
        {
            if (((1 << hit.collider.gameObject.layer) & customerLayer) != 0)
            {
                var group = hit.collider.GetComponentInParent<CustomerGroup>();
                if (group != null)
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

        // Move waiter to booth approach point (optional)
        if (waiterAgent != null && booth.approachPoint != null)
            waiterAgent.SetDestination(booth.approachPoint.position);

        // IMPORTANT: Hook the seated event BEFORE calling AssignToBooth,
        // so we never miss the event.
        void HandleSeated(CustomerGroup g)
        {
            // Only respond to THIS group (safety)
            if (g != group) return;

            // Unsubscribe immediately to prevent repeats/leaks
            group.OnGroupSeated -= HandleSeated;

            // Spawn the menu book on the assigned booth
            if (booth != null)
                booth.SpawnMenuBook();

            Debug.Log($"Menu spawned for {group.name} at {booth.name}");
        }

        group.OnGroupSeated -= HandleSeated; // safety (avoid double sub)
        group.OnGroupSeated += HandleSeated;

        // Now assign them
        group.AssignToBooth(booth);

        // Clear selection
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