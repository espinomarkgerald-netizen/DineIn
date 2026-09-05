using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovement : MonoBehaviour
{
    private sealed class DeferredInteraction : IInteractable, ICancelableTaskTarget
    {
        private readonly Transform standPoint;
        private readonly float interactRadius;
        private Action onArrived;
        private Action onCancelled;
        private bool finished;

        public DeferredInteraction(
            Transform targetStandPoint,
            float targetInteractRadius,
            Action arrived,
            Action cancelled)
        {
            standPoint = targetStandPoint;
            interactRadius = Mathf.Max(0.25f, targetInteractRadius);
            onArrived = arrived;
            onCancelled = cancelled;
        }

        public Transform StandPoint => standPoint;
        public bool AutoReturnHome => false;
        public bool CanInteract() => !finished && standPoint != null;
        public float GetInteractRadius() => interactRadius;

        public void Interact(PlayerMovement mover)
        {
            if (finished) return;
            finished = true;
            Action callback = onArrived;
            Action recovery = onCancelled;
            onArrived = null;
            onCancelled = null;

            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                recovery?.Invoke();
            }
        }

        public void OnTaskCancelled()
        {
            if (finished) return;
            finished = true;
            Action callback = onCancelled;
            onArrived = null;
            onCancelled = null;

            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    public enum State
    {
        IdleAtHome,
        MovingToTarget,
        DoingJob,
        ReturningHome
    }

    [Header("Click / Move")]
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private LayerMask clickMask = ~0;
    [SerializeField] private float tapThreshold = 10f;

    [Header("Arrival")]
    [SerializeField] private float arriveDistance = 0.25f;
    [SerializeField] private bool useInteractRadiusArrival = true;
    [SerializeField] private float interactStopMultiplier = 0.85f;

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

    private float defaultStoppingDistance;
    private int destinationIssuedFrame = -1;

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

        int managementTerminalLayer = LayerMask.NameToLayer("ManagementTerminal");
        if (managementTerminalLayer >= 0)
            clickMask |= 1 << managementTerminalLayer;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        agent.updateRotation = false;
        agent.autoRepath = true;
        defaultStoppingDistance = agent.stoppingDistance;

        animationHelper = new PlayerMovementAnimation(this);
    }

    private void Start()
    {
        // Only fall back to Camera.main when no camera was injected externally (e.g. by PlayerSetup).
        if (activeCam == null)
            activeCam = Camera.main;

        lastCommandTime = Time.time;
        idleTimer = 0f;

        if (homePoint != null)
            GoHomeImmediate();
    }

    /// <summary>
    /// Re-evaluates and assigns the scene camera using the same priority logic as PlayerSetup.
    /// Call this after scene camera state changes (e.g. a room manager swapping cameras mid-session).
    /// </summary>
    public void RefreshSceneCamera()
    {
        Camera found = PlayerSetup.FindActiveSceneCamera();
        if (found != null)
            activeCam = found;
    }

    private bool tutorialPressStartedOnUI;

    private void Update()
    {
        // Tutorial popups can disappear on click. Remember the press independently
        // of ManagerPlayer's control flag; never turn its release into a world task.
        if (TutorialSystem.IsTutorialMode)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    tutorialPressStartedOnUI = TutorialCustomerFlowBridge.IsTutorialUIPress(touch.position, touch.fingerId);
            }
            else if (Input.GetMouseButtonDown(0))
                tutorialPressStartedOnUI = TutorialCustomerFlowBridge.IsTutorialUIPress(Input.mousePosition, -1);
        }

        if (activeCam == null) return;

        if (isPlayerControlled && state != State.DoingJob)
        {
            // Always handle touch input when touches are present (covers mobile + tablet).
            // Fall back to mouse input on desktop / editor when no touches are active.
            if (Input.touchCount > 0)
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
            if (TutorialSystem.IsTutorialMode && tutorialPressStartedOnUI) return;
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
            if (TutorialSystem.IsTutorialMode && tutorialPressStartedOnUI) return;
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

        if (activeCam == null)
        {
            activeCam = PlayerSetup.FindActiveSceneCamera();
            if (activeCam == null)
                activeCam = Camera.main;
            if (activeCam == null)
                return;
        }

        RegisterCommand();

        Ray ray = activeCam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, clickMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        WaiterHands ownedHands = WaiterHands.For(this);
        bool isCarryingTray = ownedHands != null && ownedHands.HasTray;
        bool isCarryingBill = ownedHands != null && ownedHands.HasBill;
        bool isCarryingMoney = ownedHands != null && ownedHands.HasMoney;
        bool isCarryingBag = TakeoutBagInteractable.PlayerHasHeldBag;

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

                // When holding a takeout bag, prioritise the matching takeout customer.
                if (isCarryingBag && it is TakeoutCustomerInteractable)
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

        float targetRadius = Mathf.Max(0f, target.GetInteractRadius());
        agent.stoppingDistance = Mathf.Max(defaultStoppingDistance, targetRadius * interactStopMultiplier);

        if (!TryStartPath(currentDestination))
            FailCurrentMove("That task cannot be reached from here.");
    }

    private void TickArrival()
    {
        if (state != State.MovingToTarget && state != State.ReturningHome) return;
        if (agent.pathPending) return;

        if (state == State.MovingToTarget && currentTarget != null && useInteractRadiusArrival)
        {
            float interactRadius = Mathf.Max(arriveDistance, currentTarget.GetInteractRadius());
            if (GetPlanarDistanceToCurrentTarget() <= interactRadius)
            {
                HandleArrival();
                return;
            }
        }

        float stopDist = Mathf.Max(agent.stoppingDistance, arriveDistance);

        if (!agent.hasPath)
        {
            // SetDestination may not expose its path until the following frame.
            // Do not reject a valid UI-triggered interaction during that handoff.
            if (Time.frameCount <= destinationIssuedFrame + 1)
                return;

            FailCurrentMove("That task cannot be reached from here.");
            return;
        }

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            if (agent.velocity.sqrMagnitude <= 0.01f)
                FailCurrentMove("That task cannot be reached from here.");
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
            agent.stoppingDistance = defaultStoppingDistance;

            if (autoFinishTask)
                autoFinishTask = false;

            return;
        }

        if (state != State.MovingToTarget)
            return;

        if (currentTarget == null)
        {
            state = State.IdleAtHome;
            agent.stoppingDistance = defaultStoppingDistance;

            if (autoFinishTask)
                autoFinishTask = false;

            return;
        }

        interactFired = true;
        state = State.DoingJob;

        var target = currentTarget;
        target.Interact(this);

        // Some interactions intentionally chain into a second destination
        // (for example, a busser picks up a dirty tray and immediately walks to
        // the sink). Do not let completion of the first target cancel that move.
        if (state == State.MovingToTarget && currentTarget != null &&
            !ReferenceEquals(currentTarget, target))
            return;

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
            agent.stoppingDistance = defaultStoppingDistance;
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
        agent.stoppingDistance = defaultStoppingDistance;
        state = State.IdleAtHome;

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
        NotifyTaskCancelled();
        UnlockTask();

        if (homePoint == null)
        {
            state = State.IdleAtHome;
            agent.stoppingDistance = defaultStoppingDistance;
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
        agent.stoppingDistance = defaultStoppingDistance;
        if (!TryStartPath(currentDestination))
            FailCurrentMove(null);
    }

    public void GoHomeImmediate()
    {
        NotifyTaskCancelled();
        UnlockTask();

        if (homePoint == null) return;

        agent.Warp(homePoint.position);
        ForceStopAgent();

        currentTarget = null;
        currentStandPoint = null;
        currentDestination = homePoint.position;
        interactFired = false;
        agent.stoppingDistance = defaultStoppingDistance;
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

    /// <summary>
    /// Runs a UI-selected interaction only after the Manager has physically
    /// reached its booth/customer approach point.
    /// </summary>
    public bool UI_MoveToAction(
        Transform standPoint,
        float interactRadius,
        Action onArrived,
        Action onCancelled = null)
    {
        if (standPoint == null || onArrived == null)
            return false;

        if (taskLocked)
        {
            onCancelled?.Invoke();
            return false;
        }

        NotifyTaskCancelled();

        DeferredInteraction interaction = new DeferredInteraction(
            standPoint,
            interactRadius,
            onArrived,
            onCancelled);

        LockTask(interaction);
        UI_MoveTo(interaction);
        return currentTarget == interaction && state == State.MovingToTarget;
    }

    public void UI_MoveToPoint(Vector3 worldPoint)
    {
        if (taskLocked)
            return;

        NotifyTaskCancelled();

        RegisterCommand();

        currentTarget = null;
        currentStandPoint = null;
        currentDestination = worldPoint;
        interactFired = false;

        state = State.MovingToTarget;
        agent.stoppingDistance = defaultStoppingDistance;
        if (!TryStartPath(currentDestination))
            FailCurrentMove("That destination cannot be reached.");
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
        NotifyTaskCancelled();
        UnlockTask();
        currentTarget = null;
        currentStandPoint = null;
        interactFired = false;
        agent.stoppingDistance = defaultStoppingDistance;
        ForceStopAgent();
        state = State.IdleAtHome;
    }

    private void NotifyTaskCancelled()
    {
        if (currentTarget is ICancelableTaskTarget currentCancelable)
            currentCancelable.OnTaskCancelled();

        if (lockedTarget != null && lockedTarget != currentTarget && lockedTarget is ICancelableTaskTarget lockedCancelable)
            lockedCancelable.OnTaskCancelled();
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
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsMoving", false);
        }
    }

    private bool TryStartPath(Vector3 requestedDestination)
    {
        if (agent == null || !agent.enabled)
            return false;

        if (!agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 3f, agent.areaMask))
        {
            agent.Warp(startHit.position);
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PlayerMovement] {name} is not on an active NavMesh.", this);
            return false;
        }

        // Booth/item anchors are often authored at tabletop or UI height.
        // Resolve their X/Z against the Manager's current NavMesh floor so an
        // elevated interaction point does not falsely report as unreachable.
        Vector3 floorProbe = requestedDestination;
        floorProbe.y = agent.nextPosition.y;

        if (!NavMesh.SamplePosition(
                floorProbe,
                out NavMeshHit hit,
                3f,
                agent.areaMask))
        {
            Debug.LogWarning(
                $"[PlayerMovement] No walkable point found near {requestedDestination}.",
                this);
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning(
                $"[PlayerMovement] No complete path found to {hit.position}.",
                this);
            return false;
        }

        currentDestination = hit.position;
        agent.isStopped = false;
        agent.ResetPath();
        bool accepted = agent.SetDestination(currentDestination);
        destinationIssuedFrame = accepted ? Time.frameCount : -1;
        return accepted;
    }

    private void FailCurrentMove(string warning)
    {
        NotifyTaskCancelled();
        UnlockTask();
        currentTarget = null;
        currentStandPoint = null;
        destinationIssuedFrame = -1;
        interactFired = false;
        state = State.IdleAtHome;

        if (agent != null)
            agent.stoppingDistance = defaultStoppingDistance;

        ForceStopAgent();

        if (!string.IsNullOrEmpty(warning) &&
            !(TutorialSystem.IsTutorialMode && warning == "That task cannot be reached from here."))
            WarningSlideUI.Instance?.Show(warning);
    }

    private float GetPlanarDistanceToCurrentTarget()
    {
        if (currentTarget == null)
            return float.MaxValue;

        Vector3 targetPos = currentStandPoint != null ? currentStandPoint.position : currentDestination;

        Vector3 a = transform.position;
        Vector3 b = targetPos;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
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
        NotifyTaskCancelled();
        UnlockTask();

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.stoppingDistance = defaultStoppingDistance;
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
