using Photon.Pun;
using UnityEngine;

/// <summary>
/// Syncs animator state and particle/effect state for all networked players via IPunObservable.
///
/// This component must be added to the PhotonView's ObservedComponents list on the Player prefab.
/// Multiplayer managers use buffered poses here; legacy prefabs retain PhotonTransformView.
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
    private bool ownsPose;
    private MultiplayerPoseBuffer poses = new();
    private bool hasReceivedPosition;
    private Vector3 receivedPosition;
    private double minimumPoseTime;
    private int poseOwner, poseView, poseDay;
    private string poseRun;

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
        ownsPose = GetComponent<ManagerNetworkOwnership>() != null;
        if (ownsPose)
            foreach (var legacy in GetComponents<PhotonTransformView>())
            { legacy.enabled = false; photonView.ObservedComponents?.Remove(legacy); }
    }

    private void Start()
    {
        // Reconcile after PhotonView's observable discovery in Awake on every peer.
        if (ownsPose)
            foreach (var legacy in GetComponents<PhotonTransformView>())
            { legacy.enabled = false; photonView.ObservedComponents?.Remove(legacy); }
    }

    private void OnEnable() => ResetReceivedPose();
    private void OnDisable() => ResetReceivedPose();

    // Validate against the last position actually received by this peer. The
    // interpolated render transform intentionally trails it by about 100 ms.
    // Requests never supply their own authoritative distance-check position.
    public static Vector3 AuthorityPosition(GameObject actorRoot)
    {
        if (actorRoot == null) return Vector3.zero;
        var session = MultiplayerSessionManager.Instance;
        var sync = actorRoot.GetComponent<NetworkPlayerMovementSync>();
        if (session == null || !session.IsAuthority || sync == null || !sync.isActiveAndEnabled
            || !sync.ownsPose || sync.photonView.IsMine
            || sync.photonView.OwnerActorNr == session.LocalActorNumber)
            return actorRoot.transform.position;
        sync.RefreshPoseScope();
        return sync.hasReceivedPosition ? sync.receivedPosition : actorRoot.transform.position;
    }

    private void RefreshPoseScope()
    {
        var session = MultiplayerSessionManager.Instance;
        string run = session != null ? session.RunId : string.Empty;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0;
        if (poseOwner == photonView.OwnerActorNr && poseView == photonView.ViewID
            && poseDay == day && poseRun == run) return;
        ResetReceivedPose();
        poseOwner = photonView.OwnerActorNr;
        poseView = photonView.ViewID;
        poseRun = run;
        poseDay = day;
    }

    private void ResetReceivedPose()
    {
        poses = new MultiplayerPoseBuffer();
        hasReceivedPosition = false;
        receivedPosition = default;
        // Ignore queued movement sent before a day/ownership/rejoin reset.
        minimumPoseTime = PhotonNetwork.Time;
    }

    private bool ReceivePose(double sentAt, Vector3 position, Quaternion rotation)
    {
        if (sentAt < minimumPoseTime || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)
            || !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)
            || !poses.Add(sentAt, position, rotation)) return false;
        receivedPosition = position;
        hasReceivedPosition = true;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void Update()
    {
        // Only remote players need to have state pushed to their components from here.
        if (photonView.IsMine) return;
        if (ownsPose) RefreshPoseScope();
        if (ownsPose && poses.Read(PhotonNetwork.Time - 0.1d, out var position, out var rotation))
            transform.SetPositionAndRotation(position, rotation);

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
            if (ownsPose) { stream.SendNext(transform.position); stream.SendNext(transform.rotation); }
        }
        else
        {
            float speed = (float)stream.ReceiveNext();
            bool moving = (bool)stream.ReceiveNext();
            bool carrying = (bool)stream.ReceiveNext();
            if (ownsPose)
            {
                var position = (Vector3)stream.ReceiveNext();
                var rotation = (Quaternion)stream.ReceiveNext();
                RefreshPoseScope();
                // Inactive actors can temporarily have a master controller;
                // only movement from the actual human owner belongs to this avatar.
                if (info.Sender == null || info.Sender.ActorNumber != photonView.OwnerActorNr
                    || !ReceivePose(info.SentServerTime, position, rotation)) return;
            }
            netSpeed = Finite(speed) ? speed : 0f;
            netIsMoving = moving;
            netIsCarrying = carrying;
        }
    }
}
