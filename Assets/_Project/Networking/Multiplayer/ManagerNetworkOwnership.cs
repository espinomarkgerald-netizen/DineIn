using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// Only the network Manager copy uses this guard; the single-player prefab is unchanged.
[DefaultExecutionOrder(-100)]
public class ManagerNetworkOwnership : MonoBehaviourPun
{
    [SerializeField] private Behaviour[] localOnly;

    private void Start()
    {
        if (photonView.IsMine) return;
        foreach (var component in localOnly)
            if (component != null) component.enabled = false;
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
}
