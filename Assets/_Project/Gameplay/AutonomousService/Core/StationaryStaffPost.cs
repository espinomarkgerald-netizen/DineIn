using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// Only attached to fixed cashier posts. Moving Fast Food workers retain their existing bot.
[DisallowMultipleComponent]
public sealed class StationaryStaffPost : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float occupiedRadius = .35f;
    [SerializeField, Min(.05f)] private float displacementTolerance = .15f;
    [SerializeField, Min(.1f)] private float checkInterval = .2f;
    private Vector3 home;
    private Quaternion facing;
    private NavMeshAgent agent;
    private NavMeshObstacle obstacle;
    private bool initialized, returning;
    private float nextCheck;

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        home = transform.position; facing = transform.rotation;
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.enabled = false;
        }
        foreach (var body in GetComponentsInChildren<Rigidbody>())
        { body.isKinematic = true; body.useGravity = false; }
        foreach (var animator in GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
        obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.enabled = false;
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = Vector3.up * .75f;
        obstacle.height = 1.5f; obstacle.radius = occupiedRadius;
        obstacle.carving = true;
        Park();
    }

    private void Park()
    {
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.enabled = false;
        }
        transform.rotation = facing;
        obstacle.enabled = true; returning = false;
    }

    private void Update()
    {
        if (!initialized || !MultiplayerServiceStaffBridge.CanSimulate || Time.timeScale <= 0 || Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        var displacement = transform.position - home;
        displacement.y = 0f; // Navigation restores the ground position; agent base offsets can differ.
        if (displacement.sqrMagnitude <= displacementTolerance * displacementTolerance)
        { if (returning) Park(); return; }
        if (returning || agent == null) return;
        returning = true;
        StartCoroutine(ReturnToPost());
    }

    private IEnumerator ReturnToPost()
    {
        obstacle.enabled = false;
        // Allow the carved footprint to be removed before asking for a path into it.
        yield return null;
        agent.enabled = true;
        if (!agent.isOnNavMesh || !NavMesh.SamplePosition(home, out var target, .5f, agent.areaMask))
        { agent.enabled = false; obstacle.enabled = true; returning = false; yield break; }
        agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, displacementTolerance * .5f);
        agent.isStopped = false;
        returning = agent.SetDestination(target.position);
        if (!returning) { agent.enabled = false; obstacle.enabled = true; }
    }

    private void OnDisable()
    {
        StopAllCoroutines(); returning = false;
        if (agent != null) agent.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
    }
    private void OnEnable() { if (initialized) Park(); }
}
