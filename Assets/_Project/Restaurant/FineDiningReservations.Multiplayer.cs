using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

public sealed partial class FineDiningReservations
{
    [Header("Multiplayer service capacity")]
    [Min(0f)] public float groupsPerAdditionalPlayer = 2.5f;
    [Serializable] public sealed class NetworkParty { public string booking; public int view; }
    [Serializable] public sealed class NetworkState
    {
        public FineDiningReservationSave schedule;
        public int groupBudget, availableGroups, lastArrival;
        public NetworkParty[] parties;
    }
    private NetworkState observedReservations;
    private bool observedPartiesReady;
    private readonly HashSet<string> locallyViewedBookings = new(StringComparer.Ordinal);
    private bool UsesMultiplayer => gameObject.scene.name == FineDiningScene.Multiplayer
        && !MultiplayerProgressionContext.RestorationInProgress;
    private bool IsMultiplayerObserver => UsesMultiplayer && !FineDiningMultiplayerSession.IsAuthority;
    private bool CanSimulateReservations => !UsesMultiplayer || (FineDiningMultiplayerSession.IsAuthority
        && MultiplayerProgressionContext.Ready && MultiplayerSessionManager.Instance.IsConnected);
    public bool HasObservedReservationParties => !IsMultiplayerObserver || (observedReservations != null && observedPartiesReady);
    public int ReservationDay => state.day;
    private bool WasViewed(FineDiningBooking booking) => UsesMultiplayer ? locallyViewedBookings.Contains(booking.id) : booking.viewed;
    private float AdditionalHumanCapacity
    {
        get
        {
            var session = MultiplayerSessionManager.Instance;
            if (!UsesMultiplayer || session?.Run == null) return 0f;
            return Mathf.Max(0, session.Run.participants.Count(p => session.ValidActor(p.actor)) - 1) * groupsPerAdditionalPlayer;
        }
    }

    public NetworkState CaptureNetworkReservations() => new NetworkState
    {
        schedule = JsonUtility.FromJson<FineDiningReservationSave>(JsonUtility.ToJson(state)),
        groupBudget = GroupBudget, availableGroups = AvailableAdmissionGroups, lastArrival = lastArrivalMinute,
        parties = parties.Where(pair => pair.Value != null)
            .Select(pair => new NetworkParty { booking = pair.Key,
                view = pair.Value.GetComponentInParent<MultiplayerCustomerSpawn>()?.photonView.ViewID ?? 0 })
            .Where(party => party.view > 0).OrderBy(party => party.booking).ToArray()
    };

    // Observers never use save restoration: it clears live parties and restarts service checkpoints.
    public bool ApplyObservedReservations(NetworkState snapshot)
    {
        if (!IsMultiplayerObserver || snapshot?.schedule?.bookings == null || GameFlowManager.Instance == null
            || snapshot.schedule.day != GameFlowManager.Instance.CurrentDay) return false;
        int unread = UnreadRequests;
        if (state.day != snapshot.schedule.day) locallyViewedBookings.Clear();
        state = snapshot.schedule;
        observedReservations = snapshot;
        lastArrivalMinute = snapshot.lastArrival;
        ResolveObservedParties();
        Changed?.Invoke();
        if (UnreadRequests > unread) NewRequests?.Invoke();
        return true;
    }

    private void ResolveObservedParties()
    {
        if (observedReservations == null) return;
        observedPartiesReady = true;
        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in observedReservations.parties ?? Array.Empty<NetworkParty>())
        {
            if (mapping == null || string.IsNullOrEmpty(mapping.booking) || mapping.view <= 0) continue;
            liveIds.Add(mapping.booking);
            var customer = PhotonView.Find(mapping.view)?.GetComponent<MultiplayerCustomerSpawn>();
            if (customer?.Group == null) { observedPartiesReady = false; continue; }
            parties[mapping.booking] = customer.Group;
        }
        foreach (var id in parties.Keys.Where(id => !liveIds.Contains(id)).ToArray()) parties.Remove(id);
    }

    public void ResetObservedReservations()
    {
        observedReservations = null;
        observedPartiesReady = false;
    }
}
