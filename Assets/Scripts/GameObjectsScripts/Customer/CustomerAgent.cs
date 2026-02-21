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

    private bool useIdleFacing;
    private Vector3 idleFacingForward = Vector3.forward;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.stoppingDistance = 0.15f;
        Agent.autoBraking = true;
    }

    private void Update()
    {
        // Idle facing for standing customers only
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

    /// <summary>
    /// Moves the customer to a world position.
    /// If seated, it will auto-unseat so leaving works.
    /// </summary>
    public void WalkTo(Vector3 worldPos)
    {
        if (Agent == null) return;

        // ✅ Leaving support: if seated, stand up first
        if (IsSeated)
            Unseat();

        Agent.updatePosition = true;
        Agent.updateRotation = true;

        Agent.isStopped = false;

        // ✅ Prevent "agent stuck" after SnapToSeat (agent/transform desync)
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

        // ✅ Critical: prevents later teleporting/jitter
        Agent.Warp(seatPos);

        Agent.updatePosition = false;
        Agent.updateRotation = false;

        transform.rotation = seatRot;

        useIdleFacing = false;
        IsSeated = true;
    }

    /// <summary>
    /// Re-enables navmesh movement after being seated.
    /// Safe to call multiple times.
    /// </summary>
    public void Unseat()
    {
        if (Agent == null) return;

        IsSeated = false;

        // Enable agent updates again
        Agent.updatePosition = true;
        Agent.updateRotation = true;

        // ✅ Resync agent to current transform before moving
        Agent.Warp(transform.position);

        Agent.isStopped = false;
    }
}
