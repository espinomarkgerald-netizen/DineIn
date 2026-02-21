using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonStateProbe : MonoBehaviourPunCallbacks
{
    private ClientState _last;

    private void Update()
    {
        var s = PhotonNetwork.NetworkClientState;
        if (s != _last)
        {
            _last = s;
            Debug.Log($"[PhotonStateProbe] State -> {s}  Ready={PhotonNetwork.IsConnectedAndReady}  InRoom={PhotonNetwork.InRoom}");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[PhotonStateProbe] DISCONNECTED: {cause}");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[PhotonStateProbe] ConnectedToMaster");
    }
}
