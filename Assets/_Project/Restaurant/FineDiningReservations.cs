using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Lobby3 booking schedule. Customers, tasks, stock and payments keep their existing owners.</summary>
[DisallowMultipleComponent]
public sealed partial class FineDiningReservations : MonoBehaviour
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
    [Header("Fine Dining progression (total groups, including walk-ins)")]
    public Vector2Int earlyGroupTarget = new Vector2Int(6, 8);
    public Vector2Int middleGroupTarget = new Vector2Int(9, 14);
    public Vector2Int lateGroupTarget = new Vector2Int(15, 20);
    [Min(1)] public int middleProgressionDay = 6;
    [Min(1)] public int lateProgressionDay = 15;
    [Min(1)] public int middleUsableTables = 4;
    [Min(1)] public int lateUsableTables = 7;
    [Range(1, 20)] public int maximumDailyGroups = 20;
    [Tooltip("Minimum time between newly offered arrivals. Accepted times never move.")]
    [Min(1)] public int minimumArrivalSpacingMinutes = 10;
    [Min(1)] public int largePartySpacingMinutes = 20;
    [Header("Service capacity (normal staff, no equipment upgrades required)")]
    [Min(1)] public int soloServiceGroupCapacity = 8;
    [Min(0f)] public float groupsPerAssignedStaff = 2.5f;
    [Range(0f, 1f)] public float busyTableFraction = .8f;
    [Range(.5f, 1f)] public float quietWalkInIntervalMultiplier = .85f;
    [Tooltip("The eating portion of expectedDiningMinutes; only this portion receives the eating multiplier.")]
    [Min(0)] public int baselineEatingMinutes = 20;
    [Min(0)] public int additionalDrinkWorkMinutes = 10;
    [Header("Fine Dining customer timing")]
    [Min(1f)] public float walkInReceptionPatienceMultiplier = 2f;
    [Min(1f)] public float eatingDurationMultiplier = 1.5f;
    [Range(0f, 1f)] public float additionalDrinkChance = .85f;
    [Range(.1f, .8f)] public float additionalDrinkTriggerFraction = .35f;
    [Min(1f)] public float additionalDrinkServiceSeconds = 75f;
    [Tooltip("Maximum optional service time before dining resumes without a penalty or extra charge.")]
    [Min(30f)] public float additionalDrinkMaximumServiceSeconds = 180f;
    [Range(0f, 1f)] public float wineRefillChance = 1f;
    [Header("Lobby3 customer bubble sizing (pixels at 1080 viewport height)")]
    [Min(48f)] public float customerBubbleWidth = 160f;
    [Min(0f)] public float customerBubbleVerticalGap = 10f;
    [Min(0f)] public float customerBubbleTouchPadding = 12f;
    [Header("Lobby3 customer bubble focus")]
    [Min(1)] public int maxExpandedCustomerBubbles = 3;
    [Range(.3f, 1f)] public float compactCustomerBubbleScale = .55f;
    [Range(0f, 1f)] public float urgentCustomerBubblePatience = .3f;
    [Header("Additional drink presentation")]
    public GameObject orderRequestBubblePrefab;
    public GameObject drinkRequestBubblePrefab;
    public GameObject wineBottlePrefab;
    public Recipe[] wineRefillProducts = Array.Empty<Recipe>();
    [Tooltip("Available from day one; an optional pitcher pour when the party did not order an eligible wine.")]
    public Recipe waterPourProduct;
    public Sprite waterPitcherIcon;
    [Tooltip("Extra tip for a completed player pour, awarded once per party. Ignored requests have no penalty.")]
    public Vector2Int pourServiceTipRange = new Vector2Int(500, 1000);
    private WinePouringUI cachedPourUI;
    public WinePouringUI PourUI
    {
        get
        {
            if (cachedPourUI == null) cachedPourUI = FindFirstObjectByType<WinePouringUI>(FindObjectsInactive.Include);
            return cachedPourUI;
        }
    }
    public Vector3 wineBottleHandOffset = Vector3.zero;
    public Vector3 wineBottleHandRotation = Vector3.zero;
    public Vector3 wineBottleHandScale = Vector3.one;
    public FineDiningWaitingArea waitingArea;
    public event Action NewRequests;
    public int UnreadRequests => state.bookings.Count(b => b.status == FineDiningBookingStatus.Pending && !WasViewed(b) && b.arrivalMinute >= NowMinute);
    public void MarkRequestsViewed()
    {
        foreach (var b in state.bookings)
            if (UsesMultiplayer) locallyViewedBookings.Add(b.id); else b.viewed = true;
        if (UsesMultiplayer) Changed?.Invoke(); else PersistDecision();
    }
    public int ReservationGroupTarget => Mathf.Clamp(Mathf.RoundToInt(GroupBudget * reservationShare), 0, Mathf.Max(0, GroupBudget - 1));
    public int WalkInLimit => Mathf.Max(0, GroupBudget - ReservationGroupTarget);
    public bool CanAdmitWalkIn => CanSimulateReservations && !IsTutorialScene && state.walkInsSpawned < WalkInLimit && AvailableAdmissionGroups > 0 &&
        HasWalkInWaitingCapacity && !ServiceBusy && MaximumWalkInSize > 0 &&
        NowMinute - lastArrivalMinute >= minimumArrivalSpacingMinutes;
    public void TrackWalkIn() { if (!CanSimulateReservations) return; state.walkInsSpawned++; lastArrivalMinute = NowMinute; Changed?.Invoke(); }
    [Range(0f, 1f)] public float pianistBonus = .2f;
    [Min(1)] public int maximumPartySize = 4;
    [Min(1)] public int mobileMaximumPartySize = 3;
    [SerializeField] private FineDiningReservationSave state = new FineDiningReservationSave();

    private readonly Dictionary<string, CustomerGroup> parties = new Dictionary<string, CustomerGroup>();
    private float nextRefresh;
    private int lastArrivalMinute = int.MinValue / 2;
    private int lastAvailableAdmissionGroups = -1;
    private int lastExpiredRequests = -1;
    private bool lastPreparing;
    private bool lastServiceActive;
    public event Action Changed;
    private bool IsTutorialScene => gameObject.scene.name == "Lobby3Tutorial";
    public IReadOnlyList<FineDiningBooking> Bookings => state.bookings;
    public int WindowMinutes => Mathf.Max(1, expectedDiningMinutes + cleanupMinutes +
        Mathf.CeilToInt(Mathf.Min(expectedDiningMinutes, baselineEatingMinutes) * (Mathf.Max(1f, eatingDurationMultiplier) - 1f) +
            Mathf.Clamp01(additionalDrinkChance) * additionalDrinkWorkMinutes));
    public int OpeningMinute => dayManager != null ? dayManager.OpeningHour * 60 : 17 * 60;
    public int ClosingMinute => dayManager != null ? dayManager.ClosingHour * 60 : 22 * 60;
    public int NowMinute => dayManager != null ? Mathf.FloorToInt(dayManager.CurrentGameHour * 60f) : OpeningMinute;
    public bool Preparing => dayManager != null && !dayManager.ShiftRunning && !dayManager.ClosingOut && !dayManager.HasDayResults && !state.finalized;
    public int ReservedGuests => state.bookings.Where(IsAccepted).Sum(b => b.size);
    public int AcceptedGroups => state.bookings.Count(IsAccepted);
    public int UnspawnedGroups => state.bookings.Count(b => b.status == FineDiningBookingStatus.Confirmed && !parties.ContainsKey(b.id));
    [Tooltip("Fraction of seating turnover to target, leaving room for service and cleanup.")]
    [Range(.1f, 1f)] public float seatingUtilization = .75f;
    [Min(1f)] public float expectedPartySize = 2.5f;
    [Range(.1f, 1f)] public float waitingPartiesPerTable = .5f;
    public int SeatingGroupCapacity => CalculateSeatingBudget(
        tables == null ? Array.Empty<int>() : tables.Where(t => t != null && t.Usable).Select(t => t.Capacity),
        ClosingMinute - OpeningMinute, WindowMinutes, seatingUtilization, expectedPartySize);
    public static int CalculateSeatingBudget(IEnumerable<int> capacities, int serviceMinutes, int visitMinutes, float utilization, float partySize)
    {
        float tablesPerTurn = capacities.Where(c => c > 0).Sum(c => Mathf.Min(1f, c / Mathf.Max(1f, partySize)));
        return Mathf.Max(0, Mathf.FloorToInt(tablesPerTurn * Mathf.FloorToInt((float)serviceMinutes / Mathf.Max(1, visitMinutes)) * Mathf.Clamp01(utilization)));
    }
    public int UsableTableCount => tables == null ? 0 : tables.Count(t => t != null && t.Usable);
    public int ProgressionGroupTarget
    {
        get
        {
            int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : Mathf.Max(1, state.day);
            Vector2Int range = day >= lateProgressionDay && UsableTableCount >= lateUsableTables ? lateGroupTarget :
                day >= middleProgressionDay && UsableTableCount >= middleUsableTables ? middleGroupTarget : earlyGroupTarget;
            int cap = Mathf.Clamp(maximumDailyGroups, 1, 20);
            int minimum = Mathf.Clamp(range.x, 1, cap), maximum = Mathf.Clamp(range.y, minimum, cap);
            // Stable within a day and after reload; no demand roll every frame.
            return minimum + (day - 1) % (maximum - minimum + 1);
        }
    }
    private static readonly EmployeeRole[] ServiceRoles = { EmployeeRole.Host, EmployeeRole.Waiter, EmployeeRole.Busser,
        EmployeeRole.Cashier, EmployeeRole.Chef, EmployeeRole.SousChef, EmployeeRole.Bartender };
    public int StaffGroupCapacity
    {
        get
        {
            float staff = 0f;
            if (EmployeeManager.Instance != null)
                foreach (var role in ServiceRoles)
                {
                    var employee = EmployeeManager.Instance.GetAssignedEmployee(role);
                    if (employee != null) staff += Mathf.Clamp(employee.speed / 100f, .5f, 1.25f);
                }
            float drinkWorkload = 1f + Mathf.Clamp01(additionalDrinkChance) * additionalDrinkWorkMinutes / Mathf.Max(1f, expectedDiningMinutes);
            return Mathf.Max(1, Mathf.FloorToInt((soloServiceGroupCapacity + staff * groupsPerAssignedStaff + AdditionalHumanCapacity) *
                (ClosingMinute - OpeningMinute) / 300f / drinkWorkload));
        }
    }
    public int CapacityGroupTarget => Mathf.Min(ProgressionGroupTarget, SeatingGroupCapacity, StaffGroupCapacity);
    // Preserve accepted booking data, but never admit more than the restaurant hard cap.
    public int GroupBudget => IsMultiplayerObserver && observedReservations != null ? observedReservations.groupBudget : Mathf.Min(Mathf.Clamp(maximumDailyGroups, 1, 20),
        Mathf.Max(AcceptedGroups + state.walkInsSpawned, Mathf.Min(state.groupBudget, CapacityGroupTarget)));
    public float WalkInIntervalSeconds => dayManager == null ? 60f :
        dayManager.ShiftLengthSeconds * Mathf.Max(0f, 1f - (float)WindowMinutes / Mathf.Max(1, ClosingMinute - OpeningMinute)) /
        Mathf.Max(1, WalkInLimit) * (WaitingPartyCount == 0 && OccupiedTableCount < Mathf.Max(1, UsableTableCount / 2)
            ? quietWalkInIntervalMultiplier : 1f);
    private int WaitingPartyCount => (receptionLine != null ? receptionLine.WaitingParties : Enumerable.Empty<CustomerGroup>())
        .Concat(waitingArea != null ? waitingArea.Parties : Enumerable.Empty<CustomerGroup>())
        .Where(g => g != null && g.CanBeSeated).Distinct().Count();
    private int OccupiedTableCount => tables == null ? 0 : tables.Count(t => t != null && t.Usable && !t.booth.IsAvailableFor(1));
    public bool ServiceBusy => WaitingPartyCount > 0 || (UsableTableCount > 0 &&
        (float)OccupiedTableCount / UsableTableCount >= busyTableFraction);
    public int MaximumWalkInSize => tables == null || NowMinute + WindowMinutes > ClosingMinute ? 0 :
        tables.Where(t => t != null && t.Usable && t.booth.IsAvailableFor(1) &&
            FineDiningReservationRules.Fits(state.bookings, t.tableId, NowMinute, WindowMinutes, ClosingMinute) &&
            !state.bookings.Any(b => FineDiningReservationRules.ProtectsTable(b) && b.tableId == t.tableId && b.arrivalMinute <= NowMinute))
        .Select(t => Mathf.Min(2, t.Capacity)).DefaultIfEmpty(0).Max();
    private bool HasWalkInWaitingCapacity
    {
        get
        {
            int usable = tables == null ? 0 : tables.Count(t => t != null && t.Usable);
            if (usable == 0 || NowMinute + WindowMinutes > ClosingMinute) return false;
            return WaitingPartyCount < Mathf.Max(1, Mathf.CeilToInt(usable * waitingPartiesPerTable));
        }
    }
    public string NoSeatingMessage(int partySize) => tables != null && tables.Any(t => t != null && t.Usable && t.Capacity >= partySize)
        ? "No suitable tables are available right now. Please wait for a table to be cleared."
        : "No purchased table can accommodate this party. Please add a table with enough seats for this party.";
    public int ProgressionAnnouncedDay => state.progressionAnnouncedDay;
    public void MarkProgressionAnnounced(int day) { if (!CanSimulateReservations) return; state.progressionAnnouncedDay=day; PersistDecision(); }
    // A new booking reserves one remaining group, replacing a potential walk-in.
    public int AvailableAdmissionGroups => IsMultiplayerObserver && observedReservations != null ? observedReservations.availableGroups : Mathf.Max(0, GroupBudget - UnspawnedGroups -
        (dayManager != null && dayManager.ShiftRunning ? dayManager.GroupsSpawnedThisShift : 0));
    public int ExpectedWalkInGroups => Mathf.Min(AvailableAdmissionGroups, Mathf.Max(0, WalkInLimit - state.walkInsSpawned));
    public int ExpectedGuests => ReservedGuests + ExpectedWalkInGroups * 2;
    public string DemandSummary => $"Booked guests: {ReservedGuests}\nExpected walk-ins: {ExpectedWalkInGroups * 2}\nExpected total guests: {ExpectedGuests}";

    private static bool IsAccepted(FineDiningBooking b) => b.status != FineDiningBookingStatus.Pending && b.status != FineDiningBookingStatus.Declined;
    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        if (IsMultiplayerObserver) { ResolveObservedParties(); return; }
        if (!CanSimulateReservations) return;
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
        if (!CanSimulateReservations) return;
        int day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1;
        if (dayManager == null || (GameSaveManager.Instance != null && GameSaveManager.Instance.IsApplyingSave)) return;
        if (state.day == day)
        {
            // Preparation purchases/staff changes and legacy oversized forecasts use today's capacity.
            if (Preparing) state.groupBudget = CapacityGroupTarget;
            EnsurePartyIdentities();
            return;
        }
        if (dayManager.ShiftRunning) return;
        state = new FineDiningReservationSave { day = day, groupBudget = CapacityGroupTarget, progressionAnnouncedDay=state.progressionAnnouncedDay };
        parties.Clear();
        lastArrivalMinute = int.MinValue / 2;
        state.nextRequestMinute = OpeningMinute + Mathf.Max(1, requestCadenceMinutes);
        if (!IsTutorialScene) GenerateRequests(Mathf.Min(ReservationGroupTarget, requestsPerDay), OpeningMinute);
        EnsurePartyIdentities(false);
        PersistDecision();
    }

    private void GenerateServiceRequests()
    {
        // The final tutorial introduces one authored party at a time.
        if (IsTutorialScene) return;
        if (dayManager == null || state.finalized || (!Preparing && !dayManager.ShiftRunning) || NowMinute >= requestCutoffHour * 60) return;
        int pending = state.bookings.Count(b => b.status == FineDiningBookingStatus.Pending && b.arrivalMinute >= NowMinute);
        int remaining = Mathf.Min(ReservationGroupTarget - AcceptedGroups - pending, AvailableAdmissionGroups - pending);
        if (remaining <= 0) return;
        if (Preparing)
        {
            // Keep a small choice of bookable offers available when the player declines in preparation.
            int batch = Mathf.Min(remaining, Mathf.Max(0, requestsPerDay - pending));
            if (batch <= 0 || !GenerateRequests(batch, Mathf.Max(OpeningMinute, NowMinute))) return;
        }
        else
        {
            if (state.nextRequestMinute <= 0) state.nextRequestMinute = OpeningMinute + Mathf.Max(1, requestCadenceMinutes);
            if (NowMinute < state.nextRequestMinute) return;
            state.nextRequestMinute = NowMinute + Mathf.Max(1, requestCadenceMinutes);
            int waves = Mathf.Max(1, (requestCutoffHour * 60 - NowMinute) / Mathf.Max(1, requestCadenceMinutes));
            bool pianist = EmployeeManager.Instance?.GetAssignedEmployee(EmployeeRole.Pianist) != null;
            int batch = Mathf.Min(remaining, Mathf.CeilToInt((float)remaining / waves * (pianist ? 1f + pianistBonus : 1f)));
            if (ServiceBusy) batch = Mathf.Min(batch, 1);
            if (!GenerateRequests(batch, NowMinute + Mathf.Max(1, requestLeadMinutes))) return;
        }
        EnsurePartyIdentities(false);
        PersistDecision();
    }

    private int ForecastTableReadyMinute(FineDiningTable table)
    {
        if (Preparing || table.booth.IsAvailableFor(1)) return NowMinute;
        var group = table.booth.CurrentGroup;
        if (group == null) return NowMinute + cleanupMinutes;
        int remaining = group.state == CustomerGroup.GroupState.NeedsBill ? cleanupMinutes + 10 :
            group.state == CustomerGroup.GroupState.Eating ? cleanupMinutes + 10 +
                Mathf.CeilToInt(baselineEatingMinutes * eatingDurationMultiplier + additionalDrinkChance * additionalDrinkWorkMinutes) : WindowMinutes;
        return NowMinute + remaining;
    }

    private bool GenerateRequests(int count, int earliest)
    {
        if (count <= 0 || tables == null) return false;
        // Existing scenes used a 30-minute grid. The arrival spacing also permits staggered
        // requests between those grid points rather than piling several tables into one slot.
        int interval = Mathf.Max(1, Mathf.Min(bookingIntervalMinutes, minimumArrivalSpacingMinutes));
        int first = Mathf.CeilToInt((float)Mathf.Max(earliest, NowMinute) / interval) * interval;
        int last = ClosingMinute - WindowMinutes;
        var usable = tables.Where(t => t != null && t.Usable).OrderBy(t => t.Capacity).ThenBy(t => t.tableId).ToArray();
        if (usable.Length == 0 || first > last) return false;
        string[] names = { "Avery", "Morgan", "Jamie", "Riley", "Casey", "Robin", "Alex", "Taylor" };
        var random = new System.Random(state.day * 7919 + state.bookings.Count * 137 + earliest);
        // Pending offers consume forecast capacity too; accepted legacy commitments remain intact.
        var plan = state.bookings.Where(b => FineDiningReservationRules.ProtectsTable(b) ||
            b.status == FineDiningBookingStatus.Pending && b.arrivalMinute >= NowMinute)
            .Select(b => new FineDiningBooking { id = b.id, size = b.size,
                tableId = string.IsNullOrEmpty(b.tableId) ? b.suggestedTableId : b.tableId,
                arrivalMinute = b.arrivalMinute, status = FineDiningBookingStatus.Confirmed }).ToList();
        int added = 0;
        for (; added < count; added++)
        {
            int maxSize = Mathf.Min(usable.Max(t => t.Capacity), Mathf.Max(1, maximumPartySize));
            if (Application.isMobilePlatform) maxSize = Mathf.Min(maxSize, Mathf.Max(1, mobileMaximumPartySize));
            int size = random.Next(1, maxSize + 1);
            FineDiningTable selectedTable = null;
            int selectedMinute = first;
            // If the remaining gaps only suit a small party, offer one that can really be served.
            for (; size >= 1 && selectedTable == null; size--)
            {
                int bestDistance = -1;
                for (int minute = first; minute <= last; minute += interval)
                {
                    if (plan.Any(b => Math.Abs(b.arrivalMinute - minute) < minimumArrivalSpacingMinutes ||
                        size >= 3 && b.size >= 3 && Math.Abs(b.arrivalMinute - minute) < largePartySpacingMinutes)) continue;
                    var table = usable.FirstOrDefault(t => t.Capacity >= size && minute >= ForecastTableReadyMinute(t) &&
                        FineDiningReservationRules.Fits(plan, t.tableId, minute, WindowMinutes, ClosingMinute));
                    if (table == null) continue;
                    int distance = plan.Count == 0 ? last - minute : plan.Min(b => Math.Abs(b.arrivalMinute - minute));
                    if (distance <= bestDistance) continue;
                    bestDistance = distance;
                    selectedTable = table;
                    selectedMinute = minute;
                }
            }
            if (selectedTable == null) break;
            size++; // The successful size was decremented by the loop iterator.
            var booking = new FineDiningBooking { id = $"fd:{state.day}:{state.bookings.Count}",
                guestName = names[random.Next(names.Length)] + " party", size = size,
                arrivalMinute = selectedMinute, suggestedTableId = selectedTable.tableId };
            state.bookings.Add(booking);
            plan.Add(new FineDiningBooking { id = booking.id, tableId = selectedTable.tableId, arrivalMinute = selectedMinute,
                size = size, status = FineDiningBookingStatus.Confirmed });
        }
        if (added > 0) NewRequests?.Invoke();
        return added > 0;
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

    public bool IsOnTheWay(string id) => parties.TryGetValue(id, out var group) && group != null &&
        group.state == CustomerGroup.GroupState.WalkingToLobby;

    public string ArrivalStatus(FineDiningBooking booking)
    {
        if (IsOnTheWay(booking.id)) return "ON THE WAY";
        if (Preparing) return "UPCOMING";
        int minutes = booking.arrivalMinute - NowMinute;
        if (minutes < 0) return "ARRIVAL DELAYED";
        return minutes <= reminderMinutes ? "ARRIVING SOON" : "UPCOMING";
    }

    public bool IsDining(string id) => parties.TryGetValue(id, out var group) && group != null && group.state == CustomerGroup.GroupState.Eating;

    public FineDiningBooking Booking(string id) => string.IsNullOrEmpty(id) ? null : state.bookings.Find(b => b.id == id);
    public string TableName(string id) => tables?.FirstOrDefault(t => t != null && t.tableId == id)?.displayName ?? "Unassigned table";
    public static string Clock(int minute) => $"{((minute / 60 + 11) % 12) + 1}:{minute % 60:00} {(minute >= 720 ? "PM" : "AM")}";

    public bool CanAcceptRequest(FineDiningBooking booking, out string reason)
    {
        if (!FineDiningReservationRules.CanAcceptRequest(booking, Preparing,
            dayManager != null && dayManager.ShiftRunning && !state.finalized,
            NowMinute, AvailableAdmissionGroups, out reason)) return false;
        // Legacy pending offers may share an arrival time. New acceptance must follow
        // the same pacing as newly generated offers; existing commitments never move.
        if (state.bookings.Any(other => other.id != booking.id && IsAccepted(other) &&
            (Math.Abs(other.arrivalMinute - booking.arrivalMinute) < minimumArrivalSpacingMinutes ||
             booking.size >= 3 && other.size >= 3 && Math.Abs(other.arrivalMinute - booking.arrivalMinute) < largePartySpacingMinutes)))
        {
            reason = "Another party is booked near this arrival time. Decline this request to receive a better-spaced offer.";
            return false;
        }
        return true;
    }

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
        else if (booking.status == FineDiningBookingStatus.Pending && !Preparing && ForecastTableReadyMinute(table) > booking.arrivalMinute)
            reason = "This table needs more service and cleanup time before the party arrives.";
        else if (booking.arrivalMinute < OpeningMinute || !FineDiningReservationRules.Fits(state.bookings, table.tableId,
            Mathf.Max(booking.arrivalMinute, dayManager.ShiftRunning ? NowMinute : booking.arrivalMinute), WindowMinutes, ClosingMinute, booking.id))
            reason = "This visit conflicts with another booking or closing time.";
        return reason.Length == 0;
    }

    public bool Assign(string id, FineDiningTable table, out string reason)
    {
        if (!CanSimulateReservations || (UsesMultiplayer && !MultiplayerFineDiningService.CommittingReservation))
        { reason = "Waiting for the shared reservation decision."; return false; }
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
        if (!CanSimulateReservations || (UsesMultiplayer && !MultiplayerFineDiningService.CommittingReservation))
        { reason = "Waiting for the shared reservation decision."; return false; }
        var booking = Booking(id);
        reason = "This request can no longer be declined.";
        if (!CanDeclineRequest(booking)) return false;
        booking.status = FineDiningBookingStatus.Declined;
        reason = "";
        PersistDecision();
        return true;
    }
    private void PersistDecision() { Changed?.Invoke(); if (!UsesMultiplayer) GameSaveManager.Instance?.RequestSave(); }

    public bool CanSpawnArrival(string reservationId)
    {
        if (!CanSimulateReservations) return false;
        if (IsTutorialScene) return false; // The tutorial bridge owns arrivals, not the service clock.
        if (dayManager == null || !dayManager.ShiftRunning ||
            dayManager.GroupsSpawnedThisShift >= GroupBudget ||
            NowMinute - lastArrivalMinute < Mathf.Max(1, minimumArrivalSpacingMinutes)) return false;
        if (string.IsNullOrEmpty(reservationId)) return CanAdmitWalkIn;
        var booking = Booking(reservationId);
        return booking != null && booking.status == FineDiningBookingStatus.Confirmed &&
            booking.arrivalMinute <= NowMinute && !parties.ContainsKey(reservationId) &&
            WaitingPartyCount < Mathf.Max(1, Mathf.CeilToInt(UsableTableCount * waitingPartiesPerTable));
    }
    public FineDiningBooking DueBooking() => state.bookings.Where(b => CanSpawnArrival(b.id))
        .OrderBy(b => b.arrivalMinute).FirstOrDefault();

    /// <summary>Prepares a request, never accepts it. The player still uses the real reservation panel.</summary>
    public FineDiningBooking PrepareTutorialBooking(string guestName, int size)
    {
        if (!IsTutorialScene || dayManager == null || parties.Values.Any(party => party != null)) return null;
        var table = tables?.Where(candidate => candidate != null && candidate.Usable && candidate.Capacity >= size)
            .OrderBy(candidate => candidate.Capacity).FirstOrDefault();
        if (table == null || size < 1) return null;
        state = new FineDiningReservationSave
        {
            day = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentDay : 1,
            groupBudget = CapacityGroupTarget
        };
        parties.Clear();
        lastArrivalMinute = int.MinValue / 2;
        var booking = new FineDiningBooking
        {
            id = Guid.NewGuid().ToString("N"), guestName = guestName, size = size,
            arrivalMinute = Mathf.Max(OpeningMinute, NowMinute), suggestedTableId = table.tableId,
            customerType = (int)CustomerGroup.CustomerType.Green, status = FineDiningBookingStatus.Pending
        };
        state.bookings.Add(booking);
        Changed?.Invoke();
        NewRequests?.Invoke();
        return booking;
    }

    public bool CanSpawnTutorialArrival(string reservationId)
    {
        if (!IsTutorialScene) return false;
        if (string.IsNullOrEmpty(reservationId)) return true; // A prepared diner for a later lesson.
        var booking = Booking(reservationId);
        return booking != null && booking.status == FineDiningBookingStatus.Confirmed &&
            !parties.ContainsKey(reservationId) && tables != null && tables.Any(table => table != null &&
                table.tableId == booking.tableId && table.Usable && table.booth.IsAvailableFor(booking.size));
    }
    public void TrackSpawn(FineDiningBooking booking, CustomerGroup group)
    {
        if (!CanSimulateReservations) return;
        if (booking == null || group == null || parties.ContainsKey(booking.id)) return;
        parties[booking.id] = group;
        lastArrivalMinute = NowMinute;
        group.OnGroupSeated += OnSeated;
        Changed?.Invoke();
    }
    private void OnSeated(CustomerGroup group)
    {
        if (!CanSimulateReservations) return;
        var booking = Booking(group.FineDiningReservationId);
        if (booking == null) return;
        booking.status = FineDiningBookingStatus.Seated;
        booking.seatedOnTime = NowMinute <= booking.arrivalMinute + 5;
        Changed?.Invoke();
    }
    public void PaidDeparture(CustomerGroup group)
    {
        if (!CanSimulateReservations) return;
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
        if (booking != null)
        {
            if (!string.IsNullOrEmpty(booking.tableId) && booking.tableId != table.tableId)
            { reason = "This party is assigned to " + TableName(booking.tableId) + "."; return false; }
            if (state.bookings.Any(b => b.id != booking.id && b.tableId == table.tableId &&
                FineDiningReservationRules.ProtectsTable(b) && b.arrivalMinute < booking.arrivalMinute))
            { reason = "An earlier booked party is still due at this table."; return false; }
            reason = "";
            return true;
        }
        int start = NowMinute;
        if (!FineDiningReservationRules.Fits(state.bookings, table.tableId, start, WindowMinutes, ClosingMinute))
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
        if (booking != null && !string.IsNullOrEmpty(booking.tableId))
            return preferred != null && CanSeat(group, preferred.booth, out _) ? preferred.booth : null;
        return tables?.Where(t => t != null && CanSeat(group, t.booth, out _)).OrderBy(t => t.Capacity).FirstOrDefault()?.booth;
    }
    public bool CommitSeating(CustomerGroup group, Booth booth)
    {
        if (!CanSimulateReservations) return false;
        if (!CanSeat(group, booth, out _)) return false;
        var booking = Booking(group.FineDiningReservationId);
        if (booking != null) booking.tableId = tables.First(t => t != null && t.booth == booth).tableId;
        return true;
    }
    public CustomerGroup NextReceptionParty(IEnumerable<CustomerGroup> waiting) => waiting.Concat(waitingArea != null ? waitingArea.Parties : Enumerable.Empty<CustomerGroup>()).Distinct().Where(g => g != null &&
        g.state == CustomerGroup.GroupState.Waiting && g.CanBeSeated && g.ReadyForReceptionInteraction && RestaurantTaskClaim.CanBotStart(g, 0f) && FindTable(g) != null)
        .OrderBy(g => Booking(g.FineDiningReservationId) == null ? 1 : 0)
        .ThenBy(g => Booking(g.FineDiningReservationId)?.arrivalMinute ?? int.MaxValue).FirstOrDefault();

    public void FinishDay()
    {
        if (!CanSimulateReservations) return;
        if (state.finalized) return;
        foreach (var b in state.bookings)
            if (FineDiningReservationRules.ProtectsTable(b)) b.status = FineDiningBookingStatus.Left;
        state.finalized = true;
        Changed?.Invoke();
    }
    public string ResultSummary => $"Reservations: {AcceptedGroups} booked\n{state.bookings.Count(b => b.seatedOnTime)} on time / {state.bookings.Count(b => b.status == FineDiningBookingStatus.Left)} unserved";
    public void FillSaveData(GameSaveData data) => data.fineDiningReservations = JsonUtility.FromJson<FineDiningReservationSave>(JsonUtility.ToJson(state));
    public void ApplySaveData(GameSaveData data)
    {
        state = data.fineDiningReservations != null ? JsonUtility.FromJson<FineDiningReservationSave>(JsonUtility.ToJson(data.fineDiningReservations)) : new FineDiningReservationSave();
        state.bookings ??= new List<FineDiningBooking>();
        // Interrupted service restarts preparation; runtime admissions belong to the checkpoint.
        if (Preparing) state.walkInsSpawned = 0;
        parties.Clear();
        lastArrivalMinute = int.MinValue / 2;
        Changed?.Invoke();
    }
}



