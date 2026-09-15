using System;
using Photon.Pun;
using UnityEngine;

public sealed partial class ManagerComplaintSystem
{
    [Serializable] public sealed class NetworkState
    {
        public int view, type, owner;
        public bool open, resolving;
        public string customerLine, response, coaching;
        public Color color;
    }
    private int networkOwner;
    private bool networkCommit;
    public void SuspendNetworkPresentation()
    {
        if (!MultiplayerServiceActions.IsActive) return;
        HidePresentationImmediate();
        RestoreCamera();
        worldMarker?.SetWorldMarkerVisible(false);
        if (MultiplayerSessionManager.Instance.Ended) StopAllCoroutines();
    }
    public NetworkState CaptureNetworkState() => activeGroup == null ? null : new NetworkState
    {
        view = activeGroup.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0,
        type = (int)activeType, owner = networkOwner, open = dialogueOpen, resolving = resolving,
        customerLine = customerLineText != null ? customerLineText.text : string.Empty,
        response = managerResponseText != null ? managerResponseText.text : string.Empty,
        coaching = coachingText != null ? coachingText.text : string.Empty,
        color = coachingText != null ? coachingText.color : Color.white
    };
    public bool OpenNetworkComplaint(CustomerGroup group, int actor)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsAuthority || group != activeGroup || activeDefinition == null || resolving
            || networkOwner != 0 && networkOwner != actor) return false;
        networkOwner = actor;
        networkCommit = true;
        try { OpenActiveComplaint(); } finally { networkCommit = false; }
        if (actor != session.LocalActorNumber) { HidePresentationImmediate(); RestoreCamera(); }
        return true;
    }
    public bool ResolveNetworkComplaint(CustomerGroup group, int actor, int choice)
    {
        var session = MultiplayerSessionManager.Instance;
        if (session == null || !session.IsAuthority || group != activeGroup || activeDefinition == null
            || resolving || networkOwner != actor || !dialogueOpen || choice < 0 || choice > 2) return false;
        var response = choice == 0 ? activeDefinition.professional : choice == 1 ? activeDefinition.acceptable : activeDefinition.poor;
        if (response == null) return false;
        networkCommit = true;
        try { ResolveResponse(response, false); } finally { networkCommit = false; }
        if (actor != session.LocalActorNumber) { HidePresentationImmediate(); RestoreCamera(); }
        return true;
    }
    public void ApplyNetworkState(NetworkState value)
    {
        if (!MultiplayerRestaurantBridge.IsObserver) return;
        var group = value != null ? MultiplayerServiceActions.Resolve(value.view) : null;
        if (group == null)
        {
            if (activeGroup != null || worldMarker != null) CancelActiveComplaint(false);
            return;
        }
        bool changed = activeGroup != group || activeType != (ManagerComplaintType)value.type;
        if (changed)
        {
            CancelActiveComplaint(false);
            activeGroup = group; activeType = (ManagerComplaintType)value.type;
            activeDefinition = settings != null ? settings.GetDefinition(activeType) : null;
            SpawnWorldMarker();
        }
        networkOwner = value.owner;
        bool localOpen = value.open && value.owner == MultiplayerSessionManager.Instance.LocalActorNumber;
        bool wasOpen = dialogueOpen;
        dialogueOpen = value.open; resolving = value.resolving;
        worldMarker?.SetWorldMarkerVisible(!value.open);
        if (localOpen && activeDefinition != null)
        {
            if (changed || !wasOpen) { PopulateDialogue(); FocusCameraOnGroup(); }
            dialogueRoot?.SetActive(true);
            if (dialoguePanel != null) dialoguePanel.localScale = Vector3.one;
            if (customerLineText != null) customerLineText.text = value.customerLine;
            if (managerResponseText != null) managerResponseText.text = value.response;
            if (coachingText != null) { coachingText.text = value.coaching; coachingText.color = value.color; }
            SetResponseButtonsInteractable(!value.resolving && MultiplayerSessionManager.Instance.CanAct);
        }
        else { HidePresentationImmediate(); RestoreCamera(); }
    }
}
