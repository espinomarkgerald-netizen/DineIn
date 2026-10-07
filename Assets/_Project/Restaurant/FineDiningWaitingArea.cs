using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Physical reception seating only; existing CustomerGroup owns patience, service and leaving.</summary>
public sealed class FineDiningWaitingArea : MonoBehaviour
{
    [Serializable] public sealed class Seat { public Transform seat; public Transform approach; }
    public FineDiningReservations reservations;
    public LobbyLineManager receptionLine;
    public Seat[] seats = Array.Empty<Seat>();
    [Min(.5f)] public float maximumPairDistance = 6f;
    private readonly Dictionary<CustomerGroup, Seat[]> occupants = new Dictionary<CustomerGroup, Seat[]>();
    private float nextCheck;
    public IEnumerable<CustomerGroup> Parties => occupants.Keys;
    public bool Contains(CustomerGroup group) => group != null && occupants.ContainsKey(group);

    private void Update()
    {
        if(Time.time < nextCheck || reservations == null || receptionLine == null) return;
        nextCheck=Time.time+.25f;
        foreach(var pair in occupants.ToArray())
        {
            var group=pair.Key;
            if(group==null || !group.CanBeSeated) { Release(group); continue; }
            for(int i=0;i<group.members.Count;i++)
            {
                var member=group.members[i];var seat=pair.Value[i];
                if(member==null || member.IsSeated) continue;
                if(member.HasArrived(seat.approach.position)) member.SnapToSeat(seat.seat.position,seat.seat.rotation);
                else if(member.Agent==null || !member.Agent.isOnNavMesh || (!member.Agent.pathPending && member.Agent.pathStatus!=NavMeshPathStatus.PathComplete)) group.LeaveReceptionUnavailable();
            }
        }
        var front=receptionLine.WaitingParties.FirstOrDefault(g=>g!=null && g.state==CustomerGroup.GroupState.Waiting && g.CanBeSeated && string.IsNullOrEmpty(g.FineDiningReservationId) && RestaurantTaskClaim.CanBotStart(g,0f) && reservations.FindTable(g)==null);
        if(front==null || front.state!=CustomerGroup.GroupState.Waiting || !front.CanBeSeated || !string.IsNullOrEmpty(front.FineDiningReservationId) || !RestaurantTaskClaim.CanBotStart(front,0f) || reservations.FindTable(front)!=null) return;
        if(!TryWait(front)) front.LeaveReceptionUnavailable();
    }
    public bool TryWait(CustomerGroup group)
    {
        if(group==null || group.Size<1 || group.Size>2 || Contains(group))return false;
        var free=seats.Where(s=>s!=null && s.seat!=null && s.approach!=null && !occupants.Values.Any(v=>v.Contains(s))).ToArray();
        Seat[] selected=null;
        foreach(var first in free)
        {
            var candidate=group.Size==1?new[]{first}:free.Where(s=>s!=first && Vector3.Distance(s.seat.position,first.seat.position)<=maximumPairDistance).OrderBy(s=>(s.seat.position-first.seat.position).sqrMagnitude).Select(s=>new[]{first,s}).FirstOrDefault();
            if(candidate==null)continue;
            bool reachable=true;
            for(int i=0;i<group.Size;i++)
            {var agent=group.members[i]?.Agent;var path=new NavMeshPath();if(agent==null || !agent.isOnNavMesh || !agent.CalculatePath(candidate[i].approach.position,path) || path.status!=NavMeshPathStatus.PathComplete){reachable=false;break;}}
            if(reachable){selected=candidate;break;}
        }
        if(selected==null)return false;
        occupants.Add(group,selected);
        group.OnGroupAssignedToBooth+=Release;group.OnGroupLeftLine+=Release;
        receptionLine.DetachForWaiting(group);
        for(int i=0;i<group.Size;i++) if(!group.members[i].TryWalkTo(selected[i].approach.position,out _)){group.LeaveReceptionUnavailable();Release(group);return false;}
        return true;
    }
    private void Release(CustomerGroup group)
    {
        if(ReferenceEquals(group,null))return;
        occupants.Remove(group);
        if(group==null)return;
        group.OnGroupAssignedToBooth-=Release;group.OnGroupLeftLine-=Release;
        foreach(var member in group.members)if(member!=null && member.IsSeated)member.Unseat();
    }
    private void OnDisable(){foreach(var group in occupants.Keys.ToArray())Release(group);}
}
