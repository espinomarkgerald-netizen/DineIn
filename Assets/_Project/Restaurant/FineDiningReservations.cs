using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Lobby3 booking schedule. Customers, tasks, stock and payments keep their existing owners.</summary>
[DisallowMultipleComponent]
public sealed class FineDiningReservations : MonoBehaviour
{
    public static FineDiningReservations Instance { get; private set; }
    public static FineDiningReservations For(Component component) => Instance != null &&
        Instance.isActiveAndEnabled && component != null && Instance.gameObject.scene == component.gameObject.scene
        ? Instance : null;

    [Header("Scene bindings")]
    public FineDiningTable[] tables;
    public LobbyLineManager receptionLine;
    public GameDayManager dayManager;
    [Header("Booking windows (in-game minutes)")]
    [Min(1)] public int bookingIntervalMinutes = 30;
    [Min(1)] public int expectedDiningMinutes = 60;
    [Min(0)] public int cleanupMinutes = 10;
    [Min(0)] public int reminderMinutes = 15;
    [Header("Daily demand")]
    [Min(1)] public int requestsPerDay = 3;
    [Range(.1f, .95f)] public float reservationShare = .85f;
    [Range(17, 22)] public int requestCutoffHour = 20;
    [Min(1)] public int requestCadenceMinutes = 20;
    [Min(1)] public int requestLeadMinutes = 30;
    public FineDiningWaitingArea waitingArea;
    public event Action NewRequests;
    public int UnreadRequests => state.bookings.Count(b => b.status == FineDiningBookingStatus.Pending && !b.viewed && b.arrivalMinute >= NowMinute);
    public void MarkRequestsViewed() { foreach(var b in state.bookings) b.viewed = true; PersistDecision(); }
    public int ReservationGroupTarget => Mathf.Clamp(Mathf.RoundToInt(state.groupBudget * reservationShare), 0, Mathf.Max(0, state.groupBudget - 1));
    public int WalkInLimit => Mathf.Max(1, state.groupBudget - ReservationGroupTarget);
    public bool CanAdmitWalkIn => state.walkInsSpawned < WalkInLimit && AvailableAdmissionGroups > 0;
    public void TrackWalkIn() { state.walkInsSpawned++; Changed?.Invoke(); }
    [Range(0f, 1f)] public float pianistBonus = .2f;
    [Min(1)] public int maximumPartySize = 4;
    [Min(1)] public int mobileMaximumPartySize = 3;
    [SerializeField] private FineDiningReservationSave state = new FineDiningReservationSave();

    private readonly Dictionary<string, CustomerGroup> parties = new Dictionary<string, CustomerGroup>();
    private float nextRefresh;
    private int lastAvailableAdmissionGroups = -1;
    private int lastExpiredRequests = -1;
    private bool lastPreparing;
    private bool lastServiceActive;
    public event Action Changed;
    public IReadOnlyList<FineDiningBooking> Bookings => state.bookings;
    public int WindowMinutes => expectedDiningMinutes + cleanupMinutes;
    public int OpeningMinute => dayManager != null ? dayManager.OpeningHour * 60 : 17 * 60;
    public int ClosingMinute => dayManager != null ? dayManager.ClosingHour * 60 : 22 * 60;
    public int NowMinute => dayManager != null ? Mathf.FloorToInt(dayManager.CurrentGameHour * 60f) : OpeningMinute;
    public bool Preparing => dayManager != null && !dayManager.ShiftRunning && !dayManager.ClosingOut && !dayManager.HasDayResults && !state.finalized;
    public int ReservedGuests => state.bookings.Where(IsAccepted).Sum(b => b.size);
    public int AcceptedGroups => state.bookings.Count(IsAccepted);
    public int UnspawnedGroups => state.bookings.Count(b => b.status == FineDiningBookingStatus.Confirmed && !parties.ContainsKey(b.id));
    public int GroupBudget => state.groupBudget;
    public int ProgressionAnnouncedDay => state.progressionAnnouncedDay;
    public void MarkProgressionAnnounced(int day) { state.progressionAnnouncedDay=day; PersistDecision(); }
    // A new booking reserves one remaining group, replacing a potential walk-in.
    public int AvailableAdmissionGroups => Mathf.Max(0, state.groupBudget - UnspawnedGroups -
        (dayManager != null && dayManager.ShiftRunning ? dayManager.GroupsSpawnedThisShift : 0));
    public int ExpectedWalkInGroups => Mathf.Max(0, WalkInLimit - state.walkInsSpawned);
    public int ExpectedGuests => ReservedGuests + ExpectedWalkInGroups * 2;
    public string DemandSummary => $"Booked: {ReservedGuests} guests • Expected walk-ins remaining: about {ExpectedWalkInGroups * 2} guests";

