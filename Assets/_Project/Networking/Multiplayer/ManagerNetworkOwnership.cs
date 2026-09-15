using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// Only the network Manager copy uses this guard; the single-player prefab is unchanged.
[DefaultExecutionOrder(-100)]
public class ManagerNetworkOwnership : MonoBehaviourPun
{
    [SerializeField] private Behaviour[] localOnly;
    private bool inputBlocked;

    private void Update()
    {
        var session = MultiplayerSessionManager.Instance;
        bool blocked = session == null || !session.CanAct || !photonView.IsMine
            || photonView.OwnerActorNr != session.LocalActorNumber;
        if (blocked == inputBlocked) return;
        inputBlocked = blocked;
        GetComponent<ManagerPlayer>()?.SetExternalInputSuppressed(blocked);
    }

    private void Start()
    {
        if (photonView.IsMine && photonView.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber) return;
        foreach (var component in localOnly)
            if (component != null) component.enabled = false;
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
}
