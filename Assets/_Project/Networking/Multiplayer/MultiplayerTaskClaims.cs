using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Scene-owned infrastructure only. No restaurant gameplay calls this yet.
[RequireComponent(typeof(MultiplayerSessionManager))]
public class MultiplayerTaskClaims : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Reserved for task claims within the multiplayer networking layer.
    private const byte RequestEvent = 181, ResultEvent = 182, SnapshotRequestEvent = 183, SnapshotEvent = 184;
    private readonly Dictionary<string, int> claims = new(StringComparer.Ordinal);
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
    public bool RequestClaim(string taskId) => SendRequest(taskId, true);
    public bool Release(string taskId) => SendRequest(taskId, false);

    public void CompleteOnAuthority(string taskId, int actor)
    {
        if (session.IsAuthority && IsClaimedBy(taskId, actor)) Publish(taskId, 0, 0, true);
    }

    private bool SendRequest(string taskId, bool acquire)
    {
        if (!Active || string.IsNullOrWhiteSpace(taskId)) return false;
        if (session.IsAuthority)
        {
            HandleRequest(taskId, session.LocalActorNumber, acquire);
            return true;
        }
        return PhotonNetwork.RaiseEvent(RequestEvent, new object[] { taskId, acquire },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private void HandleRequest(string taskId, int actor, bool acquire)
    {
        if (!session.IsAuthority || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player)
            || player.IsInactive || string.IsNullOrWhiteSpace(taskId)) return;
        int owner = GetOwner(taskId);
        if (acquire)
        {
            var previousPickups = new List<string>();
            foreach (var claim in claims)
                if (claim.Value == actor && claim.Key != taskId && claim.Key.StartsWith("Order:", StringComparison.Ordinal)
                    && claim.Key.EndsWith(":Pickup", StringComparison.Ordinal)) previousPickups.Add(claim.Key);
            foreach (string previous in previousPickups) Publish(previous, 0, 0, true);
        }
        bool accepted = acquire ? owner == 0 || owner == actor : owner == actor;
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
        if (accepted) owner = acquire ? actor : 0;
        Publish(taskId, owner, acquire ? actor : 0, accepted);
    }

    private void Publish(string taskId, int owner, int requester, bool accepted)
    {
        if (owner == 0 && taskId.StartsWith("Customer:", StringComparison.Ordinal)
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
        Apply(taskId, owner);
        PhotonNetwork.RaiseEvent(ResultEvent, new object[] { taskId, owner, requester, accepted },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        if (requester == session.LocalActorNumber) ClaimResult?.Invoke(taskId, accepted);
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
            var keys = new string[claims.Count];
            var owners = new int[claims.Count];
            claims.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++) owners[i] = claims[keys[i]];
            PhotonNetwork.RaiseEvent(SnapshotEvent, new object[] { keys, owners },
                new RaiseEventOptions { TargetActors = new[] { photonEvent.Sender } }, SendOptions.SendReliable);
            return;
        }
        if (!(photonEvent.CustomData is object[] data)) return;
        if (photonEvent.Code == RequestEvent && session.IsAuthority && data.Length == 2
            && data[0] is string task && data[1] is bool acquire)
        {
            HandleRequest(task, photonEvent.Sender, acquire);
            return;
        }
        // Clients never accept ownership updates from another non-authority client.
        if (photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
        if (photonEvent.Code == ResultEvent && data.Length == 4 && data[0] is string id
            && data[1] is int owner && data[2] is int requester && data[3] is bool accepted)
        {
            Apply(id, owner);
            if (requester == session.LocalActorNumber) ClaimResult?.Invoke(id, accepted);
        }
        else if (photonEvent.Code == SnapshotEvent && data.Length == 2
            && data[0] is string[] keys && data[1] is int[] owners && keys.Length == owners.Length)
        {
            ClearClaims();
            for (int i = 0; i < keys.Length; i++) Apply(keys[i], owners[i]);
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
        ClaimsReset?.Invoke();
    }
}
