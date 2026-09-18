using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

public sealed partial class HygieneManager
{
    private const byte CleaningRequest = 178, CleaningReply = 179;
    private readonly Dictionary<string, string> cleaningReplies = new();
    private readonly Dictionary<int, float> pendingCleaning = new();
    private readonly List<int> expiredCleaning = new();
    private readonly Dictionary<int, int> routeFailures = new();
    private int cleaningSequence, shownFeedbackHour = -1;
    private float nextFeedbackCheck;
    private int cleaningSelectionFrame = -1;
    private readonly Dictionary<string, int> boothFailures = new();
    private Booth selectedCleaningBooth;
    private LobbyAutonomousService lobbyService;

    public static bool HandsEmpty(PlayerMovement mover)
    {
        if (mover == null) return false;
        var hands = mover.GetComponent<WaiterHands>();
        return (hands == null || !(hands.HasTray || hands.HasBill || hands.HasMoney || hands.HasTicket))
            && mover.GetComponent<BusserHands>()?.HasTray != true
            && mover.GetComponentInChildren<TakeoutBagInteractable>(true) == null;
    }
    public bool IsBoothQueued(Booth booth) => booth != null && !string.IsNullOrEmpty(State.queuedBooth)
        && State.queuedBooth == MultiplayerCustomerInteractionBridge.BoothIdentity(booth);

    // Called from the existing world click path, after its UI/touch filtering.
    public bool TrySelectCleaning(PlayerMovement mover, RaycastHit[] hits)
    {
        if (InputBlocked || GameplayUIBlocker.IsBlocked() || MultiplayerRestockView.Active || GameDayManager.Instance?.ServiceActive != true) return false;
        if (cleaningSelectionFrame == Time.frameCount) return true;
        if (selectedCleaningBooth != null) selectedCleaningBooth.CloseCleaningPrompt();
        selectedCleaningBooth = null;
        foreach (var hit in hits)
        {
            if (!TutorialCustomerFlowBridge.AllowsWorldInteraction(hit.collider.transform)) continue;
            var booth = hit.collider.GetComponentInParent<Booth>();
            if (booth != null && booth.CanRequestHumanCleanup)
            { cleaningSelectionFrame = Time.frameCount; selectedCleaningBooth = booth; booth.OpenCleaningPrompt(); return true; }
            if (hit.collider.GetComponentInParent<BusserHands>() != null)
            { cleaningSelectionFrame = Time.frameCount; RequestLobbyDialogue(); return true; }
            // Do not select a booth through a customer or service item.
            if (hit.collider.GetComponentInParent<CustomerAgent>() != null || hit.collider.GetComponentInParent<FoodTray>() != null) return false;
        }
        return false;
    }

