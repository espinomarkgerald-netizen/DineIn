using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovement : MonoBehaviour
{
    public enum State
    {
        IdleAtHome,
        MovingToTarget,
        DoingJob,
        ReturningHome
    }

    [Header("Click / Move")]
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private LayerMask clickMask;
    [SerializeField] private float tapThreshold = 10f;

    [Header("Arrival")]
    [SerializeField] private float arriveDistance = 0.25f;

    [Header("Home")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private bool returnHomeWhenIdle = true;
    [SerializeField] private float returnHomeIdleSeconds = 1.25f;
    [SerializeField] private float returnHomeDelay = 1.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string carryingBoolParam = "IsCarrying";

    [Header("Facing")]
    [SerializeField] private bool rotateToMovement = true;
    [SerializeField] private float rotationSpeed = 14f;

    private NavMeshAgent agent;
    private Camera activeCam;

    private Vector2 pressStartPos;

    private State state = State.IdleAtHome;

    private IInteractable currentTarget;
    private Transform currentStandPoint;
    private Vector3 currentDestination;

    private float idleTimer;
    private float lastCommandTime;

    private bool interactFired;
    private Coroutine returnRoutine;

    private PlayerMovementAnimation animationHelper;

    private bool isPlayerControlled;
    private bool autoFinishTask;
    private Vector2 lastPointerScreenPos;

    private bool taskLocked;
    private IInteractable lockedTarget;

    public NavMeshAgent Agent => agent;
    public Animator Animator => animator;
    public bool RotateToMovement => rotateToMovement;
    public float RotationSpeed => rotationSpeed;
    public string CarryingBoolParam => carryingBoolParam;

    public bool IsTaskLocked => taskLocked;
    public IInteractable LockedTarget => lockedTarget;
    public State CurrentState => state;
    public IInteractable CurrentTarget => currentTarget;

    public void SetCamera(Camera cam) => activeCam = cam;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        agent.updateRotation = false;
        agent.autoRepath = true;

        animationHelper = new PlayerMovementAnimation(this);
    }

    private void Start()
    {
        if (activeCam == null)
            activeCam = Camera.main;

        lastCommandTime = Time.time;
        idleTimer = 0f;

        if (homePoint != null)
            GoHomeImmediate();
    }

    private void Update()
    {
        if (activeCam == null) return;

        if (isPlayerControlled && state != State.DoingJob)
        {
            if (Input.touchSupported && Application.isMobilePlatform)
                HandleTouchInput();
            else
                HandleMouseInput();
        }

        TickArrival();
        TickIdleReturnHome();
        animationHelper.Tick();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
            pressStartPos = Input.mousePosition;

        if (Input.GetMouseButtonUp(0))
        {
            if (IsPointerOverUI(-1)) return;

            float dist = Vector2.Distance(pressStartPos, (Vector2)Input.mousePosition);
            if (dist > tapThreshold) return;

            lastPointerScreenPos = Input.mousePosition;
            TryClickInteractable(Input.mousePosition);
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount <= 0) return;

        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
            pressStartPos = t.position;

        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            if (IsPointerOverUI(t.fingerId)) return;

            float dist = Vector2.Distance(pressStartPos, t.position);
            if (dist > tapThreshold) return;

            lastPointerScreenPos = t.position;
            TryClickInteractable(t.position);
        }
    }

    private void TryClickInteractable(Vector2 screenPos)
    {
        if (taskLocked)
            return;

        RegisterCommand();

        Ray ray = activeCam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, clickMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool isCarryingTray = WaiterHands.Instance != null && WaiterHands.Instance.HasTray;
        bool isCarryingBill = WaiterHands.Instance != null && WaiterHands.Instance.HasBill;
        bool isCarryingMoney = WaiterHands.Instance != null && WaiterHands.Instance.HasMoney;

        IInteractable bestTarget = null;
        RaycastHit bestHit = default;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var interactables = hit.collider.GetComponentsInParent<IInteractable>(true);
            if (interactables == null || interactables.Length == 0) continue;

            for (int k = 0; k < interactables.Length; k++)
            {
                var it = interactables[k];
                if (it == null) continue;

                if (isCarryingMoney && it is CashierBoothInteractable)
                {
                    if (!it.CanInteract()) continue;

                    bestTarget = it;
                    bestHit = hit;
                    goto FoundTarget;
                }

                if (!it.CanInteract()) continue;

                if (isCarryingTray)
                {
                    if (it is CustomerDeliverInteractable || it.GetType().Name == "BoothDeliverInteractable")
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }

                    if (it is CashierBoothInteractable)
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }

                    if (!(it is ClickToMoveTarget))
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }
                }

                if (isCarryingBill)
                {
                    if (it is CashierBoothInteractable)
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }

                    if (!(it is ClickToMoveTarget))
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }
                }

                if (isCarryingMoney)
                {
                    if (!(it is ClickToMoveTarget))
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }
                }

                if (it is CashierBoothInteractable)
                {
                    bestTarget = it;
                    bestHit = hit;
                    goto FoundTarget;
                }

                if (bestTarget == null)
                {
                    bestTarget = it;
                    bestHit = hit;
                }
                else if (bestTarget is ClickToMoveTarget && !(it is ClickToMoveTarget))
                {
                    bestTarget = it;
                    bestHit = hit;
                }
            }
        }

    FoundTarget:
        if (bestTarget == null) return;

        Transform stand = bestTarget.StandPoint;
        Vector3 worldPos = stand != null ? stand.position : bestHit.point;

        MoveToInteractable(bestTarget, stand, worldPos);
    }

    private void MoveToInteractable(IInteractable target, Transform standPoint, Vector3 worldPos)
    {
        if (target == null) return;
        if (taskLocked && target != lockedTarget) return;

        RegisterCommand();

        currentTarget = target;
        currentStandPoint = standPoint;
        currentDestination = worldPos;

        interactFired = false;

        state = State.MovingToTarget;
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(currentDestination);
    }

    private void TickArrival()
    {
        if (state != State.MovingToTarget && state != State.ReturningHome) return;
        if (agent.pathPending) return;

        float stopDist = Mathf.Max(agent.stoppingDistance, arriveDistance);

        if (!agent.hasPath)
        {
            HandleArrival();
            return;
        }

        if (agent.remainingDistance > stopDist) return;
        if (agent.velocity.sqrMagnitude > 0.01f) return;

        HandleArrival();
    }

    private void HandleArrival()
    {
        if (interactFired) return;

        ForceStopAgent();

        if (state == State.ReturningHome)
        {
            state = State.IdleAtHome;

            if (autoFinishTask)
                autoFinishTask = false;

            return;
        }

        if (state != State.MovingToTarget)
            return;

        if (currentTarget == null)
        {
            state = State.IdleAtHome;

            if (autoFinishTask)
                autoFinishTask = false;

            return;
        }

        interactFired = true;
        state = State.DoingJob;

        var target = currentTarget;
        target.Interact(this);

        if (target.AutoReturnHome)
        {
            FinishCurrentJob();
        }
        else
        {
            UnlockTask();

            currentTarget = null;
            currentStandPoint = null;
            ForceStopAgent();
            state = State.IdleAtHome;

            if (autoFinishTask)
                autoFinishTask = false;
        }
    }

    public void FinishCurrentJob()
    {
        UnlockTask();

        currentTarget = null;
        currentStandPoint = null;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        returnRoutine = StartCoroutine(ReturnHomeDelayed());
    }

    private IEnumerator ReturnHomeDelayed()
    {
        float startedAt = Time.time;
        yield return new WaitForSeconds(returnHomeDelay);

        if (lastCommandTime > startedAt)
            yield break;

        ReturnHome();
    }

    private void TickIdleReturnHome()
    {
        if (!returnHomeWhenIdle) return;
        if (homePoint == null) return;
        if (state == State.DoingJob) return;
        if (state == State.ReturningHome) return;
        if (state == State.MovingToTarget) return;

        float dist = Vector3.Distance(transform.position, homePoint.position);
        if (dist <= Mathf.Max(agent.stoppingDistance, arriveDistance) + 0.05f)
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= returnHomeIdleSeconds)
            ReturnHome();
    }

    public void ReturnHome()
    {
        UnlockTask();

        if (homePoint == null)
        {
            state = State.IdleAtHome;
            if (autoFinishTask)
                autoFinishTask = false;
            return;
        }

        RegisterCommand();

        currentTarget = null;
        currentStandPoint = null;
        currentDestination = homePoint.position;
        interactFired = false;

        state = State.ReturningHome;

        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(currentDestination);
    }

    public void GoHomeImmediate()
    {
        UnlockTask();

        if (homePoint == null) return;

        agent.Warp(homePoint.position);
        ForceStopAgent();

        currentTarget = null;
        currentStandPoint = null;
        currentDestination = homePoint.position;
        interactFired = false;
        state = State.IdleAtHome;
    }

    public void UI_MoveTo(IInteractable target)
    {
        if (target == null) return;
        if (taskLocked && target != lockedTarget) return;

        RegisterCommand();

        Transform stand = target.StandPoint;
        Vector3 worldPos = stand != null ? stand.position : transform.position;

        MoveToInteractable(target, stand, worldPos);
    }

    public void UI_MoveToPoint(Vector3 worldPoint)
    {
        if (taskLocked)
            return;

        RegisterCommand();

        currentTarget = null;
        currentStandPoint = null;
        currentDestination = worldPoint;
        interactFired = false;

        state = State.MovingToTarget;
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(currentDestination);
    }

    public void LockTask(IInteractable target)
    {
        if (target == null) return;

        taskLocked = true;
        lockedTarget = target;
    }

    public void UnlockTask()
    {
        taskLocked = false;
        lockedTarget = null;
    }

    public void CancelLockedTask()
    {
        UnlockTask();
        currentTarget = null;
        currentStandPoint = null;
        interactFired = false;
        ForceStopAgent();
        state = State.IdleAtHome;
    }

    private void RegisterCommand()
    {
        lastCommandTime = Time.time;
        idleTimer = 0f;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }

    private void ForceStopAgent()
    {
        if (agent == null) return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsMoving", false);
        }
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;

        if (fingerId == -1)
            return EventSystem.current.IsPointerOverGameObject();

        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private void OnEnable()
    {
        WaiterHands.OnHandsStateChanged += HandleHandsStateChanged;
    }

    private void OnDisable()
    {
        WaiterHands.OnHandsStateChanged -= HandleHandsStateChanged;
    }

    private void HandleHandsStateChanged()
    {
        if (!isPlayerControlled) return;
        if (taskLocked) return;
        TryRefreshInteractableNow();
    }

    private void TryRefreshInteractableNow()
    {
        TryClickInteractable(lastPointerScreenPos);
    }

    public void StopForRoleSwitch()
    {
        UnlockTask();

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        currentTarget = null;
        currentStandPoint = null;
        interactFired = false;
        state = State.IdleAtHome;

        if (animator != null)
            animator.Play("idle", 0, 0f);
    }

    public void ResumeAfterRoleSwitch()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.isStopped = false;
    }

    public bool CanSwitchRole()
    {
        return !taskLocked;
    }

    public void SetPlayerControlled(bool value)
    {
        isPlayerControlled = value;
    }

    public void BeginAutoFinish()
    {
        autoFinishTask = true;
    }

    public void CancelAutoFinish()
    {
        autoFinishTask = false;
    }

    public bool IsPlayerControlled()
    {
        return isPlayerControlled;
    }

    public bool IsActiveControlledRole()
    {
        return isPlayerControlled;
    }
}