using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonStateWatcher : MonoBehaviourPunCallbacks
{
    void Update()
    {
        Debug.Log($"[PhotonState] State={PhotonNetwork.NetworkClientState} " +
                  $"InRoom={PhotonNetwork.InRoom} " +
                  $"IsMaster={PhotonNetwork.IsMasterClient}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"[PhotonState] ❌ DISCONNECTED: {cause}");
    }
}
