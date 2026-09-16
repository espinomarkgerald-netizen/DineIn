using System;

// One host-issued preparation request. Votes and countdown belong to this identity,
// never to a reusable per-player boolean from a previous day or request.
[Serializable]
public sealed class MultiplayerDayReadiness
{
    public string id, roster;
    public int day, host;
    public int[] actors, sequences;
    public bool[] votes;
    public double deadline = -1;
    public int Count
    {
        get { int count = 0; if (votes != null) foreach (bool vote in votes) if (vote) count++; return count; }
    }
    public bool AllReady => actors != null && actors.Length > 0 && Count == actors.Length;
    public bool CountingDown => deadline >= 0;
    public bool Valid => !string.IsNullOrEmpty(id) && day > 0 && actors != null && votes != null
        && sequences != null && actors.Length > 0 && actors.Length == votes.Length && actors.Length == sequences.Length
        && Array.IndexOf(actors, host) >= 0 && !double.IsNaN(deadline) && !double.IsInfinity(deadline);
    public static MultiplayerDayReadiness Create(int day, int host, int[] actors, string roster)
    {
        var value = new MultiplayerDayReadiness { id = Guid.NewGuid().ToString("N"), day = day,
            host = host, actors = (int[])actors.Clone(), roster = roster,
            votes = new bool[actors.Length], sequences = new int[actors.Length] };
        int index = Array.IndexOf(actors, host);
        if (index >= 0) value.votes[index] = true;
        return value;
    }
    public bool IsReady(int actor) { int i = Array.IndexOf(actors, actor); return i >= 0 && votes[i]; }
    public int Sequence(int actor) { int i = Array.IndexOf(actors, actor); return i >= 0 ? sequences[i] : -1; }
    public bool SetVote(string request, int actor, int sequence, bool ready)
    {
        if (!Valid || request != id || actor == host || sequence <= 0) return false;
        int index = Array.IndexOf(actors, actor);
        if (index < 0 || sequence <= sequences[index]) return false;
        sequences[index] = sequence; votes[index] = ready;
        if (!AllReady) deadline = -1;
        return true;
    }
    public bool BeginCountdown(double now)
    {
        if (!AllReady || CountingDown) return false;
        deadline = now + 3d;
        return true;
    }
}