    public void RequestBoothStaff(Booth booth)
    {
        if (booth != null) SendCleaningRequest("booth", MultiplayerCustomerInteractionBridge.BoothIdentity(booth));
    }
    public void RequestLobbyDialogue() => SendCleaningRequest("lobby", "");
    private void SendCleaningRequest(string operation, string target)
    {
        if (Authority)
        { WarningSlideUI.Instance?.Show(HandleCleaningRequest(operation, target)); return; }
        int id = ++cleaningSequence;
        if (pendingCleaning.Count >= 4) return;
        pendingCleaning.Add(id, Time.unscaledTime + 8f);
        if (!MultiplayerWire.Raise(CleaningRequest, new object[] { id, operation, target },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable))
        { pendingCleaning.Remove(id); WarningSlideUI.Instance?.Show("Unable to contact staff. Please try again."); }
    }
    public void OnEvent(EventData data)
    {
        if (data.Code != CleaningRequest && data.Code != CleaningReply || !MultiplayerWire.TryRead(data, out var payload)
            || payload is not object[] args) return;
        var session = MultiplayerSessionManager.Instance;
        if (data.Code == CleaningReply)
        {
            if (data.Sender == session.Run.hostActor && args.Length == 2 && args[0] is int replyId
                && args[1] is string message && pendingCleaning.Remove(replyId)) WarningSlideUI.Instance?.Show(message);
            return;
        }
        if (!Authority || args.Length != 3 || args[0] is not int id || args[1] is not string operation
            || args[2] is not string target || target.Length > 512 || !session.ValidActor(data.Sender)) return;
        string key = data.Sender + ":" + id;
        if (!cleaningReplies.TryGetValue(key, out string result))
        {
            result = HandleCleaningRequest(operation, target);
            if (cleaningReplies.Count >= 64) cleaningReplies.Clear();
            cleaningReplies[key] = result;
        }
        MultiplayerWire.Raise(CleaningReply, new object[] { id, result },
            new RaiseEventOptions { TargetActors = new[] { data.Sender } }, SendOptions.SendReliable);
    }
    private void ExpireCleaningRequests()
    {
        expiredCleaning.Clear();
        foreach (var request in pendingCleaning)
            if (Time.unscaledTime >= request.Value) expiredCleaning.Add(request.Key);
        foreach (int id in expiredCleaning) pendingCleaning.Remove(id);
        if (expiredCleaning.Count > 0) WarningSlideUI.Instance?.Show("Staff haven't responded. Please try again.");
    }
    private string HandleCleaningRequest(string operation, string target)
    {
        if (!CanRecordActivity) return "Cleaning is available during service.";
        if (lobbyService == null) lobbyService = FindFirstObjectByType<LobbyAutonomousService>();
        if (operation == "lobby")
        {
            if (State.lobbyCleaningRequested) return "Staff are already cleaning the lobby.";
            if (State.floorMarks.Count == 0) return "The floor is already clean.";
            if (!dialogueReady || GameplayUIBlocker.IsBlocked() || ManagerComplaintSystem.Instance?.HasActiveComplaint == true)
                return "Please close the current panel first.";
            OpenDecision(HygieneArea.Lobby);
            return "Let's take care of the lobby.";
        }
        if (operation != "booth") return "That cleaning request is unavailable.";
        var booth = MultiplayerCustomerInteractionBridge.ResolveBooth(target);
        if (booth == null || !booth.CanRequestHumanCleanup) return "This table is occupied or already clean.";
        if (lobbyService == null || !lobbyService.HasCleaningStaff) return "Assign a busser before requesting staff cleaning.";
        if (IsBoothQueued(booth)) return "This table is already waiting for staff.";
        if (!string.IsNullOrEmpty(State.queuedBooth)) return "Staff already have a table queued for cleaning.";
        boothFailures.Remove(target);
        State.queuedBooth = target; Changed(); Publish();
        return "Staff will clean this table after their current task.";
    }
    private void ResetCleaningRequests()
    {
        if (selectedCleaningBooth != null) selectedCleaningBooth.CloseCleaningPrompt();
        selectedCleaningBooth = null;
        State.queuedBooth = null; State.floorRoute.Clear(); State.lobbyCleaningRequested = false;
        State.floorRouteDone = State.floorRouteTotal = 0;
        State.floorRouteActive = false; State.floorWorkSeconds = State.floorWorkProgress = 0f;
        State.ResetRouteLookup(); floorRetryAfter = 0f; boothFailures.Clear();
        cleaningReplies.Clear(); pendingCleaning.Clear(); routeFailures.Clear(); shownFeedbackHour = -1;
    }
    private void CaptureFloorRoute()
    {
        if (State.lobbyCleaningRequested) return;
        if (lobbyService == null) lobbyService = FindFirstObjectByType<LobbyAutonomousService>();
        if (lobbyService == null || !lobbyService.HasCleaningStaff)
        { WarningSlideUI.Instance?.Show("Assign a busser before requesting staff cleaning."); return; }
        HygieneCleaningRoute.Capture(State, lobbyService.CleaningStaffPosition);
        routeFailures.Clear(); Changed();
    }
    private void SkipUnreachableMark(HygieneFloorMark mark)
    {
        routeFailures.TryGetValue(mark.id, out int failed); routeFailures[mark.id] = ++failed;
        // Leave dirt intact, try other points, and release the role after bounded retries.
        if (failed >= 3)
        {
            State.floorRoute.Remove(mark.id); State.floorRouteDone++;
            if (++State.floorRouteSkipped == 1)
                WarningSlideUI.Instance?.Show("Staff couldn't reach part of the floor. Please clear the way and ask again.");
        }
        Changed();
    }
    private void TickCleaningRequests()
    {
        float progress = Mathf.Round(LobbyCleaningProgress * 100f) / 100f;
        if (!Mathf.Approximately(State.floorWorkProgress, progress)) { State.floorWorkProgress = progress; Changed(); }
        if (!string.IsNullOrEmpty(State.queuedBooth))
        {
            var booth = MultiplayerCustomerInteractionBridge.ResolveBooth(State.queuedBooth);
            if (booth == null || !booth.CanRequestHumanCleanup || lobbyService == null || !lobbyService.HasCleaningStaff)
            { State.queuedBooth = null; Changed(); }
        }
        if (Time.time < nextFeedbackCheck || State.decisionOpen) return;
        nextFeedbackCheck = Time.time + 1f;
        if (State.lobbyCleaningRequested)
        {
            var live = new HashSet<int>(); foreach (var mark in State.floorMarks) live.Add(mark.id);
            int removed = State.floorRoute.RemoveAll(id => !live.Contains(id));
            if (removed > 0) { State.floorRouteDone += removed; Changed(); }
            if ((!State.floorRouteActive && State.floorRoute.Count == 0) || lobbyService == null || !lobbyService.HasCleaningStaff)
                CancelLobbyCleaning();
        }
        int hour = Mathf.FloorToInt(GameDayManager.Instance.CurrentGameHour);
        if (hour <= State.feedbackHour || TutorialSystem.IsTutorialMode) return;
        foreach (var pair in customerActivity)
        {
            var group = pair.Key;
            if (group == null || !group.CanShowHygieneFeedback) continue;
            float dirt = BoothDirt(group.assignedBooth);
            Vector3 position = group.assignedBooth.GetNavigableApproachPosition();
            foreach (var mark in State.floorMarks)
                if ((mark.position - position).sqrMagnitude <= 9f) dirt = Mathf.Max(dirt, mark.dirt);
            if (dirt >= .25f && dirt < .55f) continue;
            bool positive = dirt < .25f;
            State.feedbackHour = hour;
            State.feedbackCustomer = group.GetComponent<PhotonView>()?.ViewID ?? 0;
            State.feedbackPositive = positive; State.feedbackExpires = PhotonNetwork.Time + 5d;
            group.PresentHygieneFeedback(positive);
            shownFeedbackHour = hour;
            AlienApprovalManager.Instance?.RegisterHygieneFeedback(hour, positive);
            Changed(); Publish(); break;
        }
    }
    private void PresentHygieneFeedback()
    {
        if (Authority || State.feedbackHour <= shownFeedbackHour || PhotonNetwork.Time >= State.feedbackExpires) return;
        var group = PhotonView.Find(State.feedbackCustomer)?.GetComponent<CustomerGroup>();
        if (group == null || !group.CanShowHygieneFeedback) return;
        shownFeedbackHour = State.feedbackHour;
        group.PresentHygieneFeedback(State.feedbackPositive);
    }
    public void ReportBoothApproachFailure(Booth booth)
    {
        if (!Authority || !IsBoothQueued(booth)) return;
        string id = State.queuedBooth; boothFailures.TryGetValue(id, out int failed); boothFailures[id] = ++failed;
        if (failed < 3) return;
        State.queuedBooth = null; Changed();
        WarningSlideUI.Instance?.Show("Staff couldn't reach the table. Please clear the way and ask again.");
    }
}
