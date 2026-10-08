using UnityEngine;
using UnityEngine.AI;

/// <summary>Visual-only promenade walkers; no customer, economy, hygiene or task components.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class FineDiningPromenadeWalker : MonoBehaviour
{
    public Transform[] route;
    public Animator animator;
    [Min(0)] public float pauseAtWaypoint = 2f;
    private NavMeshAgent agent;
    private int waypoint;
    private float pause;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (route == null || route.Length < 2 || !agent.isOnNavMesh) { enabled = false; return; }
        agent.SetDestination(route[0].position);
    }
    private void Update()
    {
        if (!agent.isOnNavMesh) return;
        if (animator != null) animator.SetFloat(Speed, agent.velocity.magnitude, .15f, Time.deltaTime);
        if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + .15f) return;
        pause += Time.deltaTime;
        if (pause < pauseAtWaypoint) return;
        pause = 0;
        waypoint = (waypoint + 1) % route.Length;
        agent.SetDestination(route[waypoint].position);
    }
}
