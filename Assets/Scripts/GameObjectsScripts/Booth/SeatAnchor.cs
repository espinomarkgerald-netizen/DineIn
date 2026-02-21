using System.Collections.Generic;
using UnityEngine;

public static class SeatAnchor
{
    private static readonly Dictionary<Transform, GameObject> occupied = new();

    public static bool IsSeatOccupied(Transform seat)
    {
        if (seat == null) return false;

        if (!occupied.ContainsKey(seat))
            return false;

        // Clean up destroyed objects automatically
        if (occupied[seat] == null)
        {
            occupied.Remove(seat);
            return false;
        }

        return true;
    }

    public static bool TryOccupy(Transform seat, GameObject who)
    {
        if (seat == null || who == null)
            return false;

        // If already occupied, deny
        if (IsSeatOccupied(seat))
            return false;

        occupied[seat] = who;
        return true;
    }

    public static void VacateSeat(Transform seat)
    {
        if (seat == null) return;

        if (occupied.ContainsKey(seat))
            occupied.Remove(seat);
    }

    public static void VacateAllFor(GameObject who)
    {
        if (who == null) return;

        var toRemove = new List<Transform>();

        foreach (var kv in occupied)
        {
            if (kv.Value == who)
                toRemove.Add(kv.Key);
        }

        foreach (var s in toRemove)
            occupied.Remove(s);
    }

    public static void ClearAll()
    {
        occupied.Clear();
    }
}
