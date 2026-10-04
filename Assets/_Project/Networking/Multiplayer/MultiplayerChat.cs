using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Reliable room events; a guest never supplies its identity or a displayed sender name.
public sealed class MultiplayerChat : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte SendEvent = 166, MessageEvent = 167, HistoryRequest = 168, HistoryReply = 169;
    [Serializable] public sealed class Message
    { public long sequence; public string account, sender, value; public int kind; public double sentAt; public bool animate; }
    [Serializable] private sealed class History { public List<Message> messages = new(); }
    [SerializeField] private MultiplayerChatCatalog catalog;
    public MultiplayerChatCatalog Catalog => catalog;
    public IReadOnlyList<Message> Messages => history.messages;
    public event Action<bool> Changed;
    // Transient feedback is deliberately not replayed when reconnect history arrives.
    public event Action<Message> MessageReceived;
    private MultiplayerSessionManager session;
    private readonly History history = new();
    private readonly Dictionary<string, double> lastSend = new(StringComparer.Ordinal);
    private readonly Dictionary<int, double> lastHistoryRequest = new();
    private string runId;
    private long sequence;
    private float nextSend, nextHistoryRequest;
    private bool historyReceived;
    public bool CanSend => session != null && session.CanAct && Time.unscaledTime >= nextSend;
    private void Start() { session = MultiplayerSessionManager.Instance; runId = session?.RunId; }
    private void Update()
    {
        if (session == null || catalog == null) return;
        if (session.RunId != runId)
        { runId = session.RunId; history.messages.Clear(); lastSend.Clear(); sequence = 0; historyReceived = false; Changed?.Invoke(false); }
        if (!historyReceived && session.IsConnected && session.ValidActor(session.LocalActorNumber) && Time.unscaledTime >= nextHistoryRequest)
        {
            nextHistoryRequest = Time.unscaledTime + 2f;
            if (session.IsAuthority) historyReceived = true;
            else PhotonNetwork.RaiseEvent(HistoryRequest, runId, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
    }
    public bool Send(int kind, string value)
    {
        if (!CanSend || !Validate(kind, value, out value)) return false;
        bool sent;
        if (session.IsAuthority) { Accept(session.LocalActorNumber, kind, value); sent = true; }
        else sent = PhotonNetwork.RaiseEvent(SendEvent, new object[] { runId, kind, value },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        if (sent) nextSend = Time.unscaledTime + catalog.sendCooldown;
        return sent;
    }
    private bool Validate(int kind, string value, out string clean)
    {
        clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || clean.Length > catalog.characterLimit) return false;
        if (kind == 0)
        { foreach (char c in clean) if (char.IsControl(c)) return false; return true; }
        return kind >= 1 && kind <= 3 && catalog.Find(clean, kind) != null;
    }
    private void Accept(int actor, int kind, string value)
    {
        if (!session.IsAuthority || !session.ValidActor(actor) || !Validate(kind, value, out value)) return;
        var member = session.Run.participants.Find(p => p.actor == actor);
        if (member == null || lastSend.TryGetValue(member.accountId, out double last) && PhotonNetwork.Time - last < catalog.sendCooldown) return;
        lastSend[member.accountId] = PhotonNetwork.Time;
        bool animate = kind == 3 && session.TryGetManager(actor, out var avatar)
            && avatar.TryGetComponent<MultiplayerAvatarEmote>(out var emote) && emote.CanPlay;
        var message = new Message { sequence = ++sequence, account = member.accountId, sender = member.displayName,
            kind = kind, value = value, sentAt = PhotonNetwork.Time, animate = animate };
        Add(message);
        PhotonNetwork.RaiseEvent(MessageEvent, new object[] { runId, JsonUtility.ToJson(message) },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }
    public void OnEvent(EventData ev)
    {
        if (session == null || !session.IsConnected || session.Ended || catalog == null) return;
        if (ev.Code == HistoryRequest && session.IsAuthority && ev.CustomData is string requested && requested == runId && session.ValidActor(ev.Sender))
        {
            if (lastHistoryRequest.TryGetValue(ev.Sender, out double last) && PhotonNetwork.Time - last < 1) return;
            lastHistoryRequest[ev.Sender] = PhotonNetwork.Time;
            PhotonNetwork.RaiseEvent(HistoryReply, new object[] { runId, JsonUtility.ToJson(history) },
                new RaiseEventOptions { TargetActors = new[] { ev.Sender } }, SendOptions.SendReliable);
            return;
        }
        if (ev.CustomData is not object[] data || data.Length < 2 || data[0] is not string run || run != runId) return;
        if (ev.Code == SendEvent && data.Length == 3 && data[1] is int kind && data[2] is string value)
        { Accept(ev.Sender, kind, value); return; }
        if (ev.Sender != session.Run.hostActor || ev.Sender != PhotonNetwork.MasterClient?.ActorNumber || data.Length != 2 || data[1] is not string json || json.Length > 65536) return;
        try
        {
            if (ev.Code == MessageEvent) Add(JsonUtility.FromJson<Message>(json));
            else if (ev.Code == HistoryReply)
            {
                var snapshot = JsonUtility.FromJson<History>(json);
                if (snapshot?.messages == null || snapshot.messages.Count > catalog.historyLimit) return;
                foreach (var m in snapshot.messages) Add(m, false);
                historyReceived = true;
            }
        }
        catch (ArgumentException) { /* Ignore malformed room packets. */ }
    }
    private void Add(Message message, bool notify = true)
    {
        if (message == null || message.sequence <= 0 || !Validate(message.kind, message.value, out _) ||
            !session.Run.participants.Exists(p => p.accountId == message.account) || history.messages.Exists(m => m.sequence == message.sequence)) return;
        history.messages.Add(message);
        history.messages.Sort((a, b) => a.sequence.CompareTo(b.sequence));
        while (history.messages.Count > catalog.historyLimit) history.messages.RemoveAt(0);
        Changed?.Invoke(notify);
        if (notify) MessageReceived?.Invoke(message);
    }
    public override void OnDisconnected(DisconnectCause cause) { historyReceived = false; }
}
