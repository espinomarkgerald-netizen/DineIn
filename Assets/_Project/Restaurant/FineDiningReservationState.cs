using System;
using System.Collections.Generic;

public enum FineDiningBookingStatus { Pending, Confirmed, Arrived, Seated, Completed, Declined, Left }

[Serializable]
public sealed class FineDiningBooking
{
    public string id;
    public string guestName;
    public bool viewed;
    public string suggestedTableId;
    // Presentation identity, selected once with the existing customer weights.
    public int customerType = -1;
    public int size;
    public int arrivalMinute;
    public string tableId;
    public FineDiningBookingStatus status;
    public bool reminded;
    public bool arrivalNotified;
    public bool seatedOnTime;
}

[Serializable]
public sealed class FineDiningReservationSave
{
    public int day;
    public int groupBudget;
    public bool finalized;
    public int nextRequestMinute;
    public int progressionAnnouncedDay;
    public int walkInsSpawned;
    public List<FineDiningBooking> bookings = new List<FineDiningBooking>();
}

/// <summary>Schedule-only rules; physical table readiness remains owned by Booth.</summary>
public static class FineDiningReservationRules
{
    public static bool ProtectsTable(FineDiningBooking booking) => booking != null &&
        (booking.status == FineDiningBookingStatus.Confirmed ||
         booking.status == FineDiningBookingStatus.Arrived || booking.status == FineDiningBookingStatus.Seated);

    public static bool CanAcceptRequest(FineDiningBooking booking, bool preparing, bool serving,
        int nowMinute, int remainingAdmissionGroups, out string reason)
    {
        reason = "";
        if (booking == null || booking.status != FineDiningBookingStatus.Pending)
            reason = "This request has already been decided.";
        else if (!preparing && !serving)
            reason = "Reservations are closed for this evening.";
        else if (serving && booking.arrivalMinute < nowMinute)
            reason = "The requested arrival time has passed.";
        else if (remainingAdmissionGroups <= 0)
            reason = "Tonight's remaining arrivals are already allocated.";
        return reason.Length == 0;
    }

    public static bool Overlaps(int start, int end, int otherStart, int otherEnd) =>
        start < otherEnd && otherStart < end;

    public static bool Fits(IReadOnlyList<FineDiningBooking> bookings, string tableId, int start,
        int duration, int closingMinute, string excludingId = null)
    {
        if (string.IsNullOrEmpty(tableId) || duration <= 0 || start + duration > closingMinute) return false;
        for (int i = 0; i < bookings.Count; i++)
        {
            var other = bookings[i];
            if (!ProtectsTable(other) || other.id == excludingId || other.tableId != tableId) continue;
            if (Overlaps(start, start + duration, other.arrivalMinute, other.arrivalMinute + duration)) return false;
        }
        return true;
    }
}
