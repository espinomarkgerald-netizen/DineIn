using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // ADDED: This lets the script talk to your UI!

[RequireComponent(typeof(NavMeshAgent))]
public class KitchenPlayerMovement : MonoBehaviour {
    private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private Camera cam;
    private Transform targetInteractable;
    private Transform targetStandPoint;

    void Awake() {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        agent.updateRotation = false;
    }

    void Start() {
        cam = Camera.main;
    }

    void Update() {
        HandleInput();
        UpdateAnimator();
        HandleRotation();
        CheckIfWeArrived();
    }

    void HandleInput() {
        // 1. MOBILE CHECK: Is a finger tapping the screen right now?
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {

            // --- THE UI SHIELD (MOBILE) ---
            // If the tap hit a UI element, stop right here and ignore the movement!
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue())) {
                return;
            }

            ProcessClick(Touchscreen.current.primaryTouch.position.ReadValue());
        }
        // 2. PC CHECK: Is the left mouse button clicking?
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {

            // --- THE UI SHIELD (PC) ---
            // If the mouse is hovering over a UI element, stop right here and ignore the movement!
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return;
            }

            ProcessClick(Mouse.current.position.ReadValue());
        }
    }

    void ProcessClick(Vector2 screenPosition) {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            if (hit.collider.TryGetComponent(out Shelf shelf)) {
                targetInteractable = hit.transform;
                targetStandPoint = shelf.standPoint != null ? shelf.standPoint : hit.transform;
                MoveToTarget(targetStandPoint.position);
            } else if (hit.collider.TryGetComponent(out Counter counter)) {
                targetInteractable = hit.transform;
                targetStandPoint = counter.standPoint != null ? counter.standPoint : hit.transform;
                MoveToTarget(targetStandPoint.position);
            } else if (hit.collider.TryGetComponent(out TrashCan trashCan)) {
                targetInteractable = hit.transform;
                targetStandPoint = trashCan.standPoint != null ? trashCan.standPoint : hit.transform;
                MoveToTarget(targetStandPoint.position);
            } else {
                targetInteractable = null;
                targetStandPoint = null;
                MoveToTarget(hit.point);
            }
        }
    }

    void CheckIfWeArrived() {
        if (targetInteractable == null || targetStandPoint == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, targetStandPoint.position);

        if (distanceToTarget <= 1.5f) {
            PlayerHolding myHands = GetComponent<PlayerHolding>();

            if (targetInteractable.TryGetComponent(out Shelf shelf)) {
                shelf.Interact(myHands);
            } else if (targetInteractable.TryGetComponent(out Counter counter)) {
                counter.Interact(myHands);
            } else if (targetInteractable.TryGetComponent(out TrashCan trashCan)) {
                trashCan.Interact(myHands);
            }

            targetInteractable = null;
            targetStandPoint = null;
            agent.isStopped = true;
        }
    }

    void UpdateAnimator() {
        if (animator == null) return;
        animator.SetFloat(speedParam, agent.velocity.magnitude);
    }

    void HandleRotation() {
        Vector3 vel = agent.velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.01f) return;
        Quaternion targetRot = Quaternion.LookRotation(vel);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
    }

    public void MoveToTarget(Vector3 targetPosition) {
        if (agent != null) {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
    }
}