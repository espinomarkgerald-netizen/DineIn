using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed partial class MultiplayerServiceActions
{
    private const byte FeedbackEvent = 178;
    [Serializable] private sealed class Feedback
    {
        public string run, action;
        public int day, cue;
        public int paymentAmount, customerView;
        public double sentAt;
        public bool hasPosition;
        public Vector3 position;
    }
    private long feedbackSequence;
    private string feedbackRun;
    private readonly HashSet<string> heardFeedback = new();
    private readonly Queue<string> feedbackHistory = new();

    public void PublishFeedback(GameplaySfx.Cue cue, UnityEngine.Object target, int paymentAmount = 0)
    {
        if (session == null || !session.IsAuthority || session.IsRecovering || session.Run == null) return;
        var message = new Feedback { run = session.RunId, day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 0,
            paymentAmount = cue == GameplaySfx.Cue.Payment ? Math.Max(0, paymentAmount) : 0,
            customerView = target is CustomerGroup group ? ViewId(group) : 0,
            cue = (int)cue, sentAt = PhotonNetwork.Time,
            action = session.LocalActorNumber + ":" + (++feedbackSequence) + ":" + (target != null ? target.GetInstanceID() : 0) };
        message.hasPosition = GameplaySfx.TryPosition(target, out message.position);
        // Explicitly uncached; no snapshot or late-join history contains presentation events.
        HearFeedback(message);
        MultiplayerWire.Raise(FeedbackEvent, JsonUtility.ToJson(message),
            new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.DoNotCache }, SendOptions.SendReliable);
    }

    private void ReceiveFeedback(EventData ev)
    {
        if (session == null || !session.IsConnected || !session.CanAct || session.IsRecovering || session.Run == null ||
            ev.Sender != session.Run.hostActor || ev.Sender != PhotonNetwork.MasterClient?.ActorNumber ||
            !MultiplayerWire.TryRead(ev, out var payload) || payload is not string json || json.Length > 1024) return;
        try { HearFeedback(JsonUtility.FromJson<Feedback>(json)); }
        catch (ArgumentException) { /* Invalid presentation payload cannot affect gameplay. */ }
    }

    private void HearFeedback(Feedback message)
    {
        if (message == null || message.run != session.RunId || string.IsNullOrEmpty(message.action) ||
            message.action.Length > 128 || message.cue < 0 || (message.cue > 2 && message.cue != (int)GameplaySfx.Cue.Tip) ||
            double.IsNaN(message.sentAt) || double.IsInfinity(message.sentAt) ||
            Math.Abs(PhotonNetwork.Time - message.sentAt) > 2d ||
            GameFlowManager.Instance == null || message.day != GameFlowManager.Instance.CurrentDay) return;
        if (message.hasPosition && (!GameplaySfxView.Finite(message.position.x) ||
            !GameplaySfxView.Finite(message.position.y) || !GameplaySfxView.Finite(message.position.z))) return;
        if (feedbackRun != message.run) { feedbackRun = message.run; heardFeedback.Clear(); feedbackHistory.Clear(); }
        if (!heardFeedback.Add(message.action)) return;
        feedbackHistory.Enqueue(message.action);
        if (feedbackHistory.Count > 256) heardFeedback.Remove(feedbackHistory.Dequeue());
        if (message.hasPosition) GameplaySfx.PresentAction((GameplaySfx.Cue)message.cue, message.position);
        else GameplaySfx.PresentAction((GameplaySfx.Cue)message.cue);
        if (message.cue == (int)GameplaySfx.Cue.Payment && message.paymentAmount > 0)
            Resolve(message.customerView)?.PresentPayment(message.paymentAmount);
    }
}
