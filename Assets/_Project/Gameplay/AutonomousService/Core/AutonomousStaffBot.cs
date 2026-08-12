using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives an existing staff character with its NavMeshAgent and existing animator.
/// The service coordinator owns task choice; this component only handles movement,
/// short work pauses, and the animation values shared by the player characters.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AutonomousStaffBot : MonoBehaviour
{
    public enum StaffState
    {
        IdleAtHome,
        Reacting,
        MovingToTask,
        Working,
        ReturningHome
    }

    [Header("Movement")]
    [SerializeField] private float arrivalDistance = 0.35f;
    [SerializeField] private float rotationSpeed = 14f;
    [SerializeField] private float maxTravelSeconds = 20f;
    [SerializeField] private float maxInteractionTravelSeconds = 6f;
    [SerializeField] private float navMeshReadyTimeout = 2f;
    [SerializeField] private float destinationSampleRadius = 1.5f;

    [Header("Behaviour Timing")]
    [SerializeField] private Vector2 reactionDelayRange = new Vector2(0.1f, 0.35f);
    [SerializeField, Range(0f, 0.4f)] private float workTimeVariance = 0.15f;

    [Header("Home")]
    [SerializeField] private Transform homePoint;

    [Header("Idle Presentation")]
    [SerializeField] private bool enableIdlePresentation;
    [SerializeField] private Vector2 idleLookIntervalRange = new Vector2(2.5f, 5.5f);
    [SerializeField] private Vector2 happyIdleIntervalRange = new Vector2(7f, 13f);
    [SerializeField] private float idleTurnSpeed = 4f;
    [SerializeField] private float happyIdleFallbackDuration = 2.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private Coroutine activeTask;
    private bool carrying;
    private Vector3 fallbackHomePosition;
    private Quaternion fallbackHomeRotation;
    private Transform[] idleLookTargets;
    private Quaternion idleLookRotation;
    private float nextIdleLookTime;
    private float nextHappyIdleTime;
    private float happyIdleEndTime;
    private float happyIdleDuration;
    private bool playingHappyIdle;

    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.idle");
    private static readonly int HappyIdleStateHash = Animator.StringToHash("Base Layer.Happy Idle");

    public bool IsBusy => activeTask != null;
    public bool LastMoveSucceeded { get; private set; }
    public StaffState CurrentState { get; private set; } = StaffState.IdleAtHome;

    private void Awake()
    {
        fallbackHomePosition = transform.position;
        fallbackHomeRotation = transform.rotation;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
        idleLookRotation = fallbackHomeRotation;
        happyIdleDuration = ResolveHappyIdleDuration();
    }

    private void Start()
    {
        if (agent == null)
            return;

        // Match PlayerMovement's proven facing behaviour. NavMeshAgent rotation
        // made these imported character rigs appear to strafe or run backwards.
        agent.updateRotation = false;
        agent.autoRepath = true;
        agent.isStopped = true;
    }

    private void Update()
    {
        if (agent == null)
            return;

        bool moving = agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;

        if (moving && playingHappyIdle)
            StopHappyIdle();

        FaceMovement(moving);

        if (!moving && CurrentState == StaffState.IdleAtHome)
            UpdateIdlePresentation();

        if (animator == null)
            return;

        animator.SetFloat("Speed", moving ? agent.velocity.magnitude : 0f);
        animator.SetBool("IsMoving", moving);
        animator.SetBool("IsCarrying", carrying);
    }

    private void FaceMovement(bool moving)
    {
        if (!moving)
            return;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    private void OnDisable()
    {
        if (activeTask != null)
        {
            StopCoroutine(activeTask);
            activeTask = null;
        }

        StopAgent();

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsMoving", false);
        }

        CurrentState = StaffState.IdleAtHome;
        playingHappyIdle = false;
    }

    public void ConfigureHome(Transform configuredHome, int avoidancePriority)
    {
        homePoint = configuredHome;

        if (agent != null)
            agent.avoidancePriority = Mathf.Clamp(avoidancePriority, 0, 99);
    }

    public void ConfigureIdlePresentation(params Transform[] lookTargets)
    {
        idleLookTargets = lookTargets;
        enableIdlePresentation = true;
        BeginIdlePresentation();
    }

    public void StartTask(IEnumerator task)
    {
        if (task == null || IsBusy)
            return;

        StopHappyIdle();
        activeTask = StartCoroutine(RunTask(task));
    }

    public void SetCarrying(bool value)
    {
        // The lobby carry controller is exclusively the two-handed tray pose.
        // Bills, money and bags remain attached to their hand anchors while the
        // bot keeps its normal idle/walk animation.
        WaiterHands waiterHands = GetComponent<WaiterHands>();
        BusserHands busserHands = GetComponent<BusserHands>();
        if (waiterHands != null || busserHands != null)
        {
            carrying = (waiterHands != null && waiterHands.HasTray) ||
                       (busserHands != null && busserHands.HasTray);
            return;
        }

        carrying = value;
    }

    public IEnumerator MoveTo(Transform target)
    {
        if (target == null)
            yield break;

        yield return MoveToInternal(target.position, StaffState.MovingToTask);
        if (LastMoveSucceeded)
            yield return FaceRotation(target.rotation);
    }

    public IEnumerator MoveTo(Vector3 destination)
    {
        yield return MoveToInternal(destination, StaffState.MovingToTask);
    }

    public IEnumerator MoveNear(Vector3 destination, float sampleRadius)
    {
        yield return MoveToInternal(
            destination,
            StaffState.MovingToTask,
            Mathf.Max(destinationSampleRadius, sampleRadius));
    }

    public IEnumerator MoveWithin(
        Vector3 destination,
        float acceptedDistance,
        float sampleRadius = -1f)
    {
        yield return MoveToInternal(
            destination,
            StaffState.MovingToTask,
            sampleRadius,
            Mathf.Max(arrivalDistance, acceptedDistance),
            maxInteractionTravelSeconds);
    }

    public IEnumerator FaceTowards(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            yield break;

        yield return FaceRotation(Quaternion.LookRotation(direction.normalized, Vector3.up));
    }

    public IEnumerator ReturnHome()
    {
        Vector3 destination = homePoint != null ? homePoint.position : fallbackHomePosition;

        yield return MoveToInternal(destination, StaffState.ReturningHome);
        CurrentState = StaffState.IdleAtHome;
        BeginIdlePresentation();
    }

    public IEnumerator WorkFor(float seconds)
    {
        CurrentState = StaffState.Working;

        float variance = Mathf.Max(0f, seconds) * workTimeVariance;
        float duration = Random.Range(
            Mathf.Max(0f, seconds - variance),
            Mathf.Max(0f, seconds + variance)
        );

        if (duration > 0f)
            yield return new WaitForSeconds(duration);
    }

    private IEnumerator MoveToInternal(
        Vector3 destination,
        StaffState movementState,
        float sampleRadiusOverride = -1f,
        float acceptedDistanceOverride = -1f,
        float maxTravelSecondsOverride = -1f)
    {
        LastMoveSucceeded = false;

        if (agent == null)
            yield break;

        float navMeshWaitStarted = Time.time;
        while (!agent.isOnNavMesh && Time.time - navMeshWaitStarted < navMeshReadyTimeout)
            yield return null;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[AutonomousStaffBot] {name} is not on a NavMesh and cannot move.", this);
            yield break;
        }

        Vector3 requestedDestination = destination;
        float acceptedDistance = acceptedDistanceOverride > 0f
            ? acceptedDistanceOverride
            : Mathf.Max(arrivalDistance, agent.stoppingDistance);
        bool isInteractionMove = acceptedDistanceOverride > 0f;

        Vector3 initialInteractionOffset = requestedDestination - transform.position;
        initialInteractionOffset.y = 0f;
        if (isInteractionMove && initialInteractionOffset.magnitude <= acceptedDistance)
        {
            StopAgent();
            LastMoveSucceeded = true;
            yield break;
        }

        float sampleRadius = sampleRadiusOverride > 0f
            ? sampleRadiusOverride
            : destinationSampleRadius;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            destination = hit.position;
        else
        {
            Debug.LogWarning(
                $"[AutonomousStaffBot] {name} could not resolve a walkable destination near " +
                $"{destination} within radius {sampleRadius:0.##}.",
                this);
            yield break;
        }

        CurrentState = movementState;
        agent.isStopped = false;
        agent.ResetPath();

        if (!agent.SetDestination(destination))
        {
            StopAgent();
            yield break;
        }

        float travelStarted = Time.time;
        bool arrived = false;
        float travelLimit = maxTravelSecondsOverride > 0f
            ? maxTravelSecondsOverride
            : maxTravelSeconds;
        float pathArrivalDistance = Mathf.Max(arrivalDistance, agent.stoppingDistance);
        Vector3 sampledToRequested = requestedDestination - destination;
        sampledToRequested.y = 0f;
        bool sampledPointIsValidInteraction = !isInteractionMove ||
            sampledToRequested.magnitude <= acceptedDistance;

        while (agent.isOnNavMesh)
        {
            Vector3 resolvedOffset = destination - transform.position;
            resolvedOffset.y = 0f;
            Vector3 interactionOffset = requestedDestination - transform.position;
            interactionOffset.y = 0f;

            bool reachedInteraction = isInteractionMove &&
                interactionOffset.magnitude <= acceptedDistance;
            bool reachedCompletePath = !agent.pathPending &&
                agent.pathStatus == NavMeshPathStatus.PathComplete &&
                sampledPointIsValidInteraction &&
                (agent.remainingDistance <= pathArrivalDistance ||
                 resolvedOffset.magnitude <= pathArrivalDistance);

            if (reachedInteraction || reachedCompletePath)
            {
                arrived = true;
                break;
            }

            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[AutonomousStaffBot] {name} could not find a valid path.", this);
                break;
            }

            if (Time.time - travelStarted >= travelLimit)
            {
                Debug.LogWarning(
                    $"[AutonomousStaffBot] {name} timed out after {travelLimit:0.#}s. " +
                    $"Requested={requestedDestination}, sampled={destination}, " +
                    $"interactionDistance={interactionOffset.magnitude:0.##}, " +
                    $"remainingDistance={agent.remainingDistance:0.##}, path={agent.pathStatus}.",
                    this);
                break;
            }

            yield return null;
        }

        StopAgent();
        LastMoveSucceeded = arrived;
    }

    private IEnumerator FaceRotation(Quaternion targetRotation)
    {
        float elapsed = 0f;
        const float maxTurnSeconds = 0.5f;

        while (Quaternion.Angle(transform.rotation, targetRotation) > 1.5f && elapsed < maxTurnSeconds)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void StopAgent()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void BeginIdlePresentation()
    {
        if (!enableIdlePresentation)
            return;

        ChooseIdleLookDirection();
        nextIdleLookTime = Time.time + RandomInRange(idleLookIntervalRange);
        nextHappyIdleTime = Time.time + RandomInRange(happyIdleIntervalRange);
    }

    private void UpdateIdlePresentation()
    {
        if (!enableIdlePresentation)
            return;

        if (Time.time >= nextIdleLookTime)
        {
            ChooseIdleLookDirection();
            nextIdleLookTime = Time.time + RandomInRange(idleLookIntervalRange);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            idleLookRotation,
            Time.deltaTime * idleTurnSpeed
        );

        if (playingHappyIdle)
        {
            if (Time.time >= happyIdleEndTime)
                StopHappyIdle();

            return;
        }

        if (Time.time >= nextHappyIdleTime)
            StartHappyIdle();
    }

    private void ChooseIdleLookDirection()
    {
        if (idleLookTargets != null && idleLookTargets.Length > 0)
        {
            int startIndex = Random.Range(0, idleLookTargets.Length);
            for (int offset = 0; offset < idleLookTargets.Length; offset++)
            {
                Transform target = idleLookTargets[(startIndex + offset) % idleLookTargets.Length];
                if (target == null)
                    continue;

                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.04f)
                    continue;

                idleLookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return;
            }
        }

        Quaternion homeRotation = homePoint != null ? homePoint.rotation : fallbackHomeRotation;
        idleLookRotation = homeRotation * Quaternion.Euler(0f, Random.Range(-65f, 65f), 0f);
    }

    private void StartHappyIdle()
    {
        nextHappyIdleTime = Time.time + RandomInRange(happyIdleIntervalRange);

        if (animator == null || !animator.HasState(0, HappyIdleStateHash))
            return;

        playingHappyIdle = true;
        happyIdleEndTime = Time.time + Mathf.Max(0.25f, happyIdleDuration);
        animator.CrossFade(HappyIdleStateHash, 0.15f);
    }

    private void StopHappyIdle()
    {
        if (!playingHappyIdle)
            return;

        playingHappyIdle = false;
        if (animator != null && animator.HasState(0, IdleStateHash))
            animator.CrossFade(IdleStateHash, 0.12f);
    }

    private float ResolveHappyIdleDuration()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return happyIdleFallbackDuration;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.ToLowerInvariant().Contains("happy idle"))
                return clip.length;
        }

        return happyIdleFallbackDuration;
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private IEnumerator RunTask(IEnumerator task)
    {
        CurrentState = StaffState.Reacting;

        float reactionDelay = Random.Range(
            Mathf.Min(reactionDelayRange.x, reactionDelayRange.y),
            Mathf.Max(reactionDelayRange.x, reactionDelayRange.y)
        );

        if (reactionDelay > 0f)
            yield return new WaitForSeconds(reactionDelay);

        yield return task;
        yield return ReturnHome();
        activeTask = null;
    }
}
