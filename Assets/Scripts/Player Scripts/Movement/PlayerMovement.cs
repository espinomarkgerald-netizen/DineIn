using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovement : MonoBehaviour
{
    public enum State { IdleAtHome, MovingToTarget, DoingJob, ReturningHome }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float angularSpeed = 900f;
    [SerializeField] private float stoppingDistance = 0.22f;
    [SerializeField] private bool autoBraking = true;

    [Header("Arrival / Stop")]
    [SerializeField] private float arriveSlack = 0.08f;
    [SerializeField] private float stopVelocityThreshold = 0.06f;
    [SerializeField] private float withinSlackConfirmTime = 0.08f; // needs to be inside slack for a short time

    [Header("Click / Raycast")]
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private LayerMask clickMask;
    [SerializeField] private float tapThreshold = 10f;

    [Header("NavMesh Targeting")]
    [SerializeField] private float navMeshSnapRadius = 2.0f;
    [SerializeField] private float reachableSearchMaxRadius = 2.5f;
    [SerializeField] private float reachableSearchStep = 0.35f;

    [Header("NavMesh Safety Snap")]
    [SerializeField] private float keepOnNavmeshCheckEvery = 0.25f;
    [SerializeField] private float warpBackIfOffBy = 0.35f;

    [Header("Home / Idle Spot")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private float homeArriveSlack = 0.10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float animLerpSpeed = 12f;
    [SerializeField] private string carryingBoolParam = "IsCarrying";

    [Header("Facing")]
    [SerializeField] private bool rotateToMovement = true;
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Return Delay")]
    [SerializeField] private float returnHomeDelay = 1.5f;

    private NavMeshAgent agent;
    private Camera activeCam;

    private Vector2 pressStartPos;
    private float smoothedSpeed;

    private State state = State.IdleAtHome;

    private IInteractable currentTarget;
    private Transform currentStandPoint;

    private Vector3 currentDestination;     // navmesh destination we actually set
    private Vector3 desiredStandWorldPoint; // raw target point (standpoint) for distance fallback

    private float snapTimer;

    private float withinSlackTimer;
    private bool interactFired;

    public void SetCamera(Camera cam) => activeCam = cam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = autoBraking;

        agent.updateRotation = false;
        agent.autoRepath = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    void Start()
    {
        if (activeCam == null) activeCam = Camera.main;

        if (homePoint != null)
            GoHomeImmediate();
        else
            state = State.IdleAtHome;
    }

    void Update()
    {
        if (activeCam == null) return;

        KeepAgentSnappedToNavmesh();

        if (state != State.DoingJob)
        {
            if (Input.touchSupported && Application.isMobilePlatform)
                HandleTouchInput();
            else
                HandleMouseInput();
        }

        TickArrivals();
        UpdateAnimationAndFacing();
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

            TryClickInteractable(t.position);
        }
    }

    private void TryClickInteractable(Vector2 screenPos)
    {
        Ray ray = activeCam.ScreenPointToRay(screenPos);
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, clickMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool isCarryingTray = (WaiterHands.Instance != null && WaiterHands.Instance.HasTray);
        bool isCarryingBill = (WaiterHands.Instance != null && WaiterHands.Instance.HasBill);

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
                if (!it.CanInteract()) continue;

                if (isCarryingTray)
                {
                    if (it is CustomerDeliverInteractable || it.GetType().Name == "BoothDeliverInteractable")
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

                if (it is CashierBoothInteractable)
                {
                    bestTarget = it;
                    bestHit = hit;
                    goto FoundTarget;
                }

                if (isCarryingBill)
                {
                    if (!(it is ClickToMoveTarget))
                    {
                        bestTarget = it;
                        bestHit = hit;
                        goto FoundTarget;
                    }
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
        if (bestTarget != null)
        {
            Transform stand = bestTarget.StandPoint != null ? bestTarget.StandPoint : null;
            Vector3 worldPos = (stand != null) ? stand.position : bestHit.point;
            MoveToInteractable(bestTarget, stand, worldPos);
        }
    }

    private void MoveToInteractable(IInteractable interactable, Transform standPoint, Vector3 desiredWorldPoint)
    {
        currentTarget = interactable;
        currentStandPoint = standPoint;

        interactFired = false;
        withinSlackTimer = 0f;

        state = State.MovingToTarget;
        agent.isStopped = false;

        Vector3 raw = (standPoint != null) ? standPoint.position : desiredWorldPoint;
        desiredStandWorldPoint = raw;

        Vector3 snapped = GetNearestNavMeshPoint(raw, 4.0f);

        if (!IsPathCompleteTo(snapped))
        {
            if (TryFindReachableDestination(raw, out Vector3 reachable))
                snapped = reachable;
            else
                snapped = GetNearestNavMeshPoint(raw, 8.0f);
        }

        currentDestination = snapped;

        agent.ResetPath();
        agent.SetDestination(currentDestination);

        // Debug.Log($"[Mover] Heading to: {interactable.GetType().Name} at {currentDestination}");
    }

    private void TickArrivals()
    {
        if (state != State.MovingToTarget && state != State.ReturningHome) return;

        if (agent.pathPending) return;

        float slack = agent.stoppingDistance + arriveSlack;
        if (state == State.ReturningHome) slack = agent.stoppingDistance + homeArriveSlack;

        bool hasValidDistance = agent.hasPath && agent.remainingDistance >= 0f && agent.remainingDistance != Mathf.Infinity;

        Vector3 p = transform.position; p.y = 0f;

        Vector3 navDest = currentDestination; navDest.y = 0f;
        float destDist = Vector3.Distance(p, navDest);

        Vector3 stand = desiredStandWorldPoint; stand.y = 0f;
        float standDist = Vector3.Distance(p, stand);

        bool insideSlack =
            (hasValidDistance && agent.remainingDistance <= slack) ||
            destDist <= slack ||
            standDist <= slack;

        if (!insideSlack)
        {
            withinSlackTimer = 0f;
            return;
        }

        withinSlackTimer += Time.deltaTime;

        float v = agent.velocity.magnitude;
        bool slowEnough = v <= (stopVelocityThreshold * 3f);

        if (withinSlackTimer < withinSlackConfirmTime && !slowEnough)
            return;

        HandleArrivalOnce();
    }

    private void HandleArrivalOnce()
    {
        if (interactFired) return;

        ForceStopAgent();

        if (state == State.MovingToTarget)
        {
            state = State.DoingJob;
            interactFired = true;

            var capturedTarget = currentTarget;
            capturedTarget?.Interact(this);

            if (capturedTarget != null)
            {
                if (capturedTarget.AutoReturnHome)
                {
                    FinishCurrentJob();
                }
                else
                {
                    currentTarget = null;
                    currentStandPoint = null;
                    state = State.IdleAtHome;
                }
            }
            else
            {
                state = State.IdleAtHome;
            }
        }
        else if (state == State.ReturningHome)
        {
            state = State.IdleAtHome;
        }
    }

    private void ForceStopAgent()
    {
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    public void FinishCurrentJob()
    {
        currentTarget = null;
        currentStandPoint = null;
        StartCoroutine(ReturnHomeDelayed());
    }

    private System.Collections.IEnumerator ReturnHomeDelayed()
    {
        yield return new WaitForSeconds(returnHomeDelay);
        ReturnHome();
    }

    public void ReturnHome()
    {
        if (homePoint == null)
        {
            state = State.IdleAtHome;
            return;
        }

        currentTarget = null;
        currentStandPoint = null;

        interactFired = false;
        withinSlackTimer = 0f;

        state = State.ReturningHome;

        agent.isStopped = false;
        agent.ResetPath();

        Vector3 desired = homePoint.position;
        desiredStandWorldPoint = desired;

        if (TryFindReachableDestination(desired, out Vector3 dest))
        {
            currentDestination = dest;
            agent.SetDestination(currentDestination);
        }
        else
        {
            ForceStopAgent();
            state = State.IdleAtHome;
        }
    }

    public void GoHomeImmediate()
    {
        if (homePoint == null) return;

        Vector3 desired = homePoint.position;

        if (TryFindReachableDestination(desired, out Vector3 dest))
            agent.Warp(dest);

        ForceStopAgent();
        state = State.IdleAtHome;
    }

    private Vector3 GetNearestNavMeshPoint(Vector3 pos, float radius)
    {
        Vector3 p = pos;
        p.y = transform.position.y;

        if (NavMesh.SamplePosition(p, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return hit.position;

        if (NavMesh.SamplePosition(p, out hit, radius * 3f, NavMesh.AllAreas))
            return hit.position;

        return p;
    }

    private bool TryFindReachableDestination(Vector3 desiredWorld, out Vector3 result)
    {
        result = desiredWorld;

        if (!NavMesh.SamplePosition(desiredWorld, out NavMeshHit firstHit, navMeshSnapRadius, NavMesh.AllAreas))
            return false;

        if (IsPathCompleteTo(firstHit.position))
        {
            result = firstHit.position;
            return true;
        }

        Vector3 origin = firstHit.position;

        for (float r = reachableSearchStep; r <= reachableSearchMaxRadius; r += reachableSearchStep)
        {
            if (TryPickRingPoint(origin, r, out Vector3 candidate))
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, reachableSearchStep + 0.05f, NavMesh.AllAreas))
                {
                    if (IsPathCompleteTo(hit.position))
                    {
                        result = hit.position;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsPathCompleteTo(Vector3 dest)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(dest, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    private bool TryPickRingPoint(Vector3 center, float radius, out Vector3 point)
    {
        point = center;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        int samples = 10;
        float step = 360f / samples;

        for (int i = 0; i < samples; i++)
        {
            float a = step * i;
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * forward;
            Vector3 p = center + dir * radius;

            point = p;
            return true;
        }

        return false;
    }

    private void KeepAgentSnappedToNavmesh()
    {
        snapTimer += Time.deltaTime;
        if (snapTimer < keepOnNavmeshCheckEvery) return;
        snapTimer = 0f;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return;

        float dist = Vector3.Distance(transform.position, hit.position);
        if (dist >= warpBackIfOffBy)
        {
            agent.Warp(hit.position);
            ForceStopAgent();

            if (state == State.MovingToTarget || state == State.ReturningHome)
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.SetDestination(currentDestination);
            }
        }
    }

    private void UpdateAnimationAndFacing()
    {
        Vector3 v = agent.velocity;
        v.y = 0f;

        float targetSpeed = v.magnitude;

        if (agent.isStopped || !agent.hasPath || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.03f))
            targetSpeed = 0f;

        smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, Time.deltaTime * animLerpSpeed);

        if (animator != null)
        {
            animator.SetFloat(speedParam, smoothedSpeed);

            bool isCarrying = (WaiterHands.Instance != null && WaiterHands.Instance.HasTray);
            animator.SetBool(carryingBoolParam, isCarrying);
        }

        if (rotateToMovement && v.sqrMagnitude > 0.0004f)
        {
            Quaternion targetRot = Quaternion.LookRotation(v.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;

        if (fingerId == -1)
            return EventSystem.current.IsPointerOverGameObject();

        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }
}