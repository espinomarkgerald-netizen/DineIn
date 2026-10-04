using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class MultiplayerMenuController
{
    private bool pendingRejoin, awaitingAdmission, fallbackRejoin;
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
        if (reconnectButton != null)
        {
            reconnectButton.interactable = show && !busy && !starting;
            var label = reconnectButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null) label.text = "REJOIN SESSION";
        }
        if (reconnectDescription != null && show)
            reconnectDescription.text = "Reserved place (host must be online).";
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
        if (!PhotonNetwork.InRoom) return;
        awaitingAdmission = true;
        if (!MultiplayerLastRun.TryValidateJoinedRun(rejoinPointer, out var returned, out var reason)
            && reason != "Waiting for host admission.")
        {
            rejoinFailure = reason;
            // Only confirmed end/departure/removal clears eligibility, never a timeout.
            if (MultiplayerRunRecords.Valid(returned) && (returned.runId != rejoinPointer?.runId || !string.IsNullOrEmpty(returned.endReason)
                || reason == "The host left, so this run has ended."
                || returned.participants.Exists(p => p.accountId == rejoinPointer?.accountId && (p.departed || p.kicked))))
                MultiplayerLastRun.Forget(rejoinPointer?.accountId, rejoinPointer?.runId);
            pendingRejoin = awaitingAdmission = false;
            PhotonNetwork.LeaveRoom(false);
            return;
        }
        string scene = returned.sceneName; // Validated against the original run, never the local dropdown.
        if (!Application.CanStreamedLevelBeLoaded(scene))
        { rejoinFailure = "The host's restaurant scene is unavailable in this build."; pendingRejoin = awaitingAdmission = false; PhotonNetwork.LeaveRoom(false); return; }
        pendingRejoin = awaitingAdmission = false;
        busy = false; starting = true;
        MultiplayerLastRun.Remember(returned, PhotonNetwork.LocalPlayer.ActorNumber);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [MultiplayerSessionManager.LoadedKey] = "" });
        PhotonCustomizationSync.PushToPhoton();
        SetStatus("Synchronizing the current restaurant...");
        // Eligibility is already checked by account/run/host here. Final actor admission is
        // checked in the gameplay scene with input blocked. Load now to pause PUN's replay
        // queue before cached customer/manager objects can instantiate in the menu.
        PhotonNetwork.LoadLevel(scene);
    }
}
