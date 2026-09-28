using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DineIn.Appearance
{
    [RequireComponent(typeof(CharacterAppearance))]
    public sealed class PlayerAppearanceBinding : MonoBehaviourPunCallbacks
    {
        public const string PropertyKey = "AppearanceV2";
        private CharacterAppearance look;
        private ApplyCustomizationOnSpawn legacy;
        public bool Previewing { get; set; }
        public override void OnEnable()
        {
            base.OnEnable(); PlayerCustomizationData.AppearanceChanged += Refresh;
        }
        public override void OnDisable()
        {
            PlayerCustomizationData.AppearanceChanged -= Refresh; base.OnDisable();
        }
        private void Start() => Refresh();
        public void Refresh()
        {
            if (Previewing) return;
            if (look == null) { look = GetComponent<CharacterAppearance>(); legacy = GetComponent<ApplyCustomizationOnSpawn>(); }
            var owner = photonView != null ? photonView.Owner : null;
            if (owner != null && !photonView.IsMine)
                look.Apply(AppearanceRecipe.Decode(owner.CustomProperties[PropertyKey] as string));
            else look.Apply(PlayerCustomizationData.CommittedAppearance);
            if (legacy != null)
            {
                if (look.IsCustomized) legacy.HideLegacyHat();
                else legacy.ApplyLegacyAppearance(owner != null ? owner.CustomProperties : new Hashtable
                {
                    ["HeadI"] = PlayerCustomizationData.HeadColorIndex, ["BodyI"] = PlayerCustomizationData.BodyColorIndex,
                    ["ArmsI"] = PlayerCustomizationData.ArmsColorIndex, ["LegsI"] = PlayerCustomizationData.LegsColorIndex,
                    ["HatI"] = PlayerCustomizationData.EquippedHatId
                });
            }
        }
        public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
        {
            if (photonView != null && target == photonView.Owner &&
                (changed.ContainsKey(PropertyKey) || changed.ContainsKey("HeadI") || changed.ContainsKey("BodyI") ||
                 changed.ContainsKey("ArmsI") || changed.ContainsKey("LegsI") || changed.ContainsKey("HatI"))) Refresh();
        }
    }
}
