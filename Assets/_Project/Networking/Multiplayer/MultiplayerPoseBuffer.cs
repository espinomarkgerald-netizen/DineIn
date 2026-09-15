using System.Collections.Generic;
using UnityEngine;

// Bounded render history. Reject old packets and interpolate at a shared network
// time, instead of repeatedly chasing the newest position each rendered frame.
public sealed class MultiplayerPoseBuffer
{
    private struct Sample { public double at; public Vector3 position; public Quaternion rotation; }
    private readonly List<Sample> samples = new(8);
    public int Count => samples.Count;
    public bool Add(double at, Vector3 position, Quaternion rotation, bool teleport = false)
    {
        if (double.IsNaN(at) || double.IsInfinity(at) || (samples.Count > 0 && at <= samples[samples.Count - 1].at)) return false;
        if (teleport) samples.Clear();
        if (samples.Count == 8) samples.RemoveAt(0);
        samples.Add(new Sample { at = at, position = position, rotation = rotation });
        return true;
    }
    public bool Read(double renderAt, out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        if (samples.Count == 0) return false;
        while (samples.Count > 2 && samples[1].at <= renderAt) samples.RemoveAt(0);
        var a = samples[0]; var b = samples.Count > 1 ? samples[1] : a;
        float t = b.at > a.at ? Mathf.Clamp01((float)((renderAt - a.at) / (b.at - a.at))) : 1f;
        position = Vector3.Lerp(a.position, b.position, t);
        rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
        return true;
    }
}
