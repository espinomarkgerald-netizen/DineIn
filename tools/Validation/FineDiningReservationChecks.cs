// Standalone offline check: compile with FineDiningReservationState.cs, outside Unity.
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class FineDiningReservationChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    public static void Main()
    {
        var booking = new FineDiningBooking { id = "fd:1:0", guestName = "Morgan party", size = 4,
            arrivalMinute = 18 * 60, tableId = "fd-table-01", status = FineDiningBookingStatus.Confirmed };
        var bookings = new List<FineDiningBooking> { booking };
        const int visitAndCleanup = 70, closing = 22 * 60;
        Check(!FineDiningReservationRules.Fits(bookings, booking.tableId, 18 * 60, visitAndCleanup, closing), "Same-time conflict");
        Check(!FineDiningReservationRules.Fits(bookings, booking.tableId, 18 * 60 - 69, visitAndCleanup, closing), "Cleanup overlap");
        Check(FineDiningReservationRules.Fits(bookings, booking.tableId, 18 * 60 - 70, visitAndCleanup, closing), "Adjacent earlier visit");
        Check(FineDiningReservationRules.Fits(bookings, booking.tableId, 18 * 60 + 70, visitAndCleanup, closing), "Adjacent later visit");
        Check(FineDiningReservationRules.Fits(bookings, "fd-table-02", 18 * 60, visitAndCleanup, closing), "Different table");
        Check(FineDiningReservationRules.Fits(bookings, booking.tableId, 18 * 60, visitAndCleanup, closing, booking.id), "Reassignment excludes itself");
        Check(FineDiningReservationRules.Fits(bookings, "fd-table-02", closing - visitAndCleanup, visitAndCleanup, closing), "Cleanup ends at closing");
        Check(!FineDiningReservationRules.Fits(bookings, "fd-table-02", closing - visitAndCleanup + 1, visitAndCleanup, closing), "Cleanup past closing");
        Check(!FineDiningReservationRules.Fits(bookings, "", 18 * 60, visitAndCleanup, closing), "Missing stable table ID");
        Check(!FineDiningReservationRules.Fits(bookings, "fd-table-02", 18 * 60, 0, closing), "Invalid duration");
        foreach (FineDiningBookingStatus status in Enum.GetValues<FineDiningBookingStatus>())
        {
            booking.status = status;
            bool protects = status == FineDiningBookingStatus.Confirmed || status == FineDiningBookingStatus.Arrived || status == FineDiningBookingStatus.Seated;
            Check(FineDiningReservationRules.ProtectsTable(booking) == protects, "Protection for " + status);
        }
        booking.status = FineDiningBookingStatus.Confirmed;
        var checkpoint = new FineDiningReservationSave { day = 1, groupBudget = 18, bookings = bookings };
        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize(checkpoint, options);
        booking.status = FineDiningBookingStatus.Completed;
        booking.tableId = "fd-table-02";
        var restored = JsonSerializer.Deserialize<FineDiningReservationSave>(json, options);
        Check(restored.bookings[0].status == FineDiningBookingStatus.Confirmed, "Checkpoint restores upcoming status");
        Check(restored.bookings[0].tableId == "fd-table-01", "Checkpoint restores accepted assignment");
        Check(restored.bookings[0].guestName == "Morgan party" && restored.bookings[0].size == 4, "Party identity survives save");
        Check(restored.day == 1 && restored.groupBudget == 18 && !restored.finalized, "Preparation demand survives save");
        var request = new FineDiningBooking { id = "fd:1:1", size = 2, arrivalMinute = 19 * 60,
            status = FineDiningBookingStatus.Pending };
        Check(FineDiningReservationRules.CanAcceptRequest(request, true, false, 17 * 60, 1, out _), "Preparation request");
        Check(FineDiningReservationRules.CanAcceptRequest(request, false, true, 18 * 60, 1, out _), "Future request during service");
        Check(FineDiningReservationRules.CanAcceptRequest(request, false, true, 19 * 60, 1, out _), "Request at current arrival minute");
        Check(!FineDiningReservationRules.CanAcceptRequest(request, false, true, 19 * 60 + 1, 1, out var lateReason)
            && !string.IsNullOrEmpty(lateReason), "Expired request explains why acceptance is disabled");
        Check(!FineDiningReservationRules.CanAcceptRequest(request, false, false, 18 * 60, 1, out _), "Closed service");
        Check(!FineDiningReservationRules.CanAcceptRequest(request, false, true, 18 * 60, 0, out var fullReason)
            && !string.IsNullOrEmpty(fullReason), "Walk-ins already consumed remaining admission budget");
        Check(!FineDiningReservationRules.CanAcceptRequest(request, true, false, 17 * 60, 0, out _), "Preparation budget limit");
        request.status = FineDiningBookingStatus.Confirmed;
        Check(!FineDiningReservationRules.CanAcceptRequest(request, false, true, 18 * 60, 1, out _), "Duplicate accept rejected");
        request.status = FineDiningBookingStatus.Declined;
        Check(!FineDiningReservationRules.CanAcceptRequest(request, true, false, 17 * 60, 1, out _), "Declined request cannot be accepted");
        Check(!FineDiningReservationRules.CanAcceptRequest(null, true, false, 17 * 60, 1, out _), "Missing request rejected");
        var old = JsonSerializer.Deserialize<FineDiningReservationSave>("{}", options);
        Check(old.day == 0 && old.bookings.Count == 0 && !old.finalized, "Older save defaults");
        Console.WriteLine($"PASS: {checks} reservation schedule and serialized-state checks. Unity runtime was not run.");
    }
}
