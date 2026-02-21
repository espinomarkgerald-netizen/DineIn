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
        if (p == null) return;

        int head = GetInt(p.CustomProperties, "HeadI", 0);
        int body = GetInt(p.CustomProperties, "BodyI", 0);
        int arms = GetInt(p.CustomProperties, "ArmsI", 0);
        int legs = GetInt(p.CustomProperties, "LegsI", 0);
        int hat  = GetInt(p.CustomProperties, "HatI", 0);

        // ⚠️ If your customizers ONLY read from PlayerCustomizationData (static),
        // this will overwrite for everyone. If you’re only testing solo it’s okay.
        // If you have multiple players visible, tell me and I’ll switch this to per-player apply methods.
        PlayerCustomizationData.HeadColorIndex = head;
        PlayerCustomizationData.BodyColorIndex = body;
        PlayerCustomizationData.ArmsColorIndex = arms;
        PlayerCustomizationData.LegsColorIndex = legs;
        PlayerCustomizationData.EquippedHatId  = hat;

        colorCustomizer?.RefreshFromData();
        hatCustomizer?.RefreshFromData();
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
