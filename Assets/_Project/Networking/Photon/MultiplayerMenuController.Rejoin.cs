using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class MultiplayerMenuController
{
    private bool pendingRejoin;
    private LastMultiplayerRun rejoinPointer;
    private string rejoinFailure;
    private float nextCardRefresh;

    private void RefreshReconnectCard()
    {
        if (reconnectCard == null) return;
        var account = PlayFabAuthManager.Instance;
        var saved = account?.IsLoggedIn == true ? MultiplayerLastRun.Load(account.PlayFabId, out _) : null;
        bool show = saved != null && !PhotonNetwork.InRoom;
        reconnectCard.SetActive(show);
        if (reconnectButton != null) reconnectButton.interactable = show && !busy && !starting;
        if (reconnectDescription != null && show)
            reconnectDescription.text = "Casual Dining\nAvailability checked on reconnect.";
    }

    private void ReconnectLastRun()
    {
        if (busy || starting || PhotonNetwork.InRoom) return;
        var account = PlayFabAuthManager.Instance;
        if (account?.IsLoggedIn != true) { SetStatus("Sign in to reconnect to your last run."); return; }
        rejoinPointer = MultiplayerLastRun.Load(account.PlayFabId, out var reason);
        if (rejoinPointer == null) { RefreshReconnectCard(); SetStatus(reason ?? "There is no recent run to return to."); return; }
        Begin(false, rejoinPointer.room, true);
    }

    private void CompleteLastRunJoin()
    {
        if (!MultiplayerLastRun.TryValidateJoinedRun(rejoinPointer, out _, out var reason)
            || !Application.CanStreamedLevelBeLoaded(Destination))
        {
            rejoinFailure = reason ?? "The multiplayer scene is unavailable in this build.";
            MultiplayerLastRun.Forget(rejoinPointer?.accountId, rejoinPointer?.runId);
            pendingRejoin = false;
            // Scene synchronization stays disabled until the invalid room has been left.
            PhotonNetwork.LeaveRoom(false);
            return;
        }
        pendingRejoin = false;
        busy = false;
        starting = true;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {
            [MultiplayerSessionManager.LoadedKey] = "" });
        PhotonCustomizationSync.PushToPhoton();
        SetStatus("Rejoining the current restaurant...");
        // Session.Awake restores automatic synchronization after this single load.
        // This pauses PUN's event queue while the existing gameplay scene loads;
        // cached player objects then replay into that scene, rather than the menu.
        PhotonNetwork.LoadLevel(Destination);
    }
}
