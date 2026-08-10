using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerNameTagSetter : MonoBehaviourPunCallbacks
{
    [SerializeField] private NameTagBillboard nameTag;
    [SerializeField] private Transform followTarget; // optional (head bone)

    private void Awake()
    {
        if (nameTag == null)
            nameTag = GetComponentInChildren<NameTagBillboard>(true);
    }

    private void Start()
    {
        ApplyName();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (photonView == null || photonView.Owner == null) return;
        if (targetPlayer != photonView.Owner) return;

        // if username prop updates later, refresh
        if (changedProps != null && (changedProps.ContainsKey("Username") || changedProps.ContainsKey("user")))
            ApplyName();
    }

    private void ApplyName()
    {
        if (nameTag == null || photonView == null || photonView.Owner == null) return;

        string finalName = photonView.Owner.NickName;

        // Prefer custom property if you use it
        var props = photonView.Owner.CustomProperties;
        if (props != null)
        {
            if (props.TryGetValue("Username", out object u) && u != null && !string.IsNullOrEmpty(u.ToString()))
                finalName = u.ToString();
            else if (props.TryGetValue("user", out object u2) && u2 != null && !string.IsNullOrEmpty(u2.ToString()))
                finalName = u2.ToString();
        }

        nameTag.SetName(string.IsNullOrEmpty(finalName) ? "Player" : finalName);

        if (followTarget != null)
            nameTag.SetFollowTarget(followTarget);
    }
}
