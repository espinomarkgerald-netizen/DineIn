using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(ParticleSystem))]
public class FootDustTrail : MonoBehaviour
{
    [SerializeField] private float movingRate = 20f;
    [SerializeField] private float idleRate = 0f;
    [SerializeField] private float moveThreshold = 0.08f;

    private NavMeshAgent agent;
    private ParticleSystem ps;
    private ParticleSystem.EmissionModule emission;

    void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        ps = GetComponent<ParticleSystem>();
        emission = ps.emission;
    }

    void Start()
    {
        ps.Play(); // keep system alive
        emission.rateOverTime = idleRate;
    }

    void Update()
    {
        if (agent == null) return;

        bool moving =
            agent.hasPath &&
            !agent.pathPending &&
            agent.remainingDistance > agent.stoppingDistance &&
            agent.velocity.sqrMagnitude > (moveThreshold * moveThreshold);

        emission.rateOverTime = moving ? movingRate : idleRate;
    }
}
