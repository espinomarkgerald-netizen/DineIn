#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Explicit focused runtime check. Never opens scenes, enters Play Mode, starts a day, or saves.
/// Run only in an idle Lobby3 preparation session; campaign writes remain suppressed until cleanup.
/// </summary>
public static class FineDiningReceptionRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<Action> restore = new();
    private static readonly List<CustomerGroup> groups = new();
    private static readonly StringBuilder report = new();
    private static IEnumerator checks;
    private static FineDiningReservations reservations;
    private static FineDiningWaitingArea waiting;
    private static LobbyAutonomousService service;
    private static AutonomousStaffBot host;
    private static Coroutine hostRoutine;
    private static Booth usedBooth;
    private static GameObject fixtureRoot;
    private static int lastFrame, passed;
    public static bool IsRunning => checks != null;
    public static string LastResult { get; private set; } = "NOT RUN";
    public static string ReportPath => Path.GetFullPath("Temp/FineDiningReceptionRegression.txt");

    public static string Begin()
    {
        if (IsRunning) throw new InvalidOperationException("Reception checks are already running.");
        var scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.name != "Lobby3")
            throw new InvalidOperationException("Start these checks explicitly inside an already-running Lobby3 Play Mode session.");
        var day = Object.FindFirstObjectByType<GameDayManager>();
        var save = GameSaveManager.Instance;
        if (day == null || day.ServiceActive || day.ShiftRunning || day.ClosingOut || save == null || !save.HasCompletedInitialLoad || Time.timeScale <= 0f)
            throw new InvalidOperationException("Use idle preparation after initial save loading, with the game unpaused.");
        if (Object.FindObjectsByType<CustomerGroup>(FindObjectsSortMode.None).Any(g => g.gameObject.scene == scene))
            throw new InvalidOperationException("This focused test requires an empty reception with no active customer parties.");
        reservations = Object.FindFirstObjectByType<FineDiningReservations>();
        waiting = reservations != null ? reservations.waitingArea : null;
        service = Object.FindFirstObjectByType<LobbyAutonomousService>();
        host = service != null ? (AutonomousStaffBot)Get(service, "host") : null;
        // A protected fresh-day session may have no assigned employee. Exercise the
        // authored receptionist and its real navigation, without hiring or saving one.
        if (host == null && RoleManager.Instance != null && RoleManager.Instance.host != null)
            host = RoleManager.Instance.host.GetComponent<AutonomousStaffBot>();
        if (waiting == null || waiting.seats.Length < 2 || waiting.Parties.Any() || service == null || host == null || host.IsBusy)
            throw new InvalidOperationException("Bind the reception chairs and an idle authored receptionist bot first.");

        report.Clear(); passed = 0; groups.Clear(); restore.Clear();
        LastResult = "RUNNING";
        try
        {
            bool wasSuppressed = save.SuppressWritesForTests;
            save.SuppressWritesForTests = true;
            restore.Add(() => { if (save != null) save.SuppressWritesForTests = wasSuppressed; });
            var previousPersistence = GameSaveManager.PersistenceSuspended;
            GameSaveManager.PersistenceSuspended = () => true;
            restore.Add(() => GameSaveManager.PersistenceSuspended = previousPersistence);
            Freeze(day);
            Freeze(service);
            foreach (var spawner in Object.FindObjectsByType<GroupSpawner>(FindObjectsSortMode.None)) Freeze(spawner);
            Freeze(waiting);
            RememberField(service, "host", host);
            bool hostWasActive = host.gameObject.activeSelf;
            bool hostWasEnabled = host.enabled;
            restore.Add(() =>
            {
                if (host == null) return;
                host.enabled = hostWasEnabled;
                host.gameObject.SetActive(hostWasActive);
            });
            var legacyMovement = host.GetComponent<PlayerMovement>();
            if (legacyMovement != null) Freeze(legacyMovement);
            foreach (var walker in host.GetComponentsInChildren<HygieneWalker>(true)) Freeze(walker);
            RememberField(reservations, "nextRefresh", float.MaxValue);
            var originalState = (FineDiningReservationSave)Get(reservations, "state");
            Set(reservations, "state", new FineDiningReservationSave { day = originalState.day, groupBudget = 6 });
            restore.Add(() => Set(reservations, "state", originalState));
            var oldSeats = waiting.seats;
            var oldStanding = waiting.standingPositions;
            waiting.seats = oldSeats.Take(2).ToArray();
            waiting.standingPositions = Array.Empty<Transform>();
            restore.Add(() => { if (waiting != null) { waiting.seats = oldSeats; waiting.standingPositions = oldStanding; } });

            var nav = host.GetComponent<NavMeshAgent>();
            Vector3 position = host.transform.position;
            Quaternion rotation = host.transform.rotation;
            bool navWasEnabled = nav != null && nav.enabled;
            bool stopped = nav != null && nav.isOnNavMesh && nav.isStopped;
            bool updateRotation = nav != null && nav.updateRotation;
            bool autoRepath = nav != null && nav.autoRepath;
            var hostProperties = new[] { "CurrentState", "JobCommitted", "LastMoveSucceeded" }
                .Select(name => typeof(AutonomousStaffBot).GetProperty(name, Private))
                .ToDictionary(property => property, property => property.GetValue(host));
            restore.Add(() =>
            {
                if (host == null) return;
                Invoke(host, "StopAgent");
                if (nav != null && nav.isOnNavMesh) { nav.Warp(position); nav.isStopped = stopped; }
                host.transform.SetPositionAndRotation(position, rotation);
                if (nav != null)
                {
                    nav.updateRotation = updateRotation;
                    nav.autoRepath = autoRepath;
                    nav.enabled = navWasEnabled;
                }
                foreach (var property in hostProperties) property.Key.SetValue(host, property.Value);
            });
            host.gameObject.SetActive(true);
            host.enabled = true;
            if (nav != null) nav.enabled = true;

            fixtureRoot = new GameObject("Reception regression fixtures");
            fixtureRoot.hideFlags = HideFlags.DontSave;
            SceneManager.MoveGameObjectToScene(fixtureRoot, scene);
            lastFrame = -1;
            checks = Verify();
            EditorApplication.update += Pump;
            EditorApplication.playModeStateChanged += OnPlayMode;
            WriteReport();
            return "RUNNING focused reception checks. Poll FineDiningReceptionRegression.LastResult or " + ReportPath;
        }
        catch { Cleanup(); throw; }
    }

    public static void Cancel()
    {
        if (IsRunning) Finish("CANCELLED: reception checks were stopped before completion.");
    }

    private static IEnumerator Verify()
    {
        yield return null;
        var reserved = MakeGroup("Reserved test party", 2);
        var walkIn = MakeGroup("Walk-in test party", 2);
        var table = reservations.tables.Where(t => t != null && t.Usable && t.booth.IsAvailableFor(2))
            .OrderByDescending(t => Vector3.Distance(t.booth.GetNavigableApproachPosition(), reserved.members[0].transform.position)).FirstOrDefault();
        Check(table != null, "An unlocked empty table is available for the focused escort test.");
        var state = (FineDiningReservationSave)Get(reservations, "state");
        var booking = new FineDiningBooking
        {
            id = "reception-regression-only", guestName = "Reception check", size = 2,
            tableId = table.tableId, arrivalMinute = reservations.NowMinute, status = FineDiningBookingStatus.Confirmed
        };
        state.bookings.Add(booking);
        typeof(CustomerGroup).GetProperty("FineDiningReservationId", Private).SetValue(reserved, booking.id);
        Set(reserved, "linePatienceRemaining", 120f);
        Invoke(reserved, "UpdateLinePatience");
        Check(Mathf.Approximately((float)Get(reserved, "linePatienceRemaining"), 120f) && !reserved.QueuePatienceVisible,
            "Accepted guests have no reception patience drain or patience bar.");
        reserved.LeaveReceptionUnavailable();
        Check(reserved.state == CustomerGroup.GroupState.Waiting && reserved.FineDiningReservationId == booking.id &&
            booking.tableId == table.tableId, "Reception unavailability preserves the booked party, identity, and assigned table.");

        float originalMultiplier = reservations.walkInReceptionPatienceMultiplier;
        try
        {
            reservations.walkInReceptionPatienceMultiplier = 1;
            Set(walkIn, "linePatienceRemaining", 120f);
            Invoke(walkIn, "UpdateLinePatience");
            float normalDrain = 120f - (float)Get(walkIn, "linePatienceRemaining");
            reservations.walkInReceptionPatienceMultiplier = 2;
            Set(walkIn, "linePatienceRemaining", 120f);
            Invoke(walkIn, "UpdateLinePatience");
            float relaxedDrain = 120f - (float)Get(walkIn, "linePatienceRemaining");
            Check(normalDrain > 0 && Mathf.Abs(normalDrain - relaxedDrain * 2) < .00005f,
                "The real Fine Dining walk-in update drains patience at half the normal rate.");
        }
        finally { reservations.walkInReceptionPatienceMultiplier = originalMultiplier; }

        Check(waiting.TryWait(reserved), "A reserved couple can claim existing waiting chairs.");
        Check(waiting.Contains(reserved) && waiting.seats.All(s => SeatAnchor.IsSeatOccupied(s.seat)),
            "Both waiting chairs are claimed before customers move.");
        Check(!waiting.TryWait(walkIn) && !waiting.Contains(walkIn) && walkIn.state == CustomerGroup.GroupState.Waiting,
            "A second party cannot claim occupied chairs and is not ejected when seating is full.");
        double deadline = EditorApplication.timeSinceStartup + 20;
        while (!reserved.members.All(m => m.IsSeated) && EditorApplication.timeSinceStartup < deadline)
        {
            Invoke(waiting, "Update");
            yield return null;
        }
        Check(reserved.members.All(m => m.IsSeated), "The reserved couple walks along real NavMesh paths and sits in the waiting chairs.");
        Check(Vector3.Distance(reserved.members[0].transform.position, reserved.members[1].transform.position) > .8f,
            "Seated party members occupy distinct physical chair positions.");
        foreach (var member in reserved.members)
        {
            var animator = member.MovementAnimator;
            Check(animator != null && animator.GetBool("IsSitting"), "Existing customer animator receives its sitting state.");
        }

        // The real host seating coroutine must begin the party escort before arriving at the dining table.
        usedBooth = table.booth;
        reserved.TryClaimReceptionForBot();
        hostRoutine = service.StartCoroutine((IEnumerator)Invoke(service, "SeatGroup", reserved, usedBooth));
        deadline = EditorApplication.timeSinceStartup + 30;
        while (!reserved.HasBeenAssigned && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(reserved.HasBeenAssigned && reserved.state == CustomerGroup.GroupState.WalkingToBooth,
            "The receptionist starts assignment and escort while the party is still at reception.");
        Check(Vector3.Distance(host.transform.position, usedBooth.GetNavigableApproachPosition()) > 3f,
            "Escort begins before the receptionist reaches the dining table.");
        Check(!waiting.Contains(reserved) && waiting.seats.All(s => !SeatAnchor.IsSeatOccupied(s.seat)) &&
            reserved.members.All(m => !m.IsSeated), "Beginning the escort releases waiting chairs and stands both guests.");
        Vector3[] starts = reserved.members.Select(m => m.transform.position).ToArray();
        deadline = EditorApplication.timeSinceStartup + 4;
        while (reserved.members.Select((m, i) => Vector3.Distance(starts[i], m.transform.position)).Max() < .3f &&
            EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(reserved.members.Select((m, i) => Vector3.Distance(starts[i], m.transform.position)).Max() >= .3f &&
            Vector3.Distance(host.transform.position, usedBooth.GetNavigableApproachPosition()) > 2f,
            "Guests visibly begin following while their receptionist is still en route.");
        Check(waiting.TryWait(walkIn), "The released waiting chairs are available to the next party.");
        Invoke(waiting, "Release", walkIn);
        Check(waiting.seats.All(s => !SeatAnchor.IsSeatOccupied(s.seat)), "Waiting-place release clears all chair occupancy.");
    }

    private static CustomerGroup MakeGroup(string name, int count)
    {
        var spawner = Object.FindFirstObjectByType<GroupSpawner>();
        var prefab = (CustomerGroup)Get(spawner, "groupPrefab");
        var customerPrefab = (CustomerAgent)Get(spawner, "customerPrefabGreen");
        if (prefab == null || customerPrefab == null) throw new InvalidOperationException("Existing customer prefabs are missing.");
        var reception = waiting.receptionLine.linePoints.First(p => p != null);
        if (!NavMesh.SamplePosition(reception.position, out var hit, 2f, NavMesh.AllAreas))
            throw new InvalidOperationException("The real reception line is not on the NavMesh.");
        var group = Object.Instantiate(prefab, hit.position, reception.rotation, fixtureRoot.transform);
        group.name = name;
        group.enabled = false; // The tests invoke only the production phases under test.
        group.SetLineSlotTarget(hit.position);
        group.state = CustomerGroup.GroupState.Waiting;
        group.SetPatienceSeconds(120);
        Set(group, "tutorialDisableAutoOrderFlow", true);
        for (int i = 0; i < count; i++)
        {
            Vector3 desired = hit.position + reception.right * (i - (count - 1) * .5f) * 1.4f - reception.forward * groups.Count * 2f;
            if (!NavMesh.SamplePosition(desired, out var memberHit, 1f, NavMesh.AllAreas))
                throw new InvalidOperationException("The test customer cannot reach its reception start.");
            var member = Object.Instantiate(customerPrefab, memberHit.position, reception.rotation, group.transform);
            group.members.Add(member);
            foreach (var walker in member.GetComponentsInChildren<HygieneWalker>(true)) walker.enabled = false;
        }
        groups.Add(group);
        return group;
    }

    private static void Pump()
    {
        if (!Application.isPlaying) { Finish("CANCELLED: Play Mode ended."); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (!checks.MoveNext()) Finish("PASS: " + passed + " focused runtime reception checks.");
        }
        catch (Exception exception) { Finish("FAIL: " + (exception.InnerException ?? exception)); }
    }

    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
        passed++;
        report.AppendLine("PASS " + message);
        WriteReport();
    }

    private static void Finish(string result)
    {
        LastResult = result;
        report.AppendLine(result);
        Cleanup();
        report.AppendLine("Transient fixtures removed; prior booking state, staff position, component settings, and persistence policy restored.");
        WriteReport();
        Debug.Log("[FineDiningReceptionRegression] " + result);
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Pump;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        checks = null;
        if (service != null && hostRoutine != null) service.StopCoroutine(hostRoutine);
        hostRoutine = null;
        foreach (var group in groups)
            if (group != null)
            {
                group.StopAllCoroutines();
                group.FinishReceptionEscort();
                if (waiting != null) Invoke(waiting, "Release", group);
                group.assignedBooth = null;
                Object.DestroyImmediate(group.gameObject);
            }
        groups.Clear();
        if (usedBooth != null) usedBooth.ClearCurrentGroup();
        usedBooth = null;
        if (fixtureRoot != null) Object.DestroyImmediate(fixtureRoot);
        fixtureRoot = null;
        for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
        restore.Clear();
    }

    private static void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Cancel(); }
    private static void Freeze(Behaviour value)
    {
        bool enabled = value.enabled;
        value.enabled = false;
        restore.Add(() => { if (value != null) value.enabled = enabled; });
    }
    private static void RememberField(object target, string name, object value)
    {
        object old = Get(target, name); Set(target, name, value); restore.Add(() => Set(target, name, old));
    }
    private static object Get(object target, string name)
    {
        var field = target.GetType().GetField(name, Private);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return field.GetValue(target); // An existing field may legitimately contain null.
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void WriteReport() { Directory.CreateDirectory("Temp"); File.WriteAllText(ReportPath, report + LastResult); }
}
#endif
