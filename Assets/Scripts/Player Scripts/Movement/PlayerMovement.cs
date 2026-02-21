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
    [SerializeField] private float stoppingDistance = 0.12f;
    [SerializeField] private bool autoBraking = true;

    [Header("Stop Fix (Anti-Drift)")]
    [Tooltip("Extra slack added on top of stoppingDistance to treat as 'arrived'.")]
    [SerializeField] private float arriveSlack = 0.06f;

    [Tooltip("If remainingDistance is tiny and velocity is below this, force a full stop.")]
    [SerializeField] private float stopVelocityThreshold = 0.05f;

    [Header("Click / Raycast")]
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private LayerMask clickMask;
    [SerializeField] private float tapThreshold = 10f;

    [Header("NavMesh Snap")]
    [SerializeField] private float navMeshSnapRadius = 1.5f;

    [Header("Home / Idle Spot")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private float homeArriveSlack = 0.08f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float animLerpSpeed = 12f;

    [Header("Facing")]
    [SerializeField] private bool rotateToMovement = true;
    [SerializeField] private float rotationSpeed = 14f;

    private NavMeshAgent agent;
    private Camera activeCam;

    private Vector2 pressStartPos;
    private float smoothedSpeed;

    private State state = State.IdleAtHome;

    private IInteractable currentTarget;
    private Transform currentStandPoint;

    public void SetCamera(Camera cam) => activeCam = cam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = autoBraking;

        // We rotate manually for nicer control
        agent.updateRotation = false;

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

        // Allow clicks anytime except while doing an active job (like holding to clean)
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

            // If your camera script blocks too much, comment this out to test
            // if (CameraController.IsPanning) return;

            float dist = Vector2.Distance(pressStartPos, (Vector2)Input.mousePosition);
            if (dist > tapThreshold) return;

            TryClickInteractable(Input.mousePosition);
        }
    }

    private void HandleTouchInput()
    {
        // Only treat the first finger as the "tap" finger for interactions
        if (Input.touchCount <= 0) return;

        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
            pressStartPos = t.position;

        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            if (IsPointerOverUI(t.fingerId)) return;
            // if (CameraController.IsPanning) return;

            float dist = Vector2.Distance(pressStartPos, t.position);
            if (dist > tapThreshold) return;

            TryClickInteractable(t.position);
        }
    }

    // Finds nearest object that implements IInteractable
    private void TryClickInteractable(Vector2 screenPos)
    {
        Ray ray = activeCam.ScreenPointToRay(screenPos);

        // Includes Trigger colliders too (important for puddle triggers)
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, clickMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;
            if (!interactable.CanInteract()) continue;

            Transform stand = interactable.StandPoint != null ? interactable.StandPoint : hit.collider.transform;
            MoveToInteractable(interactable, stand);
            return;
        }
    }

    private void MoveToInteractable(IInteractable interactable, Transform standPoint)
    {
        currentTarget = interactable;
        currentStandPoint = standPoint;

        state = State.MovingToTarget;

        agent.isStopped = false;
        agent.ResetPath();

        Vector3 dest = GetNearestNavMeshPoint(currentStandPoint.position);
        agent.SetDestination(dest);
    }

    private void TickArrivals()
    {
        if (agent.pathPending) return;

        // Anti-drift: if we are basically at destination and almost not moving, kill all motion
        if (state == State.MovingToTarget || state == State.ReturningHome)
        {
            if (agent.hasPath && agent.remainingDistance <= (agent.stoppingDistance + arriveSlack))
            {
                if (agent.velocity.magnitude <= stopVelocityThreshold)
                {
                    ForceStopAgent();
                }
            }
        }

        // Arrived at target
        if (state == State.MovingToTarget && currentStandPoint != null)
        {
            if (HasArrived())
            {
                ForceStopAgent();

                state = State.DoingJob;

                currentTarget?.Interact(this);

                // Auto-return for normal objects (booth/customer/etc)
                if (currentTarget != null && currentTarget.AutoReturnHome)
                    FinishCurrentJob();
            }
        }

        // Arrived home
        if (state == State.ReturningHome && homePoint != null)
        {
            if (HasArrivedHome())
            {
                ForceStopAgent();
                state = State.IdleAtHome;
            }
        }
    }

    private bool HasArrived()
    {
        // agent.remainingDistance can be weird if no path, so handle both cases
        if (!agent.hasPath)
            return agent.velocity.magnitude <= stopVelocityThreshold;

        if (agent.remainingDistance > (agent.stoppingDistance + arriveSlack))
            return false;

        // Must also be almost stopped (prevents circle jitter near destination)
        return agent.velocity.magnitude <= (stopVelocityThreshold * 2f);
    }

    private bool HasArrivedHome()
    {
        if (!agent.hasPath)
            return agent.velocity.magnitude <= stopVelocityThreshold;

        float slack = Mathf.Max(agent.stoppingDistance, homeArriveSlack);
        if (agent.remainingDistance > slack)
            return false;

        return agent.velocity.magnitude <= (stopVelocityThreshold * 2f);
    }

    private void ForceStopAgent()
    {
        // This is the important part that kills drifting/circling
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    public void FinishCurrentJob()
    {
        currentTarget = null;
        currentStandPoint = null;
        ReturnHome();
    }

    public void ReturnHome()
    {
        if (homePoint == null)
        {
            state = State.IdleAtHome;
            return;
        }

        state = State.ReturningHome;

        agent.isStopped = false;
        agent.ResetPath();

        Vector3 dest = GetNearestNavMeshPoint(homePoint.position);
        agent.SetDestination(dest);
    }

    public void GoHomeImmediate()
    {
        if (homePoint == null) return;

        Vector3 dest = GetNearestNavMeshPoint(homePoint.position);
        agent.Warp(dest);

        ForceStopAgent();
        state = State.IdleAtHome;
    }

    private Vector3 GetNearestNavMeshPoint(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;
        return pos;
    }

    private void UpdateAnimationAndFacing()
    {
        // Use REAL velocity, not desiredVelocity (desiredVelocity can keep “requesting” movement and cause circle jitter)
        Vector3 v = agent.velocity;
        v.y = 0f;

        float targetSpeed = v.magnitude;

        // if stopped/no path, force speed 0
        if (agent.isStopped || !agent.hasPath || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.02f))
            targetSpeed = 0f;

        smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, Time.deltaTime * animLerpSpeed);

        if (animator != null)
            animator.SetFloat(speedParam, smoothedSpeed);

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
