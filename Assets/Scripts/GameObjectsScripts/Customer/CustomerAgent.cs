using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAgent : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }
    public bool IsSeated { get; private set; }

    [Header("Arrival Tuning")]
    [SerializeField] private float fallbackArriveDistance = 0.55f;

    [Header("Idle Facing")]
    [SerializeField] private float idleFaceTurnSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string sittingParam = "IsSitting";
    [SerializeField] private float animationDamp = 8f;

    private bool useIdleFacing;
    private Vector3 idleFacingForward = Vector3.forward;
    private float currentAnimSpeed;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.stoppingDistance = 0.15f;
        Agent.autoBraking = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        UpdateAnimation();

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

    private void UpdateAnimation()
    {
        if (animator == null || Agent == null) return;

        float targetSpeed = 0f;

        if (!IsSeated && !Agent.pathPending)
            targetSpeed = Agent.velocity.magnitude;

        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * animationDamp);

        animator.SetFloat(speedParam, currentAnimSpeed);
        animator.SetBool(sittingParam, IsSeated);
    }

    public void WalkTo(Vector3 worldPos)
    {
        if (Agent == null) return;

        if (IsSeated)
            Unseat();

        Agent.updatePosition = true;
        Agent.updateRotation = true;
        Agent.isStopped = false;
        Agent.Warp(transform.position);
        Agent.SetDestination(worldPos);
    }

    public bool HasArrived(Vector3 targetPos)
    {
        if (Agent == null) return false;
        if (Agent.pathPending) return false;

        if (Agent.hasPath && Agent.remainingDistance != Mathf.Infinity)
            return Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        float d = Vector3.Distance(transform.position, targetPos);
        return d <= fallbackArriveDistance;
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
        Agent.Warp(seatPos);
        Agent.updatePosition = false;
        Agent.updateRotation = false;

        transform.rotation = seatRot;

        useIdleFacing = false;
        IsSeated = true;

        if (animator != null)
            animator.SetBool(sittingParam, true);
    }

    public void Unseat()
    {
        if (Agent == null) return;

        IsSeated = false;
        Agent.updatePosition = true;
        Agent.updateRotation = true;
        Agent.Warp(transform.position);
        Agent.isStopped = false;

        if (animator != null)
            animator.SetBool(sittingParam, false);
    }
}