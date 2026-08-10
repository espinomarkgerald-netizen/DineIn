using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonDisconnectTrap : MonoBehaviourPunCallbacks
{
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"[DISCONNECT TRAP] Cause={cause}\nSTACK:\n{StackTraceUtility.ExtractStackTrace()}");
    }
}
