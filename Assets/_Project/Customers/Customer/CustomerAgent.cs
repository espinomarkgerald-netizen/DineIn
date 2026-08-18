using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAgent : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }
    public bool IsSeated { get; private set; }
    public bool IsEating { get; private set; }
    public Transform HeadAnchor => ResolveHeadAnchor();

    [Header("Arrival Tuning")]
    [SerializeField] private float fallbackArriveDistance = 0.55f;
    [SerializeField] private float destinationSampleRadius = 1.5f;
    [SerializeField] private float pathArrivalPadding = 0.2f;

    [Header("Idle Facing")]
    [SerializeField] private float idleFaceTurnSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string sittingParam = "IsSitting";
    [SerializeField] private float animationDamp = 8f;
    [Tooltip("Optional override. When empty, Resources/AlienAnimationSettings is shared by every alien.")]
    [SerializeField] private AlienAnimationSettings animationSettings;

    private bool useIdleFacing;
    private Vector3 idleFacingForward = Vector3.forward;
    private float currentAnimSpeed;
    private bool hasActiveDestination;
    private Vector3 activeDestination;
    private int destinationIssuedFrame = -1;
    private Transform headAnchor;
    private AlienProceduralAnimation proceduralAnimation;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.stoppingDistance = 0.15f;
        Agent.autoBraking = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            if (animationSettings == null)
                animationSettings = AlienAnimationSettings.LoadGlobal();

            proceduralAnimation = new AlienProceduralAnimation(
                this,
                animator,
                animationSettings);
        }
    }

    private void OnDestroy()
    {
        proceduralAnimation?.Dispose();
        proceduralAnimation = null;
    }

    private Transform ResolveHeadAnchor()
    {
        if (headAnchor != null)
            return headAnchor;

        if (animator != null && animator.isHuman)
            headAnchor = animator.GetBoneTransform(HumanBodyBones.Head);

        if (headAnchor == null)
            headAnchor = FindChildRecursive(transform, "CC_Base_Head");

        if (headAnchor == null)
            headAnchor = FindChildRecursive(transform, "Head");

        return headAnchor;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void Update()
    {
        UpdateMovementCompletion();
        UpdateAnimation();
        proceduralAnimation?.Update(Time.deltaTime);

        if (!IsSeated && useIdleFacing && Agent != null)
        {
            if (!Agent.pathPending && Agent.velocity.sqrMagnitude < 0.01f)
            {
                Vector3 f = idleFacingForward;
                f.y = 0f;

                if (f.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(f.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        Time.deltaTime * idleFaceTurnSpeed
                    );
                }
            }
        }
    }

    private void LateUpdate()
    {
        proceduralAnimation?.LateUpdate();
    }

    private void UpdateAnimation()
    {
        if (animator == null || Agent == null) return;

        float targetSpeed = 0f;

        if (!IsSeated && hasActiveDestination && !Agent.pathPending)
            targetSpeed = Agent.velocity.magnitude;

        currentAnimSpeed = hasActiveDestination
            ? Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * animationDamp)
            : 0f;

        animator.SetFloat(speedParam, currentAnimSpeed);
        animator.SetBool(sittingParam, IsSeated);
        proceduralAnimation?.SetState(
            IsSeated,
            IsEating,
            !IsSeated && currentAnimSpeed > 0.05f);
    }

    public void SetEating(bool eating)
    {
        SetEating(eating, null, 0);
    }

    public void SetEating(bool eating, FoodTray foodSource, int dinerIndex)
    {
        IsEating = eating;
        proceduralAnimation?.SetFoodSource(eating ? foodSource : null, dinerIndex);
        proceduralAnimation?.SetState(
            IsSeated,
            IsEating,
            !IsSeated && currentAnimSpeed > 0.05f);
    }

    public void WalkTo(Vector3 worldPos)
    {
        TryWalkTo(worldPos, out _);
    }

    public bool TryWalkTo(Vector3 worldPos, out Vector3 resolvedDestination)
    {
        resolvedDestination = worldPos;
        if (Agent == null) return false;

        if (IsSeated)
            Unseat();

        Agent.updatePosition = true;
        Agent.updateRotation = true;

        if (!Agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, destinationSampleRadius, NavMesh.AllAreas))
        {
            Agent.Warp(startHit.position);
        }

        if (!Agent.isOnNavMesh)
        {
            StopAtCurrentPosition();
            Debug.LogWarning($"[CustomerAgent] {name} is not on a NavMesh and cannot walk.", this);
            return false;
        }

        if (!NavMesh.SamplePosition(worldPos, out NavMeshHit destinationHit, destinationSampleRadius, NavMesh.AllAreas))
        {
            StopAtCurrentPosition();
            Debug.LogWarning($"[CustomerAgent] {name} could not resolve a walkable destination.", this);
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        if (!Agent.CalculatePath(destinationHit.position, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            StopAtCurrentPosition();
            Debug.LogWarning(
                $"[CustomerAgent] {name} could not resolve a complete path to its destination.",
                this);
            return false;
        }

        Agent.isStopped = false;
        Agent.ResetPath();

        if (!Agent.SetDestination(destinationHit.position))
        {
            StopAtCurrentPosition();
            Debug.LogWarning($"[CustomerAgent] {name} could not set its walk destination.", this);
            return false;
        }

        activeDestination = destinationHit.position;
        resolvedDestination = activeDestination;
        hasActiveDestination = true;
        destinationIssuedFrame = Time.frameCount;
        return true;
    }

    private void UpdateMovementCompletion()
    {
        if (!hasActiveDestination || Agent == null || IsSeated || Time.frameCount <= destinationIssuedFrame)
            return;

        if (!Agent.isOnNavMesh)
        {
            StopAtCurrentPosition();
            return;
        }

        if (Agent.pathPending)
            return;

        if (Agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            StopAtCurrentPosition();
            return;
        }

        float stopDistance = Agent.stoppingDistance + Mathf.Max(0f, pathArrivalPadding);
        bool reachedPathEnd = Agent.hasPath &&
                              Agent.pathStatus == NavMeshPathStatus.PathComplete &&
                              Agent.remainingDistance != Mathf.Infinity &&
                              Agent.remainingDistance <= stopDistance;
        bool reachedWithoutPath = !Agent.hasPath &&
                                  PlanarDistance(transform.position, activeDestination) <= fallbackArriveDistance;

        if (reachedPathEnd || reachedWithoutPath)
            StopAtCurrentPosition();
    }

    public void StopAtCurrentPosition()
    {
        hasActiveDestination = false;
        destinationIssuedFrame = -1;
        currentAnimSpeed = 0f;

        if (Agent != null && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        if (animator != null)
            animator.SetFloat(speedParam, 0f);
    }

    public bool HasArrived(Vector3 targetPos)
    {
        if (Agent == null) return false;
        if (Agent.pathPending) return false;

        if (Agent.hasPath && Agent.remainingDistance != Mathf.Infinity)
            return Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        float d = PlanarDistance(transform.position, targetPos);
        return d <= fallbackArriveDistance;
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    public void SetIdleFacing(Vector3 forward, bool enabled)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        idleFacingForward = forward.normalized;
        useIdleFacing = enabled;
    }

    public void SnapToSeat(Vector3 seatPos, Quaternion seatRot)
    {
        if (Agent == null) return;

        Agent.ResetPath();
        Agent.isStopped = true;
        hasActiveDestination = false;
        currentAnimSpeed = 0f;
        Agent.updatePosition = false;
        Agent.updateRotation = false;

        // Keep the NavMeshAgent at the booth approach while the visible character
        // occupies a seat inside the booth's carved obstacle.
        transform.position = seatPos;
        transform.rotation = seatRot;

        useIdleFacing = false;
        IsSeated = true;

        if (animator != null)
            animator.SetBool(sittingParam, true);

        proceduralAnimation?.SetState(true, IsEating, false);
    }

    public void Unseat()
    {
        if (Agent == null) return;

        IsSeated = false;
        IsEating = false;
        hasActiveDestination = false;
        currentAnimSpeed = 0f;

        Vector3 navMeshPosition = Agent.nextPosition;
        if (!Agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit hit, destinationSampleRadius * 2f, NavMesh.AllAreas))
        {
            Agent.Warp(hit.position);
            navMeshPosition = hit.position;
        }

        transform.position = navMeshPosition;
        Agent.updatePosition = true;
        Agent.updateRotation = true;

        if (Agent.isOnNavMesh)
            Agent.isStopped = false;

        if (animator != null)
            animator.SetBool(sittingParam, false);

        proceduralAnimation?.SetState(false, false, false);
    }
}
