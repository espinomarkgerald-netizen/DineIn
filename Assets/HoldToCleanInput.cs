using UnityEngine;
using UnityEngine.EventSystems;

public class HoldToCleanInput : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask eventLayerMask = ~0;
    [SerializeField] private float maxRayDistance = 300f;

    [Header("UI")]
    [SerializeField] private HoldToCleanUI puddleUi;
    [SerializeField] private HoldToCleanUI tableUi;

    private CleanableEvent currentTarget;
    private HoldToCleanUI currentUi;
    private bool holding;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (Input.touchSupported && Application.isMobilePlatform)
            TickTouch();
        else
            TickMouse();
    }

    private void TickMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI(-1)) return;
            TryBeginAt(Input.mousePosition);
        }

        holding = Input.GetMouseButton(0);
        TickHold();

        if (Input.GetMouseButtonUp(0))
            Cancel();
    }

    private void TickTouch()
    {
        if (Input.touchCount <= 0) return;

        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
        {
            if (IsPointerOverUI(t.fingerId)) return;
            TryBeginAt(t.position);
        }

        holding = (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary);
        TickHold();

        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            Cancel();
    }

    private void TryBeginAt(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, eventLayerMask, QueryTriggerInteraction.Collide))
            return;

        var cleanable = hit.collider.GetComponentInParent<CleanableEvent>();
        if (cleanable == null) return;

        if (!CanCleanThis(cleanable))
            return;

        currentTarget = cleanable;
        currentUi = PickUi(cleanable);
        if (currentUi != null)
            currentUi.Begin(cleanable);
    }

    private void TickHold()
    {
        if (currentTarget == null || currentUi == null) return;
        currentUi.TickHold(Time.deltaTime, holding);
    }

    private void Cancel()
    {
        if (currentUi != null)
            currentUi.Cancel();

        currentUi = null;
        currentTarget = null;
        holding = false;
    }

    private HoldToCleanUI PickUi(CleanableEvent cleanable)
    {
        if (cleanable.GetComponentInParent<TrayCleanable>() != null)
            return tableUi != null ? tableUi : puddleUi;

        if (cleanable.GetComponentInParent<TableMessEvent>() != null)
            return tableUi != null ? tableUi : puddleUi;

        return puddleUi != null ? puddleUi : tableUi;
    }

    private bool CanCleanThis(CleanableEvent cleanable)
    {
        var trayClean = cleanable.GetComponentInParent<TrayCleanable>();
        if (trayClean != null && !trayClean.IsArmed)
            return false;

        return true;
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        if (fingerId == -1) return EventSystem.current.IsPointerOverGameObject();
        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }
}