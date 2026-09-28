using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class PhotonPlayerCustomizationApplier : MonoBehaviourPunCallbacks
{
    [Header("References (optional)")]
    [SerializeField] private CharacterColorCustomizer colorCustomizer;
    [SerializeField] private CharacterHatCustomizer hatCustomizer;

    private void Awake()
    {
        if (colorCustomizer == null) colorCustomizer = GetComponentInChildren<CharacterColorCustomizer>(true);
        if (hatCustomizer == null) hatCustomizer = GetComponentInChildren<CharacterHatCustomizer>(true);
    }

    private void Start()
    {
        Apply();
    }

    private void Apply()
    {
        if (GetComponent<DineIn.Appearance.PlayerAppearanceBinding>() != null) return;
        if (photonView == null || photonView.Owner == null) return;
        if (photonView.IsMine)
        {
            // Local player uses saved data
            colorCustomizer?.RefreshFromData();
            hatCustomizer?.RefreshFromData();

            // Ensure Photon props match our saved data
            if (PlayfabManager.Instance != null && PlayfabManager.Instance.IsLoggedIn)
                PlayfabManager.Instance.PushCustomizationToPhoton();
        }
        else
        {
            ApplyFromPhotonProps(photonView.Owner);
        }
    }

    private void ApplyFromPhotonProps(Player p)
    {
        if (GetComponent<DineIn.Appearance.PlayerAppearanceBinding>() != null) return;
        if (p == null) return;

        int head = GetInt(p.CustomProperties, "HeadI", 0);
        int body = GetInt(p.CustomProperties, "BodyI", 0);
        int arms = GetInt(p.CustomProperties, "ArmsI", 0);
        int legs = GetInt(p.CustomProperties, "LegsI", 0);
        int hat  = GetInt(p.CustomProperties, "HatI", 0);

        colorCustomizer?.ApplyIndices(head, body, arms, legs);
        hatCustomizer?.ApplyHat(hat);
    }

    private int GetInt(Hashtable props, string key, int fallback)
    {
        if (props != null && props.ContainsKey(key) && props[key] is int v) return v;
        return fallback;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (photonView == null || photonView.Owner == null) return;
        if (targetPlayer != photonView.Owner) return;

        ApplyFromPhotonProps(targetPlayer);
    }
}
