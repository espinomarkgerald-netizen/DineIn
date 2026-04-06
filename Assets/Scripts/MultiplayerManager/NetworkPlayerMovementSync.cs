using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Syncs animator state and particle/effect state for all networked players via IPunObservable.
///
/// This component must be added to the PhotonView's ObservedComponents list on the Player prefab.
/// PhotonTransformView handles position/rotation — this component handles everything else:
///   - Animator parameters: Speed (float), IsMoving (bool), IsCarrying (bool)
///   - Particle emission state for PlayerMovementParticles and FootDustTrail children
///
/// On the local player: reads animator params from the live Animator each network tick and sends them.
/// On remote players: receives params and applies them directly to the Animator and particle systems.
///
/// The NavMeshAgent is disabled on remote players; PlayerSetup handles this.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class NetworkPlayerMovementSync : MonoBehaviourPun, IPunObservable
{
    private Animator animator;

    // Particle components driven by network state on remote players.
    private PlayerMovementParticles movementParticles;
    private FootDustTrail[] footDustTrails;

    // Animator values received from the network — applied every Update on remote players.
    private float netSpeed;
    private bool  netIsMoving;
    private bool  netIsCarrying;

    // Cached animator parameter hashes — zero-allocation lookups.
    private static readonly int HashSpeed      = Animator.StringToHash("Speed");
    private static readonly int HashIsMoving   = Animator.StringToHash("IsMoving");
    private static readonly int HashIsCarrying = Animator.StringToHash("IsCarrying");

    private void Awake()
    {
        animator          = GetComponentInChildren<Animator>(true);
        movementParticles = GetComponent<PlayerMovementParticles>();
        footDustTrails    = GetComponentsInChildren<FootDustTrail>(true);
    }

    private void Update()
    {
        // Only remote players need to have state pushed to their components from here.
        if (photonView.IsMine) return;

        // Apply received animator parameters.
        if (animator != null)
        {
            animator.SetFloat(HashSpeed,     netSpeed);
            animator.SetBool(HashIsMoving,   netIsMoving);
            animator.SetBool(HashIsCarrying, netIsCarrying);
        }

        // Drive particles from received movement state.
        movementParticles?.SetMovingRemote(netIsMoving);

        if (footDustTrails != null)
        {
            foreach (FootDustTrail trail in footDustTrails)
                trail.SetMovingRemote(netIsMoving);
        }
    }

    /// <summary>Called by PhotonView every network tick to serialize or deserialize animation data.</summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Read the live animator state and send it.
            float speed      = animator != null ? animator.GetFloat(HashSpeed)     : 0f;
            bool  isMoving   = animator != null ? animator.GetBool(HashIsMoving)   : false;
            bool  isCarrying = animator != null ? animator.GetBool(HashIsCarrying) : false;

            stream.SendNext(speed);
            stream.SendNext(isMoving);
            stream.SendNext(isCarrying);
        }
        else
        {
            netSpeed      = (float)stream.ReceiveNext();
            netIsMoving   = (bool)stream.ReceiveNext();
            netIsCarrying = (bool)stream.ReceiveNext();
        }
    }
}
