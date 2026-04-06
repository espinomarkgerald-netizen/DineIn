using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls foot-dust emission rate based on NavMeshAgent velocity for the local player,
/// or from network-received movement state for remote players via <see cref="SetMovingRemote"/>.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FootDustTrail : MonoBehaviour
{
    [SerializeField] private float movingRate = 20f;
    [SerializeField] private float idleRate = 0f;
    [SerializeField] private float moveThreshold = 0.08f;

    private NavMeshAgent agent;
    private PhotonView photonView;
    private ParticleSystem ps;
    private ParticleSystem.EmissionModule emission;

    // Set each frame by NetworkPlayerMovementSync for remote players.
    private bool remoteIsMoving;
    private bool isRemotePlayer;

    private void Awake()
    {
        // NavMeshAgent lives on the root; this script is on a child.
        agent      = GetComponentInParent<NavMeshAgent>();
        photonView = GetComponentInParent<PhotonView>();
        ps         = GetComponent<ParticleSystem>();
        emission   = ps.emission;
    }

    private void Start()
    {
        ps.Play(); // keep the system alive; emission rate controls visibility
        emission.rateOverTime = idleRate;

        if (photonView != null && PhotonNetwork.IsConnected && !photonView.IsMine)
            isRemotePlayer = true;
    }

    private void Update()
    {
        bool moving;

        if (isRemotePlayer)
        {
            moving = remoteIsMoving;
        }
        else
        {
            if (agent == null)
            {
                emission.rateOverTime = idleRate;
                return;
            }

            moving = agent.hasPath
                && !agent.pathPending
                && agent.remainingDistance > agent.stoppingDistance
                && agent.velocity.sqrMagnitude > (moveThreshold * moveThreshold);
        }

        emission.rateOverTime = moving ? movingRate : idleRate;
    }

    /// <summary>
    /// Called every frame by <see cref="NetworkPlayerMovementSync"/> for remote player instances.
    /// </summary>
    public void SetMovingRemote(bool moving) => remoteIsMoving = moving;
}
