using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DefaultExecutionOrder(-90)]
public class MultiplayerManagerRegistration : MonoBehaviourPunCallbacks
{
    private MultiplayerSessionManager session;
    private int actorNumber;

    private void Start()
    {
        RegisterManager();
    }

    public override void OnJoinedRoom() => RegisterManager();

    private void RegisterManager()
    {
        // PUN has assigned the owner by Start, for both local and remote instances.
        session = MultiplayerSessionManager.Instance;
        actorNumber = photonView.OwnerActorNr;
        if (session != null) session.Register(this);
    }

    private void OnDestroy()
    {
        if (session != null) session.Unregister(actorNumber, this);
    }
}
