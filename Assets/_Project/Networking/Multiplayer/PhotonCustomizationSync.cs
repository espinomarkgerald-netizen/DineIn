using ExitGames.Client.Photon;
using Photon.Pun;

public static class PhotonCustomizationSync
{
    public static void PushToPhoton()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        var props = new Hashtable
        {
            { "Username", PhotonNetwork.NickName },

            // Color indices (match ApplyCustomizationOnSpawn)
            { "HeadI", PlayerCustomizationData.HeadColorIndex },
            { "BodyI", PlayerCustomizationData.BodyColorIndex },
            { "ArmsI", PlayerCustomizationData.ArmsColorIndex },
            { "LegsI", PlayerCustomizationData.LegsColorIndex },

            // Hat index/id
            { "HatI", PlayerCustomizationData.EquippedHatId }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
}
