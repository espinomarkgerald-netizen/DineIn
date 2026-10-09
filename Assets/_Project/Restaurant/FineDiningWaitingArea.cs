using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Physical reception seating; CustomerGroup retains service, patience and departure ownership.</summary>
public sealed class FineDiningWaitingArea : MonoBehaviour
{
    [Serializable] public sealed class Seat { public Transform seat; public Transform approach; }
    public FineDiningReservations reservations;
    public LobbyLineManager receptionLine;
    public Seat[] seats = Array.Empty<Seat>();
    [Tooltip("Individual, spaced NavMesh positions near the chairs for parties larger than the available seating.")]
    public Transform[] standingPositions = Array.Empty<Transform>();
    [Min(.5f)] public float maximumPairDistance = 6f;
    [Min(.1f)] public float checkIntervalSeconds = .25f;
    [Header("Reception presentation")]
    [Tooltip("One compact task icon per reserved party. Optional Details child is shown only while selected or being handled.")]
    public GameObject reservationIndicatorPrefab;
    [Header("Reception escort")]
    [Min(.1f)] public float standTransitionSeconds = .25f;
    [Min(.5f)] public float escortMemberSpacing = 1.25f;
    [Min(.1f)] public float escortRepathSeconds = .2f;

    private sealed class Place
    {
        public Seat chair;
        public Transform anchor;
        public Vector3 destination;
    }
    private readonly Dictionary<CustomerGroup, Place[]> occupants = new Dictionary<CustomerGroup, Place[]>();
    private float nextCheck;
    public IEnumerable<CustomerGroup> Parties => occupants.Keys;
    public bool Contains(CustomerGroup group) => group != null && occupants.ContainsKey(group);

    public bool IsSettled(CustomerGroup group)
    {
        if (group == null || !occupants.TryGetValue(group, out var places) || places.Length != group.members.Count) return false;
        for (int i = 0; i < places.Length; i++)
        {
            var member = group.members[i];
            if (member == null || (places[i].chair != null ? !member.IsSeated : !member.HasArrived(places[i].destination))) return false;
        }
        return true;
    }

    private void Update()
    {
        if (Time.time < nextCheck || reservations == null || receptionLine == null) return;
        nextCheck = Time.time + checkIntervalSeconds;
        foreach (var pair in occupants.ToArray())
        {
            var group = pair.Key;
            if (group == null || !group.gameObject.activeInHierarchy || !group.CanBeSeated) { Release(group); continue; }
            for (int i = 0; i < Mathf.Min(group.members.Count, pair.Value.Length); i++)
            {
                var member = group.members[i];
                var place = pair.Value[i];
                if (member == null || member.IsSeated) continue;
                if (member.HasArrived(place.destination))
                {
                    if (place.chair != null)
                        member.SnapToSeat(place.chair.seat.position, place.chair.seat.rotation);
                    else
                        member.SetIdleFacing(place.anchor.forward, true);
                }

            }
        }

        foreach (var group in receptionLine.WaitingParties.ToArray())
        {
            if (group == null || group.state != CustomerGroup.GroupState.Waiting || !group.CanBeSeated ||
                group.CurrentReceptionTaskOwner != CustomerGroup.ReceptionTaskOwner.None ||
                RestaurantTaskClaim.IsClaimedByPlayer(group) || RestaurantTaskClaim.IsClaimedByBot(group)) continue;
            // The host may be busy even if a table is free. Guests can use chairs until the escort starts.
            // With no free waiting places they keep their existing, spaced reception-line positions.
            TryWait(group);
        }
    }

    public bool TryWait(CustomerGroup group)
    {
        if (group == null || !group.gameObject.activeInHierarchy || !group.CanBeSeated || group.Size < 1 || Contains(group)) return false;
        var occupiedPlaces = occupants.Values.SelectMany(v => v).ToArray();
        var used = new HashSet<Transform>(occupiedPlaces.Select(p => p.anchor));
        var free = new List<Place>();
        foreach (var chair in seats ?? Array.Empty<Seat>())
            if (chair != null && chair.seat != null && chair.approach != null &&
                !used.Contains(chair.seat) && !free.Any(p => p.anchor == chair.seat) && !SeatAnchor.IsSeatOccupied(chair.seat))
                free.Add(new Place { chair = chair, anchor = chair.seat, destination = chair.approach.position });
        foreach (var point in standingPositions ?? Array.Empty<Transform>())
            if (point != null && !used.Contains(point) && !free.Any(p => p.anchor == point))
                free.Add(new Place { anchor = point, destination = point.position });
        if (free.Count < group.Size) return false;

        Vector3 origin = group.members.FirstOrDefault(m => m != null)?.transform.position ?? group.transform.position;
        var firstChair = free.Where(p => p.chair != null).OrderBy(p => (p.anchor.position - origin).sqrMagnitude).FirstOrDefault();
        if (firstChair != null) origin = firstChair.anchor.position;
        var candidates = free.OrderBy(p => group.Size == 2 && Vector3.Distance(p.anchor.position, origin) > maximumPairDistance ? 1 : 0)
            .ThenBy(p => p.chair == null ? 1 : 0).ThenBy(p => (p.anchor.position - origin).sqrMagnitude).ToList();
        var selected = new List<Place>();
        var path = new NavMeshPath();
        for (int i = 0; i < group.Size; i++)
        {
            var member = group.members[i];
            var agent = member != null ? member.Agent : null;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            Place choice = null;
            foreach (var place in candidates)
            {
                if (!NavMesh.SamplePosition(place.destination, out var hit, .75f, agent.areaMask) ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float clearance = Mathf.Max(.55f, agent.radius * 2f + .1f);
                if (selected.Any(p => (p.destination - hit.position).sqrMagnitude < clearance * clearance) ||
                    occupiedPlaces.Any(p => (p.destination - hit.position).sqrMagnitude < clearance * clearance)) continue;
                place.destination = hit.position;
                choice = place;
                break;
            }
            if (choice == null) return false;
            selected.Add(choice);
            candidates.Remove(choice);
        }

        // Reserve all chairs before anyone moves so two parties cannot share a seat.
        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i].chair == null || SeatAnchor.TryOccupy(selected[i].chair.seat, group.members[i].gameObject)) continue;
            for (int j = 0; j < i; j++) if (selected[j].chair != null) SeatAnchor.VacateSeat(selected[j].chair.seat);
            return false;
        }
        occupants.Add(group, selected.ToArray());
        group.OnGroupAssignedToBooth += Release;
        group.OnGroupLeftLine += Release;
        for (int i = 0; i < group.Size; i++)
        {
            if (group.members[i].TryWalkTo(selected[i].destination, out _)) continue;
            Release(group);
            receptionLine.ForceRebuildLine();
            return false;
        }
        receptionLine.DetachForWaiting(group);
        return true;
    }

    private void Release(CustomerGroup group)
    {
        if (ReferenceEquals(group, null) || !occupants.TryGetValue(group, out var places)) return;
        occupants.Remove(group);
        foreach (var place in places) if (place.chair != null) SeatAnchor.VacateSeat(place.chair.seat);
        if (group == null) return;
        group.OnGroupAssignedToBooth -= Release;
        group.OnGroupLeftLine -= Release;
        foreach (var member in group.members)
            if (member != null)
            {
                member.SetIdleFacing(Vector3.forward, false);
                if (member.IsSeated) member.Unseat();
            }
    }

    private void OnDisable() { foreach (var group in occupants.Keys.ToArray()) Release(group); }
}
