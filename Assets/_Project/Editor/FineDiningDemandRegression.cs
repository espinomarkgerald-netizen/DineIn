#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated schedule checks; never opens or saves a restaurant or campaign.</summary>
public static class FineDiningDemandRegression
{
    private static readonly FieldInfo StateField = typeof(FineDiningReservations).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo Generate = typeof(FineDiningReservations).GetMethod("GenerateRequests", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run schedule checks outside Play Mode.");
        int checks = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Fine Dining demand: " + message);
            checks++;
        }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Demand schedule verification");
            SceneManager.MoveGameObjectToScene(root, scene);
            var reservations = root.AddComponent<FineDiningReservations>();
            reservations.soloServiceGroupCapacity = 50; // Isolate table/schedule tests from the user's active roster.
            reservations.tables = Enumerable.Range(0, 10).Select(i => MakeTable(root.transform, i, i < 3 ? 4 : 2 + i % 3)).ToArray();
            var state = new FineDiningReservationSave { day = 18, groupBudget = 100 };
            StateField.SetValue(reservations, state);
            Check(reservations.WindowMinutes == 85, "Visit estimate includes slower eating, expected drinks and cleanup.");

            reservations.middleProgressionDay = int.MaxValue;
            reservations.lateProgressionDay = int.MaxValue;
            Check(reservations.ProgressionGroupTarget >= 6 && reservations.ProgressionGroupTarget <= 8, "Early target stays within 6-8.");
            reservations.middleProgressionDay = 1;
            Check(reservations.ProgressionGroupTarget >= 9 && reservations.ProgressionGroupTarget <= 14, "Middle target stays within 9-14.");
            reservations.lateProgressionDay = 1;
            Check(reservations.ProgressionGroupTarget >= 15 && reservations.ProgressionGroupTarget <= 20, "Late target stays within 15-20.");
            Check(reservations.GroupBudget <= 20, "Legacy oversized forecast cannot create more than 20 new groups.");
            var allTables = reservations.tables;
            reservations.tables = allTables.Take(3).ToArray();
            Check(reservations.ProgressionGroupTarget <= 8, "Progression without additional usable tables remains relaxed.");
            Check(reservations.GroupBudget <= reservations.SeatingGroupCapacity, "Small restaurant demand fits seating turnover.");
            int earlyBudget = reservations.GroupBudget;
            reservations.tables = allTables;
            Check(reservations.GroupBudget > earlyBudget, "More usable booths unlock a larger supported demand.");
            reservations.soloServiceGroupCapacity = 1;
            reservations.groupsPerAssignedStaff = 0;
            Check(reservations.GroupBudget <= 1, "Service capacity constrains otherwise available tables.");
            reservations.soloServiceGroupCapacity = 50;

            int requested = reservations.ReservationGroupTarget;
            Generate.Invoke(reservations, new object[] { requested, reservations.OpeningMinute - 60 });
            Check(state.bookings.Count == requested, "Full restaurant can offer its complete reservation allocation.");
            Check(state.bookings.Count + reservations.WalkInLimit <= 20, "Reservations and walk-ins share the daily cap.");
            foreach (var booking in state.bookings)
            {
                var table = allTables.Single(t => t.tableId == booking.suggestedTableId);
                Check(booking.size <= table.Capacity && booking.size <= reservations.maximumPartySize, "Offer fits an unlocked table.");
                Check(booking.arrivalMinute >= reservations.NowMinute, "No request arrives in the past.");
                Check(booking.arrivalMinute + reservations.WindowMinutes <= reservations.ClosingMinute, "New visit and cleanup finish before closing.");
            }
            for (int i = 0; i < state.bookings.Count; i++)
                for (int j = i + 1; j < state.bookings.Count; j++)
                {
                    var a = state.bookings[i];
                    var b = state.bookings[j];
                    int gap = Math.Abs(a.arrivalMinute - b.arrivalMinute);
                    Check(gap >= reservations.minimumArrivalSpacingMinutes, "Arrivals are globally staggered.");
                    if (a.size >= 3 && b.size >= 3)
                        Check(gap >= reservations.largePartySpacingMinutes, "Large parties are not scheduled together.");
                    if (a.suggestedTableId == b.suggestedTableId)
                        Check(gap >= reservations.WindowMinutes, "Tentative offers do not double-book a table.");
                }
            int before = state.bookings.Count;
            Generate.Invoke(reservations, new object[] { 3, reservations.ClosingMinute - reservations.WindowMinutes + 1 });
            Check(state.bookings.Count == before, "No new offer can outlast remaining service capacity.");

            state.bookings = Enumerable.Range(0, 25).Select(i => new FineDiningBooking
            {
                id = "legacy:" + i, tableId = allTables[i % allTables.Length].tableId,
                arrivalMinute = reservations.OpeningMinute + i, size = 2, status = FineDiningBookingStatus.Confirmed
            }).ToList();
            var times = state.bookings.Select(b => b.arrivalMinute).ToArray();
            Check(reservations.GroupBudget == 25, "Accepted legacy commitments survive the new cap.");
            Check(reservations.AvailableAdmissionGroups == 0, "Oversized commitments allow no additional demand.");
            Check(reservations.DueBooking()?.id == "legacy:0", "Legacy scheduled arrival is still due.");
            Check(times.SequenceEqual(state.bookings.Select(b => b.arrivalMinute)), "Capacity checks never reschedule accepted bookings.");
            return "PASS: " + checks + " isolated Fine Dining demand/schedule checks. No Play Mode or campaign save was used.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static FineDiningTable MakeTable(Transform parent, int index, int capacity)
    {
        var go = new GameObject("Schedule table " + index);
        go.transform.SetParent(parent, false);
        var booth = go.AddComponent<Booth>();
        booth.approachPoint = go.transform;
        for (int i = 0; i < capacity; i++)
        {
            var seat = new GameObject("Seat " + i).transform;
            seat.SetParent(go.transform, false);
            booth.seats.Add(seat);
        }
        var table = go.AddComponent<FineDiningTable>();
        table.booth = booth;
        table.tableId = "test-table:" + index;
        table.displayName = go.name;
        table.unlockDay = 1;
        return table;
    }
}
#endif
