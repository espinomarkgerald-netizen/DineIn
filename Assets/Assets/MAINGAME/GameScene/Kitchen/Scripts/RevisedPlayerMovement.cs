using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class KitchenPlayerMovement : MonoBehaviour {
    private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private Camera cam;
    private Transform targetInteractable;
    private Transform targetStandPoint;

    // --- NEW: ROLE SYSTEM VARIABLES ---
    [Header("Chef Identity")]
    public KitchenRole myRole;
    public bool isActivePlayer = false;

    public bool isBusy = false;

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
        // --- NEW: If this chef is NOT the active player, ignore all clicks! ---
        if (!isActivePlayer || isBusy) return;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue())) return;
            CancelMenus();
            ProcessClick(Touchscreen.current.primaryTouch.position.ReadValue());
        } else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            CancelMenus();
            ProcessClick(Mouse.current.position.ReadValue());
        }
    }

    private void CancelMenus() {
        if (DrinkDispenser.activeDispenser != null) DrinkDispenser.activeDispenser.CloseMenu();
        if (Cupboard.activeCupboard != null) Cupboard.activeCupboard.CloseMenu();
    }

    // --- THE BOUNCER: Checking roles before we walk! ---
    void ProcessClick(Vector2 screenPosition) {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {

            // 1. PREP COOK EXCLUSIVE
            if (hit.collider.TryGetComponent(out Cupboard cupboard)) {
                if (myRole != KitchenRole.PrepCook) { ShowWarning("Only Prep Cook can grab ingredients!"); return; }
                SetTarget(hit.transform, cupboard.standPoint);
            }
            // 2. LINE COOK EXCLUSIVE
            else if (hit.collider.TryGetComponent(out Grill grill)) {
                if (myRole != KitchenRole.LineCook) { ShowWarning("Only Line Cook can use the Grill!"); return; }
                SetTarget(hit.transform, grill.standPoint);
            } else if (hit.collider.TryGetComponent(out Fryer fryer)) {
                if (myRole != KitchenRole.LineCook) { ShowWarning("Only Line Cook can use the Fryer!"); return; }
                SetTarget(hit.transform, fryer.standPoint);
            }
              // 3. ASSEMBLER EXCLUSIVE
              else if (hit.collider.TryGetComponent(out DrinkDispenser dispenser)) {
                if (myRole != KitchenRole.Assembler) { ShowWarning("Only Assembler can get drinks!"); return; }
                SetTarget(hit.transform, dispenser.standPoint);
            } else if (hit.collider.TryGetComponent(out DeliveryCounter delivery)) {
                if (myRole != KitchenRole.Assembler) { ShowWarning("Only Assembler can deliver orders!"); return; }
                SetTarget(hit.transform, delivery.standPoint);
            }
              // 4. NEUTRAL (Anyone can use normal counters to pass food, shelves, and trash)
              else if (hit.collider.TryGetComponent(out Counter counter)) {
                SetTarget(hit.transform, counter.standPoint);
            } else if (hit.collider.TryGetComponent(out Shelf shelf)) {
                if (myRole != KitchenRole.PrepCook) { ShowWarning("Only Prep Cook can take from shelves!"); return; }
                SetTarget(hit.transform, shelf.standPoint);
            } else if (hit.collider.TryGetComponent(out TrashCan trashCan)) {
                SetTarget(hit.transform, trashCan.standPoint);
            }
              // 5. JUST WALKING ON THE FLOOR
              else {
                targetInteractable = null;
                targetStandPoint = null;
                MoveToTarget(hit.point);
            }
        }
    }

    private void SetTarget(Transform interactable, Transform standPoint) {
        targetInteractable = interactable;
        targetStandPoint = standPoint != null ? standPoint : interactable;
        MoveToTarget(targetStandPoint.position);
    }

    private void ShowWarning(string message) {
        if (DeliveryFeedback.Instance != null) {
            DeliveryFeedback.Instance.ShowRejection(message);
        }
        Debug.Log("REJECTED: " + message);
    }

    void CheckIfWeArrived() {
        if (targetInteractable == null || targetStandPoint == null) return;
        float distanceToTarget = Vector3.Distance(transform.position, targetStandPoint.position);

        if (distanceToTarget <= 1.5f) {
            PlayerHolding myHands = GetComponent<PlayerHolding>();

            // Trigger the interaction based on what it is
            if (targetInteractable.TryGetComponent(out Cupboard cupboard)) cupboard.Interact(myHands);
            else if (targetInteractable.TryGetComponent(out Shelf shelf)) shelf.Interact(myHands);
            else if (targetInteractable.TryGetComponent(out Counter counter)) counter.Interact(myHands);
            else if (targetInteractable.TryGetComponent(out TrashCan trashCan)) trashCan.Interact(myHands);

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

    // --- NEW: Helper to stop the agent instantly when switching roles ---
    public void StopMovement() {
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = true;
            agent.ResetPath();
        }
        targetInteractable = null;
        targetStandPoint = null;
    }
}