    private static bool IsAccepted(FineDiningBooking b) => b.status != FineDiningBookingStatus.Pending && b.status != FineDiningBookingStatus.Declined;
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        if (GameSaveManager.Instance == null || !GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave) return;
        EnsureDay();
        GenerateServiceRequests();
        bool serving = dayManager != null && dayManager.ShiftRunning;
        int expiredRequests = serving ? state.bookings.Count(b => b.status == FineDiningBookingStatus.Pending && b.arrivalMinute < NowMinute) : 0;
        bool changed = lastAvailableAdmissionGroups != AvailableAdmissionGroups || lastExpiredRequests != expiredRequests ||
            lastPreparing != Preparing || lastServiceActive != serving;
        lastAvailableAdmissionGroups = AvailableAdmissionGroups;
        lastExpiredRequests = expiredRequests;
        lastPreparing = Preparing;
        lastServiceActive = serving;
        if (!serving) { if (changed) Changed?.Invoke(); return; }
        foreach (var booking in state.bookings)
        {
            if (booking.status == FineDiningBookingStatus.Confirmed && !booking.reminded && NowMinute >= booking.arrivalMinute - reminderMinutes)
            {
                booking.reminded = true;
                WarningSlideUI.Instance?.Show($"Reservation {Clock(booking.arrivalMinute)}: {booking.guestName}, {booking.size} guests.");
                changed = true;
            }
            if (!parties.TryGetValue(booking.id, out var party)) continue;
            if (party == null)
            {
                if (FineDiningReservationRules.ProtectsTable(booking)) { booking.status = FineDiningBookingStatus.Left; changed = true; }
                continue;
            }
            if (booking.status == FineDiningBookingStatus.Confirmed && party.state == CustomerGroup.GroupState.Waiting)
            {
                booking.status = FineDiningBookingStatus.Arrived;
                if (!booking.arrivalNotified)
                {
                    booking.arrivalNotified = true;
                    party.PresentReservationArrival(booking.guestName, booking.arrivalMinute);
                    WarningSlideUI.Instance?.Show($"{booking.guestName} has arrived for {TableName(booking.tableId)}.");
                }
                changed = true;
            }
            if ((party.state == CustomerGroup.GroupState.AngryLeft || party.state == CustomerGroup.GroupState.UnhappyLeft ||
                 party.state == CustomerGroup.GroupState.Leaving) && FineDiningReservationRules.ProtectsTable(booking))
            { booking.status = FineDiningBookingStatus.Left; changed = true; }
        }
        if (changed) Changed?.Invoke();
    }

    public void EnsureDay()
    {
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        if (dayManager == null || (GameSaveManager.Instance != null && GameSaveManager.Instance.IsApplyingSave)) return;
        if (state.day == day) { EnsurePartyIdentities(); return; }
        if (dayManager.ShiftRunning) return;
        state = new FineDiningReservationSave { day = day, groupBudget = dayManager.MaxCustomersThisShift, progressionAnnouncedDay=state.progressionAnnouncedDay };
        parties.Clear();
        var usable = tables == null ? new List<FineDiningTable>() : tables.Where(t => t != null && t.Usable).ToList();
        state.nextRequestMinute = OpeningMinute + Mathf.Max(1, requestCadenceMinutes);
        GenerateRequests(Mathf.Min(ReservationGroupTarget, requestsPerDay), OpeningMinute);
        EnsurePartyIdentities(false);
        PersistDecision();
    }

    private void GenerateServiceRequests()
    {
        if (dayManager == null || !dayManager.ShiftRunning || state.finalized || NowMinute >= requestCutoffHour * 60) return;
        if (state.nextRequestMinute <= 0) state.nextRequestMinute = OpeningMinute + Mathf.Max(1, requestCadenceMinutes);
        if (NowMinute < state.nextRequestMinute) return;
        state.nextRequestMinute = NowMinute + Mathf.Max(1, requestCadenceMinutes);
        int pending = state.bookings.Count(b => b.status == FineDiningBookingStatus.Pending && b.arrivalMinute >= NowMinute);
        int remaining = Mathf.Min(ReservationGroupTarget - AcceptedGroups - pending, AvailableAdmissionGroups - pending);
        int waves = Mathf.Max(1, (requestCutoffHour * 60 - NowMinute) / Mathf.Max(1, requestCadenceMinutes));
        bool pianist = EmployeeManager.Instance?.GetAssignedEmployee(EmployeeRole.Pianist) != null;
        int batch = Mathf.Min(remaining, Mathf.CeilToInt((float)remaining / waves * (pianist ? 1f + pianistBonus : 1f)));
        GenerateRequests(batch, NowMinute + Mathf.Max(1, requestLeadMinutes));
        EnsurePartyIdentities(false);
        PersistDecision();
    }

    private void GenerateRequests(int count, int earliest)
    {
        if (count <= 0 || tables == null) return;
        int interval = Mathf.Max(1, bookingIntervalMinutes);
        int first = Mathf.CeilToInt((float)earliest / interval) * interval;
        int last = ClosingMinute - WindowMinutes;
        string[] names = { "Avery", "Morgan", "Jamie", "Riley", "Casey", "Robin", "Alex", "Taylor" };
        var random = new System.Random(state.day * 7919 + state.bookings.Count * 137 + earliest);
        // Tentative offers fit each other too, so Day 1 demand never advertises impossible capacity.
        var plan = state.bookings.Where(b => b.arrivalMinute >= NowMinute && (b.status == FineDiningBookingStatus.Pending || FineDiningReservationRules.ProtectsTable(b))).Select(b => new FineDiningBooking { id=b.id, tableId=string.IsNullOrEmpty(b.tableId)?b.suggestedTableId:b.tableId, arrivalMinute=b.arrivalMinute, status=FineDiningBookingStatus.Confirmed }).ToList();
        int added = 0;
        for (int minute=first; minute<=last && added<count; minute+=interval)
        foreach(var table in tables.Where(t=>t!=null && t.Usable).OrderBy(t=>t.tableId))
        {
            if (added>=count) break;
            if (!FineDiningReservationRules.Fits(plan,table.tableId,minute,WindowMinutes,ClosingMinute)) continue;
            int size=random.Next(1,Mathf.Min(table.Capacity, maximumPartySize)+1);
            var booking=new FineDiningBooking {id=$"fd:{state.day}:{state.bookings.Count}",guestName=names[random.Next(names.Length)]+" party",size=size,arrivalMinute=minute,suggestedTableId=table.tableId};
            state.bookings.Add(booking);
            plan.Add(new FineDiningBooking {tableId=table.tableId,arrivalMinute=minute,status=FineDiningBookingStatus.Confirmed});
            added++;
        }
        if(added>0) NewRequests?.Invoke();
    }

    private void EnsurePartyIdentities(bool persist = true)
    {
        if (!state.bookings.Any(b => b.customerType < 0)) return;
        var spawner = GetComponentInParent<GroupSpawner>();
        if (spawner == null) spawner = FindObjectsByType<GroupSpawner>(FindObjectsSortMode.None).FirstOrDefault(s => s.gameObject.scene == gameObject.scene);
        if (spawner == null) return;
        foreach (var booking in state.bookings)
            if (booking.customerType < 0) booking.customerType = (int)spawner.PickReservationCustomerType();
        if (persist) PersistDecision();
    }

    public bool IsDining(string id) => parties.TryGetValue(id, out var group) && group != null && group.state == CustomerGroup.GroupState.Eating;

    public FineDiningBooking Booking(string id) => string.IsNullOrEmpty(id) ? null : state.bookings.Find(b => b.id == id);
    public string TableName(string id) => tables?.FirstOrDefault(t => t != null && t.tableId == id)?.displayName ?? "Unassigned table";
    public static string Clock(int minute) => $"{((minute / 60 + 11) % 12) + 1}:{minute % 60:00} {(minute >= 720 ? "PM" : "AM")}";

    public bool CanAcceptRequest(FineDiningBooking booking, out string reason) =>
        FineDiningReservationRules.CanAcceptRequest(booking, Preparing,
            dayManager != null && dayManager.ShiftRunning && !state.finalized,
            NowMinute, AvailableAdmissionGroups, out reason);

    public bool CanDeclineRequest(FineDiningBooking booking) => booking != null &&
        booking.status == FineDiningBookingStatus.Pending && !state.finalized &&
        (Preparing || (dayManager != null && dayManager.ShiftRunning));

    public bool CanAssign(FineDiningBooking booking, FineDiningTable table, out string reason)
    {
        reason = "";
        if (booking == null || table == null || !table.Usable || table.Capacity < booking.size) reason = "Party does not fit this table.";
        else if (parties.TryGetValue(booking.id, out var party) && party != null && party.HasBeenAssigned)
            reason = "This party is already being seated.";
        else if (booking.status != FineDiningBookingStatus.Pending && booking.status != FineDiningBookingStatus.Confirmed && booking.status != FineDiningBookingStatus.Arrived)
            reason = "This party can no longer change tables.";
        else if (state.finalized || dayManager == null || dayManager.ClosingOut || dayManager.HasDayResults)
            reason = "Reservations are closed for this evening.";
        else if (booking.status == FineDiningBookingStatus.Pending && !CanAcceptRequest(booking, out reason)) return false;
        else if (booking.arrivalMinute < OpeningMinute || !FineDiningReservationRules.Fits(state.bookings, table.tableId,
            Mathf.Max(booking.arrivalMinute, dayManager.ShiftRunning ? NowMinute : booking.arrivalMinute), WindowMinutes, ClosingMinute, booking.id))
            reason = "This visit conflicts with another booking or closing time.";
        return reason.Length == 0;
    }

    public bool Assign(string id, FineDiningTable table, out string reason)
    {
        var booking = Booking(id);
        if (!CanAssign(booking, table, out reason)) return false;
        booking.tableId = table.tableId;
        if (booking.status == FineDiningBookingStatus.Pending) booking.status = FineDiningBookingStatus.Confirmed;
        PersistDecision();
        return true;
    }
    public void Decline(string id) => Decline(id, out _);
    public bool Decline(string id, out string reason)
    {
        var booking = Booking(id);
        reason = "This request can no longer be declined.";
        if (!CanDeclineRequest(booking)) return false;
        booking.status = FineDiningBookingStatus.Declined;
        reason = "";
        PersistDecision();
        return true;
    }
    private void PersistDecision() { Changed?.Invoke(); GameSaveManager.Instance?.RequestSave(); }

    public FineDiningBooking DueBooking() => state.bookings.Where(b => b.status == FineDiningBookingStatus.Confirmed &&
        b.arrivalMinute <= NowMinute && !parties.ContainsKey(b.id)).OrderBy(b => b.arrivalMinute).FirstOrDefault();
    public void TrackSpawn(FineDiningBooking booking, CustomerGroup group)
    {
        if (booking == null || group == null) return;
        parties[booking.id] = group;
        group.OnGroupSeated += OnSeated;
        Changed?.Invoke();
    }
    private void OnSeated(CustomerGroup group)
    {
        var booking = Booking(group.FineDiningReservationId);
        if (booking == null) return;
        booking.status = FineDiningBookingStatus.Seated;
        booking.seatedOnTime = NowMinute <= booking.arrivalMinute + 5;
        Changed?.Invoke();
    }
    public void PaidDeparture(CustomerGroup group)
    {
        var booking = Booking(group.FineDiningReservationId);
        if (booking == null || booking.status == FineDiningBookingStatus.Completed) return;
        booking.status = FineDiningBookingStatus.Completed;
        Changed?.Invoke();
    }
    public bool WaitForBookedTime(CustomerGroup group)
    {
        var booking = Booking(group.FineDiningReservationId);
        return booking != null && (NowMinute < booking.arrivalMinute || group.state != CustomerGroup.GroupState.Waiting);
    }

    public bool CanSeat(CustomerGroup group, Booth booth, out string reason)
    {
        reason = "The table is occupied, dirty, or too small.";
        if (group == null || booth == null || !booth.IsAvailableFor(group.Size)) return false;
        var table = tables?.FirstOrDefault(t => t != null && t.booth == booth);
        if (table == null || !table.Usable) { reason = "This table is not configured for Fine Dining."; return false; }
        var booking = Booking(group.FineDiningReservationId);
        int start = booking == null ? NowMinute : Mathf.Max(NowMinute, booking.arrivalMinute);
        if (!FineDiningReservationRules.Fits(state.bookings, table.tableId, start, WindowMinutes, ClosingMinute, booking?.id))
        { reason = "Reserved during this visit, or too close to closing."; return false; }
        // An overdue waiting booking still owns its commitment until reassigned or departed.
        if (state.bookings.Any(b => b.id != booking?.id && b.tableId == table.tableId &&
            (b.status == FineDiningBookingStatus.Arrived || b.status == FineDiningBookingStatus.Confirmed) && b.arrivalMinute <= NowMinute))
        { reason = "A booked party is due at this table."; return false; }
        reason = "";
        return true;
    }
    public Booth FindTable(CustomerGroup group)
    {
        var booking = Booking(group.FineDiningReservationId);
        var preferred = tables?.FirstOrDefault(t => t != null && t.tableId == booking?.tableId);
        if (preferred != null && CanSeat(group, preferred.booth, out _)) return preferred.booth;
        return tables?.Where(t => t != null && CanSeat(group, t.booth, out _)).OrderBy(t => t.Capacity).FirstOrDefault()?.booth;
    }
    public bool CommitSeating(CustomerGroup group, Booth booth)
    {
        if (!CanSeat(group, booth, out _)) return false;
        var booking = Booking(group.FineDiningReservationId);
        if (booking != null) booking.tableId = tables.First(t => t != null && t.booth == booth).tableId;
        return true;
    }
    public CustomerGroup NextReceptionParty(IEnumerable<CustomerGroup> waiting) => waiting.Concat(waitingArea != null ? waitingArea.Parties : Enumerable.Empty<CustomerGroup>()).Distinct().Where(g => g != null &&
        g.state == CustomerGroup.GroupState.Waiting && g.CanBeSeated && RestaurantTaskClaim.CanBotStart(g, 0f) && FindTable(g) != null)
        .OrderBy(g => Booking(g.FineDiningReservationId) == null ? 1 : 0)
        .ThenBy(g => Booking(g.FineDiningReservationId)?.arrivalMinute ?? int.MaxValue).FirstOrDefault();

    public void FinishDay()
    {
        if (state.finalized) return;
        foreach (var b in state.bookings)
            if (FineDiningReservationRules.ProtectsTable(b)) b.status = FineDiningBookingStatus.Left;
        state.finalized = true;
        Changed?.Invoke();
    }
    public string ResultSummary => $"Reservations: {AcceptedGroups} booked • {state.bookings.Count(b => b.seatedOnTime)} on time • {state.bookings.Count(b => b.status == FineDiningBookingStatus.Left)} unserved";
    public void FillSaveData(GameSaveData data) => data.fineDiningReservations = JsonUtility.FromJson<FineDiningReservationSave>(JsonUtility.ToJson(state));
    public void ApplySaveData(GameSaveData data)
    {
        state = data.fineDiningReservations != null ? JsonUtility.FromJson<FineDiningReservationSave>(JsonUtility.ToJson(data.fineDiningReservations)) : new FineDiningReservationSave();
        state.bookings ??= new List<FineDiningBooking>();
        // Interrupted service restarts preparation; runtime admissions belong to the checkpoint.
        if (Preparing) state.walkInsSpawned = 0;
        parties.Clear();
        Changed?.Invoke();
    }
}
