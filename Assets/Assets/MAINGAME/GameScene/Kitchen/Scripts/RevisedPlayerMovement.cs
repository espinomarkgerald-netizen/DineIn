using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class KitchenPlayerMovement : MonoBehaviour {
    private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private Camera cam;
    private Transform targetInteractable;
    private Transform targetStandPoint;

    [Header("Chef Identity")]
    public KitchenRole myRole;
    public bool isActivePlayer = false;
    public bool isBusy = false;

    /// <summary>
    /// Assign any UI panels that should block world clicks when visible
    /// (e.g. the tutorial DialoguePanel). Movement is suppressed when the
    /// pointer lands inside one of these rects.
    /// </summary>
    [Header("UI Blocking Panels")]
    [SerializeField] private RectTransform[] blockingUIPanels;

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
        if (!isActivePlayer || isBusy) return;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
            Vector2 pos = Touchscreen.current.primaryTouch.position.ReadValue();
            if (IsPointerOverBlockingUI(pos)) return;
            if (DrinkDispenser.activeDispenser != null) {
                // Grace period: ignore the tap that opened the menu.
                if (DrinkDispenser.activeDispenser.OpenTimer > 0.15f) {
                    if (!DrinkDispenser.activeDispenser.IsPointerOverMenu(pos))
                        DrinkDispenser.activeDispenser.CloseMenu();
                }
                return;
            }
            if (Cupboard.activeCupboard != null) Cupboard.activeCupboard.CloseMenu();
            ProcessClick(pos);
        } else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
            Vector2 pos = Mouse.current.position.ReadValue();
            if (IsPointerOverBlockingUI(pos)) return;
            if (DrinkDispenser.activeDispenser != null) {
                // Grace period: ignore the tap that opened the menu.
                if (DrinkDispenser.activeDispenser.OpenTimer > 0.15f) {
                    if (!DrinkDispenser.activeDispenser.IsPointerOverMenu(pos))
                        DrinkDispenser.activeDispenser.CloseMenu();
                }
                return;
            }
            if (Cupboard.activeCupboard != null) Cupboard.activeCupboard.CloseMenu();
            ProcessClick(pos);
        }
    }

    /// <summary>
    /// Returns true if the screen-space position falls inside any of the
    /// explicitly assigned blocking UI panels. This is more reliable than
    /// EventSystem.IsPointerOverGameObject() which triggers for any canvas.
    /// </summary>
    private bool IsPointerOverBlockingUI(Vector2 screenPos) {
        if (blockingUIPanels == null) return false;
        foreach (RectTransform panel in blockingUIPanels) {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera canvasCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(panel, screenPos, canvasCam))
                return true;
        }
        return false;
    }

    void ProcessClick(Vector2 screenPosition) {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {

            Transform hitRoot = hit.collider.transform;

            // 1. PREP COOK EXCLUSIVE
            if (hitRoot.GetComponentInParent<Cupboard>()) {
                Cupboard cupboard = hitRoot.GetComponentInParent<Cupboard>();
                if (myRole != KitchenRole.PrepCook) { ShowWarning("Only Prep Cook can grab ingredients!"); return; }
                SetTargetPublic(cupboard.transform, cupboard.standPoint);
            }
            // 2. PREP COOK EXCLUSIVE: Breader (uses Grill script but belongs to Prep Cook)
            else if (hitRoot.GetComponentInParent<Grill>() && hitRoot.GetComponentInParent<Grill>().gameObject.name == "Breader") {
                Grill breader = hitRoot.GetComponentInParent<Grill>();
                if (myRole != KitchenRole.PrepCook) { ShowWarning("Only Prep Cook can use the Breader!"); return; }
                SetTargetPublic(breader.transform, breader.standPoint);
            }
            // LINE COOK EXCLUSIVE
            else if (hitRoot.GetComponentInParent<Grill>()) {
                Grill grill = hitRoot.GetComponentInParent<Grill>();
                if (myRole != KitchenRole.LineCook) { ShowWarning("Only Line Cook can use the Grill!"); return; }
                SetTargetPublic(grill.transform, grill.standPoint);
            } else if (hitRoot.GetComponentInParent<Fryer>()) {
                Fryer fryer = hitRoot.GetComponentInParent<Fryer>();
                if (myRole != KitchenRole.LineCook) { ShowWarning("Only Line Cook can use the Fryer!"); return; }
                SetTargetPublic(fryer.transform, fryer.standPoint);
            }
            // 3. ASSEMBLER EXCLUSIVE
            else if (hitRoot.GetComponentInParent<DrinkDispenser>()) {
                DrinkDispenser dispenser = hitRoot.GetComponentInParent<DrinkDispenser>();
                if (myRole != KitchenRole.Assembler) { ShowWarning("Only Assembler can get drinks!"); return; }
                SetTargetPublic(dispenser.transform, dispenser.standPoint);
            } else if (hitRoot.GetComponentInParent<DeliveryCounter>()) {
                DeliveryCounter delivery = hitRoot.GetComponentInParent<DeliveryCounter>();
                if (myRole != KitchenRole.Assembler) { ShowWarning("Only Assembler can deliver orders!"); return; }
                SetTargetPublic(delivery.transform, delivery.standPoint);
            }
            // --- NEW: CUP SPAWNER EXCLUSIVE ---
            else if (hitRoot.GetComponentInParent<CupSpawner>()) {
                CupSpawner cupSpawner = hitRoot.GetComponentInParent<CupSpawner>();
                if (myRole != KitchenRole.Assembler) { ShowWarning("Only Assembler can grab cups!"); return; }
                SetTargetPublic(cupSpawner.transform, cupSpawner.standPoint);
            }
            // 4. NEUTRAL (Counters, shelves, trash)
            else if (hitRoot.GetComponentInParent<Counter>()) {
                Counter counter = hitRoot.GetComponentInParent<Counter>();
                SetTargetPublic(counter.transform, counter.standPoint);
            } else if (hitRoot.GetComponentInParent<Shelf>()) {
                Shelf shelf = hitRoot.GetComponentInParent<Shelf>();
                if (myRole != KitchenRole.PrepCook) { ShowWarning("Only Prep Cook can take from shelves!"); return; }
                SetTargetPublic(shelf.transform, shelf.standPoint);
            } else if (hitRoot.GetComponentInParent<TrashCan>()) {
                TrashCan trashCan = hitRoot.GetComponentInParent<TrashCan>();
                SetTargetPublic(trashCan.transform, trashCan.standPoint);
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
        SetTargetPublic(interactable, standPoint);
    }

    public void SetTargetPublic(Transform interactable, Transform standPoint) {
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
        // Compare XZ only — StandPoints may sit at a different Y than the agent.
        Vector3 selfXZ   = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetXZ = new Vector3(targetStandPoint.position.x, 0f, targetStandPoint.position.z);
        float distanceToTarget = Vector3.Distance(selfXZ, targetXZ);

        if (distanceToTarget <= 1.5f) {
            PlayerHolding myHands = GetComponent<PlayerHolding>();

            if (targetInteractable.GetComponent<Cupboard>()) targetInteractable.GetComponent<Cupboard>().Interact(myHands);
            else if (targetInteractable.GetComponent<Shelf>()) targetInteractable.GetComponent<Shelf>().Interact(myHands);

            // Because CupSpawner inherits from Counter, this ONE line handles both normal counters AND the Cup Spawner automatically!
            else if (targetInteractable.GetComponent<Counter>()) targetInteractable.GetComponent<Counter>().Interact(myHands);

            else if (targetInteractable.GetComponent<TrashCan>()) targetInteractable.GetComponent<TrashCan>().Interact(myHands);

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
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
    }

    public void StopMovement() {
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = true;
            agent.ResetPath();
        }
        targetInteractable = null;
        targetStandPoint = null;
    }
}