using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Scene-owned authoritative human claims; consumers receive only matching results.
[RequireComponent(typeof(MultiplayerSessionManager))]
public class MultiplayerTaskClaims : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Reserved for task claims within the multiplayer networking layer.
    private const byte RequestEvent = 181, ResultEvent = 182, SnapshotRequestEvent = 183, SnapshotEvent = 184;
    private readonly Dictionary<string, int> claims = new(StringComparer.Ordinal);
    private sealed class PendingRequest
    {
        public long generation;
        public bool acquire;
        public float sentAt;
    }
    private readonly Dictionary<string, PendingRequest> pendingRequests = new(StringComparer.Ordinal);
    private readonly Dictionary<(int actor, string task), (long generation, bool accepted)> answeredRequests = new();
    private readonly Dictionary<int, long> latestHumanIntent = new();
    private long nextGeneration, revision;
    private long appliedRevision = -1;
    private float nextSnapshot;
    private const float SnapshotSeconds = 0.75f, RequestTimeoutSeconds = 4f;
    private MultiplayerSessionManager session;
    public event Action<string, bool> ClaimResult;
    public event Action<string, int> OwnerChanged;
    public event Action ClaimsReset;
    private bool Active => session != null && session.IsMultiplayerSession;

    private void Awake() => session = GetComponent<MultiplayerSessionManager>();
    private void Start() => RequestSnapshot();
    public override void OnJoinedRoom() => RequestSnapshot();

    public int GetOwner(string taskId) => Active && taskId != null && claims.TryGetValue(taskId, out int owner) ? owner : 0;
    public bool IsClaimed(string taskId) => GetOwner(taskId) != 0;
    public bool IsClaimedBy(string taskId, int actorNumber) => actorNumber > 0 && GetOwner(taskId) == actorNumber;

    // Return value means request submitted, NOT ownership granted. Await ClaimResult.
    // Only the local actor can request; the authority uses the Photon event sender.
    public bool RequestClaim(string taskId)
    {
        if (!Active || string.IsNullOrWhiteSpace(taskId)) return false;
        if (IsHumanTask(taskId))
        {
            foreach (var entry in new List<KeyValuePair<string, PendingRequest>>(pendingRequests))
                if (entry.Key != taskId && entry.Value.acquire && IsHumanTask(entry.Key))
                {
                    if (IsClaimedBy(entry.Key, session.LocalActorNumber))
                    {
                        // Supersede an acknowledgment without surrendering a valid task.
                        pendingRequests.Remove(entry.Key);
                        ClaimResult?.Invoke(entry.Key, false);
                    }
                    else Release(entry.Key);
                }
        }
        if (pendingRequests.TryGetValue(taskId, out var request) && request.acquire) return true;
        return SendRequest(taskId, true);
    }

    private static bool IsHumanTask(string id) => id != null &&
        ((id.StartsWith("Customer:", StringComparison.Ordinal) &&
            (id.EndsWith(":GreetSeat", StringComparison.Ordinal) || id.EndsWith(":Order", StringComparison.Ordinal)
                || id.EndsWith(":Bill", StringComparison.Ordinal)))
        || (id.StartsWith("Order:", StringComparison.Ordinal) && id.EndsWith(":Pickup", StringComparison.Ordinal))
        || (id.StartsWith("Booth:", StringComparison.Ordinal) && id.EndsWith(":Cleanup", StringComparison.Ordinal)));
    public bool Release(string taskId)
    {
        bool cancellingPending = taskId != null && pendingRequests.TryGetValue(taskId, out var request) && request.acquire;
        bool sent = SendRequest(taskId, false);
        if (cancellingPending) ClaimResult?.Invoke(taskId, false);
        return sent;
    }

    public void CompleteOnAuthority(string taskId, int actor)
    {
        if (session.IsAuthority && IsClaimedBy(taskId, actor)) Publish(taskId, 0, 0, true);
    }

    private bool SendRequest(string taskId, bool acquire)
    {
        if (!Active || string.IsNullOrWhiteSpace(taskId)) return false;
        var request = new PendingRequest { generation = ++nextGeneration, acquire = acquire, sentAt = Time.unscaledTime };
        pendingRequests[taskId] = request;
        bool sent = Send(taskId, request);
        if (!sent && pendingRequests.TryGetValue(taskId, out var current) && current == request)
        {
            if (acquire) pendingRequests.Remove(taskId);
            // Releases remain queued for retry, without changing the local cache.
        }
        return sent;
    }

    private bool Send(string taskId, PendingRequest request)
    {
        if (session.IsAuthority)
        {
            HandleRequest(taskId, session.LocalActorNumber, request.acquire, request.generation);
            return true;
        }
        return PhotonNetwork.RaiseEvent(RequestEvent, new object[] { taskId, request.acquire, request.generation },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private void HandleRequest(string taskId, int actor, bool acquire, long generation)
    {
        if (!session.IsAuthority || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player)
            || player.IsInactive || string.IsNullOrWhiteSpace(taskId) || generation <= 0) return;
        var key = (actor, taskId);
        if (answeredRequests.TryGetValue(key, out var previousAnswer) && generation <= previousAnswer.generation)
        {
            if (generation == previousAnswer.generation)
                SendResult(taskId, actor, previousAnswer.accepted, generation);
            return;
        }
        int owner = GetOwner(taskId);
        if (acquire && IsHumanTask(taskId))
        {
            if (latestHumanIntent.TryGetValue(actor, out long latest) && generation < latest)
            {
                answeredRequests[key] = (generation, false);
                SendResult(taskId, actor, false, generation);
                return;
            }
            latestHumanIntent[actor] = generation;
            if (owner == actor)
            {
                answeredRequests[key] = (generation, true);
                SendResult(taskId, actor, true, generation);
                return;
            }
        }
        bool accepted = acquire ? owner == 0 || owner == actor : owner == actor;
        if (acquire && taskId.StartsWith("Customer:", StringComparison.Ordinal)
            && taskId.EndsWith(":GreetSeat", StringComparison.Ordinal))
        {
            string[] parts = taskId.Split(':');
            var view = parts.Length == 3 && int.TryParse(parts[1], out int viewId) ? PhotonView.Find(viewId) : null;
            var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
            accepted &= session.TryGetManager(actor, out _) && customer != null && customer.ReadyForInteraction;
        }
        if (acquire && taskId.StartsWith("Booth:", StringComparison.Ordinal) && taskId.EndsWith(":Cleanup", StringComparison.Ordinal))
        {
            string boothId = taskId.Substring(6, taskId.Length - 6 - ":Cleanup".Length);
            var booth = MultiplayerCustomerInteractionBridge.ResolveBooth(boothId);
            accepted &= booth != null && booth.CanRequestHumanCleanup
                && session.TryGetManager(actor, out var manager) && manager != null && manager.activeInHierarchy
                && PhotonNetwork.CurrentRoom.CustomProperties["restaurant.booth.dirty:" + boothId] is int flags && (flags & 1) != 0;
        }
        if (acquire && taskId.StartsWith("Order:", StringComparison.Ordinal)
            && taskId.EndsWith(":Pickup", StringComparison.Ordinal))
        {
            string[] parts = taskId.Split(':');
            var kitchen = FindFirstObjectByType<KitchenManager>();
            accepted &= parts.Length == 3 && int.TryParse(parts[1], out int orderNumber)
                && session.TryGetManager(actor, out _) && kitchen != null
                && kitchen.TryGetPreparedTray(orderNumber, out var tray) && !tray.TargetGroup.IsNetworkObserver
                && kitchen.TryGetForecast(orderNumber, out var forecast)
                && forecast.Group == tray.TargetGroup && forecast.State == KitchenManager.ForecastState.Completed;
        }
        if (acquire && taskId.StartsWith("Customer:", StringComparison.Ordinal)
            && taskId.EndsWith(":Order", StringComparison.Ordinal))
        {
            string[] parts = taskId.Split(':');
            var view = parts.Length == 3 && int.TryParse(parts[1], out int viewId) ? PhotonView.Find(viewId) : null;
            var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
            accepted &= session.TryGetManager(actor, out _) && customer != null && customer.Group != null
                && !customer.Group.IsNetworkObserver && customer.Group.HasBeenAssigned
                && customer.Group.state == CustomerGroup.GroupState.ReadyToOrder && !customer.Group.HasConfirmedOrder;
        }
        if (acquire && taskId.StartsWith("Customer:", StringComparison.Ordinal)
            && taskId.EndsWith(":Bill", StringComparison.Ordinal))
        {
            string[] parts = taskId.Split(':');
            var view = parts.Length == 3 && int.TryParse(parts[1], out int viewId) ? PhotonView.Find(viewId) : null;
            var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
            accepted &= session.TryGetManager(actor, out _) && customer != null && customer.Group != null
                && !customer.Group.IsNetworkObserver && customer.Group.HasBeenAssigned
                && customer.Group.state == CustomerGroup.GroupState.NeedsBill && !customer.Group.HasReceivedBill;
        }
        if (accepted && acquire && RestaurantTaskClaim.IsMultiplayerTaskOwnedByBot(taskId))
            accepted = false;
        if (!accepted && acquire)
        {
            // Rejection must not run release hooks on an AI-owned task or the
            // actor's previous task (the atomic switch below remains untouched).
            answeredRequests[key] = (generation, false);
            SendResult(taskId, actor, false, generation);
            return;
        }
        if (accepted) owner = acquire ? actor : 0;
        answeredRequests[key] = (generation, accepted);
        if (accepted && acquire && IsHumanTask(taskId))
        {
            var released = new List<string>();
            foreach (var claim in claims)
                if (claim.Value == actor && claim.Key != taskId && IsHumanTask(claim.Key)) released.Add(claim.Key);
            // Install the entire switch before gameplay hooks, callbacks or snapshots observe it.
            foreach (string previous in released) claims.Remove(previous);
            claims[taskId] = actor;
            revision++;
            foreach (string previous in released) ResetReleasedOrder(previous);
            foreach (string previous in released) OwnerChanged?.Invoke(previous, 0);
            OwnerChanged?.Invoke(taskId, actor);
            SendResult(taskId, actor, true, generation);
            return;
        }
        Publish(taskId, owner, actor, accepted, generation);
    }

    private void Publish(string taskId, int owner, int requester, bool accepted, long generation = 0)
    {
        if (owner == 0) ResetReleasedOrder(taskId);
        if (GetOwner(taskId) != owner) revision++;
        Apply(taskId, owner);
        SendResult(taskId, requester, accepted, generation);
    }

    private void ResetReleasedOrder(string taskId)
    {
        if (taskId.StartsWith("Customer:", StringComparison.Ordinal)
            && taskId.EndsWith(":Order", StringComparison.Ordinal))
        {
            var parts = taskId.Split(':');
            var view = parts.Length == 3 && int.TryParse(parts[1], out int id) ? PhotonView.Find(id) : null;
            var customer = view != null ? view.GetComponent<MultiplayerCustomerSpawn>() : null;
            if (customer != null && customer.Group != null && customer.Group.IsPlayerReviewingOrder)
            {
                customer.Group.EndPlayerOrderReview();
                customer.ReviewActor = 0;
                customer.PublishAssignment();
            }
        }
    }

    private void SendResult(string taskId, int requester, bool accepted, long generation)
    {
        Capture(out var keys, out var owners);
        PhotonNetwork.RaiseEvent(ResultEvent, new object[] { taskId, requester, accepted, generation, revision, keys, owners },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        if (requester == session.LocalActorNumber) FinishRequest(taskId, generation, accepted);
    }

    private void FinishRequest(string taskId, long generation, bool accepted)
    {
        if (!pendingRequests.TryGetValue(taskId, out var request) || request.generation != generation) return;
        pendingRequests.Remove(taskId);
        if (request.acquire) ClaimResult?.Invoke(taskId, accepted && IsClaimedBy(taskId, session.LocalActorNumber));
    }

    private void Capture(out string[] keys, out int[] owners)
    {
        keys = new string[claims.Count];
        owners = new int[claims.Count];
        claims.Keys.CopyTo(keys, 0);
        for (int i = 0; i < keys.Length; i++) owners[i] = claims[keys[i]];
    }

    private void SendSnapshot(int actor = 0)
    {
        Capture(out var keys, out var owners);
        var options = actor > 0 ? new RaiseEventOptions { TargetActors = new[] { actor } }
            : new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(SnapshotEvent, new object[] { revision, keys, owners }, options, SendOptions.SendReliable);
    }

    private void Reconcile(long incomingRevision, string[] keys, int[] owners)
    {
        if (incomingRevision <= appliedRevision || keys.Length != owners.Length) return;
        var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < keys.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(keys[i]) || owners[i] <= 0 || incoming.ContainsKey(keys[i])) return;
            incoming.Add(keys[i], owners[i]);
        }
        var changes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var claim in claims)
            if (!incoming.ContainsKey(claim.Key)) changes[claim.Key] = 0;
        foreach (var claim in incoming)
            if (GetOwner(claim.Key) != claim.Value) changes[claim.Key] = claim.Value;
        // Install the complete map before callbacks; unchanged claims never lose ownership.
        claims.Clear();
        foreach (var claim in incoming) claims.Add(claim.Key, claim.Value);
        appliedRevision = incomingRevision;
        foreach (var change in changes) OwnerChanged?.Invoke(change.Key, change.Value);
    }

    private void Update()
    {
        if (!Active) return;
        if (session.IsAuthority && Time.unscaledTime >= nextSnapshot)
        {
            nextSnapshot = Time.unscaledTime + SnapshotSeconds;
            SendSnapshot();
        }
        foreach (var entry in new List<KeyValuePair<string, PendingRequest>>(pendingRequests))
        {
            if (!pendingRequests.TryGetValue(entry.Key, out var request) || request != entry.Value
                || Time.unscaledTime - request.sentAt < RequestTimeoutSeconds) continue;
            RequestSnapshot();
            if (request.acquire)
            {
                // A newer release fences off a delayed grant/request at the authority.
                Release(entry.Key);
            }
            else
            {
                request.sentAt = Time.unscaledTime;
                Send(entry.Key, request); // Same generation: retry cannot release a newer attempt.
            }
        }
    }

    private void Apply(string taskId, int owner)
    {
        int previous = GetOwner(taskId);
        if (owner == 0) claims.Remove(taskId);
        else claims[taskId] = owner;
        if (previous != owner) OwnerChanged?.Invoke(taskId, owner);
    }

    private void RequestSnapshot()
    {
        if (Active && !session.IsAuthority)
            PhotonNetwork.RaiseEvent(SnapshotRequestEvent, null,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (!Active) return;
        if (photonEvent.Code == SnapshotRequestEvent && session.IsAuthority)
        {
            if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(photonEvent.Sender)) return;
            SendSnapshot(photonEvent.Sender);
            return;
        }
        if (!(photonEvent.CustomData is object[] data)) return;
        if (photonEvent.Code == RequestEvent && session.IsAuthority && data.Length == 3
            && data[0] is string task && data[1] is bool acquire && data[2] is long requestGeneration)
        {
            HandleRequest(task, photonEvent.Sender, acquire, requestGeneration);
            return;
        }
        // Clients never accept ownership updates from another non-authority client.
        if (photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
        if (photonEvent.Code == ResultEvent && data.Length == 7 && data[0] is string id
            && data[1] is int requester && data[2] is bool accepted && data[3] is long generation
            && data[4] is long resultRevision && data[5] is string[] resultKeys && data[6] is int[] resultOwners)
        {
            if (requester == session.LocalActorNumber &&
                (!pendingRequests.TryGetValue(id, out var request) || request.generation != generation)) return;
            Reconcile(resultRevision, resultKeys, resultOwners);
            if (requester == session.LocalActorNumber) FinishRequest(id, generation, accepted);
        }
        else if (photonEvent.Code == SnapshotEvent && data.Length == 3 && data[0] is long snapshotRevision
            && data[1] is string[] keys && data[2] is int[] owners)
        {
            Reconcile(snapshotRevision, keys, owners);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!Active || !session.IsAuthority) return;
        var released = new List<string>();
        foreach (var claim in claims)
            if (claim.Value == otherPlayer.ActorNumber) released.Add(claim.Key);
        foreach (string id in released) Publish(id, 0, 0, true);
    }

    // Deliberately reset transient ownership on migration; callers must request again.
    // Authority is always read live from the session, never cached as an actor number.
    public override void OnMasterClientSwitched(Player newMasterClient) => ClearClaims();
    public override void OnLeftRoom() => ClearClaims();
    public override void OnDisconnected(DisconnectCause cause) => ClearClaims();
    private void OnDestroy() => claims.Clear();

    private void ClearClaims()
    {
        claims.Clear();
        pendingRequests.Clear();
        answeredRequests.Clear();
        latestHumanIntent.Clear();
        revision = 0;
        appliedRevision = -1;
        nextSnapshot = 0f;
        ClaimsReset?.Invoke();
    }
}
