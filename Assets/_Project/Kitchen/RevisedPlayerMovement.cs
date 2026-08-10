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

    void ProcessClick(Vector2 screenPosition) {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            Transform hitRoot = hit.collider.transform;

            if (KitchenTutorialManager.Instance != null) {
                // 1. Check Station Lock (Handcuffs)
                if (!KitchenTutorialManager.Instance.IsInteractionAllowed(hitRoot)) {
                    ShowWarning("Follow the tutorial arrow!");
                    return;
                }

                // 2. Check Item Lock (NEW)
                TutorialStep currentStep = KitchenTutorialManager.Instance.GetCurrentStep();
                if (currentStep != null && !string.IsNullOrEmpty(currentStep.requiredItemName)) {
                    PlayerHolding hands = GetComponent<PlayerHolding>();

                    // If hands are empty OR the object name doesn't match, block them!
                    if (hands.heldObject == null || hands.heldObject.name != currentStep.requiredItemName) {
                        ShowWarning("You need the " + currentStep.requiredItemName + " first!");
                        return;
                    }
                }
            }

            bool hitStation = ExecuteStationLogic(hitRoot, false);
            if (!hitStation) {
                targetInteractable = null;
                targetStandPoint = null;
                MoveToTarget(hit.point);
            }
        }
    }

    // --- NEW: THE PUPPETEER FUNCTION ---
    // The Tutorial Manager calls this to force the Chef to move!
    public void ForceMoveToStation(Transform hitRoot) {
        ExecuteStationLogic(hitRoot, true);
    }

    // Extracted logic so both the Player and the AI can use it!
    private bool ExecuteStationLogic(Transform hitRoot, bool isPuppet) {
        // 1. PREP COOK EXCLUSIVE
        if (hitRoot.GetComponentInParent<Cupboard>()) {
            Cupboard cupboard = hitRoot.GetComponentInParent<Cupboard>();
            if (myRole != KitchenRole.PrepCook && !isPuppet) { ShowWarning("Only Prep Cook can grab ingredients!"); return true; }
            SetTargetPublic(cupboard.transform, cupboard.standPoint);
            return true;
        }
        // 2. PREP COOK EXCLUSIVE: Breader
        else if (hitRoot.GetComponentInParent<Grill>() && hitRoot.GetComponentInParent<Grill>().gameObject.name == "Breader") {
            Grill breader = hitRoot.GetComponentInParent<Grill>();
            if (myRole != KitchenRole.PrepCook && !isPuppet) { ShowWarning("Only Prep Cook can use the Breader!"); return true; }
            SetTargetPublic(breader.transform, breader.standPoint);
            return true;
        }
        // LINE COOK EXCLUSIVE
        else if (hitRoot.GetComponentInParent<Grill>()) {
            Grill grill = hitRoot.GetComponentInParent<Grill>();
            if (myRole != KitchenRole.LineCook && !isPuppet) { ShowWarning("Only Line Cook can use the Grill!"); return true; }
            SetTargetPublic(grill.transform, grill.standPoint);
            return true;
        } else if (hitRoot.GetComponentInParent<Fryer>()) {
            Fryer fryer = hitRoot.GetComponentInParent<Fryer>();
            if (myRole != KitchenRole.LineCook && !isPuppet) { ShowWarning("Only Line Cook can use the Fryer!"); return true; }
            SetTargetPublic(fryer.transform, fryer.standPoint);
            return true;
        }
        // 3. ASSEMBLER EXCLUSIVE
        else if (hitRoot.GetComponentInParent<DrinkDispenser>()) {
            DrinkDispenser dispenser = hitRoot.GetComponentInParent<DrinkDispenser>();
            if (myRole != KitchenRole.Assembler && !isPuppet) { ShowWarning("Only Assembler can get drinks!"); return true; }
            SetTargetPublic(dispenser.transform, dispenser.standPoint);
            return true;
        } else if (hitRoot.GetComponentInParent<DeliveryCounter>()) {
            DeliveryCounter delivery = hitRoot.GetComponentInParent<DeliveryCounter>();
            if (myRole != KitchenRole.Assembler && !isPuppet) { ShowWarning("Only Assembler can deliver orders!"); return true; }
            SetTargetPublic(delivery.transform, delivery.standPoint);
            return true;
        }
        // --- CUP SPAWNER EXCLUSIVE ---
        else if (hitRoot.GetComponentInParent<CupSpawner>()) {
            CupSpawner cupSpawner = hitRoot.GetComponentInParent<CupSpawner>();
            if (myRole != KitchenRole.Assembler && !isPuppet) { ShowWarning("Only Assembler can grab cups!"); return true; }
            SetTargetPublic(cupSpawner.transform, cupSpawner.standPoint);
            return true;
        }
        // 4. NEUTRAL (Counters, shelves, trash)
        else if (hitRoot.GetComponentInParent<Counter>()) {
            Counter counter = hitRoot.GetComponentInParent<Counter>();
            SetTargetPublic(counter.transform, counter.standPoint);
            return true;
        } else if (hitRoot.GetComponentInParent<Shelf>()) {
            Shelf shelf = hitRoot.GetComponentInParent<Shelf>();
            if (myRole != KitchenRole.PrepCook && !isPuppet) { ShowWarning("Only Prep Cook can take from shelves!"); return true; }
            SetTargetPublic(shelf.transform, shelf.standPoint);
            return true;
        } else if (hitRoot.GetComponentInParent<TrashCan>()) {
            TrashCan trashCan = hitRoot.GetComponentInParent<TrashCan>();
            SetTargetPublic(trashCan.transform, trashCan.standPoint);
            return true;
        }

        return false; // We didn't hit a station!
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
        if (TutorialWarningPopup.Instance != null) {
            TutorialWarningPopup.Instance.ShowWarning(message);
        } else if (DeliveryFeedback.Instance != null) {
            DeliveryFeedback.Instance.ShowRejection(message);
        }
        Debug.Log("REJECTED: " + message);
    }

    void CheckIfWeArrived() {
        if (targetInteractable == null || targetStandPoint == null) return;
        float distanceToTarget = Vector3.Distance(transform.position, targetStandPoint.position);

        if (distanceToTarget <= 1.5f) {
            PlayerHolding myHands = GetComponent<PlayerHolding>();

            // 1. DO THE ACTION
            if (targetInteractable.GetComponent<Cupboard>()) targetInteractable.GetComponent<Cupboard>().Interact(myHands);
            else if (targetInteractable.GetComponent<Shelf>()) targetInteractable.GetComponent<Shelf>().Interact(myHands);
            else if (targetInteractable.GetComponent<TrashCan>()) targetInteractable.GetComponent<TrashCan>().Interact(myHands);
            else if (targetInteractable.GetComponent<DrinkDispenser>()) targetInteractable.GetComponent<DrinkDispenser>().Interact(myHands);
            else if (targetInteractable.GetComponent<DeliveryCounter>()) targetInteractable.GetComponent<DeliveryCounter>().Interact(myHands);
            else if (targetInteractable.GetComponent<Counter>()) targetInteractable.GetComponent<Counter>().Interact(myHands);

            // 2. SAVE WHAT WE CLICKED, THEN CLEAR THE BRAIN IMMEDIATELY
            Transform grabbedObject = targetInteractable;
            targetInteractable = null;
            targetStandPoint = null;
            agent.isStopped = true;

            // 3. NOW TELL THE MANAGER. If the manager gives a new order, it won't be erased!
            if (KitchenTutorialManager.Instance != null) {
                KitchenTutorialManager.Instance.ReportInteraction(grabbedObject);
            }
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

    public void StopMovement() {
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = true;
            agent.ResetPath();
        }
        targetInteractable = null;
        targetStandPoint = null;
    }
}