using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives the movement particle system based on NavMeshAgent state for the local player,
/// or from network-received movement state for remote players via <see cref="SetMovingRemote"/>.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovementParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem moveParticles;

    [Header("Movement Detection")]
    [SerializeField] private float moveThreshold = 0.05f;
    [SerializeField] private float stopDelay = 0.1f;

    private NavMeshAgent agent;
    private PhotonView photonView;

    private float stopTimer;
    private bool isPlaying;

    // Set each frame by NetworkPlayerMovementSync for remote players.
    private bool remoteIsMoving;
    private bool isRemotePlayer;

    private void Awake()
    {
        agent       = GetComponent<NavMeshAgent>();
        photonView  = GetComponent<PhotonView>();

        if (moveParticles == null)
            moveParticles = GetComponentInChildren<ParticleSystem>(true);

        if (moveParticles != null)
            moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Start()
    {
        // Determine once at start whether this is a remote player.
        if (photonView != null && PhotonNetwork.IsConnected && !photonView.IsMine)
            isRemotePlayer = true;
    }

    private void Update()
    {
        if (moveParticles == null) return;

        bool moving;

        if (isRemotePlayer)
        {
            // Remote: driven by network data pushed via SetMovingRemote().
            moving = remoteIsMoving;
        }
        else
        {
            // Local: read directly from NavMeshAgent.
            if (agent == null || !agent.isOnNavMesh)
            {
                StopIfPlaying();
                return;
            }

            float speed = agent.desiredVelocity.magnitude;
            moving = speed > moveThreshold && agent.hasPath && !agent.isStopped && !agent.pathPending;
        }

        if (moving)
        {
            stopTimer = 0f;
            if (!isPlaying)
            {
                moveParticles.Play();
                isPlaying = true;
            }
        }
        else
        {
            stopTimer += Time.deltaTime;
            if (isPlaying && stopTimer >= stopDelay)
                StopIfPlaying();
        }
    }

    /// <summary>
    /// Called every frame by <see cref="NetworkPlayerMovementSync"/> for remote player instances.
    /// </summary>
    public void SetMovingRemote(bool moving) => remoteIsMoving = moving;

    private void StopIfPlaying()
    {
        if (!isPlaying) return;
        moveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        isPlaying = false;
    }
}
