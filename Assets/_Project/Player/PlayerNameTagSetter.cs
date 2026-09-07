using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerNameTagSetter : MonoBehaviourPunCallbacks
{
    [SerializeField] private NameTagBillboard nameTag;
    [SerializeField] private Transform followTarget; // optional (head bone)
    private PhotonView ownerView;

    private void Awake()
    {
        ownerView = GetComponentInParent<PhotonView>();
        if (nameTag == null)
            nameTag = GetComponentInChildren<NameTagBillboard>(true);
    }

    private void Start()
    {
        ApplyName();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (ownerView == null || ownerView.Owner == null) return;
        if (targetPlayer != ownerView.Owner) return;

        ApplyName();
    }

    private void ApplyName()
    {
        if (nameTag == null || ownerView == null || ownerView.Owner == null) return;

        string finalName = ownerView.Owner.NickName;

        nameTag.SetName(string.IsNullOrWhiteSpace(finalName) ? "Player" : finalName);

        if (followTarget != null)
            nameTag.SetFollowTarget(followTarget);
    }
}
