using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Applies shared NavMesh crowd tuning, prevents character colliders from pushing,
/// and safely requests a fresh path when an agent has been stalled by traffic.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class CrowdNavigationAgent : MonoBehaviour
{
    private static readonly List<CrowdNavigationAgent> ActiveAgents =
        new List<CrowdNavigationAgent>();

    [Header("Editable Crowd Settings")]
    [SerializeField] private CrowdNavigationProfile profile;
    [SerializeField] private bool isCustomer;
    [Tooltip("Leave at -1 to use the role lane and a stable per-character variation.")]
    [SerializeField, Range(-1, 99)] private int authoredPriority = -1;
    [SerializeField] private bool enableStallRecovery = true;

    private NavMeshAgent agent;
    private Collider[] characterColliders;
    private float stalledFor;
    private float nextRepathTime;
    private float restorePriorityAt;
    private int stablePriority;
    private int basePriority;

    public CrowdNavigationProfile Profile => profile;
    public bool IsCustomer => isCustomer;

    public static CrowdNavigationAgent Ensure(
        GameObject target,
        bool customer,
        CrowdNavigationProfile configuredProfile = null,
        int priority = -1)
    {
        if (target == null)
            return null;

        CrowdNavigationAgent crowdAgent = target.GetComponent<CrowdNavigationAgent>();
        if (crowdAgent == null)
            crowdAgent = target.AddComponent<CrowdNavigationAgent>();

        crowdAgent.Configure(customer, configuredProfile, priority);
        return crowdAgent;
    }

    private void Awake()
    {
        ResolveAndApply();
    }

    private void OnEnable()
    {
        ResolveAndApply();
        RegisterPhysicalSeparation();
    }

    private void OnDisable()
    {
        RestorePhysicalCollisions();
        ActiveAgents.Remove(this);
        stalledFor = 0f;
    }

    private void Update()
    {
        if (agent == null)
            return;

        if (restorePriorityAt > 0f && Time.unscaledTime >= restorePriorityAt)
        {
            agent.avoidancePriority = basePriority;
            restorePriorityAt = 0f;
        }

        if (!enableStallRecovery || profile == null || !agent.enabled || !agent.isOnNavMesh ||
            agent.isStopped || !agent.hasPath || agent.pathPending)
        {
            stalledFor = 0f;
            return;
        }

        float remaining = agent.remainingDistance;
        bool stillTravelling = remaining != Mathf.Infinity &&
            remaining > agent.stoppingDistance + Mathf.Max(0.25f, agent.radius * 0.5f);
        bool stalled = stillTravelling &&
            agent.velocity.sqrMagnitude <= profile.StalledSpeed * profile.StalledSpeed;

        if (!stalled)
        {
            stalledFor = 0f;
            return;
        }

        stalledFor += Time.deltaTime;
        if (stalledFor < profile.StalledSecondsBeforeRepath || Time.unscaledTime < nextRepathTime)
            return;

        Vector3 destination = agent.destination;
        stalledFor = 0f;
        nextRepathTime = Time.unscaledTime + profile.RepathCooldown;

        // Briefly become the yielding character so two agents facing one another
        // do not continuously choose the same local-avoidance solution.
        int yieldPriority = Mathf.Clamp(
            basePriority + profile.TemporaryYieldPriority,
            0,
            99);
        agent.avoidancePriority = yieldPriority;
        restorePriorityAt = Time.unscaledTime + profile.TemporaryYieldSeconds;

        agent.ResetPath();
        agent.SetDestination(destination);
    }

    public void Configure(
        bool customer,
        CrowdNavigationProfile configuredProfile = null,
        int priority = -1)
    {
        isCustomer = customer;
        if (configuredProfile != null)
            profile = configuredProfile;
        authoredPriority = Mathf.Clamp(priority, -1, 99);
        ResolveAndApply();

        if (isActiveAndEnabled)
        {
            if (!ActiveAgents.Contains(this))
            {
                RegisterPhysicalSeparation();
            }
            else if (profile != null && profile.IgnoreCharacterToCharacterCollisions)
            {
                for (int i = 0; i < ActiveAgents.Count; i++)
                    SetMutualCollisionIgnored(ActiveAgents[i], true);
            }
        }
    }

    public void SetFormationPriorityOffset(int offset)
    {
        if (agent == null)
            ResolveAndApply();

        basePriority = Mathf.Clamp(stablePriority + offset, 0, 99);
        if (agent != null)
            agent.avoidancePriority = basePriority;
    }

    private void ResolveAndApply()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (profile == null)
            profile = CrowdNavigationProfile.LoadGlobal();
        if (agent == null || profile == null)
            return;

        stablePriority = profile.GetStablePriority(isCustomer, GetInstanceID(), authoredPriority);
        basePriority = stablePriority;
        agent.avoidancePriority = basePriority;
        agent.obstacleAvoidanceType = profile.ObstacleAvoidance;
        agent.radius = isCustomer ? profile.CustomerRadius : profile.StaffRadius;
        agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, profile.MinimumStoppingDistance);
        agent.acceleration = Mathf.Max(agent.acceleration, profile.MinimumAcceleration);
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, profile.MinimumAngularSpeed);
        agent.autoBraking = true;
        agent.autoRepath = true;
    }

    private void RegisterPhysicalSeparation()
    {
        if (profile == null || !profile.IgnoreCharacterToCharacterCollisions ||
            ActiveAgents.Contains(this))
            return;

        characterColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ActiveAgents.Count; i++)
            SetMutualCollisionIgnored(ActiveAgents[i], true);

        ActiveAgents.Add(this);
    }

    private void RestorePhysicalCollisions()
    {
        if (characterColliders == null)
            return;

        for (int i = 0; i < ActiveAgents.Count; i++)
            SetMutualCollisionIgnored(ActiveAgents[i], false);

        characterColliders = null;
    }

    private void SetMutualCollisionIgnored(CrowdNavigationAgent other, bool ignored)
    {
        if (other == null || other == this)
            return;

        Collider[] first = characterColliders ?? GetComponentsInChildren<Collider>(true);
        Collider[] second = other.characterColliders ??
            other.GetComponentsInChildren<Collider>(true);

        for (int firstIndex = 0; firstIndex < first.Length; firstIndex++)
        {
            if (first[firstIndex] == null)
                continue;

            for (int secondIndex = 0; secondIndex < second.Length; secondIndex++)
            {
                if (second[secondIndex] != null)
                    Physics.IgnoreCollision(first[firstIndex], second[secondIndex], ignored);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeRegistry()
    {
        ActiveAgents.Clear();
    }
}
