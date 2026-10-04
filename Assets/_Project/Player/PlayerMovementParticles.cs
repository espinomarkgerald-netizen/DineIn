using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Emits walking dust only during actual horizontal character travel.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovementParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem moveParticles;

    [Header("Movement Detection")]
    [SerializeField, Min(0f)] private float moveThreshold = 0.08f;
    [Tooltip("Travel faster than this multiple of navigation speed is placement/teleportation, not walking.")]
    [SerializeField, Min(1f)] private float teleportSpeedMultiplier = 2f;

    private NavMeshAgent agent;
    private PhotonView photonView;
    private NetworkPlayerMovementSync networkMovement;
    private Vector3 previousPosition;
    private bool hasPositionSample;
    private bool remoteIsMoving;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        photonView = GetComponent<PhotonView>();
        networkMovement = GetComponent<NetworkPlayerMovementSync>();
        if (moveParticles == null)
            moveParticles = GetComponentInChildren<ParticleSystem>(true);
    }

    private void OnEnable()
    {
        // Also runs for pooled/reused characters; Start only runs once.
        hasPositionSample = false;
        remoteIsMoving = false;
        if (moveParticles != null)
            moveParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        hasPositionSample = false;
        remoteIsMoving = false;
        if (moveParticles != null)
            moveParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LateUpdate()
    {
        if (moveParticles == null) return;

        // Sample the character root after navigation/network pose updates, never
        // the animated foot or the particle child. Placement establishes a baseline.
        Vector3 travel = transform.position - previousPosition;
        previousPosition = transform.position;
        travel.y = 0f;
        bool sampled = hasPositionSample;
        hasPositionSample = true;
        float dt = Time.deltaTime;
        if (!sampled || dt <= 0f || LobbyPauseMenu.IsAnyOpen)
        {
            StopEmitting();
            return;
        }

        // Staff retain legacy PhotonViews even in solo scenes. Only an actual
        // network player receives SetMovingRemote; staff use their live agent.
        bool remote = networkMovement != null && photonView != null &&
            PhotonNetwork.InRoom && photonView.ViewID != 0 &&
            photonView.Owner != null && !photonView.IsMine;
        Vector3 velocity = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh
            ? agent.velocity : Vector3.zero;
        velocity.y = 0f;
        bool movementAllowed = remote ? remoteIsMoving :
            agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            !agent.isStopped && velocity.sqrMagnitude > moveThreshold * moveThreshold;
        float minimumTravel = moveThreshold * dt;
        float maximumTravel = Mathf.Max(moveThreshold, agent.speed) * teleportSpeedMultiplier * dt;
        bool moving = movementAllowed && travel.sqrMagnitude > minimumTravel * minimumTravel &&
            travel.sqrMagnitude <= maximumTravel * maximumTravel;

        if (moving)
        {
            if (!moveParticles.isEmitting) moveParticles.Play(false);
        }
        else StopEmitting();
    }

    // Network animation intent gates remote dust, but actual received travel is
    // still required, so stale "moving" packets cannot emit on a stationary avatar.
    public void SetMovingRemote(bool moving) => remoteIsMoving = moving;

    private void StopEmitting()
    {
        if (moveParticles.isEmitting)
            moveParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}
