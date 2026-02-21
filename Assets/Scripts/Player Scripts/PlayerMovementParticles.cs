using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovementParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem moveParticles;

    [Header("Movement Detection")]
    [SerializeField] private float moveThreshold = 0.05f;
    [SerializeField] private float stopDelay = 0.1f;

    private NavMeshAgent agent;
    private float stopTimer;
    private bool isPlaying;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (moveParticles == null)
            moveParticles = GetComponentInChildren<ParticleSystem>(true);

        if (moveParticles != null)
            moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        if (agent == null || moveParticles == null) return;

        // IMPORTANT: avoid NavMesh errors when not on a baked navmesh yet
        if (!agent.isOnNavMesh)
        {
            if (isPlaying)
            {
                moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                isPlaying = false;
            }
            return;
        }

        float speed = agent.desiredVelocity.magnitude;

        bool moving =
            speed > moveThreshold &&
            agent.hasPath &&
            !agent.isStopped &&
            !agent.pathPending;

        if (moving)
        {
            stopTimer = 0f;
            if (!isPlaying) { moveParticles.Play(); isPlaying = true; }
        }
        else
        {
            stopTimer += Time.deltaTime;
            if (isPlaying && stopTimer >= stopDelay)
            {
                moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                isPlaying = false;
            }
        }
    }
}